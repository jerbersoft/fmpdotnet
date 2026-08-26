using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 31 properties below are a flat transcription of FMP's wire fields: the property name
// carries the same information a generated one-line summary would, and 31 of those would bury the type-level
// documentation above — which is where this response's actual quirks are recorded.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this used to, also meant a NEW
// undocumented public member anywhere in the SDK compiled silently. The seven transcription models are the only
// exemptions, and the zero-warning bar holds everywhere else.
#pragma warning disable CS1591

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

    /// <summary>Maps one row of the matching <c>*-bulk</c> CSV.
    ///
    /// <para><b>This type is shared with the per-symbol JSON endpoint deliberately, and that was verified rather
    /// than assumed.</b> The bulk CSV header was compared field by field against this model's
    /// <c>[JsonPropertyName]</c> values on 2026-08-26: 39 columns, 39 properties, no name on either side absent
    /// from the other. Duplicating 39 properties into a parallel bulk type would be 39 chances for the two to
    /// drift, for no gain — unlike <see cref="BulkCompanyProfile"/>, which is kept separate because
    /// <c>profile-bulk</c> genuinely differs from <c>stable/profile</c> in the TYPE of a shared column.</para>
    ///
    /// <para><b><c>acceptedDate</c> is read as Eastern here, not UTC.</b> It is EDGAR's wall clock, matching
    /// <see cref="NullableEasternInstantJsonConverter"/> on the JSON path. The CSV reader's ordinary
    /// <c>GetInstant</c> reads the identical wire shape as UTC, because <c>shares-float</c>'s <c>date</c> really
    /// is UTC — so using it here would make this property mean two different instants depending on which endpoint
    /// the row arrived from, and be wrong by that date's offset. The reading is confirmed by the distribution
    /// rather than asserted: of the 20,068 rows carrying a real time, 99.8% fall inside 06:00-21:59, which is
    /// EDGAR's acceptance window.</para>
    ///
    /// <para><b>More than half the rows have no acceptance time at all, and it does not look that way.</b>
    /// Measured over <c>income-statement-bulk</c> for 2025 Q1: 23,056 of 43,124 rows carry <c>acceptedDate</c>
    /// ending <c>00:00:00</c> — a date padded to midnight, not a filing accepted at midnight. They skew heavily
    /// non-US (80% carry an exchange suffix; the top currencies are CNY, CAD, TWD and EUR, against USD, INR and
    /// JPY among the timed rows). The value is preserved as sent rather than nulled, because midnight is a legal
    /// instant and silently discarding it would hide the pattern — but anything computing a time of day from this
    /// field should check for it. The per-symbol endpoint is not affected the same way; this is a bulk
    /// characteristic.</para></summary>
    internal static IncomeStatement FromCsv(CsvRow row) => new()
    {
        Date = row.GetDate("date"),
        Symbol = row.GetString("symbol"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        Cik = row.GetString("cik"),
        FilingDate = row.GetDate("filingDate"),
        AcceptedDate = row.GetEasternInstant("acceptedDate"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        Revenue = row.GetDecimal("revenue"),
        CostOfRevenue = row.GetDecimal("costOfRevenue"),
        GrossProfit = row.GetDecimal("grossProfit"),
        ResearchAndDevelopmentExpenses = row.GetDecimal("researchAndDevelopmentExpenses"),
        GeneralAndAdministrativeExpenses = row.GetDecimal("generalAndAdministrativeExpenses"),
        SellingAndMarketingExpenses = row.GetDecimal("sellingAndMarketingExpenses"),
        SellingGeneralAndAdministrativeExpenses = row.GetDecimal("sellingGeneralAndAdministrativeExpenses"),
        OtherExpenses = row.GetDecimal("otherExpenses"),
        OperatingExpenses = row.GetDecimal("operatingExpenses"),
        CostAndExpenses = row.GetDecimal("costAndExpenses"),
        NetInterestIncome = row.GetDecimal("netInterestIncome"),
        InterestIncome = row.GetDecimal("interestIncome"),
        InterestExpense = row.GetDecimal("interestExpense"),
        DepreciationAndAmortization = row.GetDecimal("depreciationAndAmortization"),
        Ebitda = row.GetDecimal("ebitda"),
        Ebit = row.GetDecimal("ebit"),
        NonOperatingIncomeExcludingInterest = row.GetDecimal("nonOperatingIncomeExcludingInterest"),
        OperatingIncome = row.GetDecimal("operatingIncome"),
        TotalOtherIncomeExpensesNet = row.GetDecimal("totalOtherIncomeExpensesNet"),
        IncomeBeforeTax = row.GetDecimal("incomeBeforeTax"),
        IncomeTaxExpense = row.GetDecimal("incomeTaxExpense"),
        NetIncomeFromContinuingOperations = row.GetDecimal("netIncomeFromContinuingOperations"),
        NetIncomeFromDiscontinuedOperations = row.GetDecimal("netIncomeFromDiscontinuedOperations"),
        OtherAdjustmentsToNetIncome = row.GetDecimal("otherAdjustmentsToNetIncome"),
        NetIncome = row.GetDecimal("netIncome"),
        NetIncomeDeductions = row.GetDecimal("netIncomeDeductions"),
        BottomLineNetIncome = row.GetDecimal("bottomLineNetIncome"),
        Eps = row.GetDecimal("eps"),
        EpsDiluted = row.GetDecimal("epsDiluted"),
        WeightedAverageSharesOutstanding = row.GetDecimal("weightedAverageShsOut"),
        WeightedAverageSharesOutstandingDiluted = row.GetDecimal("weightedAverageShsOutDil"),
    };
}
