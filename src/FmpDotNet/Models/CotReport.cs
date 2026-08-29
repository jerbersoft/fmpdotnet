using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 128 properties below are a flat transcription of the CFTC's own column names as FMP
// relays them: the property name carries the same information a generated one-line summary would, and 128 of
// those would bury the type-level documentation above — which is where this response's real quirks are
// recorded, including the 27 properties whose spelling deliberately differs from the wire.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this project used to, also meant a
// NEW undocumented public member anywhere in the SDK compiled silently. This is the EIGHTH exemption: the
// seven period-shaped fundamentals models from #4, and this one. The zero-warning bar holds everywhere else.
#pragma warning disable CS1591

/// <summary>One week's Commitment of Traders report for one futures contract. From
/// <c>stable/commitment-of-traders-report</c>.
///
/// <para><b>The widest record in this SDK — 128 properties</b>, against <c>FinancialRatios</c> at 66. It is
/// the CFTC's own weekly report relayed field for field: four blocks of positions, percentages, trader counts
/// and week-on-week changes, each split three ways into <c>All</c>, <c>Old</c> and <c>Other</c>.</para>
///
/// <para><b>Twenty-seven property names deliberately differ from their <c>[JsonPropertyName]</c>, because
/// FMP's spelling is wrong.</b> The attribute carries the wire verbatim and the property carries correct
/// English — the same rule under which <c>senateID</c> binds to <c>SenateId</c>. They come in two kinds.
/// <b>Twenty-six</b> are the suffix <c>Ol</c> where the block it belongs to is <c>Old</c>; each carries its
/// own inline comment naming the wire suffix, since twenty-six is too many for this paragraph alone to stand
/// in for. <b>Two</b> are the misspelling <c>Spead</c> for <c>Spread</c>, and those alone carry the
/// <c>// sic</c> marker — reserved to a genuine typo in FMP's own spelling, not to the deliberate <c>Ol</c>
/// shortening. <c>tradersNoncommSpeadOl</c> is in both counts at once, which is why 26 and 2 total 27 rather
/// than 28. (<c>netPostion</c> on <see cref="CotAnalysis"/> is a third misspelling, on a different record,
/// carrying the same <c>// sic</c> marker; it is not in this record's twenty-seven.) Do not "fix" an
/// attribute: the property would then bind nothing, silently.</para>
///
/// <para><b>The <c>Other</c> block is 36 of the 128 and is not dead weight.</b> Measured 2026-08-29, 118 of
/// 545 rows carry a non-zero value in at least one <c>Other</c> field, across 14 distinct symbols — the
/// grains and softs, where the CFTC splits old-crop from other-crop. Dropping the block to save width would
/// silently lose real data for those contracts.</para>
///
/// <para><b>The data is about two and a half years stale.</b> Measured 2026-08-29, every COT response on this
/// key — bare, by symbol, and by range — covered 2024-01-02 to 2024-02-27 and nothing later. A caller asking
/// for a recent range gets an empty array with HTTP 200. See
/// <see cref="Endpoints.CotEndpoints.GetReportAsync"/>.</para>
///
/// <para><see cref="Date"/> arrives as <c>"2024-02-27 00:00:00"</c> on every row of both COT paths, which is
/// why it takes <see cref="NullableDateAtMidnightJsonConverter"/> rather than the plain-date converter the
/// rest of this slice uses.</para></summary>
public sealed record CotReport
{
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }
    [JsonPropertyName("date")] [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))] public LocalDate? Date { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("sector")] public string? Sector { get; init; }
    [JsonPropertyName("marketAndExchangeNames")] public string? MarketAndExchangeNames { get; init; }
    [JsonPropertyName("cftcContractMarketCode")] public string? CftcContractMarketCode { get; init; }
    [JsonPropertyName("cftcMarketCode")] public string? CftcMarketCode { get; init; }
    [JsonPropertyName("cftcRegionCode")] public string? CftcRegionCode { get; init; }
    [JsonPropertyName("cftcCommodityCode")] public string? CftcCommodityCode { get; init; }
    [JsonPropertyName("openInterestAll")] public int? OpenInterestAll { get; init; }
    [JsonPropertyName("noncommPositionsLongAll")] public int? NoncommPositionsLongAll { get; init; }
    [JsonPropertyName("noncommPositionsShortAll")] public int? NoncommPositionsShortAll { get; init; }
    [JsonPropertyName("noncommPositionsSpreadAll")] public int? NoncommPositionsSpreadAll { get; init; }
    [JsonPropertyName("commPositionsLongAll")] public int? CommPositionsLongAll { get; init; }
    [JsonPropertyName("commPositionsShortAll")] public int? CommPositionsShortAll { get; init; }
    [JsonPropertyName("totReptPositionsLongAll")] public int? TotReptPositionsLongAll { get; init; }
    [JsonPropertyName("totReptPositionsShortAll")] public int? TotReptPositionsShortAll { get; init; }
    [JsonPropertyName("nonreptPositionsLongAll")] public int? NonreptPositionsLongAll { get; init; }
    [JsonPropertyName("nonreptPositionsShortAll")] public int? NonreptPositionsShortAll { get; init; }
    [JsonPropertyName("openInterestOld")] public int? OpenInterestOld { get; init; }
    [JsonPropertyName("noncommPositionsLongOld")] public int? NoncommPositionsLongOld { get; init; }
    [JsonPropertyName("noncommPositionsShortOld")] public int? NoncommPositionsShortOld { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOld")] public int? NoncommPositionsSpreadOld { get; init; }
    [JsonPropertyName("commPositionsLongOld")] public int? CommPositionsLongOld { get; init; }
    [JsonPropertyName("commPositionsShortOld")] public int? CommPositionsShortOld { get; init; }
    [JsonPropertyName("totReptPositionsLongOld")] public int? TotReptPositionsLongOld { get; init; }
    [JsonPropertyName("totReptPositionsShortOld")] public int? TotReptPositionsShortOld { get; init; }
    [JsonPropertyName("nonreptPositionsLongOld")] public int? NonreptPositionsLongOld { get; init; }
    [JsonPropertyName("nonreptPositionsShortOld")] public int? NonreptPositionsShortOld { get; init; }
    [JsonPropertyName("openInterestOther")] public int? OpenInterestOther { get; init; }
    [JsonPropertyName("noncommPositionsLongOther")] public int? NoncommPositionsLongOther { get; init; }
    [JsonPropertyName("noncommPositionsShortOther")] public int? NoncommPositionsShortOther { get; init; }
    [JsonPropertyName("noncommPositionsSpreadOther")] public int? NoncommPositionsSpreadOther { get; init; }
    [JsonPropertyName("commPositionsLongOther")] public int? CommPositionsLongOther { get; init; }
    [JsonPropertyName("commPositionsShortOther")] public int? CommPositionsShortOther { get; init; }
    [JsonPropertyName("totReptPositionsLongOther")] public int? TotReptPositionsLongOther { get; init; }
    [JsonPropertyName("totReptPositionsShortOther")] public int? TotReptPositionsShortOther { get; init; }
    [JsonPropertyName("nonreptPositionsLongOther")] public int? NonreptPositionsLongOther { get; init; }
    [JsonPropertyName("nonreptPositionsShortOther")] public int? NonreptPositionsShortOther { get; init; }
    [JsonPropertyName("changeInOpenInterestAll")] public int? ChangeInOpenInterestAll { get; init; }
    [JsonPropertyName("changeInNoncommLongAll")] public int? ChangeInNoncommLongAll { get; init; }
    [JsonPropertyName("changeInNoncommShortAll")] public int? ChangeInNoncommShortAll { get; init; }
    [JsonPropertyName("changeInNoncommSpeadAll")] public int? ChangeInNoncommSpreadAll { get; init; }  // sic: wire spells it "Spead"
    [JsonPropertyName("changeInCommLongAll")] public int? ChangeInCommLongAll { get; init; }
    [JsonPropertyName("changeInCommShortAll")] public int? ChangeInCommShortAll { get; init; }
    [JsonPropertyName("changeInTotReptLongAll")] public int? ChangeInTotReptLongAll { get; init; }
    [JsonPropertyName("changeInTotReptShortAll")] public int? ChangeInTotReptShortAll { get; init; }
    [JsonPropertyName("changeInNonreptLongAll")] public int? ChangeInNonreptLongAll { get; init; }
    [JsonPropertyName("changeInNonreptShortAll")] public int? ChangeInNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestAll")] public int? PctOfOpenInterestAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongAll")] public decimal? PctOfOiNoncommLongAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortAll")] public decimal? PctOfOiNoncommShortAll { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadAll")] public decimal? PctOfOiNoncommSpreadAll { get; init; }
    [JsonPropertyName("pctOfOiCommLongAll")] public decimal? PctOfOiCommLongAll { get; init; }
    [JsonPropertyName("pctOfOiCommShortAll")] public decimal? PctOfOiCommShortAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongAll")] public decimal? PctOfOiTotReptLongAll { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortAll")] public decimal? PctOfOiTotReptShortAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongAll")] public decimal? PctOfOiNonreptLongAll { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortAll")] public decimal? PctOfOiNonreptShortAll { get; init; }
    [JsonPropertyName("pctOfOpenInterestOl")] public int? PctOfOpenInterestOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommLongOl")] public decimal? PctOfOiNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommShortOl")] public decimal? PctOfOiNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNoncommSpreadOl")] public decimal? PctOfOiNoncommSpreadOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommLongOl")] public decimal? PctOfOiCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiCommShortOl")] public decimal? PctOfOiCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptLongOl")] public decimal? PctOfOiTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiTotReptShortOl")] public decimal? PctOfOiTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptLongOl")] public decimal? PctOfOiNonreptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOiNonreptShortOl")] public decimal? PctOfOiNonreptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("pctOfOpenInterestOther")] public int? PctOfOpenInterestOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommLongOther")] public decimal? PctOfOiNoncommLongOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommShortOther")] public decimal? PctOfOiNoncommShortOther { get; init; }
    [JsonPropertyName("pctOfOiNoncommSpreadOther")] public decimal? PctOfOiNoncommSpreadOther { get; init; }
    [JsonPropertyName("pctOfOiCommLongOther")] public decimal? PctOfOiCommLongOther { get; init; }
    [JsonPropertyName("pctOfOiCommShortOther")] public decimal? PctOfOiCommShortOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptLongOther")] public decimal? PctOfOiTotReptLongOther { get; init; }
    [JsonPropertyName("pctOfOiTotReptShortOther")] public decimal? PctOfOiTotReptShortOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptLongOther")] public decimal? PctOfOiNonreptLongOther { get; init; }
    [JsonPropertyName("pctOfOiNonreptShortOther")] public decimal? PctOfOiNonreptShortOther { get; init; }
    [JsonPropertyName("tradersTotAll")] public int? TradersTotAll { get; init; }
    [JsonPropertyName("tradersNoncommLongAll")] public int? TradersNoncommLongAll { get; init; }
    [JsonPropertyName("tradersNoncommShortAll")] public int? TradersNoncommShortAll { get; init; }
    [JsonPropertyName("tradersNoncommSpreadAll")] public int? TradersNoncommSpreadAll { get; init; }
    [JsonPropertyName("tradersCommLongAll")] public int? TradersCommLongAll { get; init; }
    [JsonPropertyName("tradersCommShortAll")] public int? TradersCommShortAll { get; init; }
    [JsonPropertyName("tradersTotReptLongAll")] public int? TradersTotReptLongAll { get; init; }
    [JsonPropertyName("tradersTotReptShortAll")] public int? TradersTotReptShortAll { get; init; }
    [JsonPropertyName("tradersTotOl")] public int? TradersTotOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommLongOl")] public int? TradersNoncommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommShortOl")] public int? TradersNoncommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersNoncommSpeadOl")] public int? TradersNoncommSpreadOld { get; init; }  // sic: BOTH defects — "Spead" and "Ol"
    [JsonPropertyName("tradersCommLongOl")] public int? TradersCommLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersCommShortOl")] public int? TradersCommShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptLongOl")] public int? TradersTotReptLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotReptShortOl")] public int? TradersTotReptShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("tradersTotOther")] public int? TradersTotOther { get; init; }
    [JsonPropertyName("tradersNoncommLongOther")] public int? TradersNoncommLongOther { get; init; }
    [JsonPropertyName("tradersNoncommShortOther")] public int? TradersNoncommShortOther { get; init; }
    [JsonPropertyName("tradersNoncommSpreadOther")] public int? TradersNoncommSpreadOther { get; init; }
    [JsonPropertyName("tradersCommLongOther")] public int? TradersCommLongOther { get; init; }
    [JsonPropertyName("tradersCommShortOther")] public int? TradersCommShortOther { get; init; }
    [JsonPropertyName("tradersTotReptLongOther")] public int? TradersTotReptLongOther { get; init; }
    [JsonPropertyName("tradersTotReptShortOther")] public int? TradersTotReptShortOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongAll")] public decimal? ConcGrossLe4TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortAll")] public decimal? ConcGrossLe4TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongAll")] public decimal? ConcGrossLe8TdrLongAll { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortAll")] public decimal? ConcGrossLe8TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe4TdrLongAll")] public decimal? ConcNetLe4TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe4TdrShortAll")] public decimal? ConcNetLe4TdrShortAll { get; init; }
    [JsonPropertyName("concNetLe8TdrLongAll")] public decimal? ConcNetLe8TdrLongAll { get; init; }
    [JsonPropertyName("concNetLe8TdrShortAll")] public decimal? ConcNetLe8TdrShortAll { get; init; }
    [JsonPropertyName("concGrossLe4TdrLongOl")] public decimal? ConcGrossLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrShortOl")] public decimal? ConcGrossLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrLongOl")] public decimal? ConcGrossLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe8TdrShortOl")] public decimal? ConcGrossLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrLongOl")] public decimal? ConcNetLe4TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe4TdrShortOl")] public decimal? ConcNetLe4TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrLongOl")] public decimal? ConcNetLe8TdrLongOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concNetLe8TdrShortOl")] public decimal? ConcNetLe8TdrShortOld { get; init; }  // wire suffix is "Ol", not "Old"
    [JsonPropertyName("concGrossLe4TdrLongOther")] public decimal? ConcGrossLe4TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe4TdrShortOther")] public decimal? ConcGrossLe4TdrShortOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrLongOther")] public decimal? ConcGrossLe8TdrLongOther { get; init; }
    [JsonPropertyName("concGrossLe8TdrShortOther")] public decimal? ConcGrossLe8TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe4TdrLongOther")] public decimal? ConcNetLe4TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe4TdrShortOther")] public decimal? ConcNetLe4TdrShortOther { get; init; }
    [JsonPropertyName("concNetLe8TdrLongOther")] public decimal? ConcNetLe8TdrLongOther { get; init; }
    [JsonPropertyName("concNetLe8TdrShortOther")] public decimal? ConcNetLe8TdrShortOther { get; init; }
    [JsonPropertyName("contractUnits")] public string? ContractUnits { get; init; }
}
