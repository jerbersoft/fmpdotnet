using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Environmental, social and governance data — per-filing scores, a company's risk rating history,
/// and sector averages to read either against.
///
/// <para><b>The sector benchmark is three years stale and says nothing about it.</b> Measured 2026-08-29,
/// the bare call answered fiscal year <b>2023</b> only. See <see cref="GetBenchmarkAsync"/>.</para>
///
/// <para><b>One parameter here is accepted and discarded</b>, which is why this facade has fewer parameters
/// than FMP's documentation implies. See <see cref="GetBenchmarkAsync"/>.</para></summary>
public sealed class EsgEndpoints(FmpTransport transport)
{
    /// <summary>One company's ESG scores, filing by filing — <c>stable/esg-disclosures</c>.
    ///
    /// <para>One row per SEC filing, newest first. Measured 2026-08-29, <c>?symbol=AAPL</c> answered 130
    /// rows across three form types — 10-Q, 10-K and the obsolete 10-K405 — with all eleven fields
    /// populated on each. See <see cref="EsgDisclosure.FormType"/> for the breakdown.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That company's scored filings. Never <see langword="null"/>; empty for a symbol FMP has not
    /// scored, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgDisclosure>> GetDisclosuresAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/esg-disclosures").With("symbol", symbol),
            FmpJsonContext.Default.ListEsgDisclosure, ct);
    }

    /// <summary>One company's ESG risk rating by fiscal year — <c>stable/esg-ratings</c>.
    ///
    /// <para><b>Not returned in year order.</b> Measured 2026-08-29 on AAPL the first three rows were 1998,
    /// 2025 and 1994. Sort on <see cref="EsgRating.FiscalYear"/> before presenting.</para>
    ///
    /// <para><see cref="EsgRating.IndustryRank"/> is the sentence <c>"3 out of 9"</c> rather than a
    /// number.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>That company's ratings, unsorted. Never <see langword="null"/>; empty for a symbol FMP has
    /// not rated, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgRating>> GetRatingsAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/esg-ratings").With("symbol", symbol),
            FmpJsonContext.Default.ListEsgRating, ct);
    }

    /// <summary>Sector-average ESG scores for one fiscal year — <c>stable/esg-benchmark</c>.
    ///
    /// <para><b>There is no <c>sector</c> parameter here, and that is not an omission.</b> FMP documents one
    /// and ignores it: measured 2026-08-29, <c>?sector=APPAREL RETAIL</c> answered a response
    /// <b>byte-identical</b> to the bare call — 1003 rows across 291 sectors. Exposing it would promise
    /// filtering the API does not perform, which is the same class of defect as the <c>-by-id</c> trap closed
    /// in #31. Filter the returned list on <see cref="EsgBenchmark.Sector"/> instead; a method parameter that
    /// looked like a query parameter but was applied locally would misrepresent what the request did.</para>
    ///
    /// <para><b>The default year is 2023</b>, three years before the measurement date. The bare call and
    /// <c>?year=2023</c> were byte-identical on 2026-08-29, and <c>?year=2025</c> answered 622 rows — fewer
    /// than 2023's 1003, but not empty. An unrecognised year answers <c>[]</c> with HTTP 200 rather than an
    /// error, so a typo reads as "no data for that year".</para></summary>
    /// <param name="year">The fiscal year. Omit for FMP's default, measured as 2023.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector per period in that year. Never <see langword="null"/>; empty for a year
    /// FMP has no benchmark for, not an error.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EsgBenchmark>> GetBenchmarkAsync(
        int? year = null, CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/esg-benchmark").With("year", year),
            FmpJsonContext.Default.ListEsgBenchmark, ct);
}
