using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>Marks <see cref="BulkStreamingMemoryTests"/> as its own xUnit collection so it never runs concurrently
/// with any other collection. <c>GC.GetTotalMemory</c> is process-wide, so parallel allocation from an unrelated
/// test would be charged against this one's 8 MB budget.</summary>
[CollectionDefinition("Bulk streaming memory", DisableParallelization = true)]
public sealed class BulkStreamingMemoryCollection;

/// <summary>The bulk pipeline must stay flat in memory however large the response is (#13).
///
/// <para><b>Why this is a test rather than a design note.</b> The two largest responses on the API are
/// <c>ratios-ttm-bulk</c> at 69.5 MB and <c>key-metrics-ttm-bulk</c> at 44 MB, measured 2026-08-26, and both
/// arrive without a <c>Content-Length</c>. Any single <c>ReadAsStringAsync</c>, <c>ToList()</c> or accidental
/// buffering anywhere in the chain is invisible on a three-row fixture and fatal here — a 69 MB body read as a
/// string is ~139 MB of UTF-16 before a single row is mapped, and on the large object heap.</para></summary>
[Collection("Bulk streaming memory")]
public class BulkStreamingMemoryTests
{
    /// <summary>A stream that produces CSV rows only as they are read, so the TEST never materialises the payload
    /// either — a harness that buffered would be proving nothing about the SDK.
    ///
    /// <para>This exists because the obvious harness is wrong in a way that looks right. A custom
    /// <see cref="HttpContent"/> that overrides only <c>SerializeToStreamAsync</c> is buffered into a
    /// <see cref="MemoryStream"/> by <c>ReadAsStreamAsync</c>, since that is the base class's fallback when
    /// <c>CreateContentReadStreamAsync</c> is not overridden. The first version of these tests did exactly that
    /// and failed — against a pipeline that streams correctly.</para></summary>
    private sealed class GeneratedCsvStream(string header, int rows, Func<int, Task<string>> row) : Stream
    {
        private byte[] _current = Encoding.UTF8.GetBytes(header + "\n");
        private int _offset;
        private int _next;

        public long BytesProduced { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
        {
            if (_offset >= _current.Length)
            {
                if (_next >= rows) return 0;
                _current = Encoding.UTF8.GetBytes(await row(_next++).ConfigureAwait(false) + "\n");
                _offset = 0;
            }

            var n = Math.Min(destination.Length, _current.Length - _offset);
            _current.AsSpan(_offset, n).CopyTo(destination.Span);
            _offset += n;
            BytesProduced += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Serves the generated stream without ever buffering it.</summary>
    private sealed class StreamingCsvContent(GeneratedCsvStream stream) : HttpContent
    {
        public GeneratedCsvStream Source { get; } = stream;

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(Source);

        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
            Source.CopyToAsync(target);

        // Deliberately false: this is how the real bulk endpoints answer — chunked, no Content-Length — so the
        // pipeline is exercised on the path it will actually take.
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private static BulkEndpoints Build(HttpContent content)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new BulkEndpoints(new FmpBulkTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));
    }

    [Fact]
    public async Task A_payload_far_larger_than_memory_budget_streams_without_retaining_it()
    {
        // The assertion is on LIVE memory sampled mid-stream, not on total allocation: streaming allocates per
        // row by design, and what must not happen is the body being held.
        // Sized to land near key-metrics-ttm-bulk's measured 44 MB — the second largest response on
        // the API. At ~63 bytes a row that is 700,000 rows.
        const int Rows = 700_000;
        var source = new GeneratedCsvStream(
            "symbol,date,open,low,high,close,adjClose,volume",
            Rows,
            i => Task.FromResult(
                $"SYM{i:D6},2026-08-25,{i % 900 + 1}.25,{i % 900}.10,{i % 900 + 2}.75,{i % 900 + 1}.50,{i % 900 + 1}.50,{i * 7}"));
        var endpoints = Build(new StreamingCsvContent(source));

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        long peakLive = 0;
        var seen = 0;

        await foreach (var bar in endpoints.StreamEndOfDayAsync(new LocalDate(2026, 8, 25)))
        {
            seen++;
            // Sampled rarely — GetTotalMemory(true) forces a collection, and doing it per row would put the test
            // in the GC for minutes.
            if (seen % 100_000 == 0)
                peakLive = Math.Max(peakLive, GC.GetTotalMemory(forceFullCollection: true) - baseline);
            if (bar.Symbol.Length == 0) throw new InvalidOperationException("row mapped without a symbol");
        }

        Assert.Equal(Rows, seen);
        Assert.True(source.BytesProduced > 40_000_000,
            $"the generated payload should be tens of MB; it was {source.BytesProduced:N0} bytes");

        // Generous by design: this must fail on a buffered body (40 MB, and ~80 MB if read as a string) and must
        // not fail on ordinary per-row churn or a large read buffer.
        Assert.True(peakLive < 8 * 1024 * 1024,
            $"live memory grew to {peakLive:N0} bytes while streaming {source.BytesProduced:N0} bytes — "
            + "something is retaining the response rather than streaming it");
    }

    [Fact]
    public async Task A_row_is_yielded_before_the_response_has_finished_arriving()
    {
        // The other half of streaming, and the one a memory assertion alone cannot catch: a pipeline that buffers
        // the whole body and THEN yields rows uses flat memory per row but delivers nothing for 69 MB. The
        // generator blocks after the first rows until the consumer has taken one.
        var firstRowSeen = new TaskCompletionSource();
        var mayContinue = new TaskCompletionSource();

        var source = new GeneratedCsvStream(
            "symbol,date,open,low,high,close,adjClose,volume",
            2_000,
            async i =>
            {
                if (i == 200) await mayContinue.Task;   // stall the producer part-way through the body
                return $"SYM{i:D4},2026-08-25,1.0,1.0,1.0,1.0,1.0,{i}";
            });
        var endpoints = Build(new StreamingCsvContent(source));
        var rows = 0;

        var consumer = Task.Run(async () =>
        {
            await foreach (var _ in endpoints.StreamEndOfDayAsync(new LocalDate(2026, 8, 25)))
                if (++rows == 1) firstRowSeen.TrySetResult();
        });

        // A pipeline that buffers the whole body first never reaches this, because the producer is still stalled.
        var arrived = await Task.WhenAny(firstRowSeen.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        mayContinue.TrySetResult();
        await consumer;

        Assert.Same(firstRowSeen.Task, arrived);
        Assert.Equal(2_000, rows);
    }
}
