using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One SEC-registered fund share class, from <c>stable/funds/disclosure-holders-search</c>.
///
/// <para><b>These rows are not holders, despite the path's name.</b> Nothing in a row says who holds what:
/// they are registrant, series and class identifiers plus a filer address. The SDK's method is named for what
/// it returns — <see cref="Endpoints.EtfAndFundsEndpoints.SearchFundsByNameAsync"/> — and this doc carries
/// the wire path, the same trade <see cref="MarketMover.ChangePercentage"/> makes for a property name.</para>
///
/// <para><b>Matching is case-insensitive, whole-word and single-word.</b> Measured 2026-08-30:
/// <c>Vanguard</c>, <c>vanguard</c> and <c>VANGUARD</c> each returned the same 548 rows; <c>Vangua</c>
/// returned <b>0</b>; <c>van</c> returned 201 (<c>VAN KAMPEN…</c>); <c>Fid</c> and <c>fidelit</c> returned 0;
/// and <c>Vanguard Group</c> — a two-word company name, the most likely thing a caller types — returned
/// <b>0</b>. The exact tokenisation was not established and this SDK does not assert one.</para>
///
/// <para><b>The single largest response in the group comes from this path.</b> <c>name=Trust</c> returned
/// <b>66,065 rows and 27.4 MB</b> measured 2026-08-30, and there is no pagination anywhere in this group —
/// <c>limit</c> and <c>page</c> were ignored. There is no way to ask for less.</para>
///
/// <para><b>More than a quarter of rows are missing their address block</b>, spelled two ways at once. See
/// <see cref="Address"/>.</para></summary>
public sealed record FundShareClass
{
    /// <summary>The share class's ticker, or <see langword="null"/>. The literal string <c>"NULL"</c> on 1,622
    /// of 5,869 rows measured 2026-08-30 (27.6%), mapped by <see cref="SentinelStringJsonConverter"/>. Many
    /// share classes are not exchange-traded and have none.</summary>
    [JsonPropertyName("symbol")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Symbol { get; init; }

    /// <summary>The registrant's SEC Central Index Key, zero-padded to ten characters. Never measured
    /// carrying a sentinel, so no converter.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The SEC class identifier — <c>"C000003891"</c>. Never measured carrying a sentinel.</summary>
    [JsonPropertyName("classId")] public string? ClassId { get; init; }

    /// <summary>The SEC series identifier — <c>"S000001469"</c>. A series may have several classes, so this is
    /// the field that groups them. Never measured carrying a sentinel.</summary>
    [JsonPropertyName("seriesId")] public string? SeriesId { get; init; }

    /// <summary>The registrant's name — this is the field <c>name</c> matches against. Never measured
    /// carrying a sentinel.</summary>
    [JsonPropertyName("entityName")] public string? EntityName { get; init; }

    /// <summary>The SEC entity organisation type, or <see langword="null"/>.
    ///
    /// <para><b>A numeric string with a non-numeric sentinel in the same field.</b> Measured 2026-08-30:
    /// <c>"30"</c> ×3,635, <c>"32"</c> ×17, <c>"33"</c> ×5 — and the literal <c>"NULL"</c> ×1,540. A caller
    /// reaching for <c>int.Parse</c> gets an outright failure on more than a quarter of rows, which is why the
    /// sentinel is converted here.</para>
    ///
    /// <para>It stays a <see cref="string"/> because it is a <b>code, not a quantity</b>: nothing a caller
    /// does with an organisation type is arithmetic, and parsing it would invent a numeric identity the source
    /// does not have.</para></summary>
    [JsonPropertyName("entityOrgType")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? EntityOrgType { get; init; }

    /// <summary>The fund series' name. Never measured carrying a sentinel — unlike
    /// <see cref="ClassName"/>, which is often the same text.</summary>
    [JsonPropertyName("seriesName")] public string? SeriesName { get; init; }

    /// <summary>The share class's name — <c>"Investor B"</c>, <c>"BATS SERIES C"</c> — or
    /// <see langword="null"/>.
    ///
    /// <para><b>The one field measured carrying two different string sentinels.</b> On the widest query taken
    /// 2026-08-30 (<c>name=Trust</c>, 66,065 rows) it was <c>"NULL"</c> ×1,278 <b>and</b> <c>"N/A"</c> ×192.
    /// A caller checking for one spelling would miss the other; <see cref="SentinelStringJsonConverter"/>
    /// maps both.</para></summary>
    [JsonPropertyName("className")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ClassName { get; init; }

    /// <summary>The registrant's SEC file number — <c>"811-21457"</c> — or <see langword="null"/>.
    /// <c>"NULL"</c> on 1,540 of 5,869 rows measured 2026-08-30, the same rows on which
    /// <see cref="Address"/> is absent.</summary>
    [JsonPropertyName("reportingFileNumber")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ReportingFileNumber { get; init; }

    /// <summary>The filer's street address, or <see langword="null"/>.
    ///
    /// <para><b>Absent on more than a quarter of rows, in two encodings.</b> Measured 2026-08-30 it was a real
    /// JSON <see langword="null"/> on 1,540 of 5,869 rows (26.2%) and <c>""</c> on 8 more — which is why it
    /// carries <see cref="SentinelStringJsonConverter"/> even though its headline absence is a genuine
    /// null.</para>
    ///
    /// <para><b>The whole address block travels together.</b> <see cref="EntityOrgType"/>,
    /// <see cref="ReportingFileNumber"/>, <see cref="City"/>, <see cref="ZipCode"/> and <see cref="State"/>
    /// were the literal string <c>"NULL"</c> on exactly the same 1,540 rows — one missing block, encoded two
    /// different ways inside one JSON object.</para></summary>
    [JsonPropertyName("address")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Address { get; init; }

    /// <summary>The filer's city, or <see langword="null"/>. <c>"NULL"</c> on 1,540 of 5,869 rows measured
    /// 2026-08-30; see <see cref="Address"/>.</summary>
    [JsonPropertyName("city")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? City { get; init; }

    /// <summary>The filer's postal code, or <see langword="null"/>. A <see cref="string"/> because leading
    /// zeros are part of a ZIP code. <c>"NULL"</c> on 1,540 of 5,869 rows measured 2026-08-30.</summary>
    [JsonPropertyName("zipCode")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? ZipCode { get; init; }

    /// <summary>The filer's state, as a two-letter code, or <see langword="null"/>. <c>"NULL"</c> on 1,540 of
    /// 5,869 rows measured 2026-08-30 — the case that makes this converter's cost worth paying: without it a
    /// caller writing <c>row.State ?? "unknown"</c> gets the string <c>"NULL"</c> on a quarter of rows and no
    /// warning.</summary>
    [JsonPropertyName("state")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? State { get; init; }
}
