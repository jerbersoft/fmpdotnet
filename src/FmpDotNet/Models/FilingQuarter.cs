using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One quarter a 13F filer has reported, from <c>stable/institutional-ownership/dates</c>.
///
/// <para><b>The index for the rest of the group.</b> Four of the nine paths on
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints"/> require a <c>year</c> and a <c>quarter</c>, and FMP
/// answers an unfiled pair with an empty array and HTTP 200 rather than an error. This is the only path that
/// says which pairs exist, which is why a caller starts here: read <see cref="Year"/> and <see cref="Quarter"/>
/// off a row and pass them straight back.</para>
///
/// <para>Measured 2026-08-28 for Berkshire Hathaway (CIK <c>0001067983</c>): 53 rows, newest first, every one a
/// calendar quarter end agreeing with its own year and quarter.</para></summary>
public sealed record FilingQuarter
{
    /// <summary>The quarter end the filing covers — <c>2026-06-30</c>, not the date it was filed. Bare ISO on
    /// this path; see <c>InstitutionalFiling.FilingDate</c> for the one path in this group that spells
    /// dates differently.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The calendar year of <see cref="Date"/>. A genuine count of nothing — it is a label — and
    /// <see cref="int"/> rather than <c>decimal</c> for that reason.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter of <see cref="Date"/>, 1 to 4.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }
}
