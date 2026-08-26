using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>Per-period metrics derived from one period's statements. From <c>stable/key-metrics</c>.
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record KeyMetrics
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

    // ---- Size and enterprise value ----
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }

    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }

    [JsonPropertyName("evToSales")] public decimal? EvToSales { get; init; }

    [JsonPropertyName("evToOperatingCashFlow")] public decimal? EvToOperatingCashFlow { get; init; }

    [JsonPropertyName("evToFreeCashFlow")] public decimal? EvToFreeCashFlow { get; init; }

    [JsonPropertyName("evToEBITDA")] public decimal? EvToEbitda { get; init; }

    [JsonPropertyName("netDebtToEBITDA")] public decimal? NetDebtToEbitda { get; init; }

    // ---- Quality ----
    [JsonPropertyName("currentRatio")] public decimal? CurrentRatio { get; init; }

    [JsonPropertyName("incomeQuality")] public decimal? IncomeQuality { get; init; }

    /// <summary>Benjamin Graham's fair-value bound, sqrt(22.5 x EPS x book value per share).</summary>
    [JsonPropertyName("grahamNumber")] public decimal? GrahamNumber { get; init; }

    /// <summary>Graham's net-net working capital per share.</summary>
    [JsonPropertyName("grahamNetNet")] public decimal? GrahamNetNet { get; init; }

    [JsonPropertyName("taxBurden")] public decimal? TaxBurden { get; init; }

    [JsonPropertyName("interestBurden")] public decimal? InterestBurden { get; init; }

    [JsonPropertyName("workingCapital")] public decimal? WorkingCapital { get; init; }

    [JsonPropertyName("investedCapital")] public decimal? InvestedCapital { get; init; }

    // ---- Returns ----
    [JsonPropertyName("returnOnAssets")] public decimal? ReturnOnAssets { get; init; }

    [JsonPropertyName("operatingReturnOnAssets")] public decimal? OperatingReturnOnAssets { get; init; }

    [JsonPropertyName("returnOnTangibleAssets")] public decimal? ReturnOnTangibleAssets { get; init; }

    [JsonPropertyName("returnOnEquity")] public decimal? ReturnOnEquity { get; init; }

    [JsonPropertyName("returnOnInvestedCapital")] public decimal? ReturnOnInvestedCapital { get; init; }

    [JsonPropertyName("returnOnCapitalEmployed")] public decimal? ReturnOnCapitalEmployed { get; init; }

    [JsonPropertyName("earningsYield")] public decimal? EarningsYield { get; init; }

    [JsonPropertyName("freeCashFlowYield")] public decimal? FreeCashFlowYield { get; init; }

    // ---- Capital intensity ----
    [JsonPropertyName("capexToOperatingCashFlow")] public decimal? CapexToOperatingCashFlow { get; init; }

    [JsonPropertyName("capexToDepreciation")] public decimal? CapexToDepreciation { get; init; }

    [JsonPropertyName("capexToRevenue")] public decimal? CapexToRevenue { get; init; }

    [JsonPropertyName("salesGeneralAndAdministrativeToRevenue")] public decimal? SalesGeneralAndAdministrativeToRevenue { get; init; }

    /// <summary>R&amp;D over revenue, as a fraction. FMP spells the wire name "Developement"; the SDK does not.</summary>
    [JsonPropertyName("researchAndDevelopementToRevenue")] public decimal? ResearchAndDevelopmentToRevenue { get; init; }

    [JsonPropertyName("stockBasedCompensationToRevenue")] public decimal? StockBasedCompensationToRevenue { get; init; }

    [JsonPropertyName("intangiblesToTotalAssets")] public decimal? IntangiblesToTotalAssets { get; init; }

    // ---- Working-capital cycle ----
    [JsonPropertyName("averageReceivables")] public decimal? AverageReceivables { get; init; }

    [JsonPropertyName("averagePayables")] public decimal? AveragePayables { get; init; }

    [JsonPropertyName("averageInventory")] public decimal? AverageInventory { get; init; }

    [JsonPropertyName("daysOfSalesOutstanding")] public decimal? DaysOfSalesOutstanding { get; init; }

    [JsonPropertyName("daysOfPayablesOutstanding")] public decimal? DaysOfPayablesOutstanding { get; init; }

    [JsonPropertyName("daysOfInventoryOutstanding")] public decimal? DaysOfInventoryOutstanding { get; init; }

    [JsonPropertyName("operatingCycle")] public decimal? OperatingCycle { get; init; }

    [JsonPropertyName("cashConversionCycle")] public decimal? CashConversionCycle { get; init; }

    // ---- Cash flow and asset value ----
    [JsonPropertyName("freeCashFlowToEquity")] public decimal? FreeCashFlowToEquity { get; init; }

    [JsonPropertyName("freeCashFlowToFirm")] public decimal? FreeCashFlowToFirm { get; init; }

    [JsonPropertyName("tangibleAssetValue")] public decimal? TangibleAssetValue { get; init; }

    [JsonPropertyName("netCurrentAssetValue")] public decimal? NetCurrentAssetValue { get; init; }
}
