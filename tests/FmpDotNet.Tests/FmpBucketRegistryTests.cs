using FmpDotNet.Http;
using Microsoft.Extensions.Logging;

namespace FmpDotNet.Tests;

public class FmpBucketRegistryTests
{
    private static FmpOptions With(string apiKey, int cap = 660, int bulkCap = 2) =>
        new() { ApiKey = apiKey, PerMinuteCap = cap, BulkPerMinuteCap = bulkCap };

    [Fact]
    public void Registrations_sharing_a_key_share_one_pair()
    {
        var registry = new FmpBucketRegistry();

        var a = registry.For("a", With("K1"));
        var b = registry.For("b", With("K1"));

        // The emitted rate stays at the cap because both registrations draw from the same reservoirs.
        Assert.Same(a, b);
        Assert.Same(a.Standard, b.Standard);
        Assert.Same(a.Bulk, b.Bulk);
    }

    [Fact]
    public void Registrations_on_different_keys_get_their_own_pairs()
    {
        var registry = new FmpBucketRegistry();

        // An Ultimate key is not dragged down to a Premium key's cap.
        Assert.NotSame(registry.For("a", With("K1")), registry.For("c", With("K2")));
    }

    [Fact]
    public void The_unset_key_is_a_shared_pair_rather_than_an_error()
    {
        var registry = new FmpBucketRegistry();

        // ApiKey defaults to "" and is never validated; every unconfigured registration lands here, and they
        // are all going to fail the same way, so sharing is right.
        Assert.Same(registry.For("", With("")), registry.For("other", With("")));
    }

    [Fact]
    public void First_writer_wins_on_caps_and_the_conflict_is_logged_naming_both_registrations()
    {
        var log = new CapturingLogger();
        var registry = new FmpBucketRegistry(log);

        var first = registry.For("", With("K1", cap: 300));
        var second = registry.For("research", With("K1", cap: 3000));

        Assert.Same(first, second);
        var warning = Assert.Single(log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("(default)", warning.Message);
        Assert.Contains("research", warning.Message);
        Assert.Contains("300", warning.Message);
        Assert.Contains("3000", warning.Message);
    }

    [Fact]
    public void Agreeing_caps_do_not_warn()
    {
        var log = new CapturingLogger();
        var registry = new FmpBucketRegistry(log);

        registry.For("a", With("K1"));
        registry.For("b", With("K1"));

        Assert.Empty(log.Entries);
    }

    private sealed class CapturingLogger : ILogger<FmpBucketRegistry>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
