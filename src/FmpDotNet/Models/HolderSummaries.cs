using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>How one 13F filer's portfolio was spread across industries in one quarter, from
/// <c>stable/institutional-ownership/holder-industry-breakdown</c>.
///
/// <para>One row per industry the filer held, sorted by weight. Berkshire's 2026 Q2 answered 24 rows,
/// measured 2026-08-28, all twelve fields populated on every one.</para>
///
/// <para><b><see cref="Performance"/> and <see cref="PerformancePercentage"/> can disagree in sign, and that is
/// FMP's answer rather than a fault.</b> See <see cref="PerformancePercentage"/>.</para></summary>
public sealed record HolderIndustryBreakdown
{
    /// <summary>The quarter end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name — <c>"BERKSHIRE HATHAWAY INC"</c>.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>The SIC industry label — <c>"ELECTRONIC COMPUTERS"</c>. The same vocabulary
    /// <see cref="HolderAnalytics.IndustryTitle"/> and <see cref="IndustryOwnershipSummary.IndustryTitle"/>
    /// use.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The industry's share of the filer's portfolio, as a percentage.</summary>
    [JsonPropertyName("weight")] public decimal? Weight { get; init; }

    /// <summary>The same weight one quarter earlier.</summary>
    [JsonPropertyName("lastWeight")] public decimal? LastWeight { get; init; }

    /// <summary>The change in weight, in percentage points.</summary>
    [JsonPropertyName("changeInWeight")] public decimal? ChangeInWeight { get; init; }

    /// <summary>That change as a percentage of <see cref="LastWeight"/>.</summary>
    [JsonPropertyName("changeInWeightPercentage")] public decimal? ChangeInWeightPercentage { get; init; }

    /// <summary>The industry slice's dollar gain or loss this quarter.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>The same gain as a percentage — <b>and it can contradict <see cref="Performance"/>'s
    /// sign.</b>
    ///
    /// <para>Measured 2026-08-28: all three of the captured Berkshire rows carry a positive
    /// <see cref="Performance"/> beside a negative percentage, the largest being <c>8,107,036,430</c> against
    /// <c>−296.8456</c>. FMP computes the percentage against a base this endpoint does not publish, so the two
    /// cannot be reconciled from the response. Both are reported exactly as sent; neither is derived here, and
    /// a consumer that assumes they agree in sign is wrong on every row measured.</para></summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The same dollar figure one quarter earlier.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }
}

/// <summary>One quarter of one 13F filer's aggregate portfolio performance, from
/// <c>stable/institutional-ownership/holder-performance-summary</c>.
///
/// <para><b>The filer's whole history, one row per quarter, newest first</b> — 53 rows for Berkshire, measured
/// 2026-08-28, matching the 53 quarters <c>institutional-ownership/dates</c> enumerates. The endpoint takes no
/// year and no quarter, which is why
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetHolderPerformanceAsync"/> takes only a CIK.</para>
///
/// <para><b>The series is self-consistent across rows:</b> each row's <see cref="LastPerformance"/> equals the
/// next row's <see cref="Performance"/>, verified on the captured pair.</para>
///
/// <para><b>Three fields here are genuine counts and are <see cref="int"/>:</b>
/// <see cref="PortfolioSize"/>, <see cref="SecuritiesAdded"/> and <see cref="SecuritiesRemoved"/>. The three
/// average holding periods are means rather than counts but are <see cref="int"/> as well — see
/// <see cref="AverageHoldingPeriod"/> for the measurement behind that. Everything else is money or a percentage
/// and is <see cref="decimal"/> — see <see cref="HolderAnalytics"/> for why.</para></summary>
public sealed record HolderPerformance
{
    /// <summary>The quarter end this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>How many distinct securities the filer reported. A count, hence <see cref="int"/>.</summary>
    [JsonPropertyName("portfolioSize")] public int? PortfolioSize { get; init; }

    /// <summary>How many securities were new this quarter. A count.</summary>
    [JsonPropertyName("securitiesAdded")] public int? SecuritiesAdded { get; init; }

    /// <summary>How many were exited this quarter. A count.</summary>
    [JsonPropertyName("securitiesRemoved")] public int? SecuritiesRemoved { get; init; }

    /// <summary>The portfolio's total reported value in dollars.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>The same, one quarter earlier. <b>Spelled <c>previousMarketValue</c> on the wire</b>, not
    /// <c>lastMarketValue</c> — the only place in this group where FMP uses "previous" rather than
    /// "last".</summary>
    [JsonPropertyName("previousMarketValue")] public decimal? PreviousMarketValue { get; init; }

    /// <summary>The dollar change in portfolio value.</summary>
    [JsonPropertyName("changeInMarketValue")] public decimal? ChangeInMarketValue { get; init; }

    /// <summary>That change as a percentage.</summary>
    [JsonPropertyName("changeInMarketValuePercentage")]
    public decimal? ChangeInMarketValuePercentage { get; init; }

    /// <summary>The mean number of quarters the filer has held its positions.
    ///
    /// <para><b>A mean, not a count — <see cref="int"/> anyway.</b> Measured 2026-08-29 across 391 rows from
    /// five large filers (Berkshire Hathaway 53, BlackRock 71, Vanguard 104, State Street 53, FMR/Fidelity 110):
    /// across this field and its two siblings below, 0 of 1,173 values were fractional. FMP rounds them. These
    /// three are the only means on this record's <see cref="int"/> list, so if FMP ever stops rounding one, that
    /// single value costs the caller the whole response.</para></summary>
    [JsonPropertyName("averageHoldingPeriod")] public int? AverageHoldingPeriod { get; init; }

    /// <summary>The same, over the ten largest positions. See <see cref="AverageHoldingPeriod"/> for the
    /// measurement behind the <see cref="int"/> typing.</summary>
    [JsonPropertyName("averageHoldingPeriodTop10")] public int? AverageHoldingPeriodTop10 { get; init; }

    /// <summary>The same, over the twenty largest. See <see cref="AverageHoldingPeriod"/> for the measurement
    /// behind the <see cref="int"/> typing.</summary>
    [JsonPropertyName("averageHoldingPeriodTop20")] public int? AverageHoldingPeriodTop20 { get; init; }

    /// <summary>Portfolio turnover for the quarter, as a fraction.</summary>
    [JsonPropertyName("turnover")] public decimal? Turnover { get; init; }

    /// <summary>FMP's alternative turnover measure computed from sales.</summary>
    [JsonPropertyName("turnoverAlternateSell")] public decimal? TurnoverAlternateSell { get; init; }

    /// <summary>The same computed from purchases.</summary>
    [JsonPropertyName("turnoverAlternateBuy")] public decimal? TurnoverAlternateBuy { get; init; }

    /// <summary>The portfolio's dollar gain or loss this quarter. Negative quarters occur.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>That gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The previous quarter's <see cref="Performance"/>. Equal to the next row's
    /// <see cref="Performance"/> — the rows chain.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }

    /// <summary>Trailing one-year dollar gain.</summary>
    [JsonPropertyName("performance1year")] public decimal? Performance1Year { get; init; }

    /// <summary>Trailing one-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage1year")] public decimal? PerformancePercentage1Year { get; init; }

    /// <summary>Trailing three-year dollar gain.</summary>
    [JsonPropertyName("performance3year")] public decimal? Performance3Year { get; init; }

    /// <summary>Trailing three-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage3year")] public decimal? PerformancePercentage3Year { get; init; }

    /// <summary>Trailing five-year dollar gain.</summary>
    [JsonPropertyName("performance5year")] public decimal? Performance5Year { get; init; }

    /// <summary>Trailing five-year gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage5year")] public decimal? PerformancePercentage5Year { get; init; }

    /// <summary>Dollar gain since the filer's first reported quarter.</summary>
    [JsonPropertyName("performanceSinceInception")] public decimal? PerformanceSinceInception { get; init; }

    /// <summary>The same as a percentage.</summary>
    [JsonPropertyName("performanceSinceInceptionPercentage")]
    public decimal? PerformanceSinceInceptionPercentage { get; init; }

    /// <summary>This quarter's percentage gain less the S&amp;P 500's. Negative means the filer
    /// trailed.</summary>
    [JsonPropertyName("performanceRelativeToSP500Percentage")]
    public decimal? PerformanceRelativeToSP500Percentage { get; init; }

    /// <summary>The same over one year.</summary>
    [JsonPropertyName("performance1yearRelativeToSP500Percentage")]
    public decimal? Performance1YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same over three years.</summary>
    [JsonPropertyName("performance3yearRelativeToSP500Percentage")]
    public decimal? Performance3YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same over five years.</summary>
    [JsonPropertyName("performance5yearRelativeToSP500Percentage")]
    public decimal? Performance5YearRelativeToSP500Percentage { get; init; }

    /// <summary>The same since inception.</summary>
    [JsonPropertyName("performanceSinceInceptionRelativeToSP500Percentage")]
    public decimal? PerformanceSinceInceptionRelativeToSP500Percentage { get; init; }
}
