using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One sector's share of an ETF's holdings, from <c>stable/etf/sector-weightings</c>.
///
/// <para><b>This data is also inside <c>EtfInfo</c>, under a different pair of key names.</b> Measured
/// 2026-08-30, <c>etf/info.sectorsList</c> and this path agreed on the key set and on <b>every value</b>, with
/// no rounding difference, on all 13 ETFs cross-checked — including SPY's and VOO's 12-element lists, QQQ's
/// 11-element list, and the 1-element lists of GLD, SLV, TLT and BND. The nested objects spell the same two
/// facts <c>industry</c> and <c>exposure</c>; see <c>EtfInfoSector</c>. So a caller who already has an
/// <c>EtfInfo</c> does not need this path, and the duplication in this SDK is deliberate rather than an
/// oversight — the two wire shapes cannot share one record, because System.Text.Json binds one
/// <see cref="JsonPropertyNameAttribute"/> per property.</para>
///
/// <para><b>Ordered alphabetically by sector, not by weight</b>, measured 2026-08-30 — the opposite of
/// <see cref="EtfCountryWeighting"/>, which looks like its matched pair and sorts by weight descending.</para>
///
/// <para>Twelve sectors is the measured maximum. A commodity fund answers one row, <c>Cash &amp; Others</c>
/// — GLD, SLV and TLT all did.</para></summary>
public sealed record EtfSectorWeighting
{
    /// <summary>The fund, echoed on every row. Measured 2026-08-30 it was constant across every row of all
    /// 13 responses. Nullable for the reason on <see cref="EtfCountryWeighting.Country"/>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The sector name — <c>"Basic Materials"</c>, <c>"Technology"</c>, <c>"Cash &amp; Others"</c>.
    ///
    /// <para>A free string rather than the SDK's <see cref="Sector"/> enum, and the reason is
    /// <c>Cash &amp; Others</c>: it is not a sector, it is the residual, and it appeared on all 13 ETFs
    /// measured 2026-08-30. An enum here would have to invent a member for it or lose the row.</para></summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The share of the fund, as a percentage — <c>37.4</c> means 37.4%.
    ///
    /// <para><b>A bare JSON number here, and a <c>"97.52%"</c> string on
    /// <see cref="EtfCountryWeighting.WeightPercentage"/></b>, measured 2026-08-30. That is why one of the two
    /// properties carries a converter and this one does not.</para>
    ///
    /// <para><b><see cref="decimal"/>, and it must stay <see cref="decimal"/>.</b> SPY's
    /// <c>Cash &amp; Others</c> weight measured <c>1.4210854715202004e-14</c> — 2⁻⁴⁶, the residue of a
    /// floating-point subtraction — which needs 30 decimal places where <see cref="decimal"/> has 28.
    /// Checked on .NET 10 rather than assumed: System.Text.Json <b>rounds it to 28 places and does not
    /// throw</b>, losing about 4e-31 of a percentage point on a value that is already numerical noise.
    /// Switching this slice to <see cref="double"/> to "fix" that would round every large figure in the group
    /// far more damagingly — <c>EtfAssetExposure.MarketValue</c> reaches 7,434,183,997,921.512 with 17
    /// significant digits.</para></summary>
    [JsonPropertyName("weightPercentage")] public decimal? WeightPercentage { get; init; }
}
