using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// The four records below differ from one another by exactly one word each, and they live in one file for that
// reason. Split across four files they drift; together, a reader editing any one of them sees all four.
//
// They cover eight paths: sector or industry, performance or PE, snapshot or historical. Measured 2026-08-29,
// those eight carry exactly four distinct key tuples, and `snapshot` and `historical` return the SAME rows
// selected differently rather than different rows — which is why there are four types here and not eight.

/// <summary>One sector's average price change on one day and one exchange. From
/// <c>stable/sector-performance-snapshot</c> and <c>stable/historical-sector-performance</c>.
///
/// <para><b>The exchange is part of the fact, not a filter on it.</b> Measured 2026-08-29, Technology on
/// 2026-08-28 read <c>-0.6192</c> on NASDAQ and <c>-1.7398</c> on NYSE, and across 20 shared dates in one
/// window not a single value matched. A row is meaningless without its <see cref="Exchange"/>.</para>
///
/// <para><b><see cref="Date"/> is not necessarily the date you asked for.</b> See the snapshot methods on
/// <c>MarketPerformanceEndpoints</c> for the measurement — a snapshot for a date past the end of the data
/// returns rows bearing three different dates.</para></summary>
public sealed record SectorPerformance
{
    /// <summary>The trading day the row describes. Nullable because the deserialiser cannot promise a key is
    /// present, not because any measured row omitted it — no null appeared in any field across 9,855 rows
    /// measured 2026-08-29.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own sector label — <c>Basic Materials</c>, <c>Technology</c>. A
    /// <see langword="string"/> and not <see cref="FmpDotNet.Sector"/>: binding the label onto the enum would
    /// need a converter, and an unrecognised label would then throw where it currently binds. The enum is an
    /// argument type.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange this average was taken over. See the type summary — this is part of the fact.
    /// Never <c>ALL</c> or an aggregate; no market-wide value appeared among those measured.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The mean price change across the sector's constituents on that exchange, as a percentage.
    ///
    /// <para><b><see cref="decimal"/> rather than <see cref="double"/>, and that is load-bearing.</b> The value
    /// arrives as an unrounded float64 expansion: measured 2026-08-29 the longest plain fractional part was 22
    /// digits and the greatest number of significant digits was 17. Values below <c>1e-6</c> in magnitude
    /// arrive in <b>exponent form</b> — ten of them in the measured corpus, all in deep history — which
    /// <c>System.Text.Json</c> binds to <see cref="decimal"/> unaided; verified 2026-08-29 on .NET 10 with this
    /// SDK's own source-generation options. Range measured across 9,016 values: <c>-74.8932</c> to
    /// <c>+73.6983</c>.</para></summary>
    [JsonPropertyName("averageChange")] public decimal? AverageChange { get; init; }
}

/// <summary>One industry's average price change on one day and one exchange. From
/// <c>stable/industry-performance-snapshot</c> and <c>stable/historical-industry-performance</c>.
///
/// <para>The same shape as <see cref="SectorPerformance"/> under a different key. Everything on that type
/// applies here — the exchange is part of the fact, and <see cref="Date"/> is not necessarily the date you
/// asked for.</para>
///
/// <para><b>The industry vocabulary is wider than these paths answer for.</b>
/// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/> returned 159 names on 2026-08-29 and only 139
/// appear in any snapshot on either NASDAQ or NYSE; the other 20 answer <c>[]</c> everywhere.</para></summary>
public sealed record IndustryPerformance
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own industry label — <c>Advertising Agencies</c>, <c>Oil &amp; Gas Midstream</c>. Labels
    /// carrying <c>&amp;</c> and <c>,</c> were measured to work when URL-encoded on the way out.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The exchange this average was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The mean price change across the industry's constituents on that exchange, as a percentage. See
    /// <see cref="SectorPerformance.AverageChange"/> for why this is <see cref="decimal"/>.</summary>
    [JsonPropertyName("averageChange")] public decimal? AverageChange { get; init; }
}

/// <summary>One sector's aggregate price-to-earnings ratio on one day and one exchange. From
/// <c>stable/sector-pe-snapshot</c> and <c>stable/historical-sector-pe</c>.
///
/// <para><see cref="SectorPerformance"/> with <see cref="Pe"/> in place of
/// <see cref="SectorPerformance.AverageChange"/>; everything documented there applies here too.</para></summary>
public sealed record SectorPe
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own sector label. See <see cref="SectorPerformance.Sector"/>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange this ratio was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The aggregate price-to-earnings ratio.
    ///
    /// <para><b>Zero is an in-band sentinel and this SDK does not translate it.</b> Measured 2026-08-29, 12 of
    /// 254 industry-PE rows read exactly <c>0</c>, emitted as JSON <c>0</c> rather than <c>0.0</c>. Across 359
    /// measured values <c>pe</c> was never negative and never null, so zero is carrying "no meaningful
    /// aggregate PE" rather than a measurement — Biotechnology on the NYSE is not a zero-multiple industry.
    /// The SDK has no way to tell which zeros are real, so it reports what FMP sent. Treat <c>0</c> as "no
    /// answer", not as a ratio.</para></summary>
    [JsonPropertyName("pe")] public decimal? Pe { get; init; }
}

/// <summary>One industry's aggregate price-to-earnings ratio on one day and one exchange. From
/// <c>stable/industry-pe-snapshot</c> and <c>stable/historical-industry-pe</c>.
///
/// <para><see cref="IndustryPerformance"/> with <see cref="Pe"/> in place of
/// <see cref="IndustryPerformance.AverageChange"/>. The <c>pe: 0</c> sentinel documented on
/// <see cref="SectorPe.Pe"/> was measured on this shape specifically — all 12 of the zeros are industry
/// rows.</para></summary>
public sealed record IndustryPe
{
    /// <summary>The trading day the row describes. See <see cref="SectorPerformance.Date"/>.</summary>
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    [JsonPropertyName("date")] public LocalDate? Date { get; init; }

    /// <summary>FMP's own industry label. See <see cref="IndustryPerformance.Industry"/>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The exchange this ratio was taken over. See <see cref="SectorPerformance.Exchange"/>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The aggregate price-to-earnings ratio. <b>Zero is an in-band sentinel</b> — see
    /// <see cref="SectorPe.Pe"/>, where the measurement is recorded.</summary>
    [JsonPropertyName("pe")] public decimal? Pe { get; init; }
}
