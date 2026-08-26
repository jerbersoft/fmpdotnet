using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

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
}
