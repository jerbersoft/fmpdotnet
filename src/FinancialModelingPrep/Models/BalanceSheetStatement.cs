using System.Text.Json.Serialization;
using FinancialModelingPrep.Serialization;
using NodaTime;

namespace FinancialModelingPrep.Models;

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
}
