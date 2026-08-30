using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One ETF's position in a given security, from <c>stable/etf/asset-exposure</c>.
///
/// <para><b>This path runs the opposite way from the other four <c>etf/*</c> paths.</b> They take a fund and
/// answer what it holds; this one takes an <b>asset</b> and answers <b>which funds hold it</b>. Measured
/// 2026-08-30, <c>symbol=AAPL</c> returned 3,293 rows, each naming a different ETF in <see cref="Symbol"/>
/// with <see cref="Asset"/> fixed at <c>AAPL</c>. The parameter is "any asset", not "any stock":
/// <c>symbol=SPY</c> answered 39 rows, the ETFs that hold SPY.</para>
///
/// <para><b>No ordering was found</b> in the responses measured 2026-08-30, and there is no pagination —
/// <c>limit</c> and <c>page</c> were ignored, with <c>symbol=NVDA</c> returning 3,860 rows and 588,479 bytes
/// with and without them.</para></summary>
public sealed record EtfAssetExposure
{
    /// <summary>The <b>fund</b> that holds the asset — a different one on every row. This is not the symbol
    /// the caller asked for; see <see cref="Asset"/>. Nullable for the reason on
    /// <see cref="EtfHolding.Symbol"/>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The security being held — <b>the symbol the caller asked for</b>, echoed on every row.
    /// Measured 2026-08-30 it was identical across every row of all 8 responses. Not routed through
    /// <see cref="Serialization.SentinelStringJsonConverter"/>: no sentinel was ever measured on this
    /// path.</summary>
    [JsonPropertyName("asset")] public string? Asset { get; init; }

    /// <summary>Shares of <see cref="Asset"/> held by <see cref="Symbol"/>. Signed — an inverse product
    /// reports a negative count, e.g. <c>NVD</c> at <c>−457,235</c> shares of NVDA, measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("sharesNumber")] public decimal? SharesNumber { get; init; }

    /// <summary>The position's share of the holding fund, as a percentage.
    ///
    /// <para><b>Bounded by neither 0 nor 100.</b> Measured 2026-08-30 the range on this field was
    /// <b>−199.9869</b> (the <c>NVD</c> inverse product) to <b>50,506</b> (a <c>HEMI</c> row whose market
    /// value was zero). It is therefore not range-checked anywhere in this SDK and must not be: a guard would
    /// reject real data.</para></summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }

    /// <summary>The position's value. Measured range 2026-08-30: <b>−103,015,045.5</b> to
    /// <b>7,434,183,997,921.512</b> — 17 significant digits, which is why every figure in this group is
    /// <see cref="decimal"/> and not <see cref="double"/>.</summary>
    [JsonPropertyName("marketValue")] public decimal? MarketValue { get; init; }
}
