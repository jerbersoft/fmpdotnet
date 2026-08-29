using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One industry's total reported 13F value for one quarter, across every filer, from
/// <c>stable/institutional-ownership/industry-summary</c>.
///
/// <para>Three fields and 394 rows per quarter — one per SIC industry, in the same vocabulary
/// <see cref="HolderIndustryBreakdown.IndustryTitle"/> uses.</para>
///
/// <para><b>This is the record that decided the numeric typing for the whole group.</b> See
/// <see cref="IndustryValue"/>.</para></summary>
public sealed record IndustryOwnershipSummary
{
    /// <summary>The SIC industry label — <c>"BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)"</c>.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>Total dollars 13F filers reported in the industry that quarter.
    ///
    /// <para><b>Fractional on 53 of 394 rows measured 2026-08-28</b> — <c>523604028974.8208</c> among them —
    /// while every money field on every other path in this group was integral across 7,946 rows. That
    /// asymmetry is why <i>every</i> money and share field in this slice is <see cref="decimal"/> rather than
    /// <c>long</c>: the family clearly goes fractional, and which member does it in which quarter is not
    /// stable.</para>
    ///
    /// <para><b>The cost of getting it wrong is the whole response.</b> <c>System.Text.Json</c> throws on a
    /// fractional value bound to an integer property, and <c>FmpTransport</c> does not wrap
    /// <c>DeserializeAsync</c> — so a single such value would cost the caller all 394 rows rather than the one
    /// field. See <see cref="CompanyProfile.Volume"/>.</para></summary>
    [JsonPropertyName("industryValue")] public decimal? IndustryValue { get; init; }

    /// <summary>The quarter end — <c>2025-12-31</c>. Bare ISO on this path.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }
}

/// <summary>One 13F filing as it arrives, from <c>stable/institutional-ownership/latest</c> — the whole-market
/// feed of new and amended 13F submissions, newest first.
///
/// <para><b>The two dates on this record use two different converters, and no other record in this group
/// does.</b> Measured 2026-08-28 over 1,000 rows: <see cref="FilingDate"/>'s time component was
/// <c>00:00:00</c> on 1,000 of 1,000 — a date wearing a datetime's clothes — while
/// <see cref="AcceptedDate"/> was at exactly midnight on 0 of 1,000 and is a real clock. Reading either with
/// the other's converter compiles and binds; reading either with the bare-ISO
/// <see cref="NullableLocalDateJsonConverter"/> that the rest of this group uses returns <see langword="null"/>
/// on every row without throwing.</para>
///
/// <para><b><see cref="FormType"/> here is 13F vocabulary, not Form 4 vocabulary.</b> See its
/// documentation.</para></summary>
public sealed record InstitutionalFiling
{
    /// <summary>The filer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name — <c>"CORNERSTONE FINANCIAL MANAGEMENT LLC"</c>. Spelled <c>name</c> on this
    /// path, not <c>investorName</c> as on <see cref="HolderPerformance.InvestorName"/> and
    /// <see cref="HolderAnalytics.InvestorName"/>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The quarter end the filing reports on. Bare ISO — this is the one date on the record that is
    /// spelled the way the rest of the group spells its dates.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date the filing was submitted.
    ///
    /// <para><b>A date, not a timestamp.</b> The wire sends <c>"2026-08-28 00:00:00"</c> and the time was
    /// <c>00:00:00</c> on 1,000 of 1,000 rows measured 2026-08-28, so it is discarded — see
    /// <see cref="NullableDateAtMidnightJsonConverter"/>. All three rows of the captured page share this field
    /// to the second while their <see cref="AcceptedDate"/> values differ by minutes, which is what the
    /// midnight actually means: it carries no time.</para></summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The moment EDGAR accepted the submission — <c>"2026-08-28 15:47:03"</c>.
    ///
    /// <para><b>A <see cref="LocalDateTime"/>, deliberately, rather than an <see cref="Instant"/>.</b>
    /// <see cref="SecFiling.AcceptedDate"/> is an <c>Instant</c> because a DST measurement established EDGAR's
    /// wall clock as US Eastern on that path. No such measurement was taken here, and inventing a zone would
    /// invent a fact: this SDK reports the wall clock FMP sent and leaves the zone to a caller who knows
    /// it.</para>
    ///
    /// <para>The time is real information: the three captured rows are 16 and 11 minutes apart on the same
    /// filing date, and it is the only field that orders them.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateTimeJsonConverter))]
    public LocalDateTime? AcceptedDate { get; init; }

    /// <summary>The 13F form type — <c>"13F-HR"</c>, <c>"13F-HR/A"</c>, <c>"13F-NT"</c>, <c>"13F-NT/A"</c>.
    ///
    /// <para><b>Not the same vocabulary as <see cref="InsiderTrade.FormType"/></b>, which carries <c>"3"</c>,
    /// <c>"4"</c> and <c>"4/A"</c>. Two field names spelled alike over two disjoint sets of values, which is
    /// why the two records are not unified: doing so would model a coincidence.</para>
    ///
    /// <para>A raw <see cref="string"/> rather than an enum, for the reason on
    /// <see cref="SecFiling.FormType"/>.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The EDGAR filing-index page for the accession.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The primary document itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
