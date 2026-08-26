using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Bulk</c> group — whole-universe CSV downloads.
///
/// <para>Every method here streams. Measured 2026-08-26, one bulk response reaches 69 MB and three of them send no
/// <c>Content-Length</c>, so there is no size at which buffering is safe.</para>
///
/// <para>These endpoints are throttled separately from the account's per-minute cap and much more tightly — a
/// second call moments after the first was already refused, and FMP's own error text warns that frequent use may
/// get the key restricted. The data behind them refreshes only once every few hours, so treat a successful
/// download as something to cache, not something to repeat.</para></summary>
public sealed class BulkEndpoints(FmpBulkTransport transport)
{
    /// <summary>Streams end-of-day bars for every symbol FMP covers on <paramref name="date"/>.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    public IAsyncEnumerable<BulkEndOfDayPrice> StreamEndOfDayAsync(LocalDate date, CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/eod-bulk").With("date", date),
            BulkEndOfDayPrice.FromCsv, ct);
}
