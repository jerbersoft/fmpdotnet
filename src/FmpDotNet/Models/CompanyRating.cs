using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's own letter rating for one company and the six component scores behind it. Serves both
/// <c>stable/ratings-snapshot</c> and <c>stable/ratings-historical</c>.
///
/// <para><b>The two paths differ by exactly one field.</b> Measured 2026-08-28, the snapshot sends nine —
/// <c>symbol</c>, <c>rating</c>, <c>overallScore</c> and the six components — and the history sends the same
/// nine plus <c>date</c>. So <see cref="Date"/> is nullable and is null on every row the snapshot returns; the
/// same pattern as <see cref="EmployeeCount"/>, where one record serves two paths and the discriminating field
/// carries the difference.</para>
///
/// <para><b>Not the same type as <see cref="BulkCompanyRating"/>, and the difference is one field.</b> That
/// type — built for <c>stable/rating-bulk</c> — carries nine fields and <b>no <c>overallScore</c></b>, which
/// both of these paths send on every row. Reusing it would drop a measured value; widening it would put a
/// permanently-null property on the bulk shape. Two records with nine overlapping fields is the honest
/// outcome.</para>
///
/// <para><b><see cref="Rating"/> is the upstream string and the scale is not the one you would guess.</b>
/// Measured across 45,008 rows on the bulk path: C, B+, C+, B, A-, B-, C-, D+, A, A+, and then <b>S-</b> and
/// <b>S</b> — two grades above A+, which no A-to-F enum would have a member for — while D- and F never appeared
/// at all.</para></summary>
public sealed record CompanyRating
{
    /// <summary>The symbol the rating is for.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The day FMP computed the rating, or <see langword="null"/>.
    ///
    /// <para><b>Always null from <c>ratings-snapshot</c></b>, which sends no date at all — so a null here means
    /// "this came from the snapshot", not "FMP does not know when". The history series is per <i>trading</i>
    /// day: weekends and holidays are absent rather than repeated.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The letter grade. See the type's remarks: the observed scale runs to <c>S</c>, above
    /// <c>A+</c>.</summary>
    [JsonPropertyName("rating")] public string? Rating { get; init; }

    /// <summary>FMP's overall score.
    ///
    /// <para><b>Every row measured was 3</b> — all five rows of the captured <c>ratings-historical</c> history
    /// and the single row of the captured <c>ratings-snapshot</c>, measured 2026-08-28. That is not evidence
    /// about the scale's bounds: six rows all landing on the same value says nothing about where the top or
    /// bottom sits, and no wider range was measured.</para>
    ///
    /// <para><b>The one field <see cref="BulkCompanyRating"/> does not carry</b>, and therefore the reason these
    /// are two records rather than one.</para></summary>
    [JsonPropertyName("overallScore")] public int? OverallScore { get; init; }

    /// <summary>Score for the discounted-cash-flow factor.</summary>
    [JsonPropertyName("discountedCashFlowScore")] public int? DiscountedCashFlowScore { get; init; }

    /// <summary>Score for the return-on-equity factor.</summary>
    [JsonPropertyName("returnOnEquityScore")] public int? ReturnOnEquityScore { get; init; }

    /// <summary>Score for the return-on-assets factor.</summary>
    [JsonPropertyName("returnOnAssetsScore")] public int? ReturnOnAssetsScore { get; init; }

    /// <summary>Score for the debt-to-equity factor.</summary>
    [JsonPropertyName("debtToEquityScore")] public int? DebtToEquityScore { get; init; }

    /// <summary>Score for the price-to-earnings factor.</summary>
    [JsonPropertyName("priceToEarningsScore")] public int? PriceToEarningsScore { get; init; }

    /// <summary>Score for the price-to-book factor.</summary>
    [JsonPropertyName("priceToBookScore")] public int? PriceToBookScore { get; init; }
}
