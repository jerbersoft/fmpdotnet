using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One position an ETF holds, from <c>stable/etf/holdings</c>.
///
/// <para><b>There is no pagination and responses get large.</b> Measured 2026-08-30, <c>limit</c> and
/// <c>page</c> are ignored exactly the way an unknown parameter is: <c>etf/holdings?symbol=BND</c> returned
/// 17,252 rows and 4,949,598 bytes with and without either, byte-identical. VXUS returned 8,821 rows and
/// 2.5 MB. There is no way to ask for less than everything, and <c>EtfInfo.HoldingsCount</c> cannot be used
/// to pre-size the result — it disagreed with this path on 32 of 33 ETFs.</para>
///
/// <para><b>Half of a bond fund's rows have no ticker.</b> Measured 2026-08-30 over 35,185 rows,
/// <see cref="Asset"/> was empty on 51.1% and <see cref="Isin"/> on 51.0% — unlisted debt and foreign lines.
/// <see cref="Name"/> was populated on <b>all</b> 35,185, so the human-readable identity is always
/// there.</para>
///
/// <para>Measured 2026-08-30, rows come back <b>ordered by weight, descending</b>, and the order held over the
/// full 17,252-row BND response. A stock symbol answers <c>[]</c> at HTTP 200 rather than an error — AAPL
/// did.</para></summary>
public sealed record EtfHolding
{
    /// <summary>The fund, echoed on every row — measured 2026-08-30 it was constant across every row of all 33
    /// responses. Nullable because the deserialiser cannot promise a key is present, not because any measured
    /// row omitted it: no row was ever missing a key on this path.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The held security's ticker, or <see langword="null"/> when it has none.
    ///
    /// <para>Measured <c>""</c> on 17,988 of 35,185 rows 2026-08-30 and mapped to <see langword="null"/> by
    /// <see cref="SentinelStringJsonConverter"/>. That is not a defect to route around: BND's 17,252 holdings
    /// are mostly unlisted debt. Use <see cref="Name"/> when this is <see langword="null"/>.</para></summary>
    [JsonPropertyName("asset")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Asset { get; init; }

    /// <summary>The held security's name — <c>"NVIDIA CORP"</c>, <c>"MKTLIQ 12/31/2049"</c>, <c>"US
    /// Dollar"</c>. Populated on all 35,185 rows measured 2026-08-30, and deliberately <b>not</b> routed
    /// through <see cref="SentinelStringJsonConverter"/>: no sentinel was ever measured here, and an empty
    /// name would be information rather than absence.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The held security's ISIN, or <see langword="null"/>. Empty on 17,927 of 35,185 rows measured
    /// 2026-08-30; see <see cref="Asset"/>.</summary>
    [JsonPropertyName("isin")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? Isin { get; init; }

    /// <summary>The held security's CUSIP, or <see langword="null"/>. Empty on 8,036 of 35,185 rows measured
    /// 2026-08-30 — a different population from <see cref="Asset"/>'s, so a row can carry a CUSIP and no
    /// ticker.</summary>
    [JsonPropertyName("securityCusip")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? SecurityCusip { get; init; }

    /// <summary>Shares held. <b>Signed and fractional</b>: measured 2026-08-30 the range was
    /// −2,920,694,176 to 71,557,356,084, and values like <c>54112647.476</c> and
    /// <c>0.0001383508577753182</c> are ordinary in bond funds. An integer type is wrong for this field
    /// twice over.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>The position's share of the fund, as a percentage — <c>8.29427804</c> means 8.29%. A bare JSON
    /// number, like <see cref="EtfSectorWeighting.WeightPercentage"/> and unlike
    /// <see cref="EtfCountryWeighting.WeightPercentage"/>. Measured range 2026-08-30: −0.34898692 to
    /// 100.</summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }

    /// <summary>The position's value. Measured range 2026-08-30: −560,343,250 to 155,526,370,000.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }

    /// <summary>When FMP last refreshed this fund's holdings — <b>a cache stamp, not an as-of date.</b>
    ///
    /// <para><b>Read as UTC, and that was established by falsification rather than assumed.</b> Measured
    /// 2026-08-30, <c>symbol=SCHD</c> returned <c>2026-08-30 06:51:13</c> in a response whose own HTTP
    /// <c>Date</c> header read <c>Sun, 30 Aug 2026 10:05:35 GMT</c>. Read as Eastern that stamp is
    /// <c>10:51:13Z</c> — 46 minutes <b>after</b> the response that carried it, which a cache stamp cannot be.
    /// Read as UTC it is 3h14m old. Reproduced 18 seconds later against a fresh response. So this takes
    /// <see cref="NullableFmpInstantJsonConverter"/>, while the identical wire shape on
    /// <c>FundDisclosure.AcceptedDate</c> takes <see cref="NullableEasternInstantJsonConverter"/>.</para>
    ///
    /// <para><b>One value for the whole response, and it can be days old.</b> Measured 2026-08-30, 33 of 33
    /// responses carried exactly one distinct value across every row, and staleness ranged from <b>3.2
    /// hours</b> (SCHD, the response above) to <b>284 hours</b> (IJH, IJR) — <b>twelve days</b>. It says when FMP
    /// refreshed its copy — not when the
    /// fund held these positions. Do not use it as a portfolio as-of date; <c>FundDisclosure.Date</c> is
    /// that.</para></summary>
    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(NullableFmpInstantJsonConverter))]
    public Instant? UpdatedAt { get; init; }
}
