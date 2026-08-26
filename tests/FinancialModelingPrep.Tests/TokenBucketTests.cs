using FinancialModelingPrep.Http;

using NodaTime;

namespace FinancialModelingPrep.Tests;

public class TokenBucketTests
{
    [Fact]
    public void Grants_up_to_capacity_without_waiting()
    {
        var bucket = new TokenBucket(capacity: 3, refillPerSecond: 1, nowSeconds: 0);

        Assert.Equal(Duration.Zero, bucket.Acquire(0));
        Assert.Equal(Duration.Zero, bucket.Acquire(0));
        Assert.Equal(Duration.Zero, bucket.Acquire(0));
        Assert.True(bucket.Acquire(0) > Duration.Zero);
    }

    [Fact]
    public void Refills_at_the_configured_rate()
    {
        var bucket = new TokenBucket(capacity: 1, refillPerSecond: 2, nowSeconds: 0);
        bucket.Acquire(0);

        // Half a second at 2/s is exactly one token.
        Assert.Equal(Duration.Zero, bucket.Acquire(0.5));
    }

    [Fact]
    public void Never_refills_above_capacity()
    {
        var bucket = new TokenBucket(capacity: 2, refillPerSecond: 10, nowSeconds: 0);

        // A long idle period must not bank tokens beyond the burst allowance.
        bucket.Acquire(1000);
        bucket.Acquire(1000);
        Assert.True(bucket.Acquire(1000) > Duration.Zero);
    }

    [Fact]
    public void Drain_holds_the_reservoir_for_the_whole_penalty()
    {
        var bucket = new TokenBucket(capacity: 10, refillPerSecond: 10, nowSeconds: 0);
        bucket.Drain(nowSeconds: 0, holdSeconds: 60);

        // The wait owed must cover the rest of the hold, not just the time to refill one token (1/10 s).
        // Without the hold term a held caller spins at the refill rate for the entire penalty.
        var wait = bucket.Acquire(0);
        Assert.True(wait >= Duration.FromSeconds(60), $"expected >= 60s, got {wait}");
    }

    [Fact]
    public void Concurrent_drains_with_the_same_advice_converge_on_one_hold()
    {
        var bucket = new TokenBucket(capacity: 10, refillPerSecond: 10, nowSeconds: 0);

        // Eight callers each take a 429 advising 60 s. Stacking them would wedge every request for eight minutes.
        for (var i = 0; i < 8; i++) bucket.Drain(nowSeconds: 0, holdSeconds: 60);

        var wait = bucket.Acquire(0);
        Assert.True(wait < Duration.FromSeconds(75), $"holds stacked: waited {wait}");
    }

    [Fact]
    public void Drain_without_a_hold_still_spends_the_burst()
    {
        var bucket = new TokenBucket(capacity: 5, refillPerSecond: 1, nowSeconds: 0);
        bucket.Drain(nowSeconds: 0, holdSeconds: 0);

        // Pacing resumes at the refill rate rather than bursting straight back into the limit that just refused us.
        Assert.True(bucket.Acquire(0) > Duration.Zero);
    }

    [Fact]
    public void Bulk_and_standard_reservoirs_are_sized_independently()
    {
        var buckets = new FmpBuckets(new FmpOptions { PerMinuteCap = 660, BulkPerMinuteCap = 2 });

        Assert.Equal(FmpBuckets.StandardBurst, buckets.Standard.Capacity);
        Assert.Equal(FmpBuckets.BulkBurst, buckets.Bulk.Capacity);
        Assert.Equal(11.0, buckets.Standard.RefillPerSecond, 3);
        Assert.Equal(2 / 60.0, buckets.Bulk.RefillPerSecond, 5);
    }
}
