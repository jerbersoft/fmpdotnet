using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>An ETF's fact sheet, from <c>stable/etf/info</c> — nineteen keys, the widest shape in the ETF and
/// mutual-fund group.
///
/// <para><b>One row per call.</b> All 33 responses measured 2026-08-30 were single-element arrays, which is why
/// the SDK surfaces this as one record rather than a list. An unknown symbol answers <c>[]</c> at HTTP 200,
/// which becomes <see langword="null"/>.</para>
///
/// <para><b><see cref="SectorsList"/> duplicates a whole endpoint.</b> Measured 2026-08-30 it agreed with
/// <c>stable/etf/sector-weightings</c> on the key set and on every value, on all 13 ETFs cross-checked. A
/// caller holding this record does not need that path.</para>
///
/// <para><b><see cref="HoldingsCount"/> is not the number of holdings.</b> Read its doc before using it for
/// anything.</para></summary>
public sealed record EtfInfo
{
    /// <summary>The fund's ticker. Nullable because the deserialiser cannot promise a key is present, not
    /// because any measured row omitted it — no row was missing a key across all 33 measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The fund's name — <c>"State Street SPDR S&amp;P 500 ETF"</c>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>A prose description of the fund, several hundred words on the funds measured 2026-08-30. It is
    /// editorial copy, not structured data.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The fund's own ISIN.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>What the fund holds, as FMP labels it.
    ///
    /// <para><b>A free string, not an enum, and the reason is in the measurement.</b> Six values appeared
    /// across 33 funds on 2026-08-30 — <c>Equity</c>, <c>Fixed Income</c>, <c>Commodities</c>,
    /// <c>International Equity</c>, <c>Large Cap Equity</c>, <c>Core Investment Grade Bond</c> — and those are
    /// not one vocabulary: <c>Equity</c>, <c>Large Cap Equity</c> and <c>International Equity</c> overlap
    /// rather than partition. An enum over a sample of 33 would fail on the 34th fund.</para></summary>
    [JsonPropertyName("assetClass")] public string? AssetClass { get; init; }

    /// <summary>The fund's own CUSIP.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>Where the fund is domiciled. <c>US</c> on all 33 rows measured 2026-08-30 — a small sample,
    /// and stated as one; this is not a claim that FMP only covers US funds.</summary>
    [JsonPropertyName("domicile")] public string? Domicile { get; init; }

    /// <summary>The issuer's page for the fund.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The issuer's brand — <c>"SPDR"</c>, <c>"Vanguard"</c>.</summary>
    [JsonPropertyName("etfCompany")] public string? EtfCompany { get; init; }

    /// <summary>The expense ratio <b>already expressed as a percentage figure</b>, not as a fraction of one:
    /// SPY measured <c>0.09</c>, which is 0.09% — nine basis points — and not 9%. Multiplying it by 100 is the
    /// mistake this sentence exists to prevent. Measured 2026-08-30.</summary>
    [JsonPropertyName("expenseRatio")] public decimal? ExpenseRatio { get; init; }

    /// <summary>Assets under management, in <see cref="NavCurrency"/>. SPY measured
    /// <c>816,147,480,000</c>.</summary>
    [JsonPropertyName("assetsUnderManagement")] public decimal? AssetsUnderManagement { get; init; }

    /// <summary>Average daily share volume.</summary>
    [JsonPropertyName("avgVolume")] public decimal? AvgVolume { get; init; }

    /// <summary>The fund's inception date — SPY measured <c>1993-01-22</c>. A plain ISO date on the wire, with
    /// no time component, unlike the two timestamps on this record's siblings.</summary>
    [JsonPropertyName("inceptionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? InceptionDate { get; init; }

    /// <summary>Net asset value per share, in <see cref="NavCurrency"/>.</summary>
    [JsonPropertyName("nav")] public decimal? Nav { get; init; }

    /// <summary>The currency <see cref="Nav"/> and <see cref="AssetsUnderManagement"/> are quoted in.
    /// <c>USD</c> on all 33 rows measured 2026-08-30 — a small sample, stated as one.</summary>
    [JsonPropertyName("navCurrency")] public string? NavCurrency { get; init; }

    /// <summary>FMP's holdings count — <b>which is not the number of holdings
    /// <see cref="Endpoints.EtfAndFundsEndpoints.GetEtfHoldingsAsync"/> returns.</b>
    ///
    /// <para>Cross-checked on 33 ETFs 2026-08-30 against the row count <c>stable/etf/holdings</c> returned for
    /// the same symbol on the same day, the two agreed on <b>one</b>. BND reports <b>346</b> and returns
    /// <b>17,252</b>. ARKK reports <b>10</b> and returns <b>47</b>. GLD and SLV report <b>0</b> and return
    /// <b>1</b>. Most gaps are small — the two paths refresh from different snapshots — but the field cannot
    /// be used to pre-size a buffer, cannot be used to page (there is no pagination on any path in this
    /// group), and cannot be used to decide whether calling the holdings path is worthwhile.</para>
    ///
    /// <para>Zero is a measured value here, not an absence.</para></summary>
    [JsonPropertyName("holdingsCount")] public int? HoldingsCount { get; init; }

    /// <summary>Whether FMP considers the fund actively trading. <b>The only genuine JSON boolean in the ETF
    /// and mutual-fund group</b> — the four <c>is*</c> fields on <c>FundDisclosure</c> are <c>Y</c>/<c>N</c>
    /// strings. <see langword="true"/> on all 33 rows measured 2026-08-30.</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }

    /// <summary>When FMP last refreshed this fact sheet.
    ///
    /// <para><b>A different wire format from <see cref="EtfHolding.UpdatedAt"/>, for the same concept.</b>
    /// This one is ISO-8601 with milliseconds and an explicit <c>Z</c> —
    /// <c>"2026-08-29T23:12:50.006Z"</c>, 33 of 33 rows measured 2026-08-30 — so it is UTC because it says so,
    /// and takes <see cref="NullableIsoInstantJsonConverter"/>. The holdings path sends
    /// <c>"2026-08-30 06:51:13"</c> for the same idea and had to have its zone established by measurement.
    /// Neither converter can read the other's format.</para></summary>
    [JsonPropertyName("updatedAt")]
    [JsonConverter(typeof(NullableIsoInstantJsonConverter))]
    public Instant? UpdatedAt { get; init; }

    /// <summary>The fund's sector breakdown, nested inside this response.
    ///
    /// <para><b>This is <c>stable/etf/sector-weightings</c>, under different key names.</b> Measured
    /// 2026-08-30, the two agreed on the key set and on <b>every value</b>, with no rounding difference, on
    /// all 13 ETFs cross-checked — SPY's and VOO's 12-element lists, QQQ's 11-element list, and the 1-element
    /// lists of GLD, SLV, TLT and BND. A caller holding this record does not need to call
    /// <see cref="Endpoints.EtfAndFundsEndpoints.GetEtfSectorWeightingsAsync"/>.</para>
    ///
    /// <para>The nested objects use <c>industry</c> and <c>exposure</c> where the path uses <c>sector</c> and
    /// <c>weightPercentage</c>, which is why <see cref="EtfInfoSector"/> exists rather than reusing
    /// <see cref="EtfSectorWeighting"/> — System.Text.Json binds one
    /// <see cref="JsonPropertyNameAttribute"/> per property, so one record cannot answer to both.</para>
    ///
    /// <para>The list came back <b>alphabetical by sector</b> on every response measured, matching the sibling
    /// path's order.</para></summary>
    [JsonPropertyName("sectorsList")] public IReadOnlyList<EtfInfoSector>? SectorsList { get; init; }
}

/// <summary>One sector's share of a fund, as nested inside <see cref="EtfInfo.SectorsList"/>.
///
/// <para><b>The same two facts as <see cref="EtfSectorWeighting"/>, under different wire keys.</b> Measured
/// 2026-08-30 the two shapes carried identical data on all 13 ETFs cross-checked, with no rounding difference.
/// The duplication in this SDK is deliberate: the nested objects say <c>industry</c> and <c>exposure</c> where
/// <c>stable/etf/sector-weightings</c> says <c>sector</c> and <c>weightPercentage</c>, and one record cannot
/// carry two <see cref="JsonPropertyNameAttribute"/> values on one property. A shared type would have to
/// rename keys in a converter, and its own doc would then be wrong about one of its two wire
/// shapes.</para></summary>
public sealed record EtfInfoSector
{
    /// <summary>The sector name — <c>"Basic Materials"</c>, <c>"Technology"</c>, <c>"Cash &amp; Others"</c>.
    ///
    /// <para><b>The wire key is <c>industry</c>, and it holds sector names.</b> The property takes the name
    /// the data actually has while the attribute carries the wire verbatim, under the same rule that binds
    /// <c>senateID</c> to <c>SenateId</c> and <c>changesPercentage</c> to
    /// <see cref="MarketMover.ChangePercentage"/>. <b>Do not "fix" the attribute</b> — the property would then
    /// bind nothing, silently.</para></summary>
    [JsonPropertyName("industry")] public string? Sector { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>37.4</c> means 37.4%. The wire key is
    /// <c>exposure</c>; the same figure is <c>weightPercentage</c> on
    /// <see cref="EtfSectorWeighting.WeightPercentage"/>, where the decimal-scale argument is
    /// recorded.</summary>
    [JsonPropertyName("exposure")] public decimal? Exposure { get; init; }
}
