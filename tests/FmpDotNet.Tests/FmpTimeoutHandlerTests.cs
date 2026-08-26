using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FmpDotNet.Http;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The bound matters, but the EXCEPTION TYPE matters more — see
/// <see cref="A_client_timeout_reports_itself_as_a_cancellation_which_is_why_this_handler_exists"/>.
///
/// <para>These arrived with fmpdotnet#10. The behaviour is the SDK's, but it was only covered by tests in the
/// consumer this SDK replaced, so moving the code without moving the tests would have quietly dropped the
/// coverage — and the last test here shows that was not hypothetical.</para></summary>
public sealed class FmpTimeoutHandlerTests
{
    /// <summary>Never completes until cancelled — a request the upstream has stopped answering.</summary>
    private sealed class StallingHandler : HttpMessageHandler
    {
        public int Sends;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class InstantHandler(HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(code));
    }

    private static FmpTimeoutHandler Bounded(HttpMessageHandler inner, Duration timeout) =>
        new(Options.Create(new FmpOptions { RequestTimeout = timeout })) { InnerHandler = inner };

    private static HttpClient Client(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://financialmodelingprep.com/"),
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    [Fact]
    public async Task A_client_timeout_reports_itself_as_a_cancellation_which_is_why_this_handler_exists()
    {
        // The measurement the whole design rests on, pinned so a framework change cannot quietly invalidate it.
        // HttpClient.Timeout does NOT throw TimeoutException — it throws TaskCanceledException, an
        // OperationCanceledException, and buries the TimeoutException as InnerException. A caller that reads an
        // OperationCanceledException as "the host is shutting down" therefore lets one slow response end a whole
        // run, silently. Setting a shorter client timeout — the obvious fix — only makes that more frequent.
        using var http = new HttpClient(new StallingHandler())
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = TimeSpan.FromMilliseconds(50),
        };

        var ex = await Record.ExceptionAsync(() => http.GetAsync("stable/profile"));

        Assert.IsType<TaskCanceledException>(ex);
        Assert.IsType<OperationCanceledException>(ex, exactMatch: false);
        Assert.IsType<TimeoutException>(ex!.InnerException);   // present, but only as the inner
    }

    [Fact]
    public async Task Times_out_a_stalled_request_as_a_TimeoutException()
    {
        using var http = Client(Bounded(new StallingHandler(), Duration.FromMilliseconds(50)));

        var ex = await Record.ExceptionAsync(() => http.GetAsync("stable/profile?symbol=AAPL"));

        // THE POINT: a TimeoutException is not an OperationCanceledException, so a caller's per-item catch takes
        // it and the cost is one item rather than the run.
        Assert.IsType<TimeoutException>(ex);
        Assert.IsNotType<OperationCanceledException>(ex, exactMatch: false);
        Assert.Contains("timed out after 0.05s", ex!.Message);
        Assert.Contains("stable/profile", ex.Message);
    }

    [Fact]
    public async Task A_caller_cancellation_still_surfaces_as_a_cancellation()
    {
        // The other half of the contract, and why the catch filter tests `ct` rather than the linked token: a real
        // shutdown MUST still abort the run. Converting this to TimeoutException would make a host log a stack of
        // "FMP request timed out" errors on every deploy.
        using var http = Client(Bounded(new StallingHandler(), Duration.FromMinutes(5)));
        using var cts = new CancellationTokenSource();

        var pending = http.GetAsync("stable/profile", cts.Token);
        await cts.CancelAsync();

        var ex = await Record.ExceptionAsync(() => pending);

        Assert.IsType<OperationCanceledException>(ex, exactMatch: false);
        Assert.IsNotType<TimeoutException>(ex);
    }

    [Fact]
    public async Task Passes_a_prompt_response_straight_through()
    {
        using var http = Client(Bounded(new InstantHandler(HttpStatusCode.Accepted), Duration.FromMinutes(1)));

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Bounds_each_attempt_separately_rather_than_the_whole_sequence()
    {
        // Stated as a test because the arithmetic is the honest limit of this design: a caller making N sequential
        // calls for one symbol is bounded at N x RequestTimeout, not at RequestTimeout. Bounding the whole
        // sequence belongs to that caller, not to an HTTP handler.
        var stall = new StallingHandler();
        using var http = Client(Bounded(stall, Duration.FromMilliseconds(30)));

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<TimeoutException>(() => http.GetAsync("stable/profile"));

        Assert.Equal(3, stall.Sends);   // three attempts, three independent deadlines
    }

    [Fact]
    public async Task A_throttle_wait_longer_than_the_deadline_does_not_consume_it()
    {
        // WHY THE HANDLER ORDER IS CONTRACTUAL. The chain is throttle → timeout → network, so the deadline starts
        // only once the shared bucket has granted a token. Here the bucket is empty and refills at 1/s, forcing a
        // ~1s wait, while the deadline is 200ms: if the timeout sat OUTSIDE the throttle — or lived on
        // HttpClient.Timeout, which starts before any handler runs — this request would be abandoned by our own
        // back-pressure. Not hypothetical, since a 429 drains the bucket for up to MaxRetryAfter: every held
        // request would time out instead of waiting.
        var bucket = new TokenBucket(capacity: 1, refillPerSecond: 1.0, nowSeconds: 0.0);
        bucket.Drain(nowSeconds: 0.0, holdSeconds: 0.0);        // empty: the next Acquire must wait ~1s

        var timeout = Bounded(new InstantHandler(), Duration.FromMilliseconds(200));
        var throttle = new TestRateLimitHandler(SystemClock.Instance, bucket, new FmpOptions())
        {
            InnerHandler = timeout,
        };
        using var http = Client(throttle);

        using var response = await http.GetAsync("stable/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_api_key_is_not_in_the_timeout_message()
    {
        // fmpdotnet#10 found this by migrating a consumer onto the SDK. FMP authenticates by query string, so the
        // built RequestUri carries the key — and an exception message is the one place a URI reliably reaches a
        // log. FmpRequest.ToString() is key-free precisely so a request can be logged safely, but by the time a
        // DelegatingHandler sees the request that structure is gone, so the redaction has to happen again here.
        using var http = Client(Bounded(new StallingHandler(), Duration.FromMilliseconds(50)));

        var ex = await Record.ExceptionAsync(
            () => http.GetAsync("stable/profile?symbol=AAPL&apikey=super-secret-key&extra=1"));

        Assert.IsType<TimeoutException>(ex);
        Assert.DoesNotContain("super-secret-key", ex!.Message);
        Assert.Contains("[redacted]", ex.Message);
        // The rest of the query is what makes a timeout diagnosable, so it must survive the redaction.
        Assert.Contains("symbol=AAPL", ex.Message);
        Assert.Contains("extra=1", ex.Message);
    }

    /// <summary>Exposes the shared throttle base with a bucket chosen by the test rather than by DI.</summary>
    private sealed class TestRateLimitHandler(IClock clock, TokenBucket bucket, FmpOptions options)
        : FmpRateLimitHandlerBase(clock, bucket, options, NullLogger.Instance);
}
