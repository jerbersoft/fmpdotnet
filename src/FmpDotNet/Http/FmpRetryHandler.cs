using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Http;

/// <summary>Re-sends a request the upstream failed to answer — a 5xx, or a fault that never produced a response at
/// all. Nothing else is retried.
///
/// <para><b>429 is deliberately excluded, and that is the whole shape of this handler.</b> A 429 is already
/// answered, and answered better than a re-send could: <see cref="FmpRateLimitHandlerBase"/> drains the SHARED
/// reservoir and holds it for the advised <c>Retry-After</c>, so every caller in the process meets back-pressure
/// rather than only the one that drew the refusal. Re-sending on top of that amplifies load at precisely the moment
/// FMP is already refusing us. A consumer of this SDK has measured the harm: it had to strip
/// <c>AddStandardResilienceHandler</c> off both clients because its retry did exactly this and its circuit breaker
/// then cascaded a handful of 429s into thousands of skipped symbols.</para>
///
/// <para><b>4xx is excluded for the opposite reason</b> — re-sending changes nothing. A 400 is a malformed
/// request, a 402/403 an entitlement or credential answer, a 404 an absent resource. Each is a stable fact about
/// the request, and three attempts produce three identical refusals a second apart.</para>
///
/// <para><b>Ordering is part of the contract, and the obvious placement is the wrong one.</b> This handler is
/// registered OUTSIDE <see cref="FmpRateLimitHandlerBase"/>, which acquires its token BEFORE delegating. A retry
/// placed inside would be reached after that single token was already drawn, so every attempt after the first
/// would bypass the reservoir entirely — the opposite of what a throttle is for, and worst on the bulk client
/// where <see cref="FmpOptions.BulkPerMinuteCap"/> defaults to 2 and three unthrottled attempts would spend
/// ninety seconds of budget in one burst. Outside, each attempt re-acquires a token, so a retry meets the same
/// back-pressure as any other call. It stays outside <see cref="FmpTimeoutHandlerBase"/> as well, so every attempt
/// gets a fresh <see cref="FmpOptions.RequestTimeout"/> rather than sharing one budget between them.</para>
///
/// <para><b>A <see cref="TimeoutException"/> is not retried.</b> The timeout handler sits inside this one, so its
/// expiry passes straight through. Retrying it would silently multiply a caller's configured
/// <see cref="FmpOptions.RequestTimeout"/> by <see cref="FmpOptions.MaxAttempts"/> — a 30-second budget becoming a
/// 90-second one, with nothing in the configuration saying so.</para>
///
/// <para><b>The retry is confined to the pre-body phase, and on the streaming surface that limit is load-bearing
/// rather than incidental.</b> This handler returns as soon as it has a response; a bulk download is read by
/// <see cref="FmpTransport.StreamCsvAsync{T}"/> under
/// <see cref="HttpCompletionOption.ResponseHeadersRead"/> long after that. A fault while those rows are being
/// enumerated is past this handler and stays the caller's problem — see that method's remarks.</para></summary>
public abstract class FmpRetryHandlerBase : DelegatingHandler
{
    private readonly IClock _clock;
    private readonly int _maxAttempts;
    private readonly Duration _baseDelay;
    private readonly Duration _maxDelay;
    private readonly ILogger _logger;
    private readonly Func<Duration, CancellationToken, Task> _delay;

    /// <summary>Creates the handler. <paramref name="delay"/> exists so a test can assert what was waited without
    /// waiting it: a <c>FakeClock</c> cannot help, because a backoff is a real <see cref="Task.Delay(TimeSpan,
    /// CancellationToken)"/> rather than clock arithmetic.</summary>
    private protected FmpRetryHandlerBase(
        IClock clock, int maxAttempts, Duration baseDelay, Duration maxDelay, ILogger logger,
        Func<Duration, CancellationToken, Task>? delay = null)
    {
        _clock = clock;
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _logger = logger;
        // ToTimeSpan at the BCL boundary only — Task.Delay has no NodaTime overload.
        _delay = delay ?? ((d, token) => Task.Delay(d.ToTimeSpan(), token));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            Duration wait;
            try
            {
                var response = await base.SendAsync(request, ct).ConfigureAwait(false);
                if (attempt >= _maxAttempts || (int)response.StatusCode < 500) return response;

                wait = BackoffFor(attempt, response);
                LogRetry(attempt, $"HTTP {(int)response.StatusCode}", wait, request);
                // Disposing before the next attempt is not tidiness: an undisposed response holds its connection
                // out of the pool, so a retry loop that dropped them would leak one per failed attempt.
                response.Dispose();
            }
            // The filter, rather than a rethrow in the body, is what keeps the ORIGINAL exception and its stack
            // intact on the final attempt: an unmatched filter never unwinds, so the last fault propagates as
            // itself rather than as something this handler re-threw. A socket reset, a refused connection and a
            // DNS failure all arrive here as HttpRequestException.
            catch (HttpRequestException ex) when (attempt < _maxAttempts)
            {
                wait = BackoffFor(attempt, response: null);
                LogRetry(attempt, ex.GetType().Name, wait, request);
            }

            await _delay(wait, ct).ConfigureAwait(false);
        }
    }

    /// <summary>How long to wait before <paramref name="attempt"/>'s successor.
    ///
    /// <para>An advised <c>Retry-After</c> wins over the computed backoff — when the upstream says when to come
    /// back, guessing is strictly worse than listening, and it is the one place a blind exponential (fmpsdk's,
    /// among others) leaves information on the table. Both forms are clamped by
    /// <see cref="FmpOptions.MaxRetryDelay"/>: the header is upstream-controlled, and the exponential would
    /// otherwise reach hours on a long attempt chain. That ceiling is deliberately NOT
    /// <see cref="FmpOptions.MaxRetryAfter"/>, which may legitimately be zero — reusing it would make "do not hold
    /// the shared bucket on a 429" silently also mean "re-send with no pacing".</para></summary>
    private Duration BackoffFor(int attempt, HttpResponseMessage? response)
    {
        if (response is not null
            && FmpRateLimitHandlerBase.ReadRetryAfter(response, _clock.GetCurrentInstant()) is { } advised)
            return Clamp(advised);

        // Deliberately computed in seconds rather than in Duration arithmetic. Duration throws on overflow, and a
        // large MaxAttempts reaches that long before the clamp below could bind — so the doubling happens where
        // overflow saturates instead of throwing, and is bounded BEFORE it becomes a Duration again.
        var baseSeconds = _baseDelay.TotalSeconds;
        // Jitter up to one base delay, so clients that started together do not retry together and hand the
        // upstream the same synchronised wave it just failed to serve.
        var seconds = (baseSeconds * Math.Pow(2, attempt - 1)) + (baseSeconds * Random.Shared.NextDouble());
        return Clamp(Duration.FromSeconds(Math.Min(seconds, _maxDelay.TotalSeconds)));
    }

    private Duration Clamp(Duration wait) =>
        wait < Duration.Zero ? Duration.Zero : wait > _maxDelay ? _maxDelay : wait;

    private void LogRetry(int attempt, string cause, Duration wait, HttpRequestMessage request) =>
        _logger.LogWarning(
            "FMP request failed with {Cause} on attempt {Attempt} of {MaxAttempts}; retrying in {Wait}: {Request}.",
            cause, attempt, _maxAttempts, wait, Describe(request));

    /// <summary>Renders a request for a log line with any <c>apikey</c> query parameter removed. The transport
    /// sends the key as a header, so a URI it built carries none; a caller-pasted <c>?apikey=</c> path would (see
    /// <see cref="UriRedaction"/>).</summary>
    private static string Describe(HttpRequestMessage request)
        => request.RequestUri is null ? request.Method.ToString()
            : $"{request.Method} {UriRedaction.Redact(request.RequestUri)}";
}

/// <summary>Retries ordinary FMP traffic under <see cref="FmpOptions.MaxAttempts"/>.</summary>
public sealed class FmpRetryHandler(IClock clock, IOptions<FmpOptions> options, ILogger<FmpRetryHandler> logger)
    : FmpRetryHandlerBase(
        clock, options.Value.MaxAttempts, options.Value.RetryBaseDelay, options.Value.MaxRetryDelay, logger);

/// <summary>Retries the <c>*-bulk</c> endpoints under <see cref="FmpOptions.BulkMaxAttempts"/>, which defaults to
/// 1 — no retry at all.
///
/// <para>Bulk is not ordinary traffic with bigger payloads; its budget is a different order.
/// <see cref="FmpOptions.BulkPerMinuteCap"/> defaults to 2, so one extra attempt costs thirty seconds of the
/// reservoir, and FMP's own error text on these endpoints warns that "frequent abuse on this API Endpoint may
/// result in restrictions placed on this API Key". A caller who wants a retry here can ask for one; nobody should
/// get it by default.</para></summary>
public sealed class FmpBulkRetryHandler(
    IClock clock, IOptions<FmpOptions> options, ILogger<FmpBulkRetryHandler> logger)
    : FmpRetryHandlerBase(
        clock, options.Value.BulkMaxAttempts, options.Value.RetryBaseDelay, options.Value.MaxRetryDelay, logger);
