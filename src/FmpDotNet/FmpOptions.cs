using NodaTime;

namespace FmpDotNet;

/// <summary>Configuration for every FMP client this SDK registers.</summary>
public sealed class FmpOptions
{
    // Durations are NodaTime's, not the BCL's. TimeSpan is both a duration and a time-of-day in .NET, which is
    // exactly the ambiguity NodaTime exists to remove; conversion to TimeSpan happens only where a BCL API demands
    // one (Task.Delay, CancellationTokenSource.CancelAfter), never in this type's surface.

    /// <summary>Configuration section these options bind to by convention.</summary>
    public const string SectionName = "Fmp";

    /// <summary>API key. FMP takes it as an <c>apikey</c> QUERY parameter on every request — never a header — so
    /// the transport appends it and no caller ever builds a URL by hand.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Bare host. The <c>/stable/</c> segment belongs to each request path, not to the base address, so a
    /// future <c>/v4/</c> endpoint can sit on the same client.</summary>
    public string BaseUrl { get; set; } = "https://financialmodelingprep.com";

    /// <summary>Self-throttle ceiling for ordinary endpoints, in requests per minute. FMP Premium allows 750;
    /// the default 660 leaves headroom because the measured emitted rate runs ~10% above target under real
    /// concurrency. The bucket paces the aggregate across every client in the process.</summary>
    public int PerMinuteCap { get; set; } = 660;

    /// <summary>How long one ordinary HTTP attempt may take before <see cref="Http.FmpTimeoutHandler"/> abandons it.
    ///
    /// <para>This is NOT <c>HttpClient.Timeout</c>, which the SDK sets to infinite. It is measured from INSIDE the
    /// rate-limit handler, so time spent waiting on the shared token bucket does not consume it. That separation is
    /// load-bearing: a 429 can hold the bucket for up to <see cref="MaxRetryAfter"/>, and a timeout that counted
    /// throttle waits would convert our own back-pressure into a wave of abandoned requests exactly when the
    /// upstream is already refusing us.</para></summary>
    public Duration RequestTimeout { get; set; } = Duration.FromSeconds(30);

    /// <summary>Ceiling on the <c>Retry-After</c> hold a single 429 may impose on the shared bucket. The header is
    /// honoured, but not without bound: it is an upstream-controlled value that stops every FMP call in the
    /// process, so a misparse or a hostile <c>Retry-After: 86400</c> would otherwise idle the host for a day while
    /// its logs said only that it was waiting. Clamping is logged.</summary>
    public Duration MaxRetryAfter { get; set; } = Duration.FromSeconds(120);

    /// <summary>Self-throttle for the <c>*-bulk</c> endpoints, in requests per minute. Measured 2026-08-26: bulk is
    /// limited independently of <see cref="PerMinuteCap"/> and far more tightly — a second call moments after the
    /// first was already refused, and FMP's own error text warns that "frequent abuse on this API Endpoint may
    /// result in restrictions placed on this API Key". The default is deliberately close to a trickle; bulk data is
    /// refreshed by FMP only once every few hours, so nothing is gained by asking more often.</summary>
    public int BulkPerMinuteCap { get; set; } = 2;

    /// <summary>How long one bulk HTTP attempt may take. Separate from <see cref="RequestTimeout"/> because bulk
    /// payloads are of a different order: measured 2026-08-26, <c>ratios-ttm-bulk</c> answers 69 MB and
    /// <c>key-metrics-ttm-bulk</c> 44 MB in a single response, which the 30 s ordinary budget will not carry on a
    /// normal connection.</summary>
    public Duration BulkRequestTimeout { get; set; } = Duration.FromMinutes(10);
}
