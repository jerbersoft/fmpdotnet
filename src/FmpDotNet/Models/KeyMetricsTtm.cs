using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>Trailing-twelve-month key metrics for every company FMP covers. From <c>stable/key-metrics-ttm-bulk</c> — 71,500 rows and <b>44.0 MB</b> measured 2026-08-26, the second largest response the SDK models.
///
/// <para><b>Every figure is trailing-twelve-month, so the <c>TTM</c> suffix FMP puts on each column is dropped
/// from the property names</b> — it says the same thing the type name already says, on all 42 of them.
/// The wire spelling is preserved in the CSV lookup, typos included.</para>
///
/// <para><b>There is no date column.</b> A row is identified by <see cref="Symbol"/> alone and describes the
/// twelve months ending whenever FMP last recomputed it, which the response does not say. Two rows fetched days
/// apart are not comparable as a time series, and nothing in the payload will tell you they differ.</para></summary>
public sealed record KeyMetricsTtm
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    /// <summary>Trailing-twelve-month <c>marketCap</c>.</summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
    /// <summary>Trailing-twelve-month <c>enterpriseValue</c>.</summary>
    [JsonPropertyName("enterpriseValueTTM")] public decimal? EnterpriseValue { get; init; }
    /// <summary>Trailing-twelve-month <c>evToSales</c>.</summary>
    [JsonPropertyName("evToSalesTTM")] public decimal? EvToSales { get; init; }
    /// <summary>Trailing-twelve-month <c>evToOperatingCashFlow</c>.</summary>
    [JsonPropertyName("evToOperatingCashFlowTTM")] public decimal? EvToOperatingCashFlow { get; init; }
    /// <summary>Trailing-twelve-month <c>evToFreeCashFlow</c>.</summary>
    [JsonPropertyName("evToFreeCashFlowTTM")] public decimal? EvToFreeCashFlow { get; init; }
    /// <summary>Trailing-twelve-month <c>evToEBITDA</c>.</summary>
    /// <remarks>FMP spells the column <c>evToEBITDATTM</c>.</remarks>
    [JsonPropertyName("evToEBITDATTM")] public decimal? EvToEbitda { get; init; }
    /// <summary>Trailing-twelve-month <c>netDebtToEBITDA</c>.</summary>
    /// <remarks>FMP spells the column <c>netDebtToEBITDATTM</c>.</remarks>
    [JsonPropertyName("netDebtToEBITDATTM")] public decimal? NetDebtToEbitda { get; init; }
    /// <summary>Trailing-twelve-month <c>currentRatio</c>.</summary>
    [JsonPropertyName("currentRatioTTM")] public decimal? CurrentRatio { get; init; }
    /// <summary>Trailing-twelve-month <c>incomeQuality</c>.</summary>
    [JsonPropertyName("incomeQualityTTM")] public decimal? IncomeQuality { get; init; }
    /// <summary>Trailing-twelve-month <c>grahamNumber</c>.</summary>
    [JsonPropertyName("grahamNumberTTM")] public decimal? GrahamNumber { get; init; }
    /// <summary>Trailing-twelve-month <c>grahamNetNet</c>.</summary>
    [JsonPropertyName("grahamNetNetTTM")] public decimal? GrahamNetNet { get; init; }
    /// <summary>Trailing-twelve-month <c>taxBurden</c>.</summary>
    [JsonPropertyName("taxBurdenTTM")] public decimal? TaxBurden { get; init; }
    /// <summary>Trailing-twelve-month <c>interestBurden</c>.</summary>
    [JsonPropertyName("interestBurdenTTM")] public decimal? InterestBurden { get; init; }
    /// <summary>Trailing-twelve-month <c>workingCapital</c>.</summary>
    [JsonPropertyName("workingCapitalTTM")] public decimal? WorkingCapital { get; init; }
    /// <summary>Trailing-twelve-month <c>investedCapital</c>.</summary>
    [JsonPropertyName("investedCapitalTTM")] public decimal? InvestedCapital { get; init; }
    /// <summary>Trailing-twelve-month <c>returnOnAssets</c>.</summary>
    [JsonPropertyName("returnOnAssetsTTM")] public decimal? ReturnOnAssets { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingReturnOnAssets</c>.</summary>
    [JsonPropertyName("operatingReturnOnAssetsTTM")] public decimal? OperatingReturnOnAssets { get; init; }
    /// <summary>Trailing-twelve-month <c>returnOnTangibleAssets</c>.</summary>
    [JsonPropertyName("returnOnTangibleAssetsTTM")] public decimal? ReturnOnTangibleAssets { get; init; }
    /// <summary>Trailing-twelve-month <c>returnOnEquity</c>.</summary>
    [JsonPropertyName("returnOnEquityTTM")] public decimal? ReturnOnEquity { get; init; }
    /// <summary>Trailing-twelve-month <c>returnOnInvestedCapital</c>.</summary>
    [JsonPropertyName("returnOnInvestedCapitalTTM")] public decimal? ReturnOnInvestedCapital { get; init; }
    /// <summary>Trailing-twelve-month <c>returnOnCapitalEmployed</c>.</summary>
    [JsonPropertyName("returnOnCapitalEmployedTTM")] public decimal? ReturnOnCapitalEmployed { get; init; }
    /// <summary>Trailing-twelve-month <c>earningsYield</c>.</summary>
    [JsonPropertyName("earningsYieldTTM")] public decimal? EarningsYield { get; init; }
    /// <summary>Trailing-twelve-month <c>freeCashFlowYield</c>.</summary>
    [JsonPropertyName("freeCashFlowYieldTTM")] public decimal? FreeCashFlowYield { get; init; }
    /// <summary>Trailing-twelve-month <c>capexToOperatingCashFlow</c>.</summary>
    [JsonPropertyName("capexToOperatingCashFlowTTM")] public decimal? CapexToOperatingCashFlow { get; init; }
    /// <summary>Trailing-twelve-month <c>capexToDepreciation</c>.</summary>
    [JsonPropertyName("capexToDepreciationTTM")] public decimal? CapexToDepreciation { get; init; }
    /// <summary>Trailing-twelve-month <c>capexToRevenue</c>.</summary>
    [JsonPropertyName("capexToRevenueTTM")] public decimal? CapexToRevenue { get; init; }
    /// <summary>Trailing-twelve-month <c>salesGeneralAndAdministrativeToRevenue</c>.</summary>
    [JsonPropertyName("salesGeneralAndAdministrativeToRevenueTTM")] public decimal? SalesGeneralAndAdministrativeToRevenue { get; init; }
    /// <summary>Trailing-twelve-month <c>researchAndDevelopementToRevenue</c>.</summary>
    /// <remarks>FMP spells the column <c>researchAndDevelopementToRevenueTTM</c>.</remarks>
    [JsonPropertyName("researchAndDevelopementToRevenueTTM")] public decimal? ResearchAndDevelopmentToRevenue { get; init; }
    /// <summary>Trailing-twelve-month <c>stockBasedCompensationToRevenue</c>.</summary>
    [JsonPropertyName("stockBasedCompensationToRevenueTTM")] public decimal? StockBasedCompensationToRevenue { get; init; }
    /// <summary>Trailing-twelve-month <c>intangiblesToTotalAssets</c>.</summary>
    [JsonPropertyName("intangiblesToTotalAssetsTTM")] public decimal? IntangiblesToTotalAssets { get; init; }
    /// <summary>Trailing-twelve-month <c>averageReceivables</c>.</summary>
    [JsonPropertyName("averageReceivablesTTM")] public decimal? AverageReceivables { get; init; }
    /// <summary>Trailing-twelve-month <c>averagePayables</c>.</summary>
    [JsonPropertyName("averagePayablesTTM")] public decimal? AveragePayables { get; init; }
    /// <summary>Trailing-twelve-month <c>averageInventory</c>.</summary>
    [JsonPropertyName("averageInventoryTTM")] public decimal? AverageInventory { get; init; }
    /// <summary>Trailing-twelve-month <c>daysOfSalesOutstanding</c>.</summary>
    [JsonPropertyName("daysOfSalesOutstandingTTM")] public decimal? DaysOfSalesOutstanding { get; init; }
    /// <summary>Trailing-twelve-month <c>daysOfPayablesOutstanding</c>.</summary>
    [JsonPropertyName("daysOfPayablesOutstandingTTM")] public decimal? DaysOfPayablesOutstanding { get; init; }
    /// <summary>Trailing-twelve-month <c>daysOfInventoryOutstanding</c>.</summary>
    [JsonPropertyName("daysOfInventoryOutstandingTTM")] public decimal? DaysOfInventoryOutstanding { get; init; }
    /// <summary>Trailing-twelve-month <c>operatingCycle</c>.</summary>
    [JsonPropertyName("operatingCycleTTM")] public decimal? OperatingCycle { get; init; }
    /// <summary>Trailing-twelve-month <c>cashConversionCycle</c>.</summary>
    [JsonPropertyName("cashConversionCycleTTM")] public decimal? CashConversionCycle { get; init; }
    /// <summary>Trailing-twelve-month <c>freeCashFlowToEquity</c>.</summary>
    [JsonPropertyName("freeCashFlowToEquityTTM")] public decimal? FreeCashFlowToEquity { get; init; }
    /// <summary>Trailing-twelve-month <c>freeCashFlowToFirm</c>.</summary>
    [JsonPropertyName("freeCashFlowToFirmTTM")] public decimal? FreeCashFlowToFirm { get; init; }
    /// <summary>Trailing-twelve-month <c>tangibleAssetValue</c>.</summary>
    [JsonPropertyName("tangibleAssetValueTTM")] public decimal? TangibleAssetValue { get; init; }
    /// <summary>Trailing-twelve-month <c>netCurrentAssetValue</c>.</summary>
    [JsonPropertyName("netCurrentAssetValueTTM")] public decimal? NetCurrentAssetValue { get; init; }

    internal static KeyMetricsTtm FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        MarketCap = row.GetDecimal("marketCap"),
        EnterpriseValue = row.GetDecimal("enterpriseValueTTM"),
        EvToSales = row.GetDecimal("evToSalesTTM"),
        EvToOperatingCashFlow = row.GetDecimal("evToOperatingCashFlowTTM"),
        EvToFreeCashFlow = row.GetDecimal("evToFreeCashFlowTTM"),
        EvToEbitda = row.GetDecimal("evToEBITDATTM"),
        NetDebtToEbitda = row.GetDecimal("netDebtToEBITDATTM"),
        CurrentRatio = row.GetDecimal("currentRatioTTM"),
        IncomeQuality = row.GetDecimal("incomeQualityTTM"),
        GrahamNumber = row.GetDecimal("grahamNumberTTM"),
        GrahamNetNet = row.GetDecimal("grahamNetNetTTM"),
        TaxBurden = row.GetDecimal("taxBurdenTTM"),
        InterestBurden = row.GetDecimal("interestBurdenTTM"),
        WorkingCapital = row.GetDecimal("workingCapitalTTM"),
        InvestedCapital = row.GetDecimal("investedCapitalTTM"),
        ReturnOnAssets = row.GetDecimal("returnOnAssetsTTM"),
        OperatingReturnOnAssets = row.GetDecimal("operatingReturnOnAssetsTTM"),
        ReturnOnTangibleAssets = row.GetDecimal("returnOnTangibleAssetsTTM"),
        ReturnOnEquity = row.GetDecimal("returnOnEquityTTM"),
        ReturnOnInvestedCapital = row.GetDecimal("returnOnInvestedCapitalTTM"),
        ReturnOnCapitalEmployed = row.GetDecimal("returnOnCapitalEmployedTTM"),
        EarningsYield = row.GetDecimal("earningsYieldTTM"),
        FreeCashFlowYield = row.GetDecimal("freeCashFlowYieldTTM"),
        CapexToOperatingCashFlow = row.GetDecimal("capexToOperatingCashFlowTTM"),
        CapexToDepreciation = row.GetDecimal("capexToDepreciationTTM"),
        CapexToRevenue = row.GetDecimal("capexToRevenueTTM"),
        SalesGeneralAndAdministrativeToRevenue = row.GetDecimal("salesGeneralAndAdministrativeToRevenueTTM"),
        ResearchAndDevelopmentToRevenue = row.GetDecimal("researchAndDevelopementToRevenueTTM"),
        StockBasedCompensationToRevenue = row.GetDecimal("stockBasedCompensationToRevenueTTM"),
        IntangiblesToTotalAssets = row.GetDecimal("intangiblesToTotalAssetsTTM"),
        AverageReceivables = row.GetDecimal("averageReceivablesTTM"),
        AveragePayables = row.GetDecimal("averagePayablesTTM"),
        AverageInventory = row.GetDecimal("averageInventoryTTM"),
        DaysOfSalesOutstanding = row.GetDecimal("daysOfSalesOutstandingTTM"),
        DaysOfPayablesOutstanding = row.GetDecimal("daysOfPayablesOutstandingTTM"),
        DaysOfInventoryOutstanding = row.GetDecimal("daysOfInventoryOutstandingTTM"),
        OperatingCycle = row.GetDecimal("operatingCycleTTM"),
        CashConversionCycle = row.GetDecimal("cashConversionCycleTTM"),
        FreeCashFlowToEquity = row.GetDecimal("freeCashFlowToEquityTTM"),
        FreeCashFlowToFirm = row.GetDecimal("freeCashFlowToFirmTTM"),
        TangibleAssetValue = row.GetDecimal("tangibleAssetValueTTM"),
        NetCurrentAssetValue = row.GetDecimal("netCurrentAssetValueTTM"),
    };
}
