using FmpDotNet.Models;
using FmpDotNet.Serialization;

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
}
