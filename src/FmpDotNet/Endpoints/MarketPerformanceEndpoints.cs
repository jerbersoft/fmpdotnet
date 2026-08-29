using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>How the market moved — the movers lists, and sector and industry performance and valuation.
///
/// <para><b>Eleven paths in three call shapes.</b> The movers take no parameters at all; the snapshots take a
/// day; the historical paths take a range. That is why this facade has eleven methods rather than one
/// parameterised method — unlike <see cref="TechnicalIndicatorsEndpoints"/>, where nine paths shared one
/// shape.</para>
///
/// <para><b>There is no market-wide sector view, and these signatures say so.</b> Every sector and industry
/// path answers for <b>one exchange</b>: <c>exchange</c> is required here because omitting it upstream
/// silently selects NASDAQ alone, and measured 2026-08-29 that is a materially different answer — Technology on
/// 2026-08-28 read <c>-0.6192</c> on NASDAQ and <c>-1.7398</c> on NYSE, with not one of 20 shared dates
/// matching. No "all exchanges" value appeared among those measured, so a caller who wants the whole market
/// iterates <see cref="DirectoryEndpoints.GetExchangesAsync"/>. The three movers lists are the only
/// market-wide thing in this group.</para></summary>
public sealed class MarketPerformanceEndpoints(FmpTransport transport)
{
    /// <summary>The fifty biggest percentage risers of the last completed session, from
    /// <c>stable/biggest-gainers</c>.
    ///
    /// <para><b>Fifty rows, every exchange, and no parameters are accepted.</b> Measured 2026-08-29,
    /// <c>limit=10</c>, <c>exchange=NYSE</c> and <c>page=1</c> each returned a response <b>byte-identical</b>
    /// to the bare request. The list cannot be narrowed, paged or extended.</para>
    ///
    /// <para><b>The rows carry no date.</b> See <see cref="Models.MarketMover"/> — the list describes a session
    /// and never names it. <see cref="QuoteEndpoints.GetQuoteAsync"/> is where a caller learns which
    /// one.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order. Measured 2026-08-29, that order is strictly descending by
    /// percentage change. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<MarketMover>> GetBiggestGainersAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/biggest-gainers"), FmpJsonContext.Default.ListMarketMover, ct);

    /// <summary>The fifty biggest percentage fallers of the last completed session, from
    /// <c>stable/biggest-losers</c>.
    ///
    /// <para>Fifty rows, every exchange, no parameters accepted — see
    /// <see cref="GetBiggestGainersAsync"/>, where the measurement is recorded. Measured 2026-08-29, this list
    /// is ordered <b>most-negative-first</b> — the opposite direction from <see cref="GetBiggestGainersAsync"/>,
    /// which descends — and shared <b>no</b> symbol with the gainers and exactly one, <c>BTAI</c>, with the
    /// most-actives.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketMover>> GetBiggestLosersAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/biggest-losers"), FmpJsonContext.Default.ListMarketMover, ct);

    /// <summary>The fifty most active symbols of the last completed session, from
    /// <c>stable/most-actives</c>.
    ///
    /// <para><b>The response carries no volume</b>, measured 2026-08-29 — the quantity that defines the ranking
    /// is not in the body. <see cref="Models.Quote.Volume"/> has it, per symbol.</para>
    ///
    /// <para>Fifty rows, every exchange, no parameters accepted — see
    /// <see cref="GetBiggestGainersAsync"/>.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Fifty rows, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketMover>> GetMostActivesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/most-actives"), FmpJsonContext.Default.ListMarketMover, ct);

    /// <summary>Every sector's average price change on one day and one exchange, from
    /// <c>stable/sector-performance-snapshot</c>.
    ///
    /// <para><b>A date past the end of the data does not answer empty — it answers a row set whose rows do not
    /// share a date.</b> Measured 2026-08-29, <c>date=2026-09-01</c> returned 11 rows bearing <b>three</b>
    /// dates: Industrials and Real Estate at 2026-08-25, Consumer Cyclical at 2026-08-27, the other eight at
    /// 2026-08-28. <c>date=2027-01-04</c> produced that split sector for sector, identically, and
    /// <see cref="GetSectorPeSnapshotAsync"/> produced it too. It is <b>not</b> "each sector's latest row":
    /// asked for 2026-08-28 directly, Industrials and Real Estate both return rows dated 2026-08-28. The values
    /// are real and the dates are honest; the row set is simply not a coherent day.</para>
    ///
    /// <para><b>Not guarded, deliberately.</b> <see cref="Models.SectorPerformance.Date"/> is on every row, so
    /// the check is one comparison at the call site. Guarding would need a clock this library does not have,
    /// and clamping would delete real rows.</para>
    ///
    /// <para>A weekend answers <c>[]</c> with HTTP 200 — measured 2026-08-22 and 2026-08-29, both Saturdays. A
    /// market holiday does <b>not</b>: 2026-01-01 returned 11 rows dated 2026-01-01.</para>
    ///
    /// <para>An unrecognised <paramref name="exchange"/> or <paramref name="sector"/> answers <c>[]</c> with
    /// HTTP 200 rather than an error, which is why <paramref name="exchange"/> is required and
    /// <paramref name="sector"/> is an enum.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for — <c>NASDAQ</c>, <c>NYSE</c> and <c>AMEX</c> were each
    /// verified 2026-08-29. Case-insensitive. Required: omitting it upstream silently selects NASDAQ alone.
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/> lists what FMP knows.</param>
    /// <param name="sector">Narrows the answer to one sector, server-side. Omit for all eleven.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPerformance>> GetSectorPerformanceSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        return transport.GetListAsync(
            new FmpRequest("stable/sector-performance-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("sector", sector?.ToQueryValue()),
            FmpJsonContext.Default.ListSectorPerformance, ct);
    }

    /// <summary>Every sector's aggregate price-to-earnings ratio on one day and one exchange, from
    /// <c>stable/sector-pe-snapshot</c>.
    ///
    /// <para><b>The out-of-range date behaviour documented on
    /// <see cref="GetSectorPerformanceSnapshotAsync"/> was measured on this path too</b>, producing the same
    /// three-date split sector for sector. Read that method's summary; it applies here unchanged.</para>
    ///
    /// <para><b>None of the 64 measured sector-PE values read <c>0</c></b> (measured 2026-08-29). Where
    /// FMP does emit it, a <c>pe</c> of exactly <c>0</c> is an in-band sentinel meaning "no meaningful
    /// aggregate" rather than a ratio of zero — see <see cref="Models.IndustryPe.Pe"/>, the shape it was
    /// actually observed on.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="sector">Narrows the answer to one sector, server-side. Omit for all eleven.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per sector on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPe>> GetSectorPeSnapshotAsync(
        LocalDate date, string exchange, Sector? sector = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);

        return transport.GetListAsync(
            new FmpRequest("stable/sector-pe-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("sector", sector?.ToQueryValue()),
            FmpJsonContext.Default.ListSectorPe, ct);
    }

    /// <summary>Every industry's average price change on one day and one exchange, from
    /// <c>stable/industry-performance-snapshot</c>.
    ///
    /// <para><b>Fewer industries come back than <see cref="DirectoryEndpoints.GetIndustriesAsync"/> lists.</b>
    /// Measured 2026-08-29 on 2026-08-28: 126 industries on NASDAQ and 128 on NYSE, against 159 documented —
    /// a union of 139. Twenty documented names answer <c>[]</c> on every exchange, so passing that list
    /// through unfiltered produces an empty result for one name in eight, indistinguishable from a
    /// typo.</para>
    ///
    /// <para><b>The out-of-range date behaviour documented on
    /// <see cref="GetSectorPerformanceSnapshotAsync"/> applies here.</b></para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="industry">Narrows the answer to one industry, server-side, using FMP's own label. Omit for
    /// all of them. Labels carrying <c>&amp;</c> and <c>,</c> are URL-encoded for you.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace; or
    /// <paramref name="industry"/> was supplied and is empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPerformance>> GetIndustryPerformanceSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (industry is not null) ArgumentException.ThrowIfNullOrWhiteSpace(industry);

        return transport.GetListAsync(
            new FmpRequest("stable/industry-performance-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("industry", industry),
            FmpJsonContext.Default.ListIndustryPerformance, ct);
    }

    /// <summary>Every industry's aggregate price-to-earnings ratio on one day and one exchange, from
    /// <c>stable/industry-pe-snapshot</c>.
    ///
    /// <para><b>Twelve of 254 measured rows read <c>pe: 0</c></b>, which means "no meaningful aggregate" rather
    /// than a ratio of zero — see <see cref="Models.IndustryPe.Pe"/>. Every one of the twelve was an industry
    /// row; no sector row carried a zero.</para>
    ///
    /// <para>The vocabulary gap documented on <see cref="GetIndustryPerformanceSnapshotAsync"/> and the
    /// out-of-range date behaviour documented on <see cref="GetSectorPerformanceSnapshotAsync"/> both apply
    /// here.</para></summary>
    /// <param name="date">The trading day to ask about.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="industry">Narrows the answer to one industry, server-side. Omit for all of them.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per industry on that exchange, or <c>[]</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace; or
    /// <paramref name="industry"/> was supplied and is empty or whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPe>> GetIndustryPeSnapshotAsync(
        LocalDate date, string exchange, string? industry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (industry is not null) ArgumentException.ThrowIfNullOrWhiteSpace(industry);

        return transport.GetListAsync(
            new FmpRequest("stable/industry-pe-snapshot")
                .With("date", date)
                .With("exchange", exchange)
                .With("industry", industry),
            FmpJsonContext.Default.ListIndustryPe, ct);
    }
}
