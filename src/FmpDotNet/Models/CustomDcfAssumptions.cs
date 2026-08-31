using System.Globalization;

namespace FmpDotNet.Models;

/// <summary>Assumption overrides for the <b>unlevered</b> custom DCF,
/// <c>stable/custom-discounted-cash-flow</c>. Sixteen optional inputs; the ones left unset are not sent, so
/// an empty <see cref="CustomDcfAssumptions"/> asks for FMP's own default valuation.
///
/// <para><b>This type exists because a wrong or unrecognised parameter is silent.</b> Measured 2026-08-31,
/// <c>custom-discounted-cash-flow?symbol=AAPL&amp;notARealParam=99</c> returned HTTP 200 with
/// <c>longTermGrowthRate</c>, <c>beta</c> and <c>equityValuePerShare</c> identical to the baseline — the only
/// fields that moved were the eight that track the live price. A misspelled override therefore produces a
/// valuation that ignored it and looks exactly like one that applied it.</para>
///
/// <para><b>And it is separate from <see cref="CustomLeveredDcfAssumptions"/> because the two endpoints
/// honour different vocabularies.</b> Seven of the properties here — <see cref="EbitdaPct"/>,
/// <see cref="DepreciationAndAmortizationPct"/>, <see cref="CashAndShortTermInvestmentsPct"/>,
/// <see cref="ReceivablesPct"/>, <see cref="InventoriesPct"/>, <see cref="PayablePct"/> and
/// <see cref="EbitPct"/> — are accepted and <b>discarded</b> by the levered path, and its
/// <c>operatingCashFlowPct</c> is discarded here. Two records make handing one to the wrong endpoint a
/// compile error. <b>This is not hypothetical:</b> the independent Python <c>fmpsdk</c> assembles both calls
/// through one shared 18-parameter helper, so eight of its eighteen levered parameters do nothing.</para>
///
/// <para><b>FMP's eighteenth documented override,
/// <c>sellingGeneralAndAdministrativeExpensesPct</c>, is on neither record.</b> Probed 2026-08-31, it moved
/// nothing on either path. A property for it would be a control that does nothing.</para>
///
/// <para><b>No value is validated.</b> Measured 2026-08-27/31, <c>longTermGrowthRate=10</c> against AAPL
/// returned <c>equityValuePerShare = -1253.46</c> against 145.72 at the default rate of 4, because a
/// terminal growth rate at or above the measured <c>wacc</c> of 9.47 inverts the terminal-value denominator.
/// FMP returns the result rather than rejecting the input, and this SDK does not invent a bound FMP does not
/// enforce.</para></summary>
public sealed record CustomDcfAssumptions
{
    /// <summary>Revenue growth per year, as a percentage. Wire name <c>revenueGrowthPct</c>. Honoured on both
    /// custom paths.</summary>
    public decimal? RevenueGrowthPct { get; init; }

    /// <summary>EBITDA as a percentage of revenue. Wire name <c>ebitdaPct</c>. <b>Discarded by the levered
    /// path</b>, which is why it is not on <see cref="CustomLeveredDcfAssumptions"/>.</summary>
    public decimal? EbitdaPct { get; init; }

    /// <summary>Depreciation and amortisation as a percentage of revenue. Wire name
    /// <c>depreciationAndAmortizationPct</c>. <b>Discarded by the levered path.</b></summary>
    public decimal? DepreciationAndAmortizationPct { get; init; }

    /// <summary>Cash and short-term investments as a percentage of revenue. Wire name
    /// <c>cashAndShortTermInvestmentsPct</c>. <b>Discarded by the levered path.</b></summary>
    public decimal? CashAndShortTermInvestmentsPct { get; init; }

    /// <summary>Receivables as a percentage of revenue. Wire name <c>receivablesPct</c>. <b>Discarded by the
    /// levered path.</b></summary>
    public decimal? ReceivablesPct { get; init; }

    /// <summary>Inventories as a percentage of revenue. Wire name <c>inventoriesPct</c>. <b>Discarded by the
    /// levered path.</b></summary>
    public decimal? InventoriesPct { get; init; }

    /// <summary>Payables as a percentage of revenue. Wire name <c>payablePct</c> — singular, as FMP spells
    /// it. <b>Discarded by the levered path.</b></summary>
    public decimal? PayablePct { get; init; }

    /// <summary>EBIT as a percentage of revenue. Wire name <c>ebitPct</c>. <b>Discarded by the levered
    /// path.</b></summary>
    public decimal? EbitPct { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue — negative on measured rows. Wire name
    /// <c>capitalExpenditurePct</c>. Honoured on both custom paths.</summary>
    public decimal? CapitalExpenditurePct { get; init; }

    /// <summary>The effective tax rate, as a percentage. Wire name <c>taxRate</c>. <b>Not the same quantity
    /// as <see cref="CustomDcfProjection.TaxRateCash"/></b>, which is an amount in dollars.</summary>
    public decimal? TaxRate { get; init; }

    /// <summary>The terminal growth rate, as a percentage. Wire name <c>longTermGrowthRate</c>.
    ///
    /// <para><b>Setting this at or above the model's <c>wacc</c> inverts the valuation, and FMP returns the
    /// negative result rather than rejecting it.</b> Measured 2026-08-27/31, <c>10</c> against AAPL produced
    /// <c>equityValuePerShare = -1253.46</c> where the default 4 produced 145.72, against a measured
    /// <c>wacc</c> of 9.47. The SDK does not bound it — see the type's summary.</para></summary>
    public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Cost of debt, as a percentage. <b>Wire name <c>costOfDebt</c>, camelCase</b> — note that the
    /// <i>response</i> spells the same concept <c>costofDebt</c> with a lowercase <c>o</c>. See
    /// <see cref="CustomDcfProjection.CostOfDebt"/>.</summary>
    public decimal? CostOfDebt { get; init; }

    /// <summary>Cost of equity, as a percentage. Wire name <c>costOfEquity</c>.
    ///
    /// <para><b>The eighteenth override, and it was found by reading the independent Python <c>fmpsdk</c>
    /// rather than by probing.</b> The measure phase tried seventeen candidate names chosen by guesswork and
    /// missed it. Probed 2026-08-31 it is honoured on <b>both</b> custom paths, moving <c>costOfEquity</c>,
    /// <c>wacc</c>, <c>terminalValue</c>, <c>presentTerminalValue</c> and <c>sumPvUfcf</c>. The lesson is
    /// recorded rather than hidden: a self-selected probe list is a lower bound on a parameter vocabulary,
    /// never a census.</para></summary>
    public decimal? CostOfEquity { get; init; }

    /// <summary>The equity risk premium, as a percentage. Wire name <c>marketRiskPremium</c>.</summary>
    public decimal? MarketRiskPremium { get; init; }

    /// <summary>The beta to use. Wire name <c>beta</c>.</summary>
    public decimal? Beta { get; init; }

    /// <summary>The risk-free rate, as a percentage. Wire name <c>riskFreeRate</c>.</summary>
    public decimal? RiskFreeRate { get; init; }

    /// <summary>Writes every set assumption onto <paramref name="request"/> and returns it.
    ///
    /// <para><see cref="FmpRequest.With(string, string?)"/> already drops nulls, so the unset properties never
    /// reach the query string — which is what makes an empty <see cref="CustomDcfAssumptions"/> a request for
    /// FMP's own default valuation rather than a request for nothing.</para></summary>
    internal FmpRequest Apply(FmpRequest request) =>
        request
            .With("revenueGrowthPct", Number(RevenueGrowthPct))
            .With("ebitdaPct", Number(EbitdaPct))
            .With("depreciationAndAmortizationPct", Number(DepreciationAndAmortizationPct))
            .With("cashAndShortTermInvestmentsPct", Number(CashAndShortTermInvestmentsPct))
            .With("receivablesPct", Number(ReceivablesPct))
            .With("inventoriesPct", Number(InventoriesPct))
            .With("payablePct", Number(PayablePct))
            .With("ebitPct", Number(EbitPct))
            .With("capitalExpenditurePct", Number(CapitalExpenditurePct))
            .With("taxRate", Number(TaxRate))
            .With("longTermGrowthRate", Number(LongTermGrowthRate))
            .With("costOfDebt", Number(CostOfDebt))
            .With("costOfEquity", Number(CostOfEquity))
            .With("marketRiskPremium", Number(MarketRiskPremium))
            .With("beta", Number(Beta))
            .With("riskFreeRate", Number(RiskFreeRate));

    /// <summary>Formats an assumption invariantly.
    ///
    /// <para>The culture is the point, and the reasoning is <see cref="ScreenerCriteria"/>'s: a value
    /// formatted under a comma-decimal culture becomes <c>beta=1,1</c> in the query string, and FMP does not
    /// reject it — an unparseable value is treated like an unrecognised one, which on this endpoint means the
    /// caller silently receives FMP's <i>default</i> valuation on a German or French host and their own
    /// everywhere else.</para></summary>
    internal static string? Number(decimal? value) =>
        value?.ToString(null, CultureInfo.InvariantCulture);
}

/// <summary>Assumption overrides for the <b>levered</b> custom DCF,
/// <c>stable/custom-levered-discounted-cash-flow</c>. Ten optional inputs — nine shared with
/// <see cref="CustomDcfAssumptions"/> and one of its own.
///
/// <para><b>Seven of the unlevered record's sixteen properties are missing here on purpose</b>, because the
/// levered endpoint accepts them and <b>discards</b> them at HTTP 200: <c>ebitdaPct</c>,
/// <c>depreciationAndAmortizationPct</c>, <c>cashAndShortTermInvestmentsPct</c>, <c>receivablesPct</c>,
/// <c>inventoriesPct</c>, <c>payablePct</c> and <c>ebitPct</c>, all probed 2026-08-31. A caller who hands one
/// of them here gets a valuation that ignored their assumption and no indication of it. With two records
/// that does not compile.</para>
///
/// <para>Everything else — that a wrong parameter is silent, that
/// <c>sellingGeneralAndAdministrativeExpensesPct</c> is exposed on neither record, and that no value is
/// validated — is as <see cref="CustomDcfAssumptions"/> records it.</para></summary>
public sealed record CustomLeveredDcfAssumptions
{
    /// <summary>Revenue growth per year, as a percentage. Wire name <c>revenueGrowthPct</c>.</summary>
    public decimal? RevenueGrowthPct { get; init; }

    /// <summary>Operating cash flow as a percentage of revenue. Wire name <c>operatingCashFlowPct</c>.
    /// <b>The one override this path honours and the unlevered path discards</b>, probed
    /// 2026-08-31.</summary>
    public decimal? OperatingCashFlowPct { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Wire name
    /// <c>capitalExpenditurePct</c>.</summary>
    public decimal? CapitalExpenditurePct { get; init; }

    /// <summary>The effective tax rate, as a percentage. Wire name <c>taxRate</c>.</summary>
    public decimal? TaxRate { get; init; }

    /// <summary>The terminal growth rate, as a percentage. Wire name <c>longTermGrowthRate</c>. Setting it
    /// at or above the model's <c>wacc</c> inverts the valuation and FMP returns the negative result — see
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>.</summary>
    public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Cost of debt, as a percentage. Wire name <c>costOfDebt</c> on the request and
    /// <c>costofDebt</c> on the response — see <see cref="CustomLeveredDcfProjection.CostOfDebt"/>.</summary>
    public decimal? CostOfDebt { get; init; }

    /// <summary>Cost of equity, as a percentage. Wire name <c>costOfEquity</c>. Probed 2026-08-31 it moves
    /// <c>costOfEquity</c>, <c>wacc</c>, <c>terminalValue</c>, <c>presentTerminalValue</c>, <c>pvLfcf</c> and
    /// <c>sumPvLfcf</c> — see <see cref="CustomDcfAssumptions.CostOfEquity"/> for how it was
    /// found.</summary>
    public decimal? CostOfEquity { get; init; }

    /// <summary>The equity risk premium, as a percentage. Wire name <c>marketRiskPremium</c>.</summary>
    public decimal? MarketRiskPremium { get; init; }

    /// <summary>The beta to use. Wire name <c>beta</c>.</summary>
    public decimal? Beta { get; init; }

    /// <summary>The risk-free rate, as a percentage. Wire name <c>riskFreeRate</c>.</summary>
    public decimal? RiskFreeRate { get; init; }

    /// <summary>Writes every set assumption onto <paramref name="request"/> and returns it. Unset properties
    /// are dropped — see <see cref="CustomDcfAssumptions.Apply"/>.
    ///
    /// <para><b>Deliberately written out rather than shared with the unlevered record.</b> Nine of these ten
    /// lines are identical to nine of that record's sixteen, and the duplication is the point: it is the
    /// only place in the SDK where the two vocabularies sit side by side and can be compared line for
    /// line.</para></summary>
    internal FmpRequest Apply(FmpRequest request) =>
        request
            .With("revenueGrowthPct", CustomDcfAssumptions.Number(RevenueGrowthPct))
            .With("operatingCashFlowPct", CustomDcfAssumptions.Number(OperatingCashFlowPct))
            .With("capitalExpenditurePct", CustomDcfAssumptions.Number(CapitalExpenditurePct))
            .With("taxRate", CustomDcfAssumptions.Number(TaxRate))
            .With("longTermGrowthRate", CustomDcfAssumptions.Number(LongTermGrowthRate))
            .With("costOfDebt", CustomDcfAssumptions.Number(CostOfDebt))
            .With("costOfEquity", CustomDcfAssumptions.Number(CostOfEquity))
            .With("marketRiskPremium", CustomDcfAssumptions.Number(MarketRiskPremium))
            .With("beta", CustomDcfAssumptions.Number(Beta))
            .With("riskFreeRate", CustomDcfAssumptions.Number(RiskFreeRate));
}
