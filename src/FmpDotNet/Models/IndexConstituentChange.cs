using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One change to an index's membership — an addition <b>or</b> a removal, never both — from the
/// three <c>stable/historical-*-constituent</c> paths.
///
/// <para><b>A row is a change, not a constituent, and this record is named for that.</b> Measured across
/// <b>2,055</b> rows on 2026-08-30 (86 Dow Jones, 1,525 S&amp;P 500, 444 Nasdaq), each row is <i>either</i> an
/// addition — <see cref="AddedSecurity"/> populated, <see cref="RemovedTicker"/> empty — <i>or</i> a removal,
/// with <see cref="Symbol"/> naming whichever it is. A caller reading <see cref="Symbol"/> as "the security
/// that joined" is wrong on every removal row.</para>
///
/// <para><b>This feed cannot answer "who was in the index on date X".</b> Of the 628 current constituents
/// carrying a <c>dateFirstAdded</c>, <b>24 have no addition row at all</b> in the matching feed, so replaying
/// the changes does not reconstruct the membership. That is why the methods are named
/// <c>…ConstituentChangesAsync</c> rather than <c>GetHistorical…ConstituentsAsync</c>, and why this SDK
/// offers no as-of-date membership method — see <c>IndexesEndpoints</c>.</para>
///
/// <para><b>One record serves all three paths.</b> The key tuple was identical on every row of all three
/// responses, measured 2026-08-30. What differs between them is not the shape but how they spell absence —
/// see <see cref="AddedSecurity"/>.</para></summary>
public sealed record IndexConstituentChange
{
    /// <summary>The date the change was announced or recorded, in FMP's US long form on the wire —
    /// <c>"June 29, 2026"</c>.
    ///
    /// <para><b>Not the same value as <see cref="Date"/>, and not derived from it.</b> The two disagree on
    /// <b>205 of 2,055</b> rows measured 2026-08-30 — 202 of them by exactly one day, with <see cref="Date"/>
    /// the earlier — plus three larger outliers. The disagreement is not a legacy artefact: 151 of the 205
    /// come from a single 1957 backfill, but <b>40 fall in 2024–2026 against 47 agreeing rows in the same
    /// span</b>, so in recent data the two differ on 46% of rows.</para>
    ///
    /// <para>Parsed by <see cref="LongFormLocalDateJsonConverter"/>, which is invariant-culture and cannot
    /// round-trip a zero-padded day. Read that converter before changing this attribute.</para></summary>
    [JsonPropertyName("dateAdded")]
    [JsonConverter(typeof(LongFormLocalDateJsonConverter))]
    public LocalDate? DateAdded { get; init; }

    /// <summary>The security that joined the index, by name — <see langword="null"/> on a removal row.
    ///
    /// <para><b>Absence is spelled two ways and which one arrives depends on the path.</b> Measured
    /// 2026-08-30 across the four text fields on this record: <c>historical-dowjones-constituent</c> sent
    /// <b>136 empty strings and zero JSON nulls</b> over all 86 rows; <c>historical-sp500-constituent</c> sent
    /// 823 empty strings <b>and 20 JSON nulls</b>; <c>historical-nasdaq-constituent</c> sent 83 and 8. An
    /// implementer who tests against the Dow Jones path alone never meets the second spelling.
    /// <see cref="SentinelStringJsonConverter"/> folds both to <see langword="null"/> so a caller needs to
    /// know neither.</para></summary>
    [JsonPropertyName("addedSecurity")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? AddedSecurity { get; init; }

    /// <summary>The ticker that left the index — <see langword="null"/> on an addition row. Absence is spelled
    /// two ways; see <see cref="AddedSecurity"/>.</summary>
    [JsonPropertyName("removedTicker")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? RemovedTicker { get; init; }

    /// <summary>The security that left the index, by name — <see langword="null"/> on an addition row.
    /// Absence is spelled two ways; see <see cref="AddedSecurity"/>.</summary>
    [JsonPropertyName("removedSecurity")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? RemovedSecurity { get; init; }

    /// <summary>The effective date of the change, ISO on the wire — <c>"2026-06-29"</c>.
    ///
    /// <para><b>A different field from <see cref="DateAdded"/> and a different wire format.</b> Both are
    /// surfaced because neither can be computed from the other; the measurement is on
    /// <see cref="DateAdded"/>.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The ticker the row is about — the security that joined on an addition row, and the one that
    /// left on a removal row. Never both.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Free text explaining the change — <c>"Market capitalization change"</c> and similar.
    ///
    /// <para><see langword="null"/> where the wire sent a sentinel; the whole 1957 backfill sends
    /// <c>""</c> here. Absence is spelled two ways; see <see cref="AddedSecurity"/>.</para></summary>
    [JsonPropertyName("reason")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Reason { get; init; }
}
