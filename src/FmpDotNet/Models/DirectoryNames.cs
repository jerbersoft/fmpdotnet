using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/available-sectors</c>: a sector label wrapped in a single-property object.
///
/// <para>The endpoint answers <c>[{"sector":"Basic Materials"}, …]</c> — 11 rows, one key each, measured against
/// the live API on 2026-08-26. Nothing hangs off the label, so an object per sector is packaging rather than
/// structure. This type exists only to unwrap it once, inside
/// <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync(CancellationToken)"/>, which is why it is
/// <see langword="internal"/>: were it public, every caller would have to reach through <c>.Sector</c> and handle
/// a null that the SDK has already dealt with. The public shape is
/// <see cref="IReadOnlyList{T}"/> of <see cref="string"/>.</para></summary>
internal sealed record SectorName
{
    /// <summary>The sector label. Nullable because the deserialiser cannot promise a key is present, not because
    /// any measured row omitted it — all 11 carried a non-empty value.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }
}

/// <summary>One row of <c>stable/available-industries</c>: an industry label wrapped in a single-property object.
///
/// <para>Same packaging as <see cref="SectorName"/> under a different key — <c>[{"industry":"Steel"}, …]</c>, 159
/// rows measured on 2026-08-26 — and unwrapped in the same place and for the same reason, so it is
/// <see langword="internal"/> too.</para></summary>
internal sealed record IndustryName
{
    /// <summary>The industry label. See <see cref="SectorName.Sector"/> for why it is nullable.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }
}
