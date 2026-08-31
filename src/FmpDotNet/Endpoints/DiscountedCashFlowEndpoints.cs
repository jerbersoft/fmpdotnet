using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Discounted cash flow — FMP's own valuations, and two models you can drive with your own
/// assumptions. Four paths.
///
/// <para><b>Four things hold across this group, every one of them measured 2026-08-31, and not one of them
/// catchable by a caller.</b></para>
///
/// <list type="number">
///   <item><description><b>Levered and unlevered are not near each other.</b> KO reads <b>83.71</b>
///     unlevered against <b>49.77</b> levered — a 41% gap — and JPM 728.00 against 907.85, in the opposite
///     direction. Neither is "the" DCF, which is why <see cref="DcfValuation"/> and
///     <see cref="LeveredDcfValuation"/> are two types despite an identical wire
///     shape.</description></item>
///   <item><description><b>The plain and custom paths do not reconcile, and neither reconciles with its own
///     price.</b> Five symbols captured back to back agreed to within ±0.18 and matched exactly on
///     <b>none</b>, with the sign inconsistent. The plain path is a stored daily value — AAPL read
///     <c>dcf = 145.66380328033068</c> identically across captures minutes apart — while the custom path
///     recomputes off a price that moved 314.74 → 314.85 → 314.87 in the same window. Their two price columns
///     disagree <b>in both directions</b>: AAPL -4.83, MSFT -2.50, XOM <b>+2.50</b>. <b>Do not reconstruct or
///     reconcile a price across these endpoints.</b> This replicates the finding already documented on
///     <see cref="ExchangeVariant.DcfDiff"/>, measured 2026-08-27 on a different pair of
///     paths.</description></item>
///   <item><description><b>The two custom paths honour two different override vocabularies, and the
///     difference is silent.</b> A parameter one accepts is discarded by the other at HTTP 200 with a
///     valuation identical to the baseline. Hence one assumptions record per path — see
///     <see cref="CustomDcfAssumptions"/>.</description></item>
///   <item><description><b>The custom responses mix history and forecast and do not say where the line
///     is.</b> Ten rows, descending 2030 → 2021, no flag on the wire, and two fields implying two different
///     boundaries. See <see cref="CustomDcfProjection"/>.</description></item>
/// </list>
///
/// <para><b>No <c>limit</c> and no <c>page</c> on any of the four</b>, because neither is honoured:
/// <c>custom-discounted-cash-flow?symbol=AAPL&amp;limit=3</c> returned the full 10 rows. <b>And no uppercase
/// guard on <c>symbol</c></b>: <c>symbol=aapl</c> answered <c>"AAPL"</c> with byte-identical values, so a
/// guard invented here would reject a request FMP serves — unlike the News searches, where lowercase returns
/// 0 rows at HTTP 200.</para></summary>
public sealed class DiscountedCashFlowEndpoints(FmpTransport transport)
{
    /// <summary>FMP's own unlevered DCF for one symbol, from <c>stable/discounted-cash-flow</c>.
    ///
    /// <para><b>A stored daily value.</b> Measured 2026-08-31, repeated calls minutes apart returned
    /// figures identical to all 14 decimal places. Use <see cref="GetCustomValuationAsync"/> for a model that
    /// recomputes.</para></summary>
    /// <param name="symbol">The ticker. Case is not checked — FMP normalises it. Required and
    /// non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<DcfValuation>> GetValuationAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/discounted-cash-flow").With("symbol", symbol),
            FmpJsonContext.Default.ListDcfValuation, ct);
    }

    /// <summary>FMP's own levered DCF for one symbol, from <c>stable/levered-discounted-cash-flow</c>.
    ///
    /// <para><b>Not a refinement of <see cref="GetValuationAsync"/> — a different question with a different
    /// answer.</b> Measured 2026-08-27/31, KO reads 83.71 unlevered against 49.77 here and JPM 728.00 against
    /// 907.85. The return type differs from the unlevered method's so the two cannot be confused after the
    /// call.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<LeveredDcfValuation>> GetLeveredValuationAsync(
        string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/levered-discounted-cash-flow").With("symbol", symbol),
            FmpJsonContext.Default.ListLeveredDcfValuation, ct);
    }

    /// <summary>An unlevered DCF driven by your own assumptions, from
    /// <c>stable/custom-discounted-cash-flow</c>.
    ///
    /// <para><b>Ten rows per response, mixing history and forecast with nothing on the wire marking which is
    /// which.</b> See <see cref="CustomDcfProjection"/>.</para>
    ///
    /// <para><b>Passing <see langword="null"/> asks for FMP's own default assumptions</b>, which is the same
    /// request an empty <see cref="CustomDcfAssumptions"/> produces: unset properties are not
    /// sent.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="assumptions">Overrides to apply. Omit for FMP's defaults. <b>Sixteen inputs, seven of
    /// which the levered path would discard</b> — see <see cref="CustomDcfAssumptions"/>. No value is
    /// validated: FMP accepts a terminal growth rate that inverts the valuation and returns the negative
    /// result.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Ten projected years, descending. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CustomDcfProjection>> GetCustomValuationAsync(
        string symbol, CustomDcfAssumptions? assumptions = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var request = new FmpRequest("stable/custom-discounted-cash-flow").With("symbol", symbol);
        return transport.GetListAsync(
            assumptions?.Apply(request) ?? request,
            FmpJsonContext.Default.ListCustomDcfProjection, ct);
    }

    /// <summary>A levered DCF driven by your own assumptions, from
    /// <c>stable/custom-levered-discounted-cash-flow</c>.
    ///
    /// <para><b>Takes a different assumptions type from <see cref="GetCustomValuationAsync"/>, and that is
    /// the point.</b> Seven overrides the unlevered path honours are accepted and <b>discarded</b> here, at
    /// HTTP 200, with a valuation identical to the baseline. The separate parameter type turns that into a
    /// compile error.</para></summary>
    /// <param name="symbol">The ticker. Required and non-blank.</param>
    /// <param name="assumptions">Overrides to apply. Omit for FMP's defaults. <b>Ten inputs</b> — see
    /// <see cref="CustomLeveredDcfAssumptions"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Ten projected years, descending. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or
    /// whitespace.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<CustomLeveredDcfProjection>> GetCustomLeveredValuationAsync(
        string symbol, CustomLeveredDcfAssumptions? assumptions = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var request = new FmpRequest("stable/custom-levered-discounted-cash-flow").With("symbol", symbol);
        return transport.GetListAsync(
            assumptions?.Apply(request) ?? request,
            FmpJsonContext.Default.ListCustomLeveredDcfProjection, ct);
    }
}
