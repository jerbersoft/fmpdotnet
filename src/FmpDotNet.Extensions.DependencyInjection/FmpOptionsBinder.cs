using Microsoft.Extensions.Configuration;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Binds <see cref="FmpOptions"/> from a configuration section by name rather than by reflection.
///
/// <para><c>ConfigurationBinder.Bind</c> is neither trim- nor AOT-safe, and this assembly declares itself
/// AOT-compatible. An explicit read per option costs less than the alternatives — a source generator, or an SDK
/// that quietly breaks when a consumer publishes trimmed.</para></summary>
internal static class FmpOptionsBinder
{
    internal static void Bind(IConfiguration section, FmpOptions o)
    {
        if (section[nameof(FmpOptions.ApiKey)] is { } apiKey) o.ApiKey = apiKey;
        if (section[nameof(FmpOptions.BaseUrl)] is { } baseUrl) o.BaseUrl = baseUrl;
        if (Int32(section[nameof(FmpOptions.PerMinuteCap)]) is { } cap) o.PerMinuteCap = cap;
        if (Int32(section[nameof(FmpOptions.BulkPerMinuteCap)]) is { } bulkCap) o.BulkPerMinuteCap = bulkCap;
        if (Span(section[nameof(FmpOptions.RequestTimeout)]) is { } timeout) o.RequestTimeout = timeout;
        if (Span(section[nameof(FmpOptions.BulkRequestTimeout)]) is { } bulkTimeout) o.BulkRequestTimeout = bulkTimeout;
        if (Span(section[nameof(FmpOptions.MaxRetryAfter)]) is { } retry) o.MaxRetryAfter = retry;
        if (Int32(section[nameof(FmpOptions.MaxAttempts)]) is { } attempts) o.MaxAttempts = attempts;
        if (Int32(section[nameof(FmpOptions.BulkMaxAttempts)]) is { } bulkAttempts) o.BulkMaxAttempts = bulkAttempts;
        if (Span(section[nameof(FmpOptions.RetryBaseDelay)]) is { } backoff) o.RetryBaseDelay = backoff;
        if (Span(section[nameof(FmpOptions.MaxRetryDelay)]) is { } maxBackoff) o.MaxRetryDelay = maxBackoff;
        if (section[nameof(FmpOptions.DeveloperBulkCacheDirectory)] is { } cache)
            o.DeveloperBulkCacheDirectory = cache;

        static int? Int32(string? raw) =>
            int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        // Accepts the "00:00:30" form configuration usually carries and a bare number of seconds, which is what
        // anyone setting this from an environment variable reaches for first.
        //
        // The bare-number case is tested FIRST and deliberately: TimeSpan.TryParse("45") succeeds and yields
        // FORTY-FIVE DAYS. Trying the clock form first therefore turns "RequestTimeout=45" — the most natural
        // thing anyone would write — into a timeout that never fires, silently, with no parse error to notice.
        // The TimeSpan hop is confined to this parse; the option itself is a NodaTime Duration.
        static Duration? Span(string? raw) => raw switch
        {
            null or "" => null,
            var s when !s.Contains(':') && double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds) => Duration.FromSeconds(seconds),
            var s when TimeSpan.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v)
                => Duration.FromTimeSpan(v),
            _ => null,
        };
    }
}
