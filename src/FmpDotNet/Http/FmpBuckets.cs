namespace FmpDotNet.Http;

/// <summary>The process-wide throttle reservoirs, one per traffic class.
///
/// <para>Registered as a single singleton rather than two so the invariant "exactly one reservoir per class" is
/// structural. Handlers are transient and <c>HttpClientFactory</c> rebuilds them; a per-handler bucket meant
/// several independent reservoirs and an aggregate rate above the cap.</para></summary>
public sealed class FmpBuckets
{
    /// <summary>Creates the pair from an options snapshot.</summary>
    public FmpBuckets(FmpOptions options, double nowSeconds = 0.0)
    {
        Standard = Create(options.PerMinuteCap, StandardBurst, nowSeconds);
        Bulk = Create(options.BulkPerMinuteCap, BulkBurst, nowSeconds);
    }

    /// <summary>Reservoir for ordinary JSON endpoints.</summary>
    public TokenBucket Standard { get; }

    /// <summary>Reservoir for the <c>*-bulk</c> CSV endpoints, which FMP limits separately and far more tightly.</summary>
    public TokenBucket Bulk { get; }

    /// <summary>Burst allowance for ordinary traffic. Comfortably above a typical parallel worker count, so every
    /// worker can take a token at startup without stalling, yet small enough that the worst-case rolling-minute
    /// throughput (burst + cap) stays under FMP Premium's 750/min hard limit. Setting capacity to the whole
    /// per-minute budget would let concurrent callers burst to ~2x the cap and trip 429s.</summary>
    internal const int StandardBurst = 30;

    /// <summary>Burst allowance for bulk. One: bulk responses run to tens of megabytes and FMP refreshes them only
    /// once every few hours, so there is no case in which firing two at once is what the caller meant.</summary>
    internal const int BulkBurst = 1;

    internal static TokenBucket Create(int perMinuteCap, int burst, double nowSeconds)
    {
        var perMinute = Math.Max(1, perMinuteCap);
        var capacity = Math.Max(1, Math.Min(burst, perMinute));
        return new TokenBucket(capacity, perMinute / 60.0, nowSeconds);
    }
}
