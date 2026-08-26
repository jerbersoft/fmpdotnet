using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 39 properties below are a flat transcription of FMP's wire fields: the property name
// carries the same information a generated one-line summary would, and 39 of those would bury the type-level
// documentation above — which is where this response's actual quirks are recorded.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this used to, also meant a NEW
// undocumented public member anywhere in the SDK compiled silently. The seven transcription models are the only
// exemptions, and the zero-warning bar holds everywhere else.
#pragma warning disable CS1591

/// <summary>One period of a cash flow statement. From <c>stable/cash-flow-statement</c>.
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record CashFlowStatement
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

    // ---- Operating activities ----
    [JsonPropertyName("netIncome")] public decimal? NetIncome { get; init; }

    [JsonPropertyName("depreciationAndAmortization")] public decimal? DepreciationAndAmortization { get; init; }

    [JsonPropertyName("deferredIncomeTax")] public decimal? DeferredIncomeTax { get; init; }

    [JsonPropertyName("stockBasedCompensation")] public decimal? StockBasedCompensation { get; init; }

    [JsonPropertyName("changeInWorkingCapital")] public decimal? ChangeInWorkingCapital { get; init; }

    [JsonPropertyName("accountsReceivables")] public decimal? AccountsReceivables { get; init; }

    [JsonPropertyName("inventory")] public decimal? Inventory { get; init; }

    [JsonPropertyName("accountsPayables")] public decimal? AccountsPayables { get; init; }

    [JsonPropertyName("otherWorkingCapital")] public decimal? OtherWorkingCapital { get; init; }

    [JsonPropertyName("otherNonCashItems")] public decimal? OtherNonCashItems { get; init; }

    [JsonPropertyName("netCashProvidedByOperatingActivities")] public decimal? NetCashProvidedByOperatingActivities { get; init; }

    // ---- Investing activities ----
    [JsonPropertyName("investmentsInPropertyPlantAndEquipment")] public decimal? InvestmentsInPropertyPlantAndEquipment { get; init; }

    [JsonPropertyName("acquisitionsNet")] public decimal? AcquisitionsNet { get; init; }

    [JsonPropertyName("purchasesOfInvestments")] public decimal? PurchasesOfInvestments { get; init; }

    [JsonPropertyName("salesMaturitiesOfInvestments")] public decimal? SalesMaturitiesOfInvestments { get; init; }

    [JsonPropertyName("otherInvestingActivities")] public decimal? OtherInvestingActivities { get; init; }

    [JsonPropertyName("netCashProvidedByInvestingActivities")] public decimal? NetCashProvidedByInvestingActivities { get; init; }

    // ---- Financing activities ----
    [JsonPropertyName("netDebtIssuance")] public decimal? NetDebtIssuance { get; init; }

    [JsonPropertyName("longTermNetDebtIssuance")] public decimal? LongTermNetDebtIssuance { get; init; }

    [JsonPropertyName("shortTermNetDebtIssuance")] public decimal? ShortTermNetDebtIssuance { get; init; }

    [JsonPropertyName("netStockIssuance")] public decimal? NetStockIssuance { get; init; }

    [JsonPropertyName("netCommonStockIssuance")] public decimal? NetCommonStockIssuance { get; init; }

    [JsonPropertyName("commonStockIssuance")] public decimal? CommonStockIssuance { get; init; }

    [JsonPropertyName("commonStockRepurchased")] public decimal? CommonStockRepurchased { get; init; }

    [JsonPropertyName("netPreferredStockIssuance")] public decimal? NetPreferredStockIssuance { get; init; }

    [JsonPropertyName("netDividendsPaid")] public decimal? NetDividendsPaid { get; init; }

    [JsonPropertyName("commonDividendsPaid")] public decimal? CommonDividendsPaid { get; init; }

    [JsonPropertyName("preferredDividendsPaid")] public decimal? PreferredDividendsPaid { get; init; }

    [JsonPropertyName("otherFinancingActivities")] public decimal? OtherFinancingActivities { get; init; }

    [JsonPropertyName("netCashProvidedByFinancingActivities")] public decimal? NetCashProvidedByFinancingActivities { get; init; }

    // ---- Reconciliation ----
    [JsonPropertyName("effectOfForexChangesOnCash")] public decimal? EffectOfForexChangesOnCash { get; init; }

    [JsonPropertyName("netChangeInCash")] public decimal? NetChangeInCash { get; init; }

    [JsonPropertyName("cashAtEndOfPeriod")] public decimal? CashAtEndOfPeriod { get; init; }

    [JsonPropertyName("cashAtBeginningOfPeriod")] public decimal? CashAtBeginningOfPeriod { get; init; }

    // ---- Derived aggregates ----
    [JsonPropertyName("operatingCashFlow")] public decimal? OperatingCashFlow { get; init; }

    [JsonPropertyName("capitalExpenditure")] public decimal? CapitalExpenditure { get; init; }

    [JsonPropertyName("freeCashFlow")] public decimal? FreeCashFlow { get; init; }

    [JsonPropertyName("incomeTaxesPaid")] public decimal? IncomeTaxesPaid { get; init; }

    [JsonPropertyName("interestPaid")] public decimal? InterestPaid { get; init; }

    /// <summary>Maps one row of the matching <c>*-bulk</c> CSV.
    ///
    /// <para><b>This type is shared with the per-symbol JSON endpoint deliberately, and that was verified rather
    /// than assumed.</b> The bulk CSV header was compared field by field against this model's
    /// <c>[JsonPropertyName]</c> values on 2026-08-26: 47 columns, 47 properties, no name on either side absent
    /// from the other. Duplicating 47 properties into a parallel bulk type would be 47 chances for the two to
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
    internal static CashFlowStatement FromCsv(CsvRow row) => new()
    {
        Date = row.GetDate("date"),
        Symbol = row.GetString("symbol"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        Cik = row.GetString("cik"),
        FilingDate = row.GetDate("filingDate"),
        AcceptedDate = row.GetEasternInstant("acceptedDate"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        NetIncome = row.GetDecimal("netIncome"),
        DepreciationAndAmortization = row.GetDecimal("depreciationAndAmortization"),
        DeferredIncomeTax = row.GetDecimal("deferredIncomeTax"),
        StockBasedCompensation = row.GetDecimal("stockBasedCompensation"),
        ChangeInWorkingCapital = row.GetDecimal("changeInWorkingCapital"),
        AccountsReceivables = row.GetDecimal("accountsReceivables"),
        Inventory = row.GetDecimal("inventory"),
        AccountsPayables = row.GetDecimal("accountsPayables"),
        OtherWorkingCapital = row.GetDecimal("otherWorkingCapital"),
        OtherNonCashItems = row.GetDecimal("otherNonCashItems"),
        NetCashProvidedByOperatingActivities = row.GetDecimal("netCashProvidedByOperatingActivities"),
        InvestmentsInPropertyPlantAndEquipment = row.GetDecimal("investmentsInPropertyPlantAndEquipment"),
        AcquisitionsNet = row.GetDecimal("acquisitionsNet"),
        PurchasesOfInvestments = row.GetDecimal("purchasesOfInvestments"),
        SalesMaturitiesOfInvestments = row.GetDecimal("salesMaturitiesOfInvestments"),
        OtherInvestingActivities = row.GetDecimal("otherInvestingActivities"),
        NetCashProvidedByInvestingActivities = row.GetDecimal("netCashProvidedByInvestingActivities"),
        NetDebtIssuance = row.GetDecimal("netDebtIssuance"),
        LongTermNetDebtIssuance = row.GetDecimal("longTermNetDebtIssuance"),
        ShortTermNetDebtIssuance = row.GetDecimal("shortTermNetDebtIssuance"),
        NetStockIssuance = row.GetDecimal("netStockIssuance"),
        NetCommonStockIssuance = row.GetDecimal("netCommonStockIssuance"),
        CommonStockIssuance = row.GetDecimal("commonStockIssuance"),
        CommonStockRepurchased = row.GetDecimal("commonStockRepurchased"),
        NetPreferredStockIssuance = row.GetDecimal("netPreferredStockIssuance"),
        NetDividendsPaid = row.GetDecimal("netDividendsPaid"),
        CommonDividendsPaid = row.GetDecimal("commonDividendsPaid"),
        PreferredDividendsPaid = row.GetDecimal("preferredDividendsPaid"),
        OtherFinancingActivities = row.GetDecimal("otherFinancingActivities"),
        NetCashProvidedByFinancingActivities = row.GetDecimal("netCashProvidedByFinancingActivities"),
        EffectOfForexChangesOnCash = row.GetDecimal("effectOfForexChangesOnCash"),
        NetChangeInCash = row.GetDecimal("netChangeInCash"),
        CashAtEndOfPeriod = row.GetDecimal("cashAtEndOfPeriod"),
        CashAtBeginningOfPeriod = row.GetDecimal("cashAtBeginningOfPeriod"),
        OperatingCashFlow = row.GetDecimal("operatingCashFlow"),
        CapitalExpenditure = row.GetDecimal("capitalExpenditure"),
        FreeCashFlow = row.GetDecimal("freeCashFlow"),
        IncomeTaxesPaid = row.GetDecimal("incomeTaxesPaid"),
        InterestPaid = row.GetDecimal("interestPaid"),
    };
}
