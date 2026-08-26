using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One period of an income statement. From <c>stable/income-statement</c>.
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record IncomeStatement
{
    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>ISO currency the statement is reported in — not necessarily USD.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>SEC Central Index Key, zero-padded. A string because the padding is significant.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>Date the filing reached the SEC.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When the SEC accepted the filing.
    ///
    /// <para>FMP sends this as EDGAR's <b>Eastern</b> wall clock with no offset; the SDK converts it to a true
    /// instant. See <see cref="NullableEasternInstantJsonConverter"/> for the measurement — this is NOT the same
    /// timezone FMP uses on the economic calendar.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>Fiscal year. FMP sends this <b>quoted</b> (<c>"2025"</c>); the SDK reads it as a number anyway.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>-<c>Q4</c> for quarterly.
    /// Note this is the <i>response</i> vocabulary, which differs from the <c>period=</c> request value.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    // ---- Revenue and cost of sales ----
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }

    [JsonPropertyName("costOfRevenue")] public decimal? CostOfRevenue { get; init; }

    [JsonPropertyName("grossProfit")] public decimal? GrossProfit { get; init; }

    // ---- Operating expenses ----
    [JsonPropertyName("researchAndDevelopmentExpenses")] public decimal? ResearchAndDevelopmentExpenses { get; init; }

    [JsonPropertyName("generalAndAdministrativeExpenses")] public decimal? GeneralAndAdministrativeExpenses { get; init; }

    [JsonPropertyName("sellingAndMarketingExpenses")] public decimal? SellingAndMarketingExpenses { get; init; }

    [JsonPropertyName("sellingGeneralAndAdministrativeExpenses")] public decimal? SellingGeneralAndAdministrativeExpenses { get; init; }

    [JsonPropertyName("otherExpenses")] public decimal? OtherExpenses { get; init; }

    [JsonPropertyName("operatingExpenses")] public decimal? OperatingExpenses { get; init; }

    [JsonPropertyName("costAndExpenses")] public decimal? CostAndExpenses { get; init; }

    // ---- Interest and non-operating items ----
    [JsonPropertyName("netInterestIncome")] public decimal? NetInterestIncome { get; init; }

    [JsonPropertyName("interestIncome")] public decimal? InterestIncome { get; init; }

    [JsonPropertyName("interestExpense")] public decimal? InterestExpense { get; init; }

    [JsonPropertyName("depreciationAndAmortization")] public decimal? DepreciationAndAmortization { get; init; }

    [JsonPropertyName("ebitda")] public decimal? Ebitda { get; init; }

    [JsonPropertyName("ebit")] public decimal? Ebit { get; init; }

    [JsonPropertyName("nonOperatingIncomeExcludingInterest")] public decimal? NonOperatingIncomeExcludingInterest { get; init; }

    [JsonPropertyName("operatingIncome")] public decimal? OperatingIncome { get; init; }

    [JsonPropertyName("totalOtherIncomeExpensesNet")] public decimal? TotalOtherIncomeExpensesNet { get; init; }

    // ---- Income and tax ----
    [JsonPropertyName("incomeBeforeTax")] public decimal? IncomeBeforeTax { get; init; }

    [JsonPropertyName("incomeTaxExpense")] public decimal? IncomeTaxExpense { get; init; }

    [JsonPropertyName("netIncomeFromContinuingOperations")] public decimal? NetIncomeFromContinuingOperations { get; init; }

    [JsonPropertyName("netIncomeFromDiscontinuedOperations")] public decimal? NetIncomeFromDiscontinuedOperations { get; init; }

    [JsonPropertyName("otherAdjustmentsToNetIncome")] public decimal? OtherAdjustmentsToNetIncome { get; init; }

    [JsonPropertyName("netIncome")] public decimal? NetIncome { get; init; }

    [JsonPropertyName("netIncomeDeductions")] public decimal? NetIncomeDeductions { get; init; }

    [JsonPropertyName("bottomLineNetIncome")] public decimal? BottomLineNetIncome { get; init; }

    // ---- Per-share and share counts ----
    [JsonPropertyName("eps")] public decimal? Eps { get; init; }

    [JsonPropertyName("epsDiluted")] public decimal? EpsDiluted { get; init; }

    [JsonPropertyName("weightedAverageShsOut")] public decimal? WeightedAverageSharesOutstanding { get; init; }

    [JsonPropertyName("weightedAverageShsOutDil")] public decimal? WeightedAverageSharesOutstandingDiluted { get; init; }
}
