using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One daily OHLCV bar from <c>stable/historical-price-eod/full</c>, with FMP's own derived fields.
///
/// <para>Measured against the live API on 2026-08-27: the ten properties below are the whole response. AAPL's
/// 2026-08-26 session answered
/// <c>{"symbol":"AAPL","date":"2026-08-26","open":310.3,"high":315.43,"low":308.8,"close":313.45,
/// "volume":34024486,"change":3.15,"changePercent":1.02,"vwap":311.995}</c>.</para>
///
/// <para><b>Split-adjusted, not dividend-adjusted.</b> This is the field set most callers want and the adjustment
/// most callers get wrong. AAPL on 2020-08-28, the session before its four-for-one split, reads
/// <see cref="Close"/> 124.81 here against 499.24 raw and 120.96 dividend-adjusted — see
/// <see cref="AdjustedEndOfDayBar"/> for the full comparison and for which endpoint carries which.</para>
///
/// <para>Rows arrive <b>newest first</b>, and the SDK does not re-sort them.</para></summary>
public sealed record EndOfDayBar
{
    /// <summary>The symbol as FMP spells it. Present on every row measured.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The session date, <c>"2026-08-26"</c> on the wire — a trading day, not a moment.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The session's opening price.</summary>
    [JsonPropertyName("open")] public decimal? Open { get; init; }

    /// <summary>The session's high.</summary>
    [JsonPropertyName("high")] public decimal? High { get; init; }

    /// <summary>The session's low.</summary>
    [JsonPropertyName("low")] public decimal? Low { get; init; }

    /// <summary>The session's closing price. Equal to <see cref="EndOfDayPrice.Price"/> on the <c>light</c>
    /// endpoint for the same session.</summary>
    [JsonPropertyName("close")] public decimal? Close { get; init; }

    /// <summary>Shares traded in the session, split-adjusted.</summary>
    [JsonPropertyName("volume")] public long? Volume { get; init; }

    /// <summary><see cref="Close"/> minus the previous session's close, in price terms — FMP's arithmetic, not
    /// the SDK's.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary><see cref="Change"/> as a percentage, on 0–100 rather than as a fraction: AAPL's 2026-08-26
    /// session reads <c>1.02</c> for a 1.02% move.
    ///
    /// <para><b>The wire spells this <c>changePercent</c> here and <c>changePercentage</c> on
    /// <see cref="Quote.ChangePercentage"/></b> — two spellings for one concept, in two endpoint groups that a
    /// caller will routinely use together. Both are mapped explicitly for that reason; neither name is the SDK's
    /// choice.</para></summary>
    [JsonPropertyName("changePercent")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? ChangePercent { get; init; }

    /// <summary>Volume-weighted average price for the session, as FMP computes it. Measured 2026-08-26 for AAPL:
    /// <c>311.995</c>, which is the mean of that session's open, high, low and close rather than a
    /// trade-weighted figure — <c>(310.3 + 315.43 + 308.8 + 313.45) / 4 = 311.995</c> exactly. Treat it as OHLC4,
    /// not as the execution benchmark the name suggests.</summary>
    [JsonPropertyName("vwap")] public decimal? Vwap { get; init; }
}
