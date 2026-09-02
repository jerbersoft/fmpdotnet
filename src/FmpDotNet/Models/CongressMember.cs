using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One term a member of Congress served, from
/// <c>stable/senate-positions</c>.
///
/// <para>One row per Congress the member sat in. The path serves the House as well as the Senate despite its
/// name — measured 2026-08-29, <c>Representative</c> and <c>Senator</c> both appear in
/// <see cref="Position"/>.</para>
///
/// <para><b>Paged 300 at a time, and <c>limit</c> is ignored.</b> Measured 2026-08-29, <c>limit=500</c>
/// answered 300; page 1 answered a further 300 with no overlap, so the universe is at least 600 and was not
/// enumerated.</para></summary>
public sealed record CongressMemberPosition
{
    /// <summary>The member's Bioguide identifier. FMP's spelling; see
    /// <see cref="CongressionalTrade.SenateId"/>.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Which Congress — 118, 119. A count of Congresses and whole by its own nature, hence
    /// <see cref="int"/> where the tenure beside it is <see cref="decimal"/>.</summary>
    [JsonPropertyName("congressNumber")] public int? CongressNumber { get; init; }

    /// <summary>The day the term began.</summary>
    [JsonPropertyName("startDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? StartDate { get; init; }

    /// <summary>The day the term ended, or <see langword="null"/> for a term still running — 22 of 300 rows
    /// measured 2026-08-29.</summary>
    [JsonPropertyName("endDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? EndDate { get; init; }

    /// <summary>The member's party for this term — <c>Democrat</c> or <c>Republican</c> on every row
    /// measured. A string rather than an enum; see <see cref="CongressionalTrade"/>.</summary>
    [JsonPropertyName("party")] public string? Party { get; init; }

    /// <summary>The seat — <c>Representative</c> or <c>Senator</c>.</summary>
    [JsonPropertyName("position")] public string? Position { get; init; }

    /// <summary>The two-letter state.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>Years served in this term so far.
    ///
    /// <para><b><see cref="decimal"/>, and the measurement is the reason.</b> Measured 2026-08-29 across 300
    /// rows, 266 values arrived as bare JSON integers and <b>34 carried a decimal point</b> — 0.7, 0.2. A
    /// smaller sample sees only the 266 and types this <see cref="int"/>, and <see cref="int"/> rejects
    /// <c>0.7</c> by throwing out of the entire 300-row response rather than the one field. See
    /// <c>CONTRIBUTING.md</c>'s typing rule, which this field is the reason for.</para></summary>
    [JsonPropertyName("yearsInTerm")] public decimal? YearsInTerm { get; init; }
}

/// <summary>One member of Congress, from <c>stable/senate-profile</c>.
///
/// <para><b>The one path in this group whose universe was enumerated to exhaustion:</b> measured 2026-08-29,
/// page 0 answered 500, page 1 answered 35 and page 2 answered none — <b>535 members</b>. <c>limit</c> is
/// ignored.</para>
///
/// <para><b>535 is the active half, and <c>limit</c> is honoured (#52).</b> Measured 2026-09-02, the bare answer
/// is byte-identical to <c>active=true</c>; <c>active=false</c> answers a further 720 over two pages, 1,255 in
/// all, and <c>limit=5</c> answers 5. Both are reached through <see cref="CongressProfileCriteria"/>.</para>
///
/// <para>Serves the House as well as the Senate, like <see cref="CongressMemberPosition"/>. Measured
/// 2026-08-29 <see cref="LatestPosition"/> also carries <c>Vice President</c>.</para></summary>
public sealed record CongressMemberProfile
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Given name.</summary>
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }

    /// <summary>Surname.</summary>
    [JsonPropertyName("lastName")] public string? LastName { get; init; }

    /// <summary>Date of birth. Measured 2026-08-29 across 500 rows, these run from 1932-12-31 to
    /// 1997-01-16.</summary>
    [JsonPropertyName("birthDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? BirthDate { get; init; }

    /// <summary>Most recent party — <c>Democrat</c>, <c>Republican</c> or <c>Independent</c>.</summary>
    [JsonPropertyName("latestParty")] public string? LatestParty { get; init; }

    /// <summary>Most recent state.</summary>
    [JsonPropertyName("latestState")] public string? LatestState { get; init; }

    /// <summary>Most recent seat — <c>Representative</c>, <c>Senator</c> or <c>Vice President</c>.</summary>
    [JsonPropertyName("latestPosition")] public string? LatestPosition { get; init; }

    /// <summary>FMP's headshot URL.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>Whether the member currently serves. Measured 2026-09-02 (#52) this is <see langword="true"/> on
    /// every row of a bare request, because the bare request <i>is</i> <c>active=true</c>; the 720 rows where it
    /// is <see langword="false"/> come only from <see cref="CongressProfileCriteria.Active"/> =
    /// <see langword="false"/>.
    ///
    /// <para><b>A genuine JSON boolean</b>, unlike
    /// <see cref="CongressionalTrade.CapitalGainsOver200Usd"/> which is the string <c>"False"</c>. The two are
    /// deliberately not modelled alike; see that property.</para></summary>
    [JsonPropertyName("active")] public bool? Active { get; init; }

    /// <summary>Total years served.
    ///
    /// <para><b><see cref="decimal"/> for the reason <see cref="CongressMemberPosition.YearsInTerm"/> is</b>,
    /// and more emphatically: measured 2026-08-29 across 500 rows, <b>493 carried a decimal
    /// point</b>.</para></summary>
    [JsonPropertyName("yearsActive")] public decimal? YearsActive { get; init; }
}
