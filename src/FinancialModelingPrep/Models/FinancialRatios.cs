using System.Text.Json.Serialization;
using FinancialModelingPrep.Serialization;
using NodaTime;

namespace FinancialModelingPrep.Models;

/// <summary>Ratios derived from one period's statements. From <c>stable/ratios</c>.
///
/// <para>Ratios are computed by FMP, not reported by the issuer, so a denominator of zero shows up as <c>0</c> and outliers are not clipped.</para>
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record FinancialRatios
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Fiscal year. FMP sends this <b>quoted</b> (<c>"2025"</c>); the SDK reads it as a number anyway.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>-<c>Q4</c> for quarterly.
    /// Note this is the <i>response</i> vocabulary, which differs from the <c>period=</c> request value.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the statement is reported in — not necessarily USD.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    // ---- Margins ----
    [JsonPropertyName("grossProfitMargin")] public decimal? GrossProfitMargin { get; init; }

    [JsonPropertyName("ebitMargin")] public decimal? EbitMargin { get; init; }

    [JsonPropertyName("ebitdaMargin")] public decimal? EbitdaMargin { get; init; }

    [JsonPropertyName("operatingProfitMargin")] public decimal? OperatingProfitMargin { get; init; }

    [JsonPropertyName("pretaxProfitMargin")] public decimal? PretaxProfitMargin { get; init; }

    [JsonPropertyName("continuousOperationsProfitMargin")] public decimal? ContinuousOperationsProfitMargin { get; init; }

    [JsonPropertyName("netProfitMargin")] public decimal? NetProfitMargin { get; init; }

    [JsonPropertyName("bottomLineProfitMargin")] public decimal? BottomLineProfitMargin { get; init; }

    // ---- Turnover ----
    [JsonPropertyName("receivablesTurnover")] public decimal? ReceivablesTurnover { get; init; }

    [JsonPropertyName("payablesTurnover")] public decimal? PayablesTurnover { get; init; }

    [JsonPropertyName("inventoryTurnover")] public decimal? InventoryTurnover { get; init; }

    [JsonPropertyName("fixedAssetTurnover")] public decimal? FixedAssetTurnover { get; init; }

    [JsonPropertyName("assetTurnover")] public decimal? AssetTurnover { get; init; }

    // ---- Liquidity ----
    [JsonPropertyName("currentRatio")] public decimal? CurrentRatio { get; init; }

    [JsonPropertyName("quickRatio")] public decimal? QuickRatio { get; init; }

    [JsonPropertyName("solvencyRatio")] public decimal? SolvencyRatio { get; init; }

    [JsonPropertyName("cashRatio")] public decimal? CashRatio { get; init; }

    // ---- Valuation ----
    [JsonPropertyName("priceToEarningsRatio")] public decimal? PriceToEarningsRatio { get; init; }

    [JsonPropertyName("priceToEarningsGrowthRatio")] public decimal? PriceToEarningsGrowthRatio { get; init; }

    [JsonPropertyName("forwardPriceToEarningsGrowthRatio")] public decimal? ForwardPriceToEarningsGrowthRatio { get; init; }

    [JsonPropertyName("priceToEarningsDilutedRatio")] public decimal? PriceToEarningsDilutedRatio { get; init; }

    [JsonPropertyName("priceToEarningsDilutedGrowthRatio")] public decimal? PriceToEarningsDilutedGrowthRatio { get; init; }

    [JsonPropertyName("priceToBookRatio")] public decimal? PriceToBookRatio { get; init; }

    [JsonPropertyName("priceToSalesRatio")] public decimal? PriceToSalesRatio { get; init; }

    [JsonPropertyName("priceToFreeCashFlowRatio")] public decimal? PriceToFreeCashFlowRatio { get; init; }

    [JsonPropertyName("priceToOperatingCashFlowRatio")] public decimal? PriceToOperatingCashFlowRatio { get; init; }

    // ---- Leverage ----
    [JsonPropertyName("debtToAssetsRatio")] public decimal? DebtToAssetsRatio { get; init; }

    [JsonPropertyName("debtToEquityRatio")] public decimal? DebtToEquityRatio { get; init; }

    [JsonPropertyName("debtToCapitalRatio")] public decimal? DebtToCapitalRatio { get; init; }

    [JsonPropertyName("longTermDebtToCapitalRatio")] public decimal? LongTermDebtToCapitalRatio { get; init; }

    [JsonPropertyName("financialLeverageRatio")] public decimal? FinancialLeverageRatio { get; init; }

    [JsonPropertyName("workingCapitalTurnoverRatio")] public decimal? WorkingCapitalTurnoverRatio { get; init; }

    // ---- Cash flow ----
    [JsonPropertyName("operatingCashFlowRatio")] public decimal? OperatingCashFlowRatio { get; init; }

    [JsonPropertyName("operatingCashFlowSalesRatio")] public decimal? OperatingCashFlowSalesRatio { get; init; }

    [JsonPropertyName("freeCashFlowOperatingCashFlowRatio")] public decimal? FreeCashFlowOperatingCashFlowRatio { get; init; }

    [JsonPropertyName("debtServiceCoverageRatio")] public decimal? DebtServiceCoverageRatio { get; init; }

    [JsonPropertyName("interestCoverageRatio")] public decimal? InterestCoverageRatio { get; init; }

    [JsonPropertyName("shortTermOperatingCashFlowCoverageRatio")] public decimal? ShortTermOperatingCashFlowCoverageRatio { get; init; }

    [JsonPropertyName("operatingCashFlowCoverageRatio")] public decimal? OperatingCashFlowCoverageRatio { get; init; }

    [JsonPropertyName("capitalExpenditureCoverageRatio")] public decimal? CapitalExpenditureCoverageRatio { get; init; }

    [JsonPropertyName("dividendPaidAndCapexCoverageRatio")] public decimal? DividendPaidAndCapexCoverageRatio { get; init; }

    // ---- Dividends ----
    [JsonPropertyName("dividendPayoutRatio")] public decimal? DividendPayoutRatio { get; init; }

    /// <summary>Dividend yield as a fraction — <c>0.0041</c> is 0.41%.</summary>
    [JsonPropertyName("dividendYield")] public decimal? DividendYield { get; init; }

    /// <summary>Dividend yield already multiplied out — <c>0.41</c> is 0.41%. The percentage twin of <see cref="DividendYield"/>; check which one you want.</summary>
    [JsonPropertyName("dividendYieldPercentage")] public decimal? DividendYieldPercentage { get; init; }

    // ---- Per share ----
    [JsonPropertyName("revenuePerShare")] public decimal? RevenuePerShare { get; init; }

    [JsonPropertyName("netIncomePerShare")] public decimal? NetIncomePerShare { get; init; }

    [JsonPropertyName("interestDebtPerShare")] public decimal? InterestDebtPerShare { get; init; }

    [JsonPropertyName("cashPerShare")] public decimal? CashPerShare { get; init; }

    [JsonPropertyName("bookValuePerShare")] public decimal? BookValuePerShare { get; init; }

    [JsonPropertyName("tangibleBookValuePerShare")] public decimal? TangibleBookValuePerShare { get; init; }

    [JsonPropertyName("shareholdersEquityPerShare")] public decimal? ShareholdersEquityPerShare { get; init; }

    [JsonPropertyName("operatingCashFlowPerShare")] public decimal? OperatingCashFlowPerShare { get; init; }

    [JsonPropertyName("capexPerShare")] public decimal? CapexPerShare { get; init; }

    [JsonPropertyName("freeCashFlowPerShare")] public decimal? FreeCashFlowPerShare { get; init; }

    [JsonPropertyName("netIncomePerEBT")] public decimal? NetIncomePerEbt { get; init; }

    /// <summary>Earnings before tax over EBIT — the share of operating profit that survives interest.</summary>
    [JsonPropertyName("ebtPerEbit")] public decimal? EbtPerEbit { get; init; }

    /// <summary>Price over FMP's own fair-value estimate, not a reported figure.</summary>
    [JsonPropertyName("priceToFairValue")] public decimal? PriceToFairValue { get; init; }

    [JsonPropertyName("debtToMarketCap")] public decimal? DebtToMarketCap { get; init; }

    /// <summary>Tax expense over pre-tax income, as a fraction.</summary>
    [JsonPropertyName("effectiveTaxRate")] public decimal? EffectiveTaxRate { get; init; }

    [JsonPropertyName("enterpriseValueMultiple")] public decimal? EnterpriseValueMultiple { get; init; }

    [JsonPropertyName("dividendPerShare")] public decimal? DividendPerShare { get; init; }
}
