using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>Period-over-period growth of one balance sheet. From <c>stable/balance-sheet-statement-growth-bulk</c> — 42,361 rows and 29.1 MB measured 2026-08-26 for 2025 Q1.
///
/// <para>Every figure is a <b>fraction</b>, not a percentage: 0.12 is twelve percent. FMP sends 0 where the prior
/// period was zero or absent, so a zero here cannot be distinguished from "no prior period to grow from".</para>
///
/// <para><b>The column names are not the ones on the non-growth endpoint.</b> They are the upstream's own, typos
/// included, and several are recorded in the remarks below — <c>Activites</c> for <c>Activities</c> among them.
/// The C# name is corrected; the string passed to the CSV reader is FMP's, verbatim, because that is what
/// arrives.</para></summary>
public sealed record BalanceSheetGrowth
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    public LocalDate? Date { get; init; }

    /// <summary>Fiscal year.</summary>
    public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>-<c>Q4</c> for quarterly.</summary>
    public string? Period { get; init; }

    /// <summary>ISO currency the underlying statement is reported in — not necessarily USD.</summary>
    public string? ReportedCurrency { get; init; }

    /// <summary>Period-over-period growth in <c>CashAndCashEquivalents</c>, as a fraction.</summary>
    public decimal? GrowthCashAndCashEquivalents { get; init; }

    /// <summary>Period-over-period growth in <c>ShortTermInvestments</c>, as a fraction.</summary>
    public decimal? GrowthShortTermInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>CashAndShortTermInvestments</c>, as a fraction.</summary>
    public decimal? GrowthCashAndShortTermInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>NetReceivables</c>, as a fraction.</summary>
    public decimal? GrowthNetReceivables { get; init; }

    /// <summary>Period-over-period growth in <c>Inventory</c>, as a fraction.</summary>
    public decimal? GrowthInventory { get; init; }

    /// <summary>Period-over-period growth in <c>OtherCurrentAssets</c>, as a fraction.</summary>
    public decimal? GrowthOtherCurrentAssets { get; init; }

    /// <summary>Period-over-period growth in <c>TotalCurrentAssets</c>, as a fraction.</summary>
    public decimal? GrowthTotalCurrentAssets { get; init; }

    /// <summary>Period-over-period growth in <c>PropertyPlantEquipmentNet</c>, as a fraction.</summary>
    public decimal? GrowthPropertyPlantEquipmentNet { get; init; }

    /// <summary>Period-over-period growth in <c>Goodwill</c>, as a fraction.</summary>
    public decimal? GrowthGoodwill { get; init; }

    /// <summary>Period-over-period growth in <c>IntangibleAssets</c>, as a fraction.</summary>
    public decimal? GrowthIntangibleAssets { get; init; }

    /// <summary>Period-over-period growth in <c>GoodwillAndIntangibleAssets</c>, as a fraction.</summary>
    public decimal? GrowthGoodwillAndIntangibleAssets { get; init; }

    /// <summary>Period-over-period growth in <c>LongTermInvestments</c>, as a fraction.</summary>
    public decimal? GrowthLongTermInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>TaxAssets</c>, as a fraction.</summary>
    public decimal? GrowthTaxAssets { get; init; }

    /// <summary>Period-over-period growth in <c>OtherNonCurrentAssets</c>, as a fraction.</summary>
    public decimal? GrowthOtherNonCurrentAssets { get; init; }

    /// <summary>Period-over-period growth in <c>TotalNonCurrentAssets</c>, as a fraction.</summary>
    public decimal? GrowthTotalNonCurrentAssets { get; init; }

    /// <summary>Period-over-period growth in <c>OtherAssets</c>, as a fraction.</summary>
    public decimal? GrowthOtherAssets { get; init; }

    /// <summary>Period-over-period growth in <c>TotalAssets</c>, as a fraction.</summary>
    public decimal? GrowthTotalAssets { get; init; }

    /// <summary>Period-over-period growth in <c>AccountPayables</c>, as a fraction.</summary>
    public decimal? GrowthAccountPayables { get; init; }

    /// <summary>Period-over-period growth in <c>ShortTermDebt</c>, as a fraction.</summary>
    public decimal? GrowthShortTermDebt { get; init; }

    /// <summary>Period-over-period growth in <c>TaxPayables</c>, as a fraction.</summary>
    public decimal? GrowthTaxPayables { get; init; }

    /// <summary>Period-over-period growth in <c>DeferredRevenue</c>, as a fraction.</summary>
    public decimal? GrowthDeferredRevenue { get; init; }

    /// <summary>Period-over-period growth in <c>OtherCurrentLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthOtherCurrentLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>TotalCurrentLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthTotalCurrentLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>LongTermDebt</c>, as a fraction.</summary>
    public decimal? GrowthLongTermDebt { get; init; }

    /// <summary>Period-over-period growth in <c>DeferredRevenueNonCurrent</c>, as a fraction.</summary>
    public decimal? GrowthDeferredRevenueNonCurrent { get; init; }

    /// <summary>Period-over-period growth in <c>DeferredTaxLiabilitiesNonCurrent</c>, as a fraction.</summary>
    public decimal? GrowthDeferredTaxLiabilitiesNonCurrent { get; init; }

    /// <summary>Period-over-period growth in <c>OtherNonCurrentLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthOtherNonCurrentLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>TotalNonCurrentLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthTotalNonCurrentLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>OtherLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthOtherLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>TotalLiabilities</c>, as a fraction.</summary>
    public decimal? GrowthTotalLiabilities { get; init; }

    /// <summary>Period-over-period growth in <c>PreferredStock</c>, as a fraction.</summary>
    public decimal? GrowthPreferredStock { get; init; }

    /// <summary>Period-over-period growth in <c>CommonStock</c>, as a fraction.</summary>
    public decimal? GrowthCommonStock { get; init; }

    /// <summary>Period-over-period growth in <c>RetainedEarnings</c>, as a fraction.</summary>
    public decimal? GrowthRetainedEarnings { get; init; }

    /// <summary>Period-over-period growth in <c>AccumulatedOtherComprehensiveIncomeLoss</c>, as a fraction.</summary>
    public decimal? GrowthAccumulatedOtherComprehensiveIncomeLoss { get; init; }

    /// <summary>Period-over-period growth in <c>OthertotalStockholdersEquity</c>, as a fraction.</summary>
    /// <remarks>FMP spells the column <c>growthOthertotalStockholdersEquity</c>.</remarks>
    public decimal? GrowthOtherTotalStockholdersEquity { get; init; }

    /// <summary>Period-over-period growth in <c>TotalStockholdersEquity</c>, as a fraction.</summary>
    public decimal? GrowthTotalStockholdersEquity { get; init; }

    /// <summary>Period-over-period growth in <c>MinorityInterest</c>, as a fraction.</summary>
    public decimal? GrowthMinorityInterest { get; init; }

    /// <summary>Period-over-period growth in <c>TotalEquity</c>, as a fraction.</summary>
    public decimal? GrowthTotalEquity { get; init; }

    /// <summary>Period-over-period growth in <c>TotalLiabilitiesAndStockholdersEquity</c>, as a fraction.</summary>
    public decimal? GrowthTotalLiabilitiesAndStockholdersEquity { get; init; }

    /// <summary>Period-over-period growth in <c>TotalInvestments</c>, as a fraction.</summary>
    public decimal? GrowthTotalInvestments { get; init; }

    /// <summary>Period-over-period growth in <c>TotalDebt</c>, as a fraction.</summary>
    public decimal? GrowthTotalDebt { get; init; }

    /// <summary>Period-over-period growth in <c>NetDebt</c>, as a fraction.</summary>
    public decimal? GrowthNetDebt { get; init; }

    /// <summary>Period-over-period growth in <c>AccountsReceivables</c>, as a fraction.</summary>
    public decimal? GrowthAccountsReceivables { get; init; }

    /// <summary>Period-over-period growth in <c>OtherReceivables</c>, as a fraction.</summary>
    public decimal? GrowthOtherReceivables { get; init; }

    /// <summary>Period-over-period growth in <c>Prepaids</c>, as a fraction.</summary>
    public decimal? GrowthPrepaids { get; init; }

    /// <summary>Period-over-period growth in <c>TotalPayables</c>, as a fraction.</summary>
    public decimal? GrowthTotalPayables { get; init; }

    /// <summary>Period-over-period growth in <c>OtherPayables</c>, as a fraction.</summary>
    public decimal? GrowthOtherPayables { get; init; }

    /// <summary>Period-over-period growth in <c>AccruedExpenses</c>, as a fraction.</summary>
    public decimal? GrowthAccruedExpenses { get; init; }

    /// <summary>Period-over-period growth in <c>CapitalLeaseObligationsCurrent</c>, as a fraction.</summary>
    public decimal? GrowthCapitalLeaseObligationsCurrent { get; init; }

    /// <summary>Period-over-period growth in <c>AdditionalPaidInCapital</c>, as a fraction.</summary>
    public decimal? GrowthAdditionalPaidInCapital { get; init; }

    /// <summary>Period-over-period growth in <c>TreasuryStock</c>, as a fraction.</summary>
    public decimal? GrowthTreasuryStock { get; init; }

    internal static BalanceSheetGrowth FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        FiscalYear = row.GetInt32("fiscalYear"),
        Period = row.GetString("period"),
        ReportedCurrency = row.GetString("reportedCurrency"),
        GrowthCashAndCashEquivalents = row.GetDecimal("growthCashAndCashEquivalents"),
        GrowthShortTermInvestments = row.GetDecimal("growthShortTermInvestments"),
        GrowthCashAndShortTermInvestments = row.GetDecimal("growthCashAndShortTermInvestments"),
        GrowthNetReceivables = row.GetDecimal("growthNetReceivables"),
        GrowthInventory = row.GetDecimal("growthInventory"),
        GrowthOtherCurrentAssets = row.GetDecimal("growthOtherCurrentAssets"),
        GrowthTotalCurrentAssets = row.GetDecimal("growthTotalCurrentAssets"),
        GrowthPropertyPlantEquipmentNet = row.GetDecimal("growthPropertyPlantEquipmentNet"),
        GrowthGoodwill = row.GetDecimal("growthGoodwill"),
        GrowthIntangibleAssets = row.GetDecimal("growthIntangibleAssets"),
        GrowthGoodwillAndIntangibleAssets = row.GetDecimal("growthGoodwillAndIntangibleAssets"),
        GrowthLongTermInvestments = row.GetDecimal("growthLongTermInvestments"),
        GrowthTaxAssets = row.GetDecimal("growthTaxAssets"),
        GrowthOtherNonCurrentAssets = row.GetDecimal("growthOtherNonCurrentAssets"),
        GrowthTotalNonCurrentAssets = row.GetDecimal("growthTotalNonCurrentAssets"),
        GrowthOtherAssets = row.GetDecimal("growthOtherAssets"),
        GrowthTotalAssets = row.GetDecimal("growthTotalAssets"),
        GrowthAccountPayables = row.GetDecimal("growthAccountPayables"),
        GrowthShortTermDebt = row.GetDecimal("growthShortTermDebt"),
        GrowthTaxPayables = row.GetDecimal("growthTaxPayables"),
        GrowthDeferredRevenue = row.GetDecimal("growthDeferredRevenue"),
        GrowthOtherCurrentLiabilities = row.GetDecimal("growthOtherCurrentLiabilities"),
        GrowthTotalCurrentLiabilities = row.GetDecimal("growthTotalCurrentLiabilities"),
        GrowthLongTermDebt = row.GetDecimal("growthLongTermDebt"),
        GrowthDeferredRevenueNonCurrent = row.GetDecimal("growthDeferredRevenueNonCurrent"),
        GrowthDeferredTaxLiabilitiesNonCurrent = row.GetDecimal("growthDeferredTaxLiabilitiesNonCurrent"),
        GrowthOtherNonCurrentLiabilities = row.GetDecimal("growthOtherNonCurrentLiabilities"),
        GrowthTotalNonCurrentLiabilities = row.GetDecimal("growthTotalNonCurrentLiabilities"),
        GrowthOtherLiabilities = row.GetDecimal("growthOtherLiabilities"),
        GrowthTotalLiabilities = row.GetDecimal("growthTotalLiabilities"),
        GrowthPreferredStock = row.GetDecimal("growthPreferredStock"),
        GrowthCommonStock = row.GetDecimal("growthCommonStock"),
        GrowthRetainedEarnings = row.GetDecimal("growthRetainedEarnings"),
        GrowthAccumulatedOtherComprehensiveIncomeLoss = row.GetDecimal("growthAccumulatedOtherComprehensiveIncomeLoss"),
        GrowthOtherTotalStockholdersEquity = row.GetDecimal("growthOthertotalStockholdersEquity"),
        GrowthTotalStockholdersEquity = row.GetDecimal("growthTotalStockholdersEquity"),
        GrowthMinorityInterest = row.GetDecimal("growthMinorityInterest"),
        GrowthTotalEquity = row.GetDecimal("growthTotalEquity"),
        GrowthTotalLiabilitiesAndStockholdersEquity = row.GetDecimal("growthTotalLiabilitiesAndStockholdersEquity"),
        GrowthTotalInvestments = row.GetDecimal("growthTotalInvestments"),
        GrowthTotalDebt = row.GetDecimal("growthTotalDebt"),
        GrowthNetDebt = row.GetDecimal("growthNetDebt"),
        GrowthAccountsReceivables = row.GetDecimal("growthAccountsReceivables"),
        GrowthOtherReceivables = row.GetDecimal("growthOtherReceivables"),
        GrowthPrepaids = row.GetDecimal("growthPrepaids"),
        GrowthTotalPayables = row.GetDecimal("growthTotalPayables"),
        GrowthOtherPayables = row.GetDecimal("growthOtherPayables"),
        GrowthAccruedExpenses = row.GetDecimal("growthAccruedExpenses"),
        GrowthCapitalLeaseObligationsCurrent = row.GetDecimal("growthCapitalLeaseObligationsCurrent"),
        GrowthAdditionalPaidInCapital = row.GetDecimal("growthAdditionalPaidInCapital"),
        GrowthTreasuryStock = row.GetDecimal("growthTreasuryStock"),
    };
}
