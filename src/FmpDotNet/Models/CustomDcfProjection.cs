using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One projected year of an <b>unlevered</b> custom discounted-cash-flow model, from
/// <c>stable/custom-discounted-cash-flow</c> — 47 keys.
///
/// <para><b>Ten rows per response, descending 2030 → 2021, mixing history and forecast — and the wire
/// carries no field saying which is which.</b> Measured 2026-08-31, two fields imply two different
/// boundaries: <see cref="RevenuePercentage"/> jitters through 2024 and smooths from 2025, while
/// <see cref="TaxRateCash"/> is constant at 16,785,417 for 2026-2030. The measurement declined to pick a
/// line and so does this SDK: <see cref="Year"/> is surfaced and the caller decides.</para>
///
/// <para><b>This path recomputes off a live price; <see cref="DcfValuation"/> is a stored daily value.</b>
/// Measured 2026-08-31, <see cref="Price"/> moved 314.74 → 314.85 → 314.87 across captures minutes apart
/// while the plain path's figures did not change at all. The two families' price columns disagree in both
/// directions — AAPL -4.83, MSFT -2.50, XOM +2.50 — so <b>do not reconcile a price across these
/// endpoints</b>.</para>
///
/// <para><b>Every numeric is <see cref="decimal"/>, and the ranges are why.</b> Measured 2026-08-31 over 290
/// rows including override probes: <see cref="Revenue"/> reaches 4.16 × 10¹⁶ and <see cref="TerminalValue"/>
/// 2.07 × 10¹⁷, while <see cref="EquityValuePerShare"/> was fractional on 289 of 290 and reached
/// <b>-1,498.72</b>. <see cref="Year"/> is the one exception, and quoting is what earns it — see its own
/// doc.</para></summary>
public sealed record CustomDcfProjection
{
    /// <summary>The projected fiscal year.
    ///
    /// <para><b>The wire sends a JSON <i>string</i> — <c>"2030"</c>, quoted.</b> It binds to
    /// <see cref="int"/> with no converter because <c>FmpJsonContext</c> sets
    /// <c>NumberHandling = AllowReadingFromString</c> globally. That quoting is also what makes
    /// <see cref="int"/> safe here where the rest of this record is <see cref="decimal"/>: a quoted value
    /// cannot arrive as <c>9.0</c>.</para></summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Projected revenue for the year.</summary>
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }

    /// <summary>Revenue growth for the year, as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.RevenueGrowthPct"/>. <b>Jitters through 2024 and smooths from
    /// 2025</b>, measured 2026-08-31 — one of the two fields that hint at where history ends.</summary>
    [JsonPropertyName("revenuePercentage")] public decimal? RevenuePercentage { get; init; }

    /// <summary>Projected EBITDA.</summary>
    [JsonPropertyName("ebitda")] public decimal? Ebitda { get; init; }

    /// <summary>EBITDA as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.EbitdaPct"/> — <b>which the levered path silently ignores</b>.</summary>
    [JsonPropertyName("ebitdaPercentage")] public decimal? EbitdaPercentage { get; init; }

    /// <summary>Projected EBIT.</summary>
    [JsonPropertyName("ebit")] public decimal? Ebit { get; init; }

    /// <summary>EBIT as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.EbitPct"/>.</summary>
    [JsonPropertyName("ebitPercentage")] public decimal? EbitPercentage { get; init; }

    /// <summary>Projected depreciation and amortisation.</summary>
    [JsonPropertyName("depreciation")] public decimal? Depreciation { get; init; }

    /// <summary>Depreciation as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.DepreciationAndAmortizationPct"/>.</summary>
    [JsonPropertyName("depreciationPercentage")] public decimal? DepreciationPercentage { get; init; }

    /// <summary>Projected cash and short-term investments.</summary>
    [JsonPropertyName("totalCash")] public decimal? TotalCash { get; init; }

    /// <summary>Cash as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.CashAndShortTermInvestmentsPct"/>.</summary>
    [JsonPropertyName("totalCashPercentage")] public decimal? TotalCashPercentage { get; init; }

    /// <summary>Projected receivables.</summary>
    [JsonPropertyName("receivables")] public decimal? Receivables { get; init; }

    /// <summary>Receivables as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.ReceivablesPct"/>.</summary>
    [JsonPropertyName("receivablesPercentage")] public decimal? ReceivablesPercentage { get; init; }

    /// <summary>Projected inventories.</summary>
    [JsonPropertyName("inventories")] public decimal? Inventories { get; init; }

    /// <summary>Inventories as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.InventoriesPct"/>.</summary>
    [JsonPropertyName("inventoriesPercentage")] public decimal? InventoriesPercentage { get; init; }

    /// <summary>Projected payables.</summary>
    [JsonPropertyName("payable")] public decimal? Payable { get; init; }

    /// <summary>Payables as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.PayablePct"/>.</summary>
    [JsonPropertyName("payablePercentage")] public decimal? PayablePercentage { get; init; }

    /// <summary>Projected capital expenditure. <b>Negative</b> on measured rows.</summary>
    [JsonPropertyName("capitalExpenditure")] public decimal? CapitalExpenditure { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Overridden by
    /// <see cref="CustomDcfAssumptions.CapitalExpenditurePct"/>.</summary>
    [JsonPropertyName("capitalExpenditurePercentage")]
    public decimal? CapitalExpenditurePercentage { get; init; }

    /// <summary>The share price the model is running against. <b>Live, and it moves between calls</b> —
    /// 314.74 → 314.85 → 314.87 for AAPL across captures minutes apart on 2026-08-31.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The beta used. Overridden by <see cref="CustomDcfAssumptions.Beta"/>.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Diluted shares outstanding. 2,793,700,000 to 15,004,697,000 measured 2026-08-31 — above
    /// Int32 on every measured row.</summary>
    [JsonPropertyName("dilutedSharesOutstanding")] public decimal? DilutedSharesOutstanding { get; init; }

    /// <summary>Cost of debt, as a percentage.
    ///
    /// <para><b>The wire name is <c>costofDebt</c> — a lowercase <c>o</c> in "of", the only field in this
    /// group that breaks camelCase.</b> Confirmed on the wire and in the independent Python <c>fmpsdk</c>'s
    /// type. Note <see cref="CostOfEquity"/> sitting beside it <i>is</i> camelCase: only one of the pair is
    /// misspelled, which is exactly the shape a copy-paste gets wrong. A test pins it.</para>
    ///
    /// <para>Overridden by <see cref="CustomDcfAssumptions.CostOfDebt"/>, whose query parameter <b>is</b>
    /// spelled <c>costOfDebt</c>. The wire is inconsistent between request and response; the SDK reproduces
    /// each side as it is.</para></summary>
    [JsonPropertyName("costofDebt")] public decimal? CostOfDebt { get; init; }

    /// <summary>The tax rate as a percentage — 15.61 to 30.11 measured 2026-08-31. <b>Not to be confused
    /// with <see cref="TaxRateCash"/></b>, which is an amount. Overridden by
    /// <see cref="CustomDcfAssumptions.TaxRate"/>.</summary>
    [JsonPropertyName("taxRate")] public decimal? TaxRate { get; init; }

    /// <summary>Cost of debt after tax, as a percentage.</summary>
    [JsonPropertyName("afterTaxCostOfDebt")] public decimal? AfterTaxCostOfDebt { get; init; }

    /// <summary>The risk-free rate as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.RiskFreeRate"/>.</summary>
    [JsonPropertyName("riskFreeRate")] public decimal? RiskFreeRate { get; init; }

    /// <summary>The equity risk premium as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.MarketRiskPremium"/>.</summary>
    [JsonPropertyName("marketRiskPremium")] public decimal? MarketRiskPremium { get; init; }

    /// <summary>Cost of equity as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.CostOfEquity"/> — <b>the eighteenth override, found by reading the
    /// Python <c>fmpsdk</c> rather than by probing</b>, and honoured on both custom paths.</summary>
    [JsonPropertyName("costOfEquity")] public decimal? CostOfEquity { get; init; }

    /// <summary>Total debt.</summary>
    [JsonPropertyName("totalDebt")] public decimal? TotalDebt { get; init; }

    /// <summary>Total equity at market value.</summary>
    [JsonPropertyName("totalEquity")] public decimal? TotalEquity { get; init; }

    /// <summary>Debt plus equity.</summary>
    [JsonPropertyName("totalCapital")] public decimal? TotalCapital { get; init; }

    /// <summary>Debt's share of total capital, as a percentage.</summary>
    [JsonPropertyName("debtWeighting")] public decimal? DebtWeighting { get; init; }

    /// <summary>Equity's share of total capital, as a percentage.</summary>
    [JsonPropertyName("equityWeighting")] public decimal? EquityWeighting { get; init; }

    /// <summary>The weighted average cost of capital, as a percentage — 5.28 to 45.96 measured 2026-08-31.
    /// <b>A <see cref="LongTermGrowthRate"/> at or above this inverts the terminal-value denominator</b> and
    /// FMP returns the negative result rather than rejecting the input; see
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>.</summary>
    [JsonPropertyName("wacc")] public decimal? Wacc { get; init; }

    /// <summary><b>A cash tax <i>amount</i> in dollars, not a rate</b> — 13,113,384 to 24,100,000 for AAPL
    /// measured 2026-08-31, while <see cref="TaxRate"/> beside it reads 15.61. The SDK keeps FMP's name
    /// rather than renaming a field a caller will look up in FMP's own documentation, and says here what it
    /// actually contains. <b>Constant at 16,785,417 for 2026-2030</b> on the measured response — one of the
    /// two fields that hint at where history ends.</summary>
    [JsonPropertyName("taxRateCash")] public decimal? TaxRateCash { get; init; }

    /// <summary>Earnings before interest after tax.</summary>
    [JsonPropertyName("ebiat")] public decimal? Ebiat { get; init; }

    /// <summary>Unlevered free cash flow for the year. <b>Levered-only in reverse</b>: the levered shape has
    /// no counterpart to this field and carries <c>freeCashFlow</c> instead.</summary>
    [JsonPropertyName("ufcf")] public decimal? Ufcf { get; init; }

    /// <summary>The sum of present-valued unlevered free cash flows. Moves when
    /// <see cref="CustomDcfAssumptions.CostOfEquity"/> is supplied.</summary>
    [JsonPropertyName("sumPvUfcf")] public decimal? SumPvUfcf { get; init; }

    /// <summary>The terminal growth rate as a percentage. Overridden by
    /// <see cref="CustomDcfAssumptions.LongTermGrowthRate"/>; -3.7 to 10 measured 2026-08-31.</summary>
    [JsonPropertyName("longTermGrowthRate")] public decimal? LongTermGrowthRate { get; init; }

    /// <summary>The terminal value.</summary>
    [JsonPropertyName("terminalValue")] public decimal? TerminalValue { get; init; }

    /// <summary>The terminal value discounted to today.</summary>
    [JsonPropertyName("presentTerminalValue")] public decimal? PresentTerminalValue { get; init; }

    /// <summary>The enterprise value the model arrives at.</summary>
    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }

    /// <summary>Debt less cash.</summary>
    [JsonPropertyName("netDebt")] public decimal? NetDebt { get; init; }

    /// <summary>Enterprise value less net debt.</summary>
    [JsonPropertyName("equityValue")] public decimal? EquityValue { get; init; }

    /// <summary>The model's per-share answer. <b>Can be deeply negative</b> — measured -1,498.72 on
    /// 2026-08-31 when a terminal growth rate at or above <see cref="Wacc"/> was supplied. FMP returns it
    /// rather than rejecting the input, and this SDK does not invent a bound FMP does not
    /// enforce.</summary>
    [JsonPropertyName("equityValuePerShare")] public decimal? EquityValuePerShare { get; init; }

    /// <summary>Free cash flow in the first terminal year.</summary>
    [JsonPropertyName("freeCashFlowT1")] public decimal? FreeCashFlowT1 { get; init; }
}

/// <summary>One projected year of a <b>levered</b> custom discounted-cash-flow model, from
/// <c>stable/custom-levered-discounted-cash-flow</c> — 34 keys.
///
/// <para><b>Deliberately not merged with <see cref="CustomDcfProjection"/>.</b> The two share 29 keys; 18 are
/// unlevered-only and 5 levered-only. A merged record would carry 23 properties that are null on whichever
/// path the caller happened to use, on a type that gives no hint which half is live.</para>
///
/// <para><b>And the split is not cosmetic — it is what makes the assumption vocabularies checkable.</b> This
/// path honours <see cref="CustomLeveredDcfAssumptions.OperatingCashFlowPct"/> and <b>silently ignores</b>
/// seven overrides its unlevered sibling honours. The independent Python <c>fmpsdk</c> assembles both calls
/// through one shared 18-parameter helper, which means eight of its eighteen levered parameters do nothing.
/// Two records make that a compile error.</para>
///
/// <para>Everything the two shapes share — the ten descending rows with no actual/projected flag, the live
/// price, the <see cref="CostOfDebt"/> misspelling, the <see cref="decimal"/> typing — is recorded on
/// <see cref="CustomDcfProjection"/>.</para></summary>
public sealed record CustomLeveredDcfProjection
{
    /// <summary>The projected fiscal year. Arrives as a quoted JSON string — see
    /// <see cref="CustomDcfProjection.Year"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The ticker, uppercased by FMP.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Projected revenue for the year.</summary>
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }

    /// <summary>Revenue growth for the year, as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.RevenueGrowthPct"/>.</summary>
    [JsonPropertyName("revenuePercentage")] public decimal? RevenuePercentage { get; init; }

    /// <summary>Projected capital expenditure.</summary>
    [JsonPropertyName("capitalExpenditure")] public decimal? CapitalExpenditure { get; init; }

    /// <summary>Capital expenditure as a percentage of revenue. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CapitalExpenditurePct"/>.</summary>
    [JsonPropertyName("capitalExpenditurePercentage")]
    public decimal? CapitalExpenditurePercentage { get; init; }

    /// <summary>The share price the model is running against. Live, and it moves between calls.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The beta used. Overridden by <see cref="CustomLeveredDcfAssumptions.Beta"/>.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Diluted shares outstanding.</summary>
    [JsonPropertyName("dilutedSharesOutstanding")] public decimal? DilutedSharesOutstanding { get; init; }

    /// <summary>Cost of debt, as a percentage. <b>Wire name <c>costofDebt</c>, with a lowercase
    /// <c>o</c></b> — see <see cref="CustomDcfProjection.CostOfDebt"/>. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CostOfDebt"/>.</summary>
    [JsonPropertyName("costofDebt")] public decimal? CostOfDebt { get; init; }

    /// <summary>The tax rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.TaxRate"/>.</summary>
    [JsonPropertyName("taxRate")] public decimal? TaxRate { get; init; }

    /// <summary>Cost of debt after tax, as a percentage.</summary>
    [JsonPropertyName("afterTaxCostOfDebt")] public decimal? AfterTaxCostOfDebt { get; init; }

    /// <summary>The risk-free rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.RiskFreeRate"/>.</summary>
    [JsonPropertyName("riskFreeRate")] public decimal? RiskFreeRate { get; init; }

    /// <summary>The equity risk premium as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.MarketRiskPremium"/>.</summary>
    [JsonPropertyName("marketRiskPremium")] public decimal? MarketRiskPremium { get; init; }

    /// <summary>Cost of equity as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.CostOfEquity"/>, which moves this,
    /// <see cref="Wacc"/>, <see cref="TerminalValue"/>, <see cref="PresentTerminalValue"/>,
    /// <see cref="PvLfcf"/> and <see cref="SumPvLfcf"/> — measured 2026-08-31.</summary>
    [JsonPropertyName("costOfEquity")] public decimal? CostOfEquity { get; init; }

    /// <summary>Total debt.</summary>
    [JsonPropertyName("totalDebt")] public decimal? TotalDebt { get; init; }

    /// <summary>Total equity at market value.</summary>
    [JsonPropertyName("totalEquity")] public decimal? TotalEquity { get; init; }

    /// <summary>Debt plus equity.</summary>
    [JsonPropertyName("totalCapital")] public decimal? TotalCapital { get; init; }

    /// <summary>Debt's share of total capital, as a percentage.</summary>
    [JsonPropertyName("debtWeighting")] public decimal? DebtWeighting { get; init; }

    /// <summary>Equity's share of total capital, as a percentage.</summary>
    [JsonPropertyName("equityWeighting")] public decimal? EquityWeighting { get; init; }

    /// <summary>The weighted average cost of capital, as a percentage.</summary>
    [JsonPropertyName("wacc")] public decimal? Wacc { get; init; }

    /// <summary>Projected operating cash flow. <b>Levered-only</b> — the unlevered shape has no counterpart.
    /// Overridden by <see cref="CustomLeveredDcfAssumptions.OperatingCashFlowPct"/>, which is the one
    /// override the <i>unlevered</i> path silently ignores.</summary>
    [JsonPropertyName("operatingCashFlow")] public decimal? OperatingCashFlow { get; init; }

    /// <summary>The present value of levered free cash flow for the year. <b>Levered-only.</b></summary>
    [JsonPropertyName("pvLfcf")] public decimal? PvLfcf { get; init; }

    /// <summary>The sum of present-valued levered free cash flows. <b>Levered-only.</b></summary>
    [JsonPropertyName("sumPvLfcf")] public decimal? SumPvLfcf { get; init; }

    /// <summary>The terminal growth rate as a percentage. Overridden by
    /// <see cref="CustomLeveredDcfAssumptions.LongTermGrowthRate"/>.</summary>
    [JsonPropertyName("longTermGrowthRate")] public decimal? LongTermGrowthRate { get; init; }

    /// <summary>Free cash flow for the year. <b>Levered-only</b>; the unlevered shape carries
    /// <see cref="CustomDcfProjection.Ufcf"/> instead, and the two are not the same
    /// quantity.</summary>
    [JsonPropertyName("freeCashFlow")] public decimal? FreeCashFlow { get; init; }

    /// <summary>The terminal value.</summary>
    [JsonPropertyName("terminalValue")] public decimal? TerminalValue { get; init; }

    /// <summary>The terminal value discounted to today.</summary>
    [JsonPropertyName("presentTerminalValue")] public decimal? PresentTerminalValue { get; init; }

    /// <summary>The enterprise value the model arrives at.</summary>
    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }

    /// <summary>Debt less cash.</summary>
    [JsonPropertyName("netDebt")] public decimal? NetDebt { get; init; }

    /// <summary>Enterprise value less net debt.</summary>
    [JsonPropertyName("equityValue")] public decimal? EquityValue { get; init; }

    /// <summary>The model's per-share answer. Can be deeply negative — see
    /// <see cref="CustomDcfProjection.EquityValuePerShare"/>.</summary>
    [JsonPropertyName("equityValuePerShare")] public decimal? EquityValuePerShare { get; init; }

    /// <summary>Free cash flow in the first terminal year.</summary>
    [JsonPropertyName("freeCashFlowT1")] public decimal? FreeCashFlowT1 { get; init; }

    /// <summary>Operating cash flow as a percentage of revenue. <b>Levered-only</b>, and the last key in
    /// FMP's own ordering rather than beside <see cref="OperatingCashFlow"/>.</summary>
    [JsonPropertyName("operatingCashFlowPercentage")]
    public decimal? OperatingCashFlowPercentage { get; init; }
}
