using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One match from <c>stable/fundraising-search</c> — three keys and nothing else.
///
/// <para><b>A row is one filing, not one company.</b> Measured 2026-08-31, <c>name=Schutt</c> answered 34
/// rows across <b>5</b> distinct CIKs. Dedupe by <see cref="Cik"/> before populating a picker; this SDK does
/// not, because the row is what the wire sent.</para>
///
/// <para><b>Three keys identical to <see cref="CrowdfundingSearchHit"/>'s, and a separate type on
/// purpose</b>: <see cref="Date"/> is an acceptance <i>timestamp</i> here and a <c>MM-DD-YYYY</c> issuer date
/// there. One record for both would need one converter for two encodings, and both wrong pairings answer
/// null rather than throwing.</para>
///
/// <para><b>This path does behave like a case-insensitive prefix match</b> — measured 2026-08-31,
/// <c>a</c> 0, <c>ab</c> 979, <c>abc</c> 56, <c>Ap</c> 421, <c>App</c> 256, <c>Apple</c>/<c>apple</c>/<c>APPLE</c>
/// 59 each, <c>pple</c> 0 — and the SDK still validates nothing, because that is upstream's rule and it will
/// go stale. Its crowdfunding sibling behaves differently and is documented as unknown.</para></summary>
public sealed record FundraisingSearchHit
{
    /// <summary>The issuer's CIK, zero-padded to ten digits. The key to
    /// <see cref="Endpoints.FundraisersEndpoints.GetFundraisingByCikAsync"/>, and the field to dedupe on.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The matched name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary><b>The filing's acceptance timestamp, not a date</b>, read as Eastern wall clock.
    ///
    /// <para>Measured 2026-08-31 for CIK <c>0001617426</c>: all <b>14</b> values here matched the 14
    /// <see cref="FundraisingNotice.AcceptedDate"/> values from <c>stable/fundraising</c> <i>exactly</i>. The
    /// field is named <c>date</c> and a <c>LocalDate?</c> would silently discard the time of day; the UTC
    /// converter for the same wire shape would move it four to five hours. The full account of the zone
    /// measurement — 3,174 values, both DST seasons, zero in hours 22-05 — is on
    /// <see cref="CrowdfundingOffering.AcceptedDate"/>.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? Date { get; init; }
}
