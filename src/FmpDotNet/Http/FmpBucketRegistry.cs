using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FmpDotNet.Http;

/// <summary>One reservoir pair per API key, because FMP meters per key.
///
/// <para>Registrations that share a key share a pair, so their aggregate rate stays at the cap; registrations on
/// different keys get their own, so an Ultimate key is not dragged down to a Premium key's cap. One registry per
/// container: the container wiring registers it as a singleton, and a factory-built client gets its own unless
/// it is handed one — which is how a host and a side client on the same key join reservoirs.</para>
///
/// <para>Keyed on a SHA-256 of the key rather than the key itself, so a debugger view or a diagnostic dump of
/// this dictionary is not a second legible copy of the secret. Defence in depth, not a security boundary: the key
/// is in every request URI, because that is how FMP authenticates.</para>
///
/// <para>The unset key is a real case. <see cref="FmpOptions.ApiKey"/> defaults to <c>""</c> and is never
/// validated, so every unconfigured registration shares the <c>""</c> pair. They are all going to fail the same
/// way, and sharing is what keeps a configuration-free test container working.</para>
///
/// <para>First writer wins on caps. Two registrations sharing a key but declaring different caps cannot both be
/// honoured; the first to resolve sizes the pair, and a later one that disagrees is logged as a warning naming
/// both, once per disagreeing registration. A warning rather than a throw, because the condition is recoverable
/// and the behaviour is defined. An instance created without a logger — as a consumer does to share one across
/// containers — warns nowhere.</para></summary>
public sealed class FmpBucketRegistry(ILogger<FmpBucketRegistry>? logger = null)
{
    private sealed record Entry(FmpBuckets Buckets, int PerMinuteCap, int BulkPerMinuteCap, string Registration)
    {
        public ConcurrentDictionary<string, byte> Warned { get; } = new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, Entry> _byKeyHash = new(StringComparer.Ordinal);
    private readonly ILogger _logger = logger ?? NullLogger<FmpBucketRegistry>.Instance;

    /// <summary>The pair for the API key in <paramref name="options"/>, created from these options the first
    /// time the key is seen. <paramref name="registrationName"/> is <c>""</c> for the default registration and
    /// is used only to name the parties in the cap-conflict warning.</summary>
    public FmpBuckets For(string registrationName, FmpOptions options)
    {
        ArgumentNullException.ThrowIfNull(registrationName);
        ArgumentNullException.ThrowIfNull(options);

        var entry = _byKeyHash.GetOrAdd(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.ApiKey))),
            _ => new Entry(new FmpBuckets(options), options.PerMinuteCap, options.BulkPerMinuteCap, registrationName));

        if ((entry.PerMinuteCap != options.PerMinuteCap || entry.BulkPerMinuteCap != options.BulkPerMinuteCap)
            && entry.Warned.TryAdd(registrationName, 0))
        {
            _logger.LogWarning(
                "FMP registrations {First} and {Second} share an API key but declare different caps "
                + "({FirstCap}/min, bulk {FirstBulkCap}/min against {SecondCap}/min, bulk {SecondBulkCap}/min). "
                + "The first to resolve sized the shared reservoir; the second's caps are ignored.",
                Display(entry.Registration), Display(registrationName),
                entry.PerMinuteCap, entry.BulkPerMinuteCap, options.PerMinuteCap, options.BulkPerMinuteCap);
        }

        return entry.Buckets;

        static string Display(string name) => name.Length == 0 ? "(default)" : name;
    }
}
