using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>Period-over-period growth of one cash flow statement. From <c>stable/cash-flow-statement-growth-bulk</c> — 41,706 rows and 17.0 MB measured 2026-08-26 for 2025 Q1.
///
/// <para>Every figure is a <b>fraction</b>, not a percentage: 0.12 is twelve percent. FMP sends 0 where the prior
/// period was zero or absent, so a zero here cannot be distinguished from "no prior period to grow from".</para>
///
/// <para><b>The column names are not the ones on the non-growth endpoint.</b> They are the upstream's own, typos
/// included, and several are recorded in the remarks below — <c>Activites</c> for <c>Activities</c> among them.
/// The C# name is corrected; the string passed to the CSV reader is FMP's, verbatim, because that is what
/// arrives.</para></summary>
public sealed record CashFlowGrowth
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

    /// <summary>Period-over-period growth in <c>NetIncome</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetIncome")] public decimal? GrowthNetIncome { get; init; }

    /// <summary>Period-over-period growth in <c>DepreciationAndAmortization</c>, as a fraction.</summary>
    [JsonPropertyName("growthDepreciationAndAmortization")] public decimal? GrowthDepreciationAndAmortization { get; init; }

    /// <summary>Period-over-period growth in <c>DeferredIncomeTax</c>, as a fraction.</summary>
    [JsonPropertyName("growthDeferredIncomeTax")] public decimal? GrowthDeferredIncomeTax { get; init; }

    /// <summary>Period-over-period growth in <c>StockBasedCompensation</c>, as a fraction.</summary>
    [JsonPropertyName("growthStockBasedCompensation")] public decimal? GrowthStockBasedCompensation { get; init; }

    /// <summary>Period-over-period growth in <c>ChangeInWorkingCapital</c>, as a fraction.</summary>
    [JsonPropertyName("growthChangeInWorkingCapital")] public decimal? GrowthChangeInWorkingCapital { get; init; }

    /// <summary>Period-over-period growth in <c>AccountsReceivables</c>, as a fraction.</summary>
    [JsonPropertyName("growthAccountsReceivables")] public decimal? GrowthAccountsReceivables { get; init; }

    /// <summary>Period-over-period growth in <c>Inventory</c>, as a fraction.</summary>
    [JsonPropertyName("growthInventory")] public decimal? GrowthInventory { get; init; }

    /// <summary>Period-over-period growth in <c>AccountsPayables</c>, as a fraction.</summary>
    [JsonPropertyName("growthAccountsPayables")] public decimal? GrowthAccountsPayables { get; init; }

    /// <summary>Period-over-period growth in <c>OtherWorkingCapital</c>, as a fraction.</summary>
    [JsonPropertyName("growthOtherWorkingCapital")] public decimal? GrowthOtherWorkingCapital { get; init; }

    /// <summary>Period-over-period growth in <c>OtherNonCashItems</c>, as a fraction.</summary>
    [JsonPropertyName("growthOtherNonCashItems")] public decimal? GrowthOtherNonCashItems { get; init; }

    /// <summary>Period-over-period growth in <c>NetCashProvidedByOperatingActivites</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthNetCashProvidedByOperatingActivites</c>.</remarks>
    [JsonPropertyName("growthNetCashProvidedByOperatingActivites")] public decimal? GrowthNetCashProvidedByOperatingActivities { get; init; }

    /// <summary>Period-over-period growth in <c>InvestmentsInPropertyPlantAndEquipment</c>, as a fraction.</summary>
    [JsonPropertyName("growthInvestmentsInPropertyPlantAndEquipment")] public decimal? GrowthInvestmentsInPropertyPlantAndEquipment { get; init; }

    /// <summary>Period-over-period growth in <c>AcquisitionsNet</c>, as a fraction.</summary>
    [JsonPropertyName("growthAcquisitionsNet")] public decimal? GrowthAcquisitionsNet { get; init; }

    /// <summary>Period-over-period growth in <c>PurchasesOfInvestments</c>, as a fraction.</summary>
    [JsonPropertyName("growthPurchasesOfInvestments")] public decimal? GrowthPurchasesOfInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>SalesMaturitiesOfInvestments</c>, as a fraction.</summary>
    [JsonPropertyName("growthSalesMaturitiesOfInvestments")] public decimal? GrowthSalesMaturitiesOfInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>OtherInvestingActivites</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthOtherInvestingActivites</c>.</remarks>
    [JsonPropertyName("growthOtherInvestingActivites")] public decimal? GrowthOtherInvestingActivities { get; init; }

    /// <summary>Period-over-period growth in <c>NetCashUsedForInvestingActivites</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthNetCashUsedForInvestingActivites</c>.</remarks>
    [JsonPropertyName("growthNetCashUsedForInvestingActivites")] public decimal? GrowthNetCashUsedForInvestingActivities { get; init; }

    /// <summary>Period-over-period growth in <c>DebtRepayment</c>, as a fraction.</summary>
    [JsonPropertyName("growthDebtRepayment")] public decimal? GrowthDebtRepayment { get; init; }

    /// <summary>Period-over-period growth in <c>CommonStockIssued</c>, as a fraction.</summary>
    [JsonPropertyName("growthCommonStockIssued")] public decimal? GrowthCommonStockIssued { get; init; }

    /// <summary>Period-over-period growth in <c>CommonStockRepurchased</c>, as a fraction.</summary>
    [JsonPropertyName("growthCommonStockRepurchased")] public decimal? GrowthCommonStockRepurchased { get; init; }

    /// <summary>Period-over-period growth in <c>DividendsPaid</c>, as a fraction.</summary>
    [JsonPropertyName("growthDividendsPaid")] public decimal? GrowthDividendsPaid { get; init; }

    /// <summary>Period-over-period growth in <c>OtherFinancingActivites</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthOtherFinancingActivites</c>.</remarks>
    [JsonPropertyName("growthOtherFinancingActivites")] public decimal? GrowthOtherFinancingActivities { get; init; }

    /// <summary>Period-over-period growth in <c>NetCashUsedProvidedByFinancingActivities</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetCashUsedProvidedByFinancingActivities")] public decimal? GrowthNetCashUsedProvidedByFinancingActivities { get; init; }

    /// <summary>Period-over-period growth in <c>EffectOfForexChangesOnCash</c>, as a fraction.</summary>
    [JsonPropertyName("growthEffectOfForexChangesOnCash")] public decimal? GrowthEffectOfForexChangesOnCash { get; init; }

    /// <summary>Period-over-period growth in <c>NetChangeInCash</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetChangeInCash")] public decimal? GrowthNetChangeInCash { get; init; }

    /// <summary>Period-over-period growth in <c>CashAtEndOfPeriod</c>, as a fraction.</summary>
    [JsonPropertyName("growthCashAtEndOfPeriod")] public decimal? GrowthCashAtEndOfPeriod { get; init; }

    /// <summary>Period-over-period growth in <c>CashAtBeginningOfPeriod</c>, as a fraction.</summary>
    [JsonPropertyName("growthCashAtBeginningOfPeriod")] public decimal? GrowthCashAtBeginningOfPeriod { get; init; }

    /// <summary>Period-over-period growth in <c>OperatingCashFlow</c>, as a fraction.</summary>
    [JsonPropertyName("growthOperatingCashFlow")] public decimal? GrowthOperatingCashFlow { get; init; }

    /// <summary>Period-over-period growth in <c>CapitalExpenditure</c>, as a fraction.</summary>
    [JsonPropertyName("growthCapitalExpenditure")] public decimal? GrowthCapitalExpenditure { get; init; }

    /// <summary>Period-over-period growth in <c>FreeCashFlow</c>, as a fraction.</summary>
    [JsonPropertyName("growthFreeCashFlow")] public decimal? GrowthFreeCashFlow { get; init; }

    /// <summary>Period-over-period growth in <c>NetDebtIssuance</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetDebtIssuance")] public decimal? GrowthNetDebtIssuance { get; init; }

    /// <summary>Period-over-period growth in <c>LongTermNetDebtIssuance</c>, as a fraction.</summary>
    [JsonPropertyName("growthLongTermNetDebtIssuance")] public decimal? GrowthLongTermNetDebtIssuance { get; init; }

    /// <summary>Period-over-period growth in <c>ShortTermNetDebtIssuance</c>, as a fraction.</summary>
    [JsonPropertyName("growthShortTermNetDebtIssuance")] public decimal? GrowthShortTermNetDebtIssuance { get; init; }

    /// <summary>Period-over-period growth in <c>NetStockIssuance</c>, as a fraction.</summary>
    [JsonPropertyName("growthNetStockIssuance")] public decimal? GrowthNetStockIssuance { get; init; }

    /// <summary>Period-over-period growth in <c>PreferredDividendsPaid</c>, as a fraction.</summary>
    [JsonPropertyName("growthPreferredDividendsPaid")] public decimal? GrowthPreferredDividendsPaid { get; init; }

    /// <summary>Period-over-period growth in <c>IncomeTaxesPaid</c>, as a fraction.</summary>
    [JsonPropertyName("growthIncomeTaxesPaid")] public decimal? GrowthIncomeTaxesPaid { get; init; }

    /// <summary>Period-over-period growth in <c>InterestPaid</c>, as a fraction.</summary>
    [JsonPropertyName("growthInterestPaid")] public decimal? GrowthInterestPaid { get; init; }

    internal static CashFlowGrowth FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        GrowthNetIncome = row.GetDecimal("growthNetIncome"),
        GrowthDepreciationAndAmortization = row.GetDecimal("growthDepreciationAndAmortization"),
        GrowthDeferredIncomeTax = row.GetDecimal("growthDeferredIncomeTax"),
        GrowthStockBasedCompensation = row.GetDecimal("growthStockBasedCompensation"),
        GrowthChangeInWorkingCapital = row.GetDecimal("growthChangeInWorkingCapital"),
        GrowthAccountsReceivables = row.GetDecimal("growthAccountsReceivables"),
        GrowthInventory = row.GetDecimal("growthInventory"),
        GrowthAccountsPayables = row.GetDecimal("growthAccountsPayables"),
        GrowthOtherWorkingCapital = row.GetDecimal("growthOtherWorkingCapital"),
        GrowthOtherNonCashItems = row.GetDecimal("growthOtherNonCashItems"),
        GrowthNetCashProvidedByOperatingActivities = row.GetDecimal("growthNetCashProvidedByOperatingActivites"),
        GrowthInvestmentsInPropertyPlantAndEquipment = row.GetDecimal("growthInvestmentsInPropertyPlantAndEquipment"),
        GrowthAcquisitionsNet = row.GetDecimal("growthAcquisitionsNet"),
        GrowthPurchasesOfInvestments = row.GetDecimal("growthPurchasesOfInvestments"),
        GrowthSalesMaturitiesOfInvestments = row.GetDecimal("growthSalesMaturitiesOfInvestments"),
        GrowthOtherInvestingActivities = row.GetDecimal("growthOtherInvestingActivites"),
        GrowthNetCashUsedForInvestingActivities = row.GetDecimal("growthNetCashUsedForInvestingActivites"),
        GrowthDebtRepayment = row.GetDecimal("growthDebtRepayment"),
        GrowthCommonStockIssued = row.GetDecimal("growthCommonStockIssued"),
        GrowthCommonStockRepurchased = row.GetDecimal("growthCommonStockRepurchased"),
        GrowthDividendsPaid = row.GetDecimal("growthDividendsPaid"),
        GrowthOtherFinancingActivities = row.GetDecimal("growthOtherFinancingActivites"),
        GrowthNetCashUsedProvidedByFinancingActivities = row.GetDecimal("growthNetCashUsedProvidedByFinancingActivities"),
        GrowthEffectOfForexChangesOnCash = row.GetDecimal("growthEffectOfForexChangesOnCash"),
        GrowthNetChangeInCash = row.GetDecimal("growthNetChangeInCash"),
        GrowthCashAtEndOfPeriod = row.GetDecimal("growthCashAtEndOfPeriod"),
        GrowthCashAtBeginningOfPeriod = row.GetDecimal("growthCashAtBeginningOfPeriod"),
        GrowthOperatingCashFlow = row.GetDecimal("growthOperatingCashFlow"),
        GrowthCapitalExpenditure = row.GetDecimal("growthCapitalExpenditure"),
        GrowthFreeCashFlow = row.GetDecimal("growthFreeCashFlow"),
        GrowthNetDebtIssuance = row.GetDecimal("growthNetDebtIssuance"),
        GrowthLongTermNetDebtIssuance = row.GetDecimal("growthLongTermNetDebtIssuance"),
        GrowthShortTermNetDebtIssuance = row.GetDecimal("growthShortTermNetDebtIssuance"),
        GrowthNetStockIssuance = row.GetDecimal("growthNetStockIssuance"),
        GrowthPreferredDividendsPaid = row.GetDecimal("growthPreferredDividendsPaid"),
        GrowthIncomeTaxesPaid = row.GetDecimal("growthIncomeTaxesPaid"),
        GrowthInterestPaid = row.GetDecimal("growthInterestPaid"),
    };
}
