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
    /// <para><b>Twelve of the 254 industry-PE snapshot rows read <c>pe: 0</c></b>, which means "no meaningful
    /// aggregate" rather than a ratio of zero — see <see cref="Models.IndustryPe.Pe"/>. Every one of the
    /// twelve was an industry row; no sector row carried a zero.</para>
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

    /// <summary>One sector's average price change over a range, on one exchange, from
    /// <c>stable/historical-sector-performance</c>.
    ///
    /// <para><b><paramref name="from"/> and <paramref name="to"/> are required because FMP's defaults are
    /// thirty months stale.</b> Measured 2026-08-29, omitting both returns 21 rows spanning
    /// <c>2024-02-01 … 2024-03-01</c> — HTTP 200, well-formed, and wrong for anyone who meant "recently". The
    /// two bounds were measured separately: <c>to</c> alone backfills <c>from</c> to 2024-02-01, and
    /// <c>from=2024-02-20</c> alone returns 9 rows ending at 2024-03-01. <c>limit=100</c> does not move either.
    /// Recent data is reachable and plentiful; only the defaults are stuck, so this SDK makes them
    /// unreachable.</para>
    ///
    /// <para><b>The exchange is part of the fact.</b> Measured on the same window, the NASDAQ and NYSE answers
    /// for Technology disagreed on all 20 shared dates.</para>
    ///
    /// <para>History reaches back to at least <b>2000-01-03</b>, measured 2026-08-29. No row cap was reached:
    /// a single request for 2000-01-01 to 2016-01-01 returned <b>4,025 rows</b>. Rows arrive newest
    /// first.</para>
    ///
    /// <para>An unrecognised <paramref name="exchange"/> answers <c>[]</c> with HTTP 200 rather than an
    /// error.</para></summary>
    /// <param name="sector">The sector to report on.</param>
    /// <param name="exchange">The exchange to answer for. Required — see
    /// <see cref="GetSectorPerformanceSnapshotAsync"/>.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="sector"/> is not a declared member. Both are checked before
    /// the request is sent: FMP answers a backwards range with HTTP 200 and <c>[]</c>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPerformance>> GetHistoricalSectorPerformanceAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-sector-performance")
                .With("sector", sector.ToQueryValue())
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListSectorPerformance, ct);
    }

    /// <summary>One sector's aggregate price-to-earnings ratio over a range, on one exchange, from
    /// <c>stable/historical-sector-pe</c>.
    ///
    /// <para>The stale-default measurement on
    /// <see cref="GetHistoricalSectorPerformanceAsync"/> was taken on this path too — the same 21 rows spanning
    /// 2024-02-01 to 2024-03-01. Read that method's summary; it applies here unchanged.</para>
    ///
    /// <para><b>A <c>pe</c> of exactly <c>0</c> means "no meaningful aggregate"</b> — see
    /// <see cref="Models.IndustryPe.Pe"/>.</para></summary>
    /// <param name="sector">The sector to report on.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="sector"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<SectorPe>> GetHistoricalSectorPeAsync(
        Sector sector, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-sector-pe")
                .With("sector", sector.ToQueryValue())
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListSectorPe, ct);
    }

    /// <summary>One industry's average price change over a range, on one exchange, from
    /// <c>stable/historical-industry-performance</c>.
    ///
    /// <para>The stale-default measurement on <see cref="GetHistoricalSectorPerformanceAsync"/> and the
    /// vocabulary gap on <see cref="GetIndustryPerformanceSnapshotAsync"/> both apply here. An industry FMP
    /// does not carry on the requested exchange answers <c>[]</c> with HTTP 200, indistinguishable from a
    /// typo — measured 2026-08-29 with <c>industry=Banks</c>, which is in
    /// <see cref="DirectoryEndpoints.GetIndustriesAsync"/> and returns nothing anywhere.</para></summary>
    /// <param name="industry">The industry to report on, using FMP's own label. Labels carrying <c>&amp;</c>
    /// and <c>,</c> are URL-encoded for you.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="industry"/> or <paramref name="exchange"/> is null,
    /// empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPerformance>> GetHistoricalIndustryPerformanceAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(industry);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-industry-performance")
                .With("industry", industry)
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListIndustryPerformance, ct);
    }

    /// <summary>One industry's aggregate price-to-earnings ratio over a range, on one exchange, from
    /// <c>stable/historical-industry-pe</c>.
    ///
    /// <para>Everything documented on <see cref="GetHistoricalIndustryPerformanceAsync"/> applies, and
    /// <b>a <c>pe</c> of exactly <c>0</c> means "no meaningful aggregate"</b> — see
    /// <see cref="Models.IndustryPe.Pe"/>, where the twelve measured zeros are recorded.</para></summary>
    /// <param name="industry">The industry to report on, using FMP's own label.</param>
    /// <param name="exchange">The exchange to answer for. Required.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, or <c>[]</c>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="industry"/> or <paramref name="exchange"/> is null,
    /// empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IndustryPe>> GetHistoricalIndustryPeAsync(
        string industry, string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(industry);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/historical-industry-pe")
                .With("industry", industry)
                .With("exchange", exchange)
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListIndustryPe, ct);
    }
}
