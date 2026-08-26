using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Http;

/// <summary>Self-throttle on an FMP <c>HttpClient</c>. A throttled caller awaits <c>Task.Delay</c> then re-checks,
/// so refill during the wait is accounted for. The bucket is a process-wide singleton, so every handler instance
/// <c>HttpClientFactory</c> builds throttles against ONE shared reservoir.
///
/// <para>Throttling is proactive AND reactive: a 429 drains the shared reservoir and holds it for the response's
/// <c>Retry-After</c>. Reacting here rather than in each endpoint client is deliberate — back-pressure belongs to
/// the shared budget, not to whichever call happened to draw the 429, so a 429 taken by one endpoint correctly
/// slows the traffic that caused it.</para>
///
/// <para>The response is handed back unchanged; the transport turns it into
/// <see cref="FmpRateLimitedException"/>. What changes is that the retry now meets a drained reservoir.</para></summary>
public abstract class FmpRateLimitHandlerBase : DelegatingHandler
{
    private readonly IClock _clock;
    private readonly TokenBucket _bucket;
    private readonly Duration _maxRetryAfter;
    private readonly ILogger _logger;

    private protected FmpRateLimitHandlerBase(
        IClock clock, TokenBucket bucket, FmpOptions options, ILogger logger)
    {
        _clock = clock;
        _bucket = bucket;
        _maxRetryAfter = options.MaxRetryAfter;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        while (true)
        {
            // TokenBucket serialises its own state, so no external lock is needed even though this reservoir is
            // shared across every handler instance and every concurrent request.
            var wait = _bucket.Acquire(NowSeconds());
            if (wait <= Duration.Zero) break;
            // ToTimeSpan at the BCL boundary only — Task.Delay has no NodaTime overload.
            await Task.Delay(wait.ToTimeSpan(), ct).ConfigureAwait(false);
        }

        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) ApplyBackoff(response);
        return response;
    }

    private void ApplyBackoff(HttpResponseMessage response)
    {
        var advised = ReadRetryAfter(response, _clock.GetCurrentInstant());
        var hold = advised is null ? Duration.Zero
            : advised > _maxRetryAfter ? _maxRetryAfter
            : advised.Value;

        if (advised > _maxRetryAfter)
            _logger.LogWarning(
                "FMP answered 429 with Retry-After {Advised}, clamped to {Clamp} (Fmp:MaxRetryAfter). Every FMP "
                + "request on this reservoir is held until then.", advised, _maxRetryAfter);
        else
            _logger.LogWarning(
                "FMP answered 429; draining the shared request budget and holding it for {Hold}. "
                + "Retry-After was {Advised}.", hold, advised is null ? "absent" : advised.ToString());

        _bucket.Drain(NowSeconds(), hold.TotalSeconds);
    }

    /// <summary>The advised wait from a 429, or null when the response carries none. RFC 9110 allows both a
    /// delta-seconds and an HTTP-date form and FMP is not documented to send either, so both are read and anything
    /// else is treated as absent — a drain with no hold, which is still a real reaction. A date already in the past
    /// yields zero rather than a negative hold.</summary>
    internal static Duration? ReadRetryAfter(HttpResponseMessage response, Instant now)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;
        if (header.Delta is { } delta)
            return delta < TimeSpan.Zero ? Duration.Zero : Duration.FromTimeSpan(delta);
        if (header.Date is { } date)
        {
            var until = Instant.FromDateTimeOffset(date) - now;
            return until <= Duration.Zero ? Duration.Zero : until;
        }
        return null;
    }

    private double NowSeconds() => _clock.GetCurrentInstant().ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
}

/// <summary>Throttles ordinary FMP traffic against <see cref="FmpBuckets.Standard"/>.</summary>
public sealed class FmpRateLimitHandler(
    IClock clock, FmpBuckets buckets, IOptions<FmpOptions> options, ILogger<FmpRateLimitHandler> logger)
    : FmpRateLimitHandlerBase(clock, buckets.Standard, options.Value, logger);

/// <summary>Throttles the <c>*-bulk</c> endpoints against <see cref="FmpBuckets.Bulk"/>, which FMP limits
/// separately from the account's per-minute cap and far more tightly.</summary>
public sealed class FmpBulkRateLimitHandler(
    IClock clock, FmpBuckets buckets, IOptions<FmpOptions> options, ILogger<FmpBulkRateLimitHandler> logger)
    : FmpRateLimitHandlerBase(clock, buckets.Bulk, options.Value, logger);
