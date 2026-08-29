using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's reading of one week's Commitment of Traders report for one contract. From
/// <c>stable/commitment-of-traders-analysis</c>.
///
/// <para>Sixteen fields against <see cref="CotReport"/>'s 128: this is the derived view — long/short balance,
/// a sentiment label, and the week-on-week change — where <see cref="CotReport"/> is the raw filing. The two
/// paths answer the same symbols and the same dates.</para>
///
/// <para><b>They do not answer the same amount of history.</b> Measured 2026-08-29 with one symbol and one
/// two-year range, this path answered <b>13 rows</b> and <see cref="CotReport"/> answered <b>105</b> — and
/// both looked equally healthy. See <see cref="Endpoints.CotEndpoints.GetAnalysisAsync"/>.</para></summary>
public sealed record CotAnalysis
{
    /// <summary>The contract symbol — <c>NG</c>, <c>ZC</c>. FMP's own codes, listed by
    /// <see cref="Endpoints.CotEndpoints.GetSymbolsAsync"/>, and not exchange tickers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The report date — the Tuesday the CFTC's positions were taken. Arrives as
    /// <c>"2024-02-27 00:00:00"</c>, hence the midnight converter.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The contract's name — <c>Natural Gas (NG)</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The sector, in FMP's own vocabulary — <c>ENERGIES</c>.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The exchange, as the CFTC names it — <c>NAT GAS NYME - NEW YORK MERCANTILE
    /// EXCHANGE</c>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The long side as a percentage of the market this week. Pairs with
    /// <see cref="CurrentShortMarketSituation"/> to 100.</summary>
    [JsonPropertyName("currentLongMarketSituation")] public decimal? CurrentLongMarketSituation { get; init; }

    /// <summary>The short side as a percentage of the market this week.</summary>
    [JsonPropertyName("currentShortMarketSituation")] public decimal? CurrentShortMarketSituation { get; init; }

    /// <summary>FMP's label for this week — <c>Bearish</c>, <c>Bullish</c>. A string and not an enum: the
    /// vocabulary was not enumerated.</summary>
    [JsonPropertyName("marketSituation")] public string? MarketSituation { get; init; }

    /// <summary>The long side as a percentage of the market the previous week.</summary>
    [JsonPropertyName("previousLongMarketSituation")] public decimal? PreviousLongMarketSituation { get; init; }

    /// <summary>The short side as a percentage of the market the previous week.</summary>
    [JsonPropertyName("previousShortMarketSituation")] public decimal? PreviousShortMarketSituation { get; init; }

    /// <summary>FMP's label for the previous week.</summary>
    [JsonPropertyName("previousMarketSituation")] public string? PreviousMarketSituation { get; init; }

    /// <summary>Net non-commercial position this week, in contracts. <b>Bound from the wire name
    /// <c>netPostion</c></b>, which is missing an <c>i</c> — its two siblings below are spelled
    /// correctly.</summary>
    [JsonPropertyName("netPostion")] public int? NetPosition { get; init; }  // sic: wire drops the "i"

    /// <summary>Net non-commercial position the previous week, in contracts.</summary>
    [JsonPropertyName("previousNetPosition")] public int? PreviousNetPosition { get; init; }

    /// <summary><b>A percent change, not a delta — this is the one field on this record that will silently
    /// cost a caller three orders of magnitude.</b>
    ///
    /// <para>It sits between two contract counts and is not their difference. Measured across all 545 rows on
    /// 2026-08-29, <b>545 match a percent reading and 4 match an absolute one</b>. On the newest NG row,
    /// <see cref="NetPosition"/> is −141,553 and <see cref="PreviousNetPosition"/> is −153,872: the
    /// difference is 12,319 and this field reads <c>8.01</c>.</para>
    ///
    /// <para>That is why this property is <see langword="decimal"/> while both its neighbours are
    /// <see langword="int"/>. Adding it to a position count compiles and is wrong.</para></summary>
    [JsonPropertyName("changeInNetPosition")] public decimal? ChangeInNetPosition { get; init; }

    /// <summary>FMP's label for the direction of travel — <c>Increasing Bullish</c>. <b>Sometimes carries a
    /// leading space</b> — <c>" Increasing Bearish"</c>, measured 2026-08-29 — which is kept rather than
    /// trimmed, because trimming would be this SDK disagreeing with the upstream about the value. Trim before
    /// matching.</summary>
    [JsonPropertyName("marketSentiment")] public string? MarketSentiment { get; init; }

    /// <summary>Whether FMP reads the week as a reversal.
    ///
    /// <para><b>A real JSON boolean</b> on all 545 rows measured 2026-08-29, which is worth stating because
    /// #31 met the opposite: <c>CongressionalTrade.CapitalGainsOver200Usd</c> arrives as the <i>string</i>
    /// <c>"False"</c> and is typed <see langword="string"/> for that reason. The two look identical in
    /// documentation and differ on the wire.</para></summary>
    [JsonPropertyName("reversalTrend")] public bool? ReversalTrend { get; init; }
}

/// <summary>One futures contract FMP publishes COT data for. From
/// <c>stable/commitment-of-traders-list</c>.
///
/// <para>The whole universe in one call — <b>65 rows</b> measured 2026-08-29, with no paging and no
/// parameters. This is where a <see cref="CotAnalysis.Symbol"/> comes from, and the codes are FMP's own
/// (<c>NG</c>, <c>ZC</c>, <c>EURGBP</c>) rather than exchange tickers.</para></summary>
public sealed record CotSymbol
{
    /// <summary>The contract code — <c>NG</c>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The contract's name, with the code repeated in parentheses — <c>Natural Gas
    /// (NG)</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}
