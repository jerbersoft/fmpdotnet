using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One bar from <c>stable/technical-indicators/{indicator}</c>, at any of the nine indicators and
/// seven timeframes.
///
/// <para>All nine paths return the same six price fields plus <b>one</b> column named after the path segment,
/// measured 2026-08-29 across 88 non-empty responses carrying exactly nine distinct key tuples. This record
/// holds that column in <see cref="Value"/> and names it in <see cref="Indicator"/>, so one type serves all
/// nine rather than nine types duplicating the price block.</para>
///
/// <para>Rows arrive <b>newest first</b> — strictly descending, no duplicate dates across 1254 daily rows —
/// and the SDK does not re-sort them.</para>
///
/// <para><b>The row does not carry its symbol.</b> No response includes one. A caller fanning out across
/// symbols and concatenating the results cannot tell them apart afterwards, and this SDK does not stamp a
/// field FMP did not send.</para></summary>
[JsonConverter(typeof(TechnicalIndicatorBarJsonConverter))]
public sealed record TechnicalIndicatorBar
{
    /// <summary>When the bar opened, as wall clock with <b>no zone asserted</b>.
    ///
    /// <para>The wire form is <c>"2026-08-28 15:59:00"</c> — space-separated, no offset. On the six intraday
    /// timeframes this is <b>Eastern</b> wall clock, established the same way as
    /// <see cref="IntradayBar.Timestamp"/> and re-measured here on 2026-08-29: bars run 09:30 to 15:59 and
    /// stop, which is the US regular session in New York local time. Read as UTC they would place the market
    /// open at 05:30 ET. Convert through tzdb — never arithmetic on an offset.</para>
    ///
    /// <para><b>On <see cref="TechnicalIndicatorTimeframe.OneDay"/> the time half is padding, not data.</b> All 1254
    /// daily rows measured 2026-08-29 carried <c>00:00:00</c>. That is why this is a
    /// <see cref="LocalDateTime"/> and not the <see cref="Instant"/> that
    /// <see cref="IntradayBar.Timestamp"/> uses: binding a daily row through the Eastern converter would
    /// assert that the bar opened at midnight in New York, which is false, and a daily bar is not an instant
    /// at all. One property honestly serving seven timeframes has to decline to name a zone.</para></summary>
    public LocalDateTime? Timestamp { get; init; }

    /// <summary>The bar's opening price.</summary>
    public decimal? Open { get; init; }

    /// <summary>The bar's highest price.</summary>
    public decimal? High { get; init; }

    /// <summary>The bar's lowest price.</summary>
    public decimal? Low { get; init; }

    /// <summary>The bar's closing price.</summary>
    public decimal? Close { get; init; }

    /// <summary>Shares or contracts traded in the bar.
    ///
    /// <para><see cref="decimal"/>, not <see cref="long"/>, and BTCUSD is why. This SDK types volume both ways
    /// deliberately — <see cref="EndOfDayBar.Volume"/> is <see cref="long"/> because daily equity bars showed
    /// no fractions, while <see cref="IntradayBar.Volume"/> is <see cref="decimal"/> because intraday bars
    /// did. This endpoint serves both from one shape, and the daily case is not safe either: measured
    /// 2026-08-29, BTCUSD carried <b>75 fractional volumes across 1825 daily rows</b>. Rounding to
    /// <see cref="long"/> would invent precision FMP did not send.</para></summary>
    public decimal? Volume { get; init; }

    /// <summary>Which indicator <see cref="Value"/> holds.
    ///
    /// <para><b>Resolved from the column that arrived</b>, not stamped from the argument that was sent. If FMP
    /// ever answers a column other than the one requested, this reports what came back rather than
    /// mislabelling it.</para>
    ///
    /// <para>Not nullable: the column must be present for the row to parse at all, so its absence is a parse
    /// failure rather than a missing value.</para></summary>
    public TechnicalIndicator Indicator { get; init; }

    /// <summary>The indicator's value for this bar.
    ///
    /// <para><b>What this means depends on the range that was requested</b>, for four of the nine indicators.
    /// See <see cref="TechnicalIndicator"/> for the measured error at each, and
    /// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> for how much history to prepend.</para>
    ///
    /// <para>Negative for <see cref="TechnicalIndicator.WilliamsR"/> — measured 2026-08-29 from −99.5844 to
    /// 0.0000.</para></summary>
    public decimal? Value { get; init; }
}
