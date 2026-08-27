using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>Period-over-period growth of one income statement. From <c>stable/income-statement-growth-bulk</c>, the whole-universe CSV download for a given <c>year</c> and <c>period</c> — 43,135 rows and 21.3 MB measured 2026-08-26 for 2025 Q1.
///
/// <para>Every figure is a <b>fraction</b>, not a percentage: 0.12 is twelve percent. FMP sends 0 where the prior
/// period was zero or absent, so a zero here cannot be distinguished from "no prior period to grow from".</para>
///
/// <para><b>The column names are not the ones on the non-growth endpoint.</b> They are the upstream's own, typos
/// included, and several are recorded in the remarks below — <c>Activites</c> for <c>Activities</c> among them.
/// The C# name is corrected; the string passed to the CSV reader is FMP's, verbatim, because that is what
/// arrives.</para></summary>
public sealed record IncomeStatementGrowth
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Fiscal year.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>-<c>Q4</c> for quarterly.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the underlying statement is reported in — not necessarily USD.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Period-over-period growth in <c>Revenue</c>, as a fraction.</summary>
    [JsonPropertyName("growthRevenue")] public decimal? GrowthRevenue { get; init; }

    /// <summary>Period-over-period growth in <c>CostOfRevenue</c>, as a fraction.</summary>
    [JsonPropertyName("growthCostOfRevenue")] public decimal? GrowthCostOfRevenue { get; init; }

    /// <summary>Period-over-period growth in <c>GrossProfit</c>, as a fraction.</summary>
    [JsonPropertyName("growthGrossProfit")] public decimal? GrowthGrossProfit { get; init; }

    /// <summary>Period-over-period growth in <c>GrossProfitRatio</c>, as a fraction.</summary>
    [JsonPropertyName("growthGrossProfitRatio")] public decimal? GrowthGrossProfitRatio { get; init; }

    /// <summary>Period-over-period growth in <c>ResearchAndDevelopmentExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthResearchAndDevelopmentExpenses")] public decimal? GrowthResearchAndDevelopmentExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>GeneralAndAdministrativeExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthGeneralAndAdministrativeExpenses")] public decimal? GrowthGeneralAndAdministrativeExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>SellingAndMarketingExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthSellingAndMarketingExpenses")] public decimal? GrowthSellingAndMarketingExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>OtherExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthOtherExpenses")] public decimal? GrowthOtherExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>OperatingExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthOperatingExpenses")] public decimal? GrowthOperatingExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>CostAndExpenses</c>, as a fraction.</summary>
    [JsonPropertyName("growthCostAndExpenses")] public decimal? GrowthCostAndExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>InterestIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthInterestIncome")] public decimal? GrowthInterestIncome { get; init; }

    /// <summary>Period-over-period growth in <c>InterestExpense</c>, as a fraction.</summary>
    [JsonPropertyName("growthInterestExpense")] public decimal? GrowthInterestExpense { get; init; }

    /// <summary>Period-over-period growth in <c>DepreciationAndAmortization</c>, as a fraction.</summary>
    [JsonPropertyName("growthDepreciationAndAmortization")] public decimal? GrowthDepreciationAndAmortization { get; init; }

    /// <summary>Period-over-period growth in <c>EBITDA</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthEBITDA</c>.</remarks>
    [JsonPropertyName("growthEBITDA")] public decimal? GrowthEbitda { get; init; }

    /// <summary>Period-over-period growth in <c>OperatingIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthOperatingIncome")] public decimal? GrowthOperatingIncome { get; init; }

    /// <summary>Period-over-period growth in <c>IncomeBeforeTax</c>, as a fraction.</summary>
    [JsonPropertyName("growthIncomeBeforeTax")] public decimal? GrowthIncomeBeforeTax { get; init; }

    /// <summary>Period-over-period growth in <c>IncomeTaxExpense</c>, as a fraction.</summary>
    [JsonPropertyName("growthIncomeTaxExpense")] public decimal? GrowthIncomeTaxExpense { get; init; }

    /// <summary>Period-over-period growth in <c>NetIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetIncome")] public decimal? GrowthNetIncome { get; init; }

    /// <summary>Period-over-period growth in <c>EPS</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthEPS</c>.</remarks>
    [JsonPropertyName("growthEPS")] public decimal? GrowthEps { get; init; }

    /// <summary>Period-over-period growth in <c>EPSDiluted</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthEPSDiluted</c>.</remarks>
    [JsonPropertyName("growthEPSDiluted")] public decimal? GrowthEpsDiluted { get; init; }

    /// <summary>Period-over-period growth in <c>WeightedAverageShsOut</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthWeightedAverageShsOut</c>.</remarks>
    [JsonPropertyName("growthWeightedAverageShsOut")] public decimal? GrowthWeightedAverageSharesOutstanding { get; init; }

    /// <summary>Period-over-period growth in <c>WeightedAverageShsOutDil</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthWeightedAverageShsOutDil</c>.</remarks>
    [JsonPropertyName("growthWeightedAverageShsOutDil")] public decimal? GrowthWeightedAverageSharesOutstandingDiluted { get; init; }

    /// <summary>Period-over-period growth in <c>EBIT</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthEBIT</c>.</remarks>
    [JsonPropertyName("growthEBIT")] public decimal? GrowthEbit { get; init; }

    /// <summary>Period-over-period growth in <c>NonOperatingIncomeExcludingInterest</c>, as a fraction.</summary>
    [JsonPropertyName("growthNonOperatingIncomeExcludingInterest")] public decimal? GrowthNonOperatingIncomeExcludingInterest { get; init; }

    /// <summary>Period-over-period growth in <c>NetInterestIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetInterestIncome")] public decimal? GrowthNetInterestIncome { get; init; }

    /// <summary>Period-over-period growth in <c>TotalOtherIncomeExpensesNet</c>, as a fraction.</summary>
    [JsonPropertyName("growthTotalOtherIncomeExpensesNet")] public decimal? GrowthTotalOtherIncomeExpensesNet { get; init; }

    /// <summary>Period-over-period growth in <c>NetIncomeFromContinuingOperations</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetIncomeFromContinuingOperations")] public decimal? GrowthNetIncomeFromContinuingOperations { get; init; }

    /// <summary>Period-over-period growth in <c>OtherAdjustmentsToNetIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthOtherAdjustmentsToNetIncome")] public decimal? GrowthOtherAdjustmentsToNetIncome { get; init; }

    /// <summary>Period-over-period growth in <c>NetIncomeDeductions</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetIncomeDeductions")] public decimal? GrowthNetIncomeDeductions { get; init; }

    internal static IncomeStatementGrowth FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        GrowthRevenue = row.GetDecimal("growthRevenue"),
        GrowthCostOfRevenue = row.GetDecimal("growthCostOfRevenue"),
        GrowthGrossProfit = row.GetDecimal("growthGrossProfit"),
        GrowthGrossProfitRatio = row.GetDecimal("growthGrossProfitRatio"),
        GrowthResearchAndDevelopmentExpenses = row.GetDecimal("growthResearchAndDevelopmentExpenses"),
        GrowthGeneralAndAdministrativeExpenses = row.GetDecimal("growthGeneralAndAdministrativeExpenses"),
        GrowthSellingAndMarketingExpenses = row.GetDecimal("growthSellingAndMarketingExpenses"),
        GrowthOtherExpenses = row.GetDecimal("growthOtherExpenses"),
        GrowthOperatingExpenses = row.GetDecimal("growthOperatingExpenses"),
        GrowthCostAndExpenses = row.GetDecimal("growthCostAndExpenses"),
        GrowthInterestIncome = row.GetDecimal("growthInterestIncome"),
        GrowthInterestExpense = row.GetDecimal("growthInterestExpense"),
        GrowthDepreciationAndAmortization = row.GetDecimal("growthDepreciationAndAmortization"),
        GrowthEbitda = row.GetDecimal("growthEBITDA"),
        GrowthOperatingIncome = row.GetDecimal("growthOperatingIncome"),
        GrowthIncomeBeforeTax = row.GetDecimal("growthIncomeBeforeTax"),
        GrowthIncomeTaxExpense = row.GetDecimal("growthIncomeTaxExpense"),
        GrowthNetIncome = row.GetDecimal("growthNetIncome"),
        GrowthEps = row.GetDecimal("growthEPS"),
        GrowthEpsDiluted = row.GetDecimal("growthEPSDiluted"),
        GrowthWeightedAverageSharesOutstanding = row.GetDecimal("growthWeightedAverageShsOut"),
        GrowthWeightedAverageSharesOutstandingDiluted = row.GetDecimal("growthWeightedAverageShsOutDil"),
        GrowthEbit = row.GetDecimal("growthEBIT"),
        GrowthNonOperatingIncomeExcludingInterest = row.GetDecimal("growthNonOperatingIncomeExcludingInterest"),
        GrowthNetInterestIncome = row.GetDecimal("growthNetInterestIncome"),
        GrowthTotalOtherIncomeExpensesNet = row.GetDecimal("growthTotalOtherIncomeExpensesNet"),
        GrowthNetIncomeFromContinuingOperations = row.GetDecimal("growthNetIncomeFromContinuingOperations"),
        GrowthOtherAdjustmentsToNetIncome = row.GetDecimal("growthOtherAdjustmentsToNetIncome"),
        GrowthNetIncomeDeductions = row.GetDecimal("growthNetIncomeDeductions"),
    };
}
