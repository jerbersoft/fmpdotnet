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

/// <summary>Period-on-period growth rates, expressed as fractions — <c>0.0628</c> is 6.28%. From <c>stable/financial-growth</c>.
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record FinancialGrowth
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

    // ---- Year-on-year growth ----
    [JsonPropertyName("revenueGrowth")] public decimal? RevenueGrowth { get; init; }

    [JsonPropertyName("grossProfitGrowth")] public decimal? GrossProfitGrowth { get; init; }

    [JsonPropertyName("ebitgrowth")] public decimal? EbitGrowth { get; init; }

    [JsonPropertyName("operatingIncomeGrowth")] public decimal? OperatingIncomeGrowth { get; init; }

    [JsonPropertyName("netIncomeGrowth")] public decimal? NetIncomeGrowth { get; init; }

    [JsonPropertyName("epsgrowth")] public decimal? EpsGrowth { get; init; }

    [JsonPropertyName("epsdilutedGrowth")] public decimal? EpsDilutedGrowth { get; init; }

    [JsonPropertyName("weightedAverageSharesGrowth")] public decimal? WeightedAverageSharesGrowth { get; init; }

    [JsonPropertyName("weightedAverageSharesDilutedGrowth")] public decimal? WeightedAverageSharesDilutedGrowth { get; init; }

    [JsonPropertyName("dividendsPerShareGrowth")] public decimal? DividendsPerShareGrowth { get; init; }

    [JsonPropertyName("operatingCashFlowGrowth")] public decimal? OperatingCashFlowGrowth { get; init; }

    [JsonPropertyName("receivablesGrowth")] public decimal? ReceivablesGrowth { get; init; }

    [JsonPropertyName("inventoryGrowth")] public decimal? InventoryGrowth { get; init; }

    [JsonPropertyName("assetGrowth")] public decimal? AssetGrowth { get; init; }

    [JsonPropertyName("bookValueperShareGrowth")] public decimal? BookValuePerShareGrowth { get; init; }

    [JsonPropertyName("debtGrowth")] public decimal? DebtGrowth { get; init; }

    [JsonPropertyName("rdexpenseGrowth")] public decimal? ResearchAndDevelopmentExpenseGrowth { get; init; }

    [JsonPropertyName("sgaexpensesGrowth")] public decimal? SellingGeneralAndAdministrativeExpensesGrowth { get; init; }

    [JsonPropertyName("freeCashFlowGrowth")] public decimal? FreeCashFlowGrowth { get; init; }

    // ---- Multi-year per-share growth ----
    [JsonPropertyName("tenYRevenueGrowthPerShare")] public decimal? TenYRevenueGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYRevenueGrowthPerShare")] public decimal? FiveYRevenueGrowthPerShare { get; init; }

    [JsonPropertyName("threeYRevenueGrowthPerShare")] public decimal? ThreeYRevenueGrowthPerShare { get; init; }

    [JsonPropertyName("tenYOperatingCFGrowthPerShare")] public decimal? TenYOperatingCFGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYOperatingCFGrowthPerShare")] public decimal? FiveYOperatingCFGrowthPerShare { get; init; }

    [JsonPropertyName("threeYOperatingCFGrowthPerShare")] public decimal? ThreeYOperatingCFGrowthPerShare { get; init; }

    [JsonPropertyName("tenYNetIncomeGrowthPerShare")] public decimal? TenYNetIncomeGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYNetIncomeGrowthPerShare")] public decimal? FiveYNetIncomeGrowthPerShare { get; init; }

    [JsonPropertyName("threeYNetIncomeGrowthPerShare")] public decimal? ThreeYNetIncomeGrowthPerShare { get; init; }

    [JsonPropertyName("tenYShareholdersEquityGrowthPerShare")] public decimal? TenYShareholdersEquityGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYShareholdersEquityGrowthPerShare")] public decimal? FiveYShareholdersEquityGrowthPerShare { get; init; }

    [JsonPropertyName("threeYShareholdersEquityGrowthPerShare")] public decimal? ThreeYShareholdersEquityGrowthPerShare { get; init; }

    [JsonPropertyName("tenYDividendperShareGrowthPerShare")] public decimal? TenYDividendPerShareGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYDividendperShareGrowthPerShare")] public decimal? FiveYDividendPerShareGrowthPerShare { get; init; }

    [JsonPropertyName("threeYDividendperShareGrowthPerShare")] public decimal? ThreeYDividendPerShareGrowthPerShare { get; init; }

    [JsonPropertyName("ebitdaGrowth")] public decimal? EbitdaGrowth { get; init; }

    [JsonPropertyName("growthCapitalExpenditure")] public decimal? GrowthCapitalExpenditure { get; init; }

    [JsonPropertyName("tenYBottomLineNetIncomeGrowthPerShare")] public decimal? TenYBottomLineNetIncomeGrowthPerShare { get; init; }

    [JsonPropertyName("fiveYBottomLineNetIncomeGrowthPerShare")] public decimal? FiveYBottomLineNetIncomeGrowthPerShare { get; init; }

    [JsonPropertyName("threeYBottomLineNetIncomeGrowthPerShare")] public decimal? ThreeYBottomLineNetIncomeGrowthPerShare { get; init; }
}
