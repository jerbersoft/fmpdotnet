using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FmpDotNet.Http;
using NodaTime;
using NodaTime.Testing;

namespace FmpDotNet.Tests;

public class FmpRateLimitHandlerTests
{
    private static readonly Instant T0 = Instant.FromUtc(2026, 8, 26, 12, 0, 0);

    private static HttpClient Build(FakeClock clock, FmpOptions options, TokenBucket bucket,
        params HttpResponseMessage[] responses)
    {
        var handler = new TestRateLimitHandler(clock, bucket, options) { InnerHandler = new StubHandler(responses) };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>Exposes the shared base with a bucket chosen by the test rather than by DI.</summary>
    private sealed class TestRateLimitHandler(IClock clock, TokenBucket bucket, FmpOptions options)
        : FmpRateLimitHandlerBase(clock, bucket, options, NullLogger.Instance);

    [Fact]
    public async Task A429_drains_the_shared_reservoir_and_holds_it_for_the_advised_time()
    {
        var clock = new FakeClock(T0);
        var bucket = new TokenBucket(capacity: 10, refillPerSecond: 10, nowSeconds: Seconds(T0));
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(""),
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45)) },
        };
        using var http = Build(clock, new FmpOptions(), bucket, response);

        await http.GetAsync("stable/profile");

        // Proactive pacing alone cannot answer a 429: the reservoir must stop granting, not keep emitting at the
        // rate the upstream just rejected.
        Assert.True(bucket.Acquire(Seconds(T0)) >= Duration.FromSeconds(45));
    }

    [Fact]
    public async Task A_hostile_retry_after_is_clamped_so_one_response_cannot_idle_the_process()
    {
        var clock = new FakeClock(T0);
        var bucket = new TokenBucket(capacity: 10, refillPerSecond: 10, nowSeconds: Seconds(T0));
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(""),
            Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromDays(1)) },
        };
        var options = new FmpOptions { MaxRetryAfter = Duration.FromSeconds(120) };
        using var http = Build(clock, options, bucket, response);

        await http.GetAsync("stable/profile");

        var wait = bucket.Acquire(Seconds(T0));
        Assert.True(wait <= Duration.FromSeconds(121), $"clamp did not apply: waited {wait}");
    }

    [Fact]
    public async Task A_successful_response_spends_a_token_but_imposes_no_hold()
    {
        var clock = new FakeClock(T0);
        var bucket = new TokenBucket(capacity: 2, refillPerSecond: 1, nowSeconds: Seconds(T0));
        using var http = Build(clock, new FmpOptions(), bucket, StubHandler.Json("[]"));

        await http.GetAsync("stable/profile");

        // One of the two tokens is gone; the other is still immediately available.
        Assert.Equal(Duration.Zero, bucket.Acquire(Seconds(T0)));
        Assert.True(bucket.Acquire(Seconds(T0)) > Duration.Zero);
    }

    [Fact]
    public void Retry_after_as_an_http_date_is_read_relative_to_the_clock()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(T0.ToDateTimeOffset().AddSeconds(30)) },
        };

        Assert.Equal(Duration.FromSeconds(30), FmpRateLimitHandlerBase.ReadRetryAfter(response, T0));
    }

    [Fact]
    public void A_retry_after_date_already_in_the_past_yields_zero_not_a_negative_hold()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = new RetryConditionHeaderValue(T0.ToDateTimeOffset().AddSeconds(-30)) },
        };

        Assert.Equal(Duration.Zero, FmpRateLimitHandlerBase.ReadRetryAfter(response, T0));
    }

    [Fact]
    public void An_absent_retry_after_is_null_so_the_drain_carries_no_hold()
    {
        Assert.Null(FmpRateLimitHandlerBase.ReadRetryAfter(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests), T0));
    }

    private static double Seconds(Instant instant) =>
        instant.ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
}
