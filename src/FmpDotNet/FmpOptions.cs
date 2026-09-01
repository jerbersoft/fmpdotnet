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

    /// <summary>Self-throttle ceiling for ordinary endpoints, in requests per minute. The bucket paces the
    /// aggregate across every client in the process.
    ///
    /// <para>The default 660 is calibrated to <b>Premium's 750/min</b>, the lowest paid tier this SDK targets,
    /// leaving headroom because the measured emitted rate runs ~10% above target under real concurrency. It is
    /// deliberately not calibrated to the key you happen to hold: a default that suited a higher tier would trip
    /// 429s for everyone below it.</para>
    ///
    /// <para><b>On a higher tier, raise this.</b> Ultimate allows 3,000/min, so a caller on that plan is leaving
    /// roughly four-fifths of their budget unused at the default. Set it to about 88% of your tier's published
    /// limit to keep the same headroom — 2,640 for Ultimate.</para></summary>
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
    /// its logs said only that it was waiting. Clamping is logged.
    ///
    /// <para><b>Zero is a legitimate setting</b> — it means a 429 drains the bucket but holds it for nothing — and
    /// that is exactly why the retry backoff is capped by <see cref="MaxRetryDelay"/> and not by this. Sourcing
    /// both ceilings here would make "do not hold the bucket" silently also mean "re-send with no pacing".</para></summary>
    public Duration MaxRetryAfter { get; set; } = Duration.FromSeconds(120);

    /// <summary>How many times one ordinary request may be SENT before its failure is handed to the caller —
    /// attempts, not retries, so the default of 3 means at most two re-sends and <b>1 disables retrying
    /// entirely</b>.
    ///
    /// <para>Only a 5xx and a transport-level fault are retried. <b>A 429 is not</b>, deliberately: it is already
    /// answered by draining the shared token bucket, so every caller in the process meets back-pressure rather
    /// than the one that drew the refusal, and re-sending would amplify load exactly when FMP is refusing us. A
    /// 4xx is not retried either — it is a stable fact about the request, and three attempts would produce three
    /// identical refusals. See <see cref="Http.FmpRetryHandlerBase"/>.</para></summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>First step of the retry backoff, doubling per attempt with jitter of up to one further
    /// <see cref="RetryBaseDelay"/> on top — so the default yields waits of roughly 1-2s then 2-3s. Capped by
    /// <see cref="MaxRetryDelay"/>, and overridden entirely when the failing response carried a
    /// <c>Retry-After</c>.
    ///
    /// <para>The jitter is not decoration. Without it, every client that started together retries together, and
    /// the upstream meets the same synchronised wave it has just failed to serve.</para></summary>
    public Duration RetryBaseDelay { get; set; } = Duration.FromSeconds(1);

    /// <summary>Ceiling on a single retry wait — both the doubling backoff and any <c>Retry-After</c> the failing
    /// response advised.
    ///
    /// <para><b>Separate from <see cref="MaxRetryAfter"/> on purpose, and the two are not interchangeable.</b>
    /// That one bounds how long a 429 may stop <i>every</i> call in the process, where zero is a coherent choice;
    /// this one bounds how long <i>one</i> call waits before trying again, where zero means an unpaced burst
    /// against an upstream that is already failing. Validation reflects the difference: that may be zero, this
    /// may not.</para></summary>
    public Duration MaxRetryDelay { get; set; } = Duration.FromSeconds(120);

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

    /// <summary>How many times one <c>*-bulk</c> request may be SENT. <b>The default of 1 means no retry at
    /// all</b>, which is the asymmetry with <see cref="MaxAttempts"/> and is deliberate.
    ///
    /// <para>Bulk is not ordinary traffic with larger payloads; its budget is a different order.
    /// <see cref="BulkPerMinuteCap"/> defaults to 2, and the retry sits outside the throttle so that every attempt
    /// re-acquires a token — which means one extra attempt costs thirty seconds of the bulk reservoir. FMP's own
    /// error text on these endpoints warns that "frequent abuse on this API Endpoint may result in restrictions
    /// placed on this API Key". Raise it if a run genuinely needs it; nobody should get it by default.</para></summary>
    public int BulkMaxAttempts { get; set; } = 1;

    /// <summary>Directory in which to replay <c>*-bulk</c> responses from disk instead of calling FMP. Null or
    /// empty — the default — means every bulk call goes to the upstream.
    ///
    /// <para><b>A development aid, not a caching layer, and the distinction is load-bearing.</b> Entries never
    /// expire and are never invalidated: once a response is on disk it is served forever, so a run against a set
    /// directory is a run against whatever FMP said the first time. Setting this in a deployed application means
    /// that application silently stops reading live data.</para>
    ///
    /// <para><b>Why it exists.</b> Bulk is throttled separately and far more tightly than the ordinary endpoints —
    /// measured 2026-08-26, a second call moments after the first was already refused — and FMP's own error text
    /// warns that "frequent abuse on this API Endpoint may result in restrictions placed on this API Key". Working
    /// on a CSV mapper means re-reading the same response repeatedly, against payloads reaching 69 MB, for data
    /// FMP refreshes only once every few hours. Those repeat calls buy nothing and spend the key's standing.</para>
    ///
    /// <para>Delete the directory to refetch. See <see cref="FmpDotNet.Http.FmpDeveloperBulkCacheHandler"/>.</para></summary>
    public string? DeveloperBulkCacheDirectory { get; set; }
}
