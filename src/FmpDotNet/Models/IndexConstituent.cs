using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One current member of an index, from <c>stable/dowjones-constituent</c>,
/// <c>stable/sp500-constituent</c> and <c>stable/nasdaq-constituent</c>.
///
/// <para><b>One record for three paths, and that is what the wire sends.</b> The key tuple was identical on
/// every row of all three responses measured 2026-08-30 — 635 rows in total, 30 Dow Jones, 503 S&amp;P 500 and
/// 102 Nasdaq.</para>
///
/// <para><b>A row count is not a company count.</b> <c>sp500-constituent</c> returned 503 rows over
/// <b>500 distinct CIKs</b> — FOX/FOXA, NWS/NWSA and GOOGL/GOOG — and <c>nasdaq-constituent</c> 102 rows over
/// 101. Every <see cref="Name"/> is distinct as well, so neither <see cref="Name"/> nor <see cref="Symbol"/>
/// identifies a company; <see cref="Cik"/> is the only field that does.</para>
///
/// <para><b>This is the membership as of the call, with no history in it.</b> The change feeds are a
/// different record — see <see cref="IndexConstituentChange"/> — and they cannot be replayed to reconstruct
/// membership at a past date.</para></summary>
public sealed record IndexConstituent
{
    /// <summary>The ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name. Distinct on all 635 rows measured 2026-08-30, which is not the same as
    /// unique per company — see the note on this record about FOX/FOXA.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>FMP's sector label — <c>"Technology"</c>, <c>"Industrials"</c>.
    ///
    /// <para><b>A string, not <see cref="FmpDotNet.Sector"/>, and that is deliberate.</b> All 11 distinct
    /// values measured across 635 rows on 2026-08-30 fall inside that enum and none outside it. The enum
    /// exists to build a <c>sector=</c> <b>query</b> value; nothing measured says what FMP does when it adds a
    /// twelfth sector, and a response-side enum would turn that into a deserialisation failure on a row the
    /// caller could otherwise have read. Every other response record in this SDK binds <c>sector</c> as a
    /// string.</para></summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>FMP's finer classification — <c>"Semiconductors"</c>, <c>"Home Improvement"</c>. Free text:
    /// 114 distinct values over 635 rows, measured 2026-08-30.</summary>
    [JsonPropertyName("subSector")] public string? SubSector { get; init; }

    /// <summary>Where the company is based, as free text — <c>"Mountain View, California"</c>,
    /// <c>"Starbase, TX"</c>. The wire spells this key <c>headQuarter</c>, singular.</summary>
    [JsonPropertyName("headQuarter")] public string? Headquarters { get; init; }

    /// <summary>When the company joined the index.
    ///
    /// <para><b>This one is a real date</b>, unlike <see cref="Founded"/>: ISO on all 628 non-null values
    /// measured 2026-08-30, with no second pattern on any path. It is <see langword="null"/> on <b>7 of 102</b>
    /// Nasdaq rows — ADBE, AMAT, CSCO, FAST, MSFT, PAYX and QCOM — and never null on the other two
    /// paths.</para></summary>
    [JsonPropertyName("dateFirstAdded")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateFirstAdded { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten digits on every row measured 2026-08-30. The
    /// only field on this record that identifies a company.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>When the company was founded — <b>as text, because it is not a date</b>.
    ///
    /// <para><b>The most consequential binding decision on this record, and it is measured rather than
    /// cautious.</b> Across 635 rows on 2026-08-30 the field takes three forms, and which one arrives depends
    /// entirely on the path:</para>
    ///
    /// <list type="bullet">
    ///   <item><description><c>dowjones-constituent</c> — ISO <c>uuuu-MM-dd</c> on
    ///     <b>30 of 30</b> rows.</description></item>
    ///   <item><description><c>nasdaq-constituent</c> — ISO on <b>102 of 102</b> rows.</description></item>
    ///   <item><description><c>sp500-constituent</c> — ISO on 23, a <b>bare year</b> on <b>477 of 503</b>,
    ///     and three values that are neither.</description></item>
    /// </list>
    ///
    /// <para>An implementer who models this from the Dow Jones response types it <see cref="LocalDate"/> and
    /// is correct on 155 of 635 rows. On <c>sp500-constituent</c> that binding drops <b>95.4%</b> of the
    /// values <b>silently</b>, because <see cref="NullableLocalDateJsonConverter"/> answers an unparseable
    /// string with <see langword="null"/> rather than throwing.</para>
    ///
    /// <para>The three remaining values are not malformed dates — they are multi-valued company history.
    /// <c>KLAC</c> sends <c>1975/1977</c>, <c>LOW</c> sends <c>1904/1946/1959</c>, <c>NSC</c> sends
    /// <c>1881/1894</c>. There is nothing in that field for a date pattern to return, on any path, so the SDK
    /// hands the caller what FMP sent and lets them decide.</para></summary>
    [JsonPropertyName("founded")] public string? Founded { get; init; }
}
