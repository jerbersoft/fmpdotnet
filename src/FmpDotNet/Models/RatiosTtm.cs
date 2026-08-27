using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Trailing-twelve-month financial ratios for every company FMP covers. From <c>stable/ratios-ttm-bulk</c> — 71,504 rows and <b>69.5 MB</b> measured 2026-08-26.
///
/// <para><b>Every figure is trailing-twelve-month, so the <c>TTM</c> suffix FMP puts on each column is dropped
/// from the property names</b> — it says the same thing the type name already says, on all 61 of them.
/// The wire spelling is preserved in the CSV lookup, typos included.</para>
///
/// <para><b>There is no date column.</b> A row is identified by <see cref="Symbol"/> alone and describes the
/// twelve months ending whenever FMP last recomputed it, which the response does not say. Two rows fetched days
/// apart are not comparable as a time series, and nothing in the payload will tell you they differ.</para></summary>
public sealed record RatiosTtm
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    /// <summary>Trailing-twelve-month <c>grossProfitMargin</c>.</summary>
    [JsonPropertyName("grossProfitMarginTTM")] public decimal? GrossProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>ebitMargin</c>.</summary>
    [JsonPropertyName("ebitMarginTTM")] public decimal? EbitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>ebitdaMargin</c>.</summary>
    [JsonPropertyName("ebitdaMarginTTM")] public decimal? EbitdaMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingProfitMargin</c>.</summary>
    [JsonPropertyName("operatingProfitMarginTTM")] public decimal? OperatingProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>pretaxProfitMargin</c>.</summary>
    [JsonPropertyName("pretaxProfitMarginTTM")] public decimal? PretaxProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>continuousOperationsProfitMargin</c>.</summary>
    [JsonPropertyName("continuousOperationsProfitMarginTTM")] public decimal? ContinuousOperationsProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>netProfitMargin</c>.</summary>
    [JsonPropertyName("netProfitMarginTTM")] public decimal? NetProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>bottomLineProfitMargin</c>.</summary>
    [JsonPropertyName("bottomLineProfitMarginTTM")] public decimal? BottomLineProfitMargin { get; init; }
    /// <summary>Trailing-twelve-month <c>receivablesTurnover</c>.</summary>
    [JsonPropertyName("receivablesTurnoverTTM")] public decimal? ReceivablesTurnover { get; init; }
    /// <summary>Trailing-twelve-month <c>payablesTurnover</c>.</summary>
    [JsonPropertyName("payablesTurnoverTTM")] public decimal? PayablesTurnover { get; init; }
    /// <summary>Trailing-twelve-month <c>inventoryTurnover</c>.</summary>
    [JsonPropertyName("inventoryTurnoverTTM")] public decimal? InventoryTurnover { get; init; }
    /// <summary>Trailing-twelve-month <c>fixedAssetTurnover</c>.</summary>
    [JsonPropertyName("fixedAssetTurnoverTTM")] public decimal? FixedAssetTurnover { get; init; }
    /// <summary>Trailing-twelve-month <c>assetTurnover</c>.</summary>
    [JsonPropertyName("assetTurnoverTTM")] public decimal? AssetTurnover { get; init; }
    /// <summary>Trailing-twelve-month <c>currentRatio</c>.</summary>
    [JsonPropertyName("currentRatioTTM")] public decimal? CurrentRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>quickRatio</c>.</summary>
    [JsonPropertyName("quickRatioTTM")] public decimal? QuickRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>solvencyRatio</c>.</summary>
    [JsonPropertyName("solvencyRatioTTM")] public decimal? SolvencyRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>cashRatio</c>.</summary>
    [JsonPropertyName("cashRatioTTM")] public decimal? CashRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToEarningsRatio</c>.</summary>
    [JsonPropertyName("priceToEarningsRatioTTM")] public decimal? PriceToEarningsRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToEarningsGrowthRatio</c>.</summary>
    [JsonPropertyName("priceToEarningsGrowthRatioTTM")] public decimal? PriceToEarningsGrowthRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>forwardPriceToEarningsGrowthRatio</c>.</summary>
    [JsonPropertyName("forwardPriceToEarningsGrowthRatioTTM")] public decimal? ForwardPriceToEarningsGrowthRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToEarningsDilutedRatio</c>.</summary>
    [JsonPropertyName("priceToEarningsDilutedRatioTTM")] public decimal? PriceToEarningsDilutedRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToEarningsDilutedGrowthRatio</c>.</summary>
    [JsonPropertyName("priceToEarningsDilutedGrowthRatioTTM")] public decimal? PriceToEarningsDilutedGrowthRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToBookRatio</c>.</summary>
    [JsonPropertyName("priceToBookRatioTTM")] public decimal? PriceToBookRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToSalesRatio</c>.</summary>
    [JsonPropertyName("priceToSalesRatioTTM")] public decimal? PriceToSalesRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToFreeCashFlowRatio</c>.</summary>
    [JsonPropertyName("priceToFreeCashFlowRatioTTM")] public decimal? PriceToFreeCashFlowRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToOperatingCashFlowRatio</c>.</summary>
    [JsonPropertyName("priceToOperatingCashFlowRatioTTM")] public decimal? PriceToOperatingCashFlowRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>debtToAssetsRatio</c>.</summary>
    [JsonPropertyName("debtToAssetsRatioTTM")] public decimal? DebtToAssetsRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>debtToEquityRatio</c>.</summary>
    [JsonPropertyName("debtToEquityRatioTTM")] public decimal? DebtToEquityRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>debtToCapitalRatio</c>.</summary>
    [JsonPropertyName("debtToCapitalRatioTTM")] public decimal? DebtToCapitalRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>longTermDebtToCapitalRatio</c>.</summary>
    [JsonPropertyName("longTermDebtToCapitalRatioTTM")] public decimal? LongTermDebtToCapitalRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>financialLeverageRatio</c>.</summary>
    [JsonPropertyName("financialLeverageRatioTTM")] public decimal? FinancialLeverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>workingCapitalTurnoverRatio</c>.</summary>
    [JsonPropertyName("workingCapitalTurnoverRatioTTM")] public decimal? WorkingCapitalTurnoverRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingCashFlowRatio</c>.</summary>
    [JsonPropertyName("operatingCashFlowRatioTTM")] public decimal? OperatingCashFlowRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingCashFlowSalesRatio</c>.</summary>
    [JsonPropertyName("operatingCashFlowSalesRatioTTM")] public decimal? OperatingCashFlowSalesRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>freeCashFlowOperatingCashFlowRatio</c>.</summary>
    [JsonPropertyName("freeCashFlowOperatingCashFlowRatioTTM")] public decimal? FreeCashFlowOperatingCashFlowRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>debtServiceCoverageRatio</c>.</summary>
    [JsonPropertyName("debtServiceCoverageRatioTTM")] public decimal? DebtServiceCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>interestCoverageRatio</c>.</summary>
    [JsonPropertyName("interestCoverageRatioTTM")] public decimal? InterestCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>shortTermOperatingCashFlowCoverageRatio</c>.</summary>
    [JsonPropertyName("shortTermOperatingCashFlowCoverageRatioTTM")] public decimal? ShortTermOperatingCashFlowCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingCashFlowCoverageRatio</c>.</summary>
    [JsonPropertyName("operatingCashFlowCoverageRatioTTM")] public decimal? OperatingCashFlowCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>capitalExpenditureCoverageRatio</c>.</summary>
    [JsonPropertyName("capitalExpenditureCoverageRatioTTM")] public decimal? CapitalExpenditureCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>dividendPaidAndCapexCoverageRatio</c>.</summary>
    [JsonPropertyName("dividendPaidAndCapexCoverageRatioTTM")] public decimal? DividendPaidAndCapexCoverageRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>dividendPayoutRatio</c>.</summary>
    [JsonPropertyName("dividendPayoutRatioTTM")] public decimal? DividendPayoutRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>dividendYield</c>.</summary>
    [JsonPropertyName("dividendYieldTTM")] public decimal? DividendYield { get; init; }
    /// <summary>Trailing-twelve-month <c>enterpriseValue</c>.</summary>
    [JsonPropertyName("enterpriseValueTTM")] public decimal? EnterpriseValue { get; init; }
    /// <summary>Trailing-twelve-month <c>revenuePerShare</c>.</summary>
    [JsonPropertyName("revenuePerShareTTM")] public decimal? RevenuePerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>netIncomePerShare</c>.</summary>
    [JsonPropertyName("netIncomePerShareTTM")] public decimal? NetIncomePerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>interestDebtPerShare</c>.</summary>
    [JsonPropertyName("interestDebtPerShareTTM")] public decimal? InterestDebtPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>cashPerShare</c>.</summary>
    [JsonPropertyName("cashPerShareTTM")] public decimal? CashPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>bookValuePerShare</c>.</summary>
    [JsonPropertyName("bookValuePerShareTTM")] public decimal? BookValuePerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>tangibleBookValuePerShare</c>.</summary>
    [JsonPropertyName("tangibleBookValuePerShareTTM")] public decimal? TangibleBookValuePerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>shareholdersEquityPerShare</c>.</summary>
    [JsonPropertyName("shareholdersEquityPerShareTTM")] public decimal? ShareholdersEquityPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingCashFlowPerShare</c>.</summary>
    [JsonPropertyName("operatingCashFlowPerShareTTM")] public decimal? OperatingCashFlowPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>capexPerShare</c>.</summary>
    [JsonPropertyName("capexPerShareTTM")] public decimal? CapexPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>freeCashFlowPerShare</c>.</summary>
    [JsonPropertyName("freeCashFlowPerShareTTM")] public decimal? FreeCashFlowPerShare { get; init; }
    /// <summary>Trailing-twelve-month <c>netIncomePerEBT</c>.</summary>
    /// <remarks>FMP spells the column <c>netIncomePerEBTTTM</c>.</remarks>
    [JsonPropertyName("netIncomePerEBTTTM")] public decimal? NetIncomePerEbt { get; init; }
    /// <summary>Trailing-twelve-month <c>ebtPerEbit</c>.</summary>
    [JsonPropertyName("ebtPerEbitTTM")] public decimal? EbtPerEbit { get; init; }
    /// <summary>Trailing-twelve-month <c>priceToFairValue</c>.</summary>
    [JsonPropertyName("priceToFairValueTTM")] public decimal? PriceToFairValue { get; init; }
    /// <summary>Trailing-twelve-month <c>debtToMarketCap</c>.</summary>
    [JsonPropertyName("debtToMarketCapTTM")] public decimal? DebtToMarketCap { get; init; }
    /// <summary>Trailing-twelve-month <c>effectiveTaxRate</c>.</summary>
    [JsonPropertyName("effectiveTaxRateTTM")] public decimal? EffectiveTaxRate { get; init; }
    /// <summary>Trailing-twelve-month <c>enterpriseValueMultiple</c>.</summary>
    [JsonPropertyName("enterpriseValueMultipleTTM")] public decimal? EnterpriseValueMultiple { get; init; }
    /// <summary>Trailing-twelve-month <c>dividendPerShare</c>.</summary>
    [JsonPropertyName("dividendPerShareTTM")] public decimal? DividendPerShare { get; init; }

    internal static RatiosTtm FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        GrossProfitMargin = row.GetDecimal("grossProfitMarginTTM"),
        EbitMargin = row.GetDecimal("ebitMarginTTM"),
        EbitdaMargin = row.GetDecimal("ebitdaMarginTTM"),
        OperatingProfitMargin = row.GetDecimal("operatingProfitMarginTTM"),
        PretaxProfitMargin = row.GetDecimal("pretaxProfitMarginTTM"),
        ContinuousOperationsProfitMargin = row.GetDecimal("continuousOperationsProfitMarginTTM"),
        NetProfitMargin = row.GetDecimal("netProfitMarginTTM"),
        BottomLineProfitMargin = row.GetDecimal("bottomLineProfitMarginTTM"),
        ReceivablesTurnover = row.GetDecimal("receivablesTurnoverTTM"),
        PayablesTurnover = row.GetDecimal("payablesTurnoverTTM"),
        InventoryTurnover = row.GetDecimal("inventoryTurnoverTTM"),
        FixedAssetTurnover = row.GetDecimal("fixedAssetTurnoverTTM"),
        AssetTurnover = row.GetDecimal("assetTurnoverTTM"),
        CurrentRatio = row.GetDecimal("currentRatioTTM"),
        QuickRatio = row.GetDecimal("quickRatioTTM"),
        SolvencyRatio = row.GetDecimal("solvencyRatioTTM"),
        CashRatio = row.GetDecimal("cashRatioTTM"),
        PriceToEarningsRatio = row.GetDecimal("priceToEarningsRatioTTM"),
        PriceToEarningsGrowthRatio = row.GetDecimal("priceToEarningsGrowthRatioTTM"),
        ForwardPriceToEarningsGrowthRatio = row.GetDecimal("forwardPriceToEarningsGrowthRatioTTM"),
        PriceToEarningsDilutedRatio = row.GetDecimal("priceToEarningsDilutedRatioTTM"),
        PriceToEarningsDilutedGrowthRatio = row.GetDecimal("priceToEarningsDilutedGrowthRatioTTM"),
        PriceToBookRatio = row.GetDecimal("priceToBookRatioTTM"),
        PriceToSalesRatio = row.GetDecimal("priceToSalesRatioTTM"),
        PriceToFreeCashFlowRatio = row.GetDecimal("priceToFreeCashFlowRatioTTM"),
        PriceToOperatingCashFlowRatio = row.GetDecimal("priceToOperatingCashFlowRatioTTM"),
        DebtToAssetsRatio = row.GetDecimal("debtToAssetsRatioTTM"),
        DebtToEquityRatio = row.GetDecimal("debtToEquityRatioTTM"),
        DebtToCapitalRatio = row.GetDecimal("debtToCapitalRatioTTM"),
        LongTermDebtToCapitalRatio = row.GetDecimal("longTermDebtToCapitalRatioTTM"),
        FinancialLeverageRatio = row.GetDecimal("financialLeverageRatioTTM"),
        WorkingCapitalTurnoverRatio = row.GetDecimal("workingCapitalTurnoverRatioTTM"),
        OperatingCashFlowRatio = row.GetDecimal("operatingCashFlowRatioTTM"),
        OperatingCashFlowSalesRatio = row.GetDecimal("operatingCashFlowSalesRatioTTM"),
        FreeCashFlowOperatingCashFlowRatio = row.GetDecimal("freeCashFlowOperatingCashFlowRatioTTM"),
        DebtServiceCoverageRatio = row.GetDecimal("debtServiceCoverageRatioTTM"),
        InterestCoverageRatio = row.GetDecimal("interestCoverageRatioTTM"),
        ShortTermOperatingCashFlowCoverageRatio = row.GetDecimal("shortTermOperatingCashFlowCoverageRatioTTM"),
        OperatingCashFlowCoverageRatio = row.GetDecimal("operatingCashFlowCoverageRatioTTM"),
        CapitalExpenditureCoverageRatio = row.GetDecimal("capitalExpenditureCoverageRatioTTM"),
        DividendPaidAndCapexCoverageRatio = row.GetDecimal("dividendPaidAndCapexCoverageRatioTTM"),
        DividendPayoutRatio = row.GetDecimal("dividendPayoutRatioTTM"),
        DividendYield = row.GetDecimal("dividendYieldTTM"),
        EnterpriseValue = row.GetDecimal("enterpriseValueTTM"),
        RevenuePerShare = row.GetDecimal("revenuePerShareTTM"),
        NetIncomePerShare = row.GetDecimal("netIncomePerShareTTM"),
        InterestDebtPerShare = row.GetDecimal("interestDebtPerShareTTM"),
        CashPerShare = row.GetDecimal("cashPerShareTTM"),
        BookValuePerShare = row.GetDecimal("bookValuePerShareTTM"),
        TangibleBookValuePerShare = row.GetDecimal("tangibleBookValuePerShareTTM"),
        ShareholdersEquityPerShare = row.GetDecimal("shareholdersEquityPerShareTTM"),
        OperatingCashFlowPerShare = row.GetDecimal("operatingCashFlowPerShareTTM"),
        CapexPerShare = row.GetDecimal("capexPerShareTTM"),
        FreeCashFlowPerShare = row.GetDecimal("freeCashFlowPerShareTTM"),
        NetIncomePerEbt = row.GetDecimal("netIncomePerEBTTTM"),
        EbtPerEbit = row.GetDecimal("ebtPerEbitTTM"),
        PriceToFairValue = row.GetDecimal("priceToFairValueTTM"),
        DebtToMarketCap = row.GetDecimal("debtToMarketCapTTM"),
        EffectiveTaxRate = row.GetDecimal("effectiveTaxRateTTM"),
        EnterpriseValueMultiple = row.GetDecimal("enterpriseValueMultipleTTM"),
        DividendPerShare = row.GetDecimal("dividendPerShareTTM"),
    };
}
