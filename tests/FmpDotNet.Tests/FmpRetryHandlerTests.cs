using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using FmpDotNet.Http;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>fmpdotnet#44. Retries a transient upstream failure so every caller does not write the same wrapper.
///
/// <para><b>429 is deliberately absent from what is retried</b>, and that is the point of several tests here.
/// A 429 is answered by <see cref="FmpRateLimitHandlerBase"/> draining the shared reservoir, so the whole process
/// meets back-pressure rather than the one caller that drew it. Re-sending on top of that would amplify load
/// exactly when FMP is already refusing us.</para></summary>
public sealed class FmpRetryHandlerTests
{
    /// <summary>Exposes the shared base with a policy chosen by the test rather than by DI, and with the wait
    /// recorded instead of taken — see <see cref="DelayRecorder"/>.</summary>
    private sealed class TestRetryHandler(
        IClock clock, int maxAttempts, Duration baseDelay, Duration maxDelay, DelayRecorder delays)
        : FmpRetryHandlerBase(clock, maxAttempts, baseDelay, maxDelay, NullLogger.Instance, delays.RecordAsync);

    /// <summary>Records what the handler asked to wait and returns immediately.
    ///
    /// <para><b>A <c>FakeClock</c> would not have served here.</b> The reservoir's arithmetic reads a clock, but a
    /// backoff is a real <see cref="Task.Delay(TimeSpan, CancellationToken)"/> — faking the clock leaves the wait
    /// itself untouched. At the cadence this handler defaults to, one three-attempt test would be seconds of wall
    /// clock against a suite that runs its whole unit set in about a second.</para></summary>
    private sealed class DelayRecorder(Action? onDelay = null)
    {
        public List<Duration> Delays { get; } = [];

        public Task RecordAsync(Duration delay, CancellationToken ct)
        {
            Delays.Add(delay);
            onDelay?.Invoke();
            // Task.Delay honours its token, so a fake that did not would let a cancellation test pass against a
            // handler that had dropped the token on the floor.
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static HttpClient Build(
        HttpMessageHandler upstream, int maxAttempts = 3, DelayRecorder? delays = null,
        Duration? baseDelay = null, Duration? maxDelay = null, IClock? clock = null)
    {
        var handler = new TestRetryHandler(
            clock ?? SystemClock.Instance,
            maxAttempts,
            baseDelay ?? Duration.FromSeconds(1),
            maxDelay ?? Duration.FromSeconds(120),
            delays ?? new DelayRecorder())
        { InnerHandler = upstream };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
    }

    [Fact]
    public async Task A_503_is_retried_and_the_answer_returned_is_the_one_that_succeeded()
    {
        var upstream = new StubHandler(
            StubHandler.Status(HttpStatusCode.ServiceUnavailable),
            StubHandler.Status(HttpStatusCode.OK));
        using var http = Build(upstream);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, upstream.Requests.Count);
    }

    [Fact]
    public async Task A_persistent_5xx_stops_at_the_attempt_cap_and_hands_back_the_last_failure()
    {
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.BadGateway));
        using var http = Build(upstream, maxAttempts: 3);

        using var response = await http.GetAsync("stable/profile");

        // The status survives so the transport can still put FMP's own body text into FmpApiException.
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(3, upstream.Requests.Count);
    }

    [Fact]
    public async Task A_429_is_never_retried_because_the_throttle_answers_it_instead()
    {
        // Not an oversight and not a gap: FmpRateLimitHandlerBase drains the SHARED reservoir on a 429, so the
        // whole process meets back-pressure. Re-sending on top of that amplifies load exactly when FMP is already
        // refusing us — which is what the resilience handler trader had to strip was doing.
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.TooManyRequests));
        using var http = Build(upstream);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(upstream.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task A_4xx_is_never_retried_because_re_sending_it_changes_nothing(HttpStatusCode status)
    {
        var upstream = new StubHandler(StubHandler.Status(status));
        using var http = Build(upstream);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(status, response.StatusCode);
        Assert.Single(upstream.Requests);
    }

    [Fact]
    public async Task A_transport_fault_is_retried_because_nothing_about_it_says_the_request_was_wrong()
    {
        // A socket reset, a refused connection and a DNS failure all reach a handler as HttpRequestException.
        var upstream = new ThrowingHandler(new HttpRequestException("connection reset"), succeedOnSend: 3);
        using var http = Build(upstream, maxAttempts: 3);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, upstream.Sends);
    }

    [Fact]
    public async Task A_transport_fault_that_never_clears_surfaces_the_last_one_rather_than_being_swallowed()
    {
        var upstream = new ThrowingHandler(new HttpRequestException("connection reset"), succeedOnSend: int.MaxValue);
        using var http = Build(upstream, maxAttempts: 3);

        await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync("stable/profile"));

        Assert.Equal(3, upstream.Sends);
    }

    [Fact]
    public async Task Each_wait_is_twice_the_last_so_a_struggling_upstream_is_backed_away_from()
    {
        var delays = new DelayRecorder();
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = Build(upstream, maxAttempts: 4, delays: delays, baseDelay: Duration.FromSeconds(1));

        using var response = await http.GetAsync("stable/profile");

        // Three waits for four attempts — nothing is waited after the last one, which would be latency the caller
        // pays for no further chance of an answer.
        Assert.Equal(3, delays.Delays.Count);
        // Jitter is added on top of each step, so each wait sits in [step, step + base): 1-2s, 2-3s, 4-5s.
        AssertWithin(Duration.FromSeconds(1), Duration.FromSeconds(2), delays.Delays[0]);
        AssertWithin(Duration.FromSeconds(2), Duration.FromSeconds(3), delays.Delays[1]);
        AssertWithin(Duration.FromSeconds(4), Duration.FromSeconds(5), delays.Delays[2]);
    }

    [Fact]
    public async Task Jitter_means_two_runs_of_the_same_policy_do_not_wait_in_lockstep()
    {
        // Without it, every client that started together retries together, and the upstream meets the same
        // synchronised wave it just failed to serve.
        var waits = new List<Duration>();
        for (var run = 0; run < 12; run++)
        {
            var delays = new DelayRecorder();
            var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.ServiceUnavailable));
            using var http = Build(upstream, maxAttempts: 2, delays: delays);
            using var response = await http.GetAsync("stable/profile");
            waits.Add(delays.Delays[0]);
        }

        Assert.True(waits.Distinct().Count() > 1, "every wait was identical — no jitter is being applied");
    }

    [Fact]
    public async Task A_retry_after_on_the_5xx_is_honoured_in_place_of_the_computed_backoff()
    {
        // The one place this SDK should beat a blind exponential: when the upstream says when to come back,
        // guessing is worse than listening.
        var delays = new DelayRecorder();
        var failure = StubHandler.Status(HttpStatusCode.ServiceUnavailable);
        failure.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
        var upstream = new StubHandler(failure, StubHandler.Status(HttpStatusCode.OK));
        using var http = Build(upstream, delays: delays, baseDelay: Duration.FromSeconds(1));

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(Duration.FromSeconds(7), Assert.Single(delays.Delays));
    }

    [Fact]
    public async Task A_hostile_retry_after_is_clamped_so_one_response_cannot_stall_the_caller()
    {
        var delays = new DelayRecorder();
        var failure = StubHandler.Status(HttpStatusCode.ServiceUnavailable);
        failure.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromDays(1));
        var upstream = new StubHandler(failure, StubHandler.Status(HttpStatusCode.OK));
        using var http = Build(upstream, delays: delays, maxDelay: Duration.FromSeconds(120));

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(Duration.FromSeconds(120), Assert.Single(delays.Delays));
    }

    [Fact]
    public async Task The_computed_backoff_is_clamped_too_so_a_long_attempt_chain_cannot_run_away()
    {
        var delays = new DelayRecorder();
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = Build(
            upstream, maxAttempts: 6, delays: delays,
            baseDelay: Duration.FromSeconds(1), maxDelay: Duration.FromSeconds(3));

        using var response = await http.GetAsync("stable/profile");

        Assert.All(delays.Delays, d => Assert.True(d <= Duration.FromSeconds(3), $"unclamped wait {d}"));
        // The last steps would be 8s and 16s unclamped, so the clamp is doing real work rather than never binding.
        Assert.Equal(Duration.FromSeconds(3), delays.Delays[^1]);
    }

    [Fact]
    public async Task A_long_attempt_chain_saturates_at_the_ceiling_instead_of_overflowing()
    {
        // The doubling is computed in seconds, not in Duration arithmetic, precisely so this cannot throw: by
        // attempt 200 the unbounded step is ~10^61 seconds, which Duration rejects long before the clamp binds.
        var delays = new DelayRecorder();
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = Build(
            upstream, maxAttempts: 200, delays: delays,
            baseDelay: Duration.FromSeconds(30), maxDelay: Duration.FromSeconds(120));

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(200, upstream.Requests.Count);
        Assert.All(delays.Delays, d => Assert.True(d <= Duration.FromSeconds(120), $"unclamped wait {d}"));
    }

    [Fact]
    public async Task Cancelling_during_a_backoff_ends_the_sequence_instead_of_attempting_again()
    {
        using var cts = new CancellationTokenSource();
        var delays = new DelayRecorder(onDelay: cts.Cancel);
        var upstream = new StubHandler(StubHandler.Status(HttpStatusCode.ServiceUnavailable));
        using var http = Build(upstream, maxAttempts: 3, delays: delays);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => http.GetAsync("stable/profile", cts.Token));

        // A shutdown must not be turned into two more requests on the way out.
        Assert.Single(upstream.Requests);
    }

    [Fact]
    public async Task A_retry_after_given_as_an_http_date_is_read_against_the_clock()
    {
        // RFC 9110 allows both forms and FMP is documented to send neither, so both are read. The date form is the
        // only reason this handler takes a clock at all.
        var now = Instant.FromUtc(2026, 9, 1, 12, 0, 0);
        var delays = new DelayRecorder();
        var failure = StubHandler.Status(HttpStatusCode.ServiceUnavailable);
        failure.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            (now + Duration.FromSeconds(9)).ToDateTimeOffset());
        var upstream = new StubHandler(failure, StubHandler.Status(HttpStatusCode.OK));
        using var http = Build(upstream, delays: delays, clock: new NodaTime.Testing.FakeClock(now));

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(Duration.FromSeconds(9), Assert.Single(delays.Delays));
    }

    [Fact]
    public async Task A_response_that_is_retried_past_is_disposed_so_its_connection_is_not_held_out_of_the_pool()
    {
        var abandoned = new TrackingContent();
        var upstream = new StubHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = abandoned },
            StubHandler.Status(HttpStatusCode.OK));
        using var http = Build(upstream, maxAttempts: 2);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(abandoned.Disposed, "the abandoned response was never disposed — one connection leaks per retry");
    }

    [Fact]
    public async Task A_fault_while_csv_rows_are_being_streamed_is_not_retried()
    {
        // The boundary this handler is confined to, pinned rather than left silently true. StreamCsvAsync reads
        // under ResponseHeadersRead and yields rows as they arrive, so by the time the body faults the retry
        // handler has long since returned its response — and re-sending would mean handing the caller a second
        // copy of rows it has already seen.
        var upstream = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new FaultingCsvContent(),
        });
        using var http = Build(upstream, maxAttempts: 3);
        var transport = new FmpTransport(http, Microsoft.Extensions.Options.Options.Create(
            new FmpOptions { ApiKey = "k" }));

        var seen = 0;
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await foreach (var row in transport.StreamCsvAsync(
                new FmpRequest("stable/profile-bulk"), r => r.GetString("symbol")))
                seen++;
        });

        Assert.True(seen > 0, "the stream faulted before any row was yielded — this is not the mid-stream case");
        Assert.Single(upstream.Requests);
    }

    /// <summary>Serves enough CSV to get past the transport's 256-byte classification peek and yield rows, then
    /// fails the way a dropped connection does.</summary>
    private sealed class FaultingCsvContent : HttpContent
    {
        private static readonly byte[] Rows = System.Text.Encoding.UTF8.GetBytes(
            "symbol,sector\n" + string.Concat(Enumerable.Repeat("AAA,Technology\n", 40)));

        public FaultingCsvContent() =>
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new FaultingStream(Rows));

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => stream.WriteAsync(Rows, 0, Rows.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>Replays a buffer, then throws as a severed connection does.</summary>
    private sealed class FaultingStream(byte[] payload) : Stream
    {
        private int _position;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= payload.Length) throw new IOException("the response ended unexpectedly");
            var n = Math.Min(count, payload.Length - _position);
            Array.Copy(payload, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Empty content that records its own disposal.</summary>
    private sealed class TrackingContent : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static void AssertWithin(Duration lower, Duration upper, Duration actual) =>
        Assert.True(actual >= lower && actual < upper, $"expected [{lower}, {upper}), got {actual}");

    /// <summary>Throws <paramref name="fault"/> on every send before the <paramref name="succeedOnSend"/>th.</summary>
    private sealed class ThrowingHandler(Exception fault, int succeedOnSend) : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sends++;
            return Sends >= succeedOnSend
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                : Task.FromException<HttpResponseMessage>(fault);
        }
    }
}
