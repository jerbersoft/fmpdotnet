using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 53 properties below are a flat transcription of FMP's wire fields: the property name
// carries the same information a generated one-line summary would, and 53 of those would bury the type-level
// documentation above — which is where this response's actual quirks are recorded.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this used to, also meant a NEW
// undocumented public member anywhere in the SDK compiled silently. The seven transcription models are the only
// exemptions, and the zero-warning bar holds everywhere else.
#pragma warning disable CS1591

/// <summary>One period of a balance sheet. From <c>stable/balance-sheet-statement</c>.
///
/// <para>Line items an issuer does not report arrive as <c>0</c> rather than null, so a zero here means "not reported" at least as often as it means "genuinely zero". Banks and insurers populate a visibly different subset from industrials.</para>
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record BalanceSheetStatement
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

    // ---- Assets — current ----
    [JsonPropertyName("cashAndCashEquivalents")] public decimal? CashAndCashEquivalents { get; init; }

    [JsonPropertyName("shortTermInvestments")] public decimal? ShortTermInvestments { get; init; }

    [JsonPropertyName("cashAndShortTermInvestments")] public decimal? CashAndShortTermInvestments { get; init; }

    [JsonPropertyName("netReceivables")] public decimal? NetReceivables { get; init; }

    [JsonPropertyName("accountsReceivables")] public decimal? AccountsReceivables { get; init; }

    [JsonPropertyName("otherReceivables")] public decimal? OtherReceivables { get; init; }

    [JsonPropertyName("inventory")] public decimal? Inventory { get; init; }

    [JsonPropertyName("prepaids")] public decimal? Prepaids { get; init; }

    [JsonPropertyName("otherCurrentAssets")] public decimal? OtherCurrentAssets { get; init; }

    [JsonPropertyName("totalCurrentAssets")] public decimal? TotalCurrentAssets { get; init; }

    // ---- Assets — non-current ----
    [JsonPropertyName("propertyPlantEquipmentNet")] public decimal? PropertyPlantEquipmentNet { get; init; }

    [JsonPropertyName("goodwill")] public decimal? Goodwill { get; init; }

    [JsonPropertyName("intangibleAssets")] public decimal? IntangibleAssets { get; init; }

    [JsonPropertyName("goodwillAndIntangibleAssets")] public decimal? GoodwillAndIntangibleAssets { get; init; }

    [JsonPropertyName("longTermInvestments")] public decimal? LongTermInvestments { get; init; }

    [JsonPropertyName("taxAssets")] public decimal? TaxAssets { get; init; }

    [JsonPropertyName("otherNonCurrentAssets")] public decimal? OtherNonCurrentAssets { get; init; }

    [JsonPropertyName("totalNonCurrentAssets")] public decimal? TotalNonCurrentAssets { get; init; }

    [JsonPropertyName("otherAssets")] public decimal? OtherAssets { get; init; }

    [JsonPropertyName("totalAssets")] public decimal? TotalAssets { get; init; }

    // ---- Liabilities — current ----
    [JsonPropertyName("totalPayables")] public decimal? TotalPayables { get; init; }

    [JsonPropertyName("accountPayables")] public decimal? AccountPayables { get; init; }

    [JsonPropertyName("otherPayables")] public decimal? OtherPayables { get; init; }

    [JsonPropertyName("accruedExpenses")] public decimal? AccruedExpenses { get; init; }

    [JsonPropertyName("shortTermDebt")] public decimal? ShortTermDebt { get; init; }

    [JsonPropertyName("capitalLeaseObligationsCurrent")] public decimal? CapitalLeaseObligationsCurrent { get; init; }

    [JsonPropertyName("taxPayables")] public decimal? TaxPayables { get; init; }

    [JsonPropertyName("deferredRevenue")] public decimal? DeferredRevenue { get; init; }

    [JsonPropertyName("otherCurrentLiabilities")] public decimal? OtherCurrentLiabilities { get; init; }

    [JsonPropertyName("totalCurrentLiabilities")] public decimal? TotalCurrentLiabilities { get; init; }

    // ---- Liabilities — non-current ----
    [JsonPropertyName("longTermDebt")] public decimal? LongTermDebt { get; init; }

    [JsonPropertyName("capitalLeaseObligationsNonCurrent")] public decimal? CapitalLeaseObligationsNonCurrent { get; init; }

    [JsonPropertyName("deferredRevenueNonCurrent")] public decimal? DeferredRevenueNonCurrent { get; init; }

    [JsonPropertyName("deferredTaxLiabilitiesNonCurrent")] public decimal? DeferredTaxLiabilitiesNonCurrent { get; init; }

    [JsonPropertyName("otherNonCurrentLiabilities")] public decimal? OtherNonCurrentLiabilities { get; init; }

    [JsonPropertyName("totalNonCurrentLiabilities")] public decimal? TotalNonCurrentLiabilities { get; init; }

    [JsonPropertyName("otherLiabilities")] public decimal? OtherLiabilities { get; init; }

    [JsonPropertyName("capitalLeaseObligations")] public decimal? CapitalLeaseObligations { get; init; }

    [JsonPropertyName("totalLiabilities")] public decimal? TotalLiabilities { get; init; }

    // ---- Equity ----
    [JsonPropertyName("treasuryStock")] public decimal? TreasuryStock { get; init; }

    [JsonPropertyName("preferredStock")] public decimal? PreferredStock { get; init; }

    [JsonPropertyName("commonStock")] public decimal? CommonStock { get; init; }

    [JsonPropertyName("retainedEarnings")] public decimal? RetainedEarnings { get; init; }

    [JsonPropertyName("additionalPaidInCapital")] public decimal? AdditionalPaidInCapital { get; init; }

    [JsonPropertyName("accumulatedOtherComprehensiveIncomeLoss")] public decimal? AccumulatedOtherComprehensiveIncomeLoss { get; init; }

    [JsonPropertyName("otherTotalStockholdersEquity")] public decimal? OtherTotalStockholdersEquity { get; init; }

    [JsonPropertyName("totalStockholdersEquity")] public decimal? TotalStockholdersEquity { get; init; }

    [JsonPropertyName("totalEquity")] public decimal? TotalEquity { get; init; }

    [JsonPropertyName("minorityInterest")] public decimal? MinorityInterest { get; init; }

    [JsonPropertyName("totalLiabilitiesAndTotalEquity")] public decimal? TotalLiabilitiesAndTotalEquity { get; init; }

    // ---- Derived aggregates ----
    [JsonPropertyName("totalInvestments")] public decimal? TotalInvestments { get; init; }

    [JsonPropertyName("totalDebt")] public decimal? TotalDebt { get; init; }

    [JsonPropertyName("netDebt")] public decimal? NetDebt { get; init; }

    /// <summary>Maps one row of the matching <c>*-bulk</c> CSV.
    ///
    /// <para><b>This type is shared with the per-symbol JSON endpoint deliberately, and that was verified rather
    /// than assumed.</b> The bulk CSV header was compared field by field against this model's
    /// <c>[JsonPropertyName]</c> values on 2026-08-26: 61 columns, 61 properties, no name on either side absent
    /// from the other. Duplicating 61 properties into a parallel bulk type would be 61 chances for the two to
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
    internal static BalanceSheetStatement FromCsv(CsvRow row) => new()
    {
        Date = row.GetDate("date"),
        Symbol = row.GetString("symbol"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        Cik = row.GetString("cik"),
        FilingDate = row.GetDate("filingDate"),
        AcceptedDate = row.GetEasternInstant("acceptedDate"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        CashAndCashEquivalents = row.GetDecimal("cashAndCashEquivalents"),
        ShortTermInvestments = row.GetDecimal("shortTermInvestments"),
        CashAndShortTermInvestments = row.GetDecimal("cashAndShortTermInvestments"),
        NetReceivables = row.GetDecimal("netReceivables"),
        AccountsReceivables = row.GetDecimal("accountsReceivables"),
        OtherReceivables = row.GetDecimal("otherReceivables"),
        Inventory = row.GetDecimal("inventory"),
        Prepaids = row.GetDecimal("prepaids"),
        OtherCurrentAssets = row.GetDecimal("otherCurrentAssets"),
        TotalCurrentAssets = row.GetDecimal("totalCurrentAssets"),
        PropertyPlantEquipmentNet = row.GetDecimal("propertyPlantEquipmentNet"),
        Goodwill = row.GetDecimal("goodwill"),
        IntangibleAssets = row.GetDecimal("intangibleAssets"),
        GoodwillAndIntangibleAssets = row.GetDecimal("goodwillAndIntangibleAssets"),
        LongTermInvestments = row.GetDecimal("longTermInvestments"),
        TaxAssets = row.GetDecimal("taxAssets"),
        OtherNonCurrentAssets = row.GetDecimal("otherNonCurrentAssets"),
        TotalNonCurrentAssets = row.GetDecimal("totalNonCurrentAssets"),
        OtherAssets = row.GetDecimal("otherAssets"),
        TotalAssets = row.GetDecimal("totalAssets"),
        TotalPayables = row.GetDecimal("totalPayables"),
        AccountPayables = row.GetDecimal("accountPayables"),
        OtherPayables = row.GetDecimal("otherPayables"),
        AccruedExpenses = row.GetDecimal("accruedExpenses"),
        ShortTermDebt = row.GetDecimal("shortTermDebt"),
        CapitalLeaseObligationsCurrent = row.GetDecimal("capitalLeaseObligationsCurrent"),
        TaxPayables = row.GetDecimal("taxPayables"),
        DeferredRevenue = row.GetDecimal("deferredRevenue"),
        OtherCurrentLiabilities = row.GetDecimal("otherCurrentLiabilities"),
        TotalCurrentLiabilities = row.GetDecimal("totalCurrentLiabilities"),
        LongTermDebt = row.GetDecimal("longTermDebt"),
        CapitalLeaseObligationsNonCurrent = row.GetDecimal("capitalLeaseObligationsNonCurrent"),
        DeferredRevenueNonCurrent = row.GetDecimal("deferredRevenueNonCurrent"),
        DeferredTaxLiabilitiesNonCurrent = row.GetDecimal("deferredTaxLiabilitiesNonCurrent"),
        OtherNonCurrentLiabilities = row.GetDecimal("otherNonCurrentLiabilities"),
        TotalNonCurrentLiabilities = row.GetDecimal("totalNonCurrentLiabilities"),
        OtherLiabilities = row.GetDecimal("otherLiabilities"),
        CapitalLeaseObligations = row.GetDecimal("capitalLeaseObligations"),
        TotalLiabilities = row.GetDecimal("totalLiabilities"),
        TreasuryStock = row.GetDecimal("treasuryStock"),
        PreferredStock = row.GetDecimal("preferredStock"),
        CommonStock = row.GetDecimal("commonStock"),
        RetainedEarnings = row.GetDecimal("retainedEarnings"),
        AdditionalPaidInCapital = row.GetDecimal("additionalPaidInCapital"),
        AccumulatedOtherComprehensiveIncomeLoss = row.GetDecimal("accumulatedOtherComprehensiveIncomeLoss"),
        OtherTotalStockholdersEquity = row.GetDecimal("otherTotalStockholdersEquity"),
        TotalStockholdersEquity = row.GetDecimal("totalStockholdersEquity"),
        TotalEquity = row.GetDecimal("totalEquity"),
        MinorityInterest = row.GetDecimal("minorityInterest"),
        TotalLiabilitiesAndTotalEquity = row.GetDecimal("totalLiabilitiesAndTotalEquity"),
        TotalInvestments = row.GetDecimal("totalInvestments"),
        TotalDebt = row.GetDecimal("totalDebt"),
        NetDebt = row.GetDecimal("netDebt"),
    };
}
