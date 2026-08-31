using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/crowdfunding-offerings-search</c> — three keys and nothing else.
///
/// <para><b>A row is one filing, not one company.</b> Measured 2026-08-31,
/// <c>name=Well</c> answered <b>44 rows across 31 distinct CIKs</b>. A caller populating a company picker
/// must dedupe by <see cref="Cik"/>; this SDK does not, because the row is what the wire sent.</para>
///
/// <para><b>Identical in shape to <see cref="FundraisingSearchHit"/> and deliberately a separate type</b>,
/// because <see cref="Date"/> is a different <i>kind</i> of value on each: <c>MM-DD-YYYY</c> here, and an
/// acceptance timestamp there. One record for both would need one converter for two encodings, and the wrong
/// one reads as null without throwing.</para>
///
/// <para><b>The matching rule is not known, and this SDK does not claim one.</b> Measured 2026-08-31:
/// <c>Well</c> and <c>Wellness</c> return byte-identical 44-row bodies while <c>Welln</c> and <c>Wellnes</c>
/// return <b>zero</b>; <c>Or</c>, <c>Ora</c> and <c>Orav</c> return zero while <c>Oravanti</c> returns one.
/// Substring, prefix and whole-word are each refuted by one of those rows. FMP's documentation describes the
/// endpoint as searching "by company name, campaign name, or platform" — <b>the platform clause is refuted by
/// measurement</b>: <c>name=NetCapital</c> returns 0 rows, though "NetCapital Funding Portal Inc." is the
/// intermediary in FMP's own documented sample. An intermediate-length query returning nothing is not an
/// error and not proof of absence.</para></summary>
public sealed record CrowdfundingSearchHit
{
    /// <summary>The issuer's CIK, zero-padded to ten digits. The key to
    /// <see cref="Endpoints.FundraisersEndpoints.GetCrowdfundingOfferingsByCikAsync"/>, and the field to dedupe on.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The matched name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The issuer's date as this corpus records it — <b><c>MM-DD-YYYY</c></b>, the same encoding
    /// and the same meaning as <see cref="CrowdfundingOffering.Date"/>, which is <i>not</i> a filing date.
    /// See <see cref="NullableMonthDayYearDateJsonConverter"/> for why the SDK's ISO converter would read
    /// every one of these as null without throwing.
    ///
    /// <para><b>Null on 461 of 7,003 rows measured 2026-08-31</b> — 6.6% — and FMP's own documented sample
    /// response shows one. A hit without a date is a normal hit.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? Date { get; init; }
}
