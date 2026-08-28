using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One institution's position in one symbol for one quarter, with FMP's quarter-over-quarter analytics
/// attached — <c>stable/institutional-ownership/extract-analytics/holder</c>.
///
/// <para><b>The same position <see cref="InstitutionalHolding"/> describes, read from the other end.</b> That
/// path answers "what does this filer hold"; this one answers "who holds this symbol", and adds the
/// derived fields FMP computes: weights, changes, ownership percentages, holding periods and performance. Thirty-nine
/// fields, all thirty-nine populated on every row measured 2026-08-28.</para>
///
/// <para><b>Paged, and the cap is 100.</b> See
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize"/> — this is the only path
/// in the group that clamps at 100 rather than 1,000, and it does it silently.</para>
///
/// <para><b>Every money, share and percentage field is <see cref="decimal"/>.</b> All 7,946 rows sampled from
/// this path and <c>extract</c> carried integral money values, so <c>long?</c> is the obvious read and it is
/// the wrong one: <c>industryValue</c> on <c>industry-summary</c> is the same kind of quantity and is
/// fractional on 53 of 394 rows. A fractional value bound to an integer property makes
/// <c>System.Text.Json</c> throw and costs the caller the whole response. Only
/// <see cref="HoldingPeriod"/> is an integer here, because it counts quarters.</para></summary>
public sealed record HolderAnalytics
{
    /// <summary>The quarter end — <c>2026-06-30</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The <b>filer's</b> Central Index Key, zero-padded. Not the issuer's — the issuer is identified
    /// by <see cref="Symbol"/> and <see cref="SecurityCusip"/> on this path.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The date the filer submitted. Bare ISO here.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The institution's name — <c>"BLACKROCK, INC."</c>.</summary>
    [JsonPropertyName("investorName")] public string? InvestorName { get; init; }

    /// <summary>The ticker asked for. Always populated here, unlike
    /// <see cref="InstitutionalHolding.Symbol"/> — this path is keyed by symbol, so a row without one cannot
    /// exist.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name — <c>"APPLE INC"</c>.</summary>
    [JsonPropertyName("securityName")] public string? SecurityName { get; init; }

    /// <summary>The class of security — <c>"COM"</c>.</summary>
    [JsonPropertyName("typeOfSecurity")] public string? TypeOfSecurity { get; init; }

    /// <summary>The security's CUSIP.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>What <see cref="SharesNumber"/> counts — <c>"SH"</c>.</summary>
    [JsonPropertyName("sharesType")] public string? SharesType { get; init; }

    /// <summary>Put, call or underlying — <c>"Share"</c> on every row measured.
    ///
    /// <para><b>Populated here, blank on the sibling path.</b> The identically-named
    /// <see cref="InstitutionalHolding.PutCallShare"/> was <c>""</c> on all 7,346 rows of <c>extract</c>. Same
    /// field name, two different behaviours, measured 2026-08-28.</para></summary>
    [JsonPropertyName("putCallShare")] public string? PutCallShare { get; init; }

    /// <summary>The filer's voting discretion — <c>"SOLE"</c>, <c>"DFND"</c>, <c>"OTR"</c>.</summary>
    [JsonPropertyName("investmentDiscretion")] public string? InvestmentDiscretion { get; init; }

    /// <summary>The issuer's SIC industry label — <c>"ELECTRONIC COMPUTERS"</c>. Upper case, the same
    /// vocabulary <see cref="IndustryClassification.IndustryTitle"/> uses.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The position's share of the filer's whole 13F portfolio, as a percentage.</summary>
    [JsonPropertyName("weight")] public decimal? Weight { get; init; }

    /// <summary>The same weight one quarter earlier.</summary>
    [JsonPropertyName("lastWeight")] public decimal? LastWeight { get; init; }

    /// <summary><see cref="Weight"/> minus <see cref="LastWeight"/>, in percentage points.</summary>
    [JsonPropertyName("changeInWeight")] public decimal? ChangeInWeight { get; init; }

    /// <summary>That change expressed as a percentage of <see cref="LastWeight"/> — a percentage of a
    /// percentage, not a second percentage-point figure.</summary>
    [JsonPropertyName("changeInWeightPercentage")] public decimal? ChangeInWeightPercentage { get; init; }

    /// <summary>The position's value in dollars at the quarter end. <b>336,524,794,350 on the measured
    /// BlackRock row</b> — 157 times <see cref="int"/>'s ceiling.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>The same value one quarter earlier.</summary>
    [JsonPropertyName("lastMarketValue")] public decimal? LastMarketValue { get; init; }

    /// <summary>The dollar change in market value.</summary>
    [JsonPropertyName("changeInMarketValue")] public decimal? ChangeInMarketValue { get; init; }

    /// <summary>That change as a percentage of <see cref="LastMarketValue"/>.</summary>
    [JsonPropertyName("changeInMarketValuePercentage")]
    public decimal? ChangeInMarketValuePercentage { get; init; }

    /// <summary>Shares held at the quarter end. <b>1,162,996,939 on the measured BlackRock row — 54% of
    /// <see cref="int"/>'s ceiling</b>, which is close enough that a reader who checks one row concludes it
    /// fits.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>Shares held one quarter earlier.</summary>
    [JsonPropertyName("lastSharesNumber")] public decimal? LastSharesNumber { get; init; }

    /// <summary>The change in share count. Negative when the filer sold.</summary>
    [JsonPropertyName("changeInSharesNumber")] public decimal? ChangeInSharesNumber { get; init; }

    /// <summary>That change as a percentage of <see cref="LastSharesNumber"/>.</summary>
    [JsonPropertyName("changeInSharesNumberPercentage")]
    public decimal? ChangeInSharesNumberPercentage { get; init; }

    /// <summary>The security's price at the quarter end, in dollars.</summary>
    [JsonPropertyName("quarterEndPrice")] public decimal? QuarterEndPrice { get; init; }

    /// <summary>FMP's estimate of the filer's average cost. Derived, not reported: a 13F carries no cost
    /// basis.</summary>
    [JsonPropertyName("avgPricePaid")] public decimal? AvgPricePaid { get; init; }

    /// <summary>Whether this is the filer's first quarter holding the security.</summary>
    [JsonPropertyName("isNew")] public bool? IsNew { get; init; }

    /// <summary>Whether the filer exited the position this quarter.</summary>
    [JsonPropertyName("isSoldOut")] public bool? IsSoldOut { get; init; }

    /// <summary>The filer's share of the issuer's outstanding stock, as a percentage.</summary>
    [JsonPropertyName("ownership")] public decimal? Ownership { get; init; }

    /// <summary>The same, one quarter earlier.</summary>
    [JsonPropertyName("lastOwnership")] public decimal? LastOwnership { get; init; }

    /// <summary>The change in ownership, in percentage points.</summary>
    [JsonPropertyName("changeInOwnership")] public decimal? ChangeInOwnership { get; init; }

    /// <summary>That change as a percentage of <see cref="LastOwnership"/>.</summary>
    [JsonPropertyName("changeInOwnershipPercentage")] public decimal? ChangeInOwnershipPercentage { get; init; }

    /// <summary>How many consecutive quarters the filer has held the security.
    ///
    /// <para><b>One of the few genuine counts in this record, and therefore <see cref="int"/>.</b> It counts
    /// quarters; 8 and 2 on the two measured rows. Typing it <c>decimal?</c> to match its neighbours would make
    /// the API worse to read for no measured reason.</para></summary>
    [JsonPropertyName("holdingPeriod")] public int? HoldingPeriod { get; init; }

    /// <summary>The quarter end at which the filer first reported the security.</summary>
    [JsonPropertyName("firstAdded")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FirstAdded { get; init; }

    /// <summary>FMP's estimate of the position's dollar gain or loss this quarter. Negative values occur — the
    /// measured BlackRock row's <see cref="LastPerformance"/> is −20,864,809,759.</summary>
    [JsonPropertyName("performance")] public decimal? Performance { get; init; }

    /// <summary>That gain as a percentage.</summary>
    [JsonPropertyName("performancePercentage")] public decimal? PerformancePercentage { get; init; }

    /// <summary>The same figure one quarter earlier. <b><c>0</c> is a measured value, not an absence</b> — it
    /// is what a filer in its first quarter gets, as on the Vanguard row captured 2026-08-28.</summary>
    [JsonPropertyName("lastPerformance")] public decimal? LastPerformance { get; init; }

    /// <summary>The change between the two.</summary>
    [JsonPropertyName("changeInPerformance")] public decimal? ChangeInPerformance { get; init; }

    /// <summary>Whether FMP includes this position in the filer's aggregate performance figures on
    /// <c>HolderPerformance</c>.</summary>
    [JsonPropertyName("isCountedForPerformance")] public bool? IsCountedForPerformance { get; init; }
}
