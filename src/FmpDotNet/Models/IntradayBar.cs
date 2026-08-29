using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One intraday bar from <c>stable/historical-chart/{interval}</c>, at any of the six sizes
/// <see cref="ChartInterval"/> offers.
///
/// <para>Measured against the live API on 2026-08-27: the six properties below are the whole response, identical
/// across all six intervals. A full 1-minute session for AAPL answered 390 bars running
/// <c>09:30</c> to <c>15:59</c>.</para>
///
/// <para><b>There is no symbol on an intraday row.</b> Every other list endpoint in this SDK carries one; this one
/// does not, and FMP sends nothing that identifies the security. A caller assembling several symbols into one
/// collection has to carry the symbol alongside the rows themselves, because it cannot be recovered from
/// them.</para>
///
/// <para><b>Bars are labelled by their opening time, and the session's last bar is short.</b> Measured 2026-08-26:
/// 1-minute bars run 09:30 through 15:59 — the 15:59 bar covering the final minute to the 16:00 close — and hourly
/// bars end at 15:30, a half-hour bar wearing an hourly label. So the timestamp is when the bar <i>started</i>,
/// the last bar of the day is not a full interval, and summing volume across a session works while assuming a
/// uniform bar duration does not.</para>
///
/// <para><b>One measured inconsistency at the session open, left recorded rather than smoothed.</b> Asking for a
/// single day (<c>from</c> and <c>to</c> the same date) answered 389 bars beginning at <c>09:31</c>, while a
/// multi-day range answered 390 beginning at <c>09:30</c>. The opening bar is therefore sometimes present and
/// sometimes not, depending on the shape of the request rather than on the market. No explanation for this was
/// established, so none is offered; a caller who needs the opening bar should ask for a range wider than the day
/// they want and filter, rather than trusting a single-day request to include it.</para>
///
/// <para>Rows arrive <b>newest first</b>, and the SDK does not re-sort them.</para></summary>
public sealed record IntradayBar
{
    /// <summary>When the bar <b>opened</b>, in <b>Eastern</b> wall clock converted to an <see cref="Instant"/>.
    ///
    /// <para>The wire form is <c>"2026-08-25 15:59:00"</c> — space-separated, not ISO-T, and carrying no offset,
    /// exactly like <c>acceptedDate</c> on the statement endpoints. The zone is established by the session
    /// boundaries rather than assumed: bars run 09:30 to 15:59 and stop, which is the US regular session in
    /// New York local time. Read as UTC they would place the market open at 05:30 ET.</para>
    ///
    /// <para>Read with <see cref="NullableEasternInstantJsonConverter"/> and deliberately <b>not</b> with
    /// <see cref="NullableFmpInstantJsonConverter"/>, the UTC converter the economic calendar uses. Both parse the
    /// identical string shape, so nothing in the payload and nothing in the compiler will object to the wrong
    /// choice — it simply shifts every bar by 4 or 5 hours. Converting back to a local zone is the caller's
    /// business and must go through tzdb, never arithmetic on an offset.</para>
    ///
    /// <para>Nullable so that one malformed stamp costs one field rather than the whole response. No null was
    /// observed in any capture.</para>
    ///
    /// <para><b>This is a deliberate divergence from <see cref="TechnicalIndicatorBar.Timestamp"/></b>, which
    /// is a <see cref="LocalDateTime"/>? asserting no zone rather than an <see cref="Instant"/> read as
    /// Eastern. That type also serves <see cref="TechnicalIndicatorTimeframe.OneDay"/>, where the time half is
    /// midnight padding rather than a real bar time — binding it through this Eastern converter would assert a
    /// daily bar opened at midnight in New York, which measured 2026-08-29 is false. One property honestly
    /// serving all seven of that type's timeframes has to decline to name a zone; this property, serving only
    /// the six intraday sizes that all carry a real bar time, does not have that problem.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? Timestamp { get; init; }

    /// <summary>The bar's opening price.</summary>
    [JsonPropertyName("open")] public decimal? Open { get; init; }

    /// <summary>The bar's low.
    ///
    /// <para>Declared here, second, because that is where it sits on the wire: intraday rows arrive as
    /// <c>open, low, high, close</c> — <b>low before high</b> — where the daily endpoints send
    /// <c>open, high, low, close</c>. The order does not affect deserialisation, which matches on name, but it
    /// does affect anyone reading a raw payload beside this file or hand-writing a fixture.</para></summary>
    [JsonPropertyName("low")] public decimal? Low { get; init; }

    /// <summary>The bar's high.</summary>
    [JsonPropertyName("high")] public decimal? High { get; init; }

    /// <summary>The bar's closing price.</summary>
    [JsonPropertyName("close")] public decimal? Close { get; init; }

    /// <summary>Shares traded within the bar. Split-adjusted, consistently with the prices.
    ///
    /// <para><b><see cref="decimal"/> rather than <see cref="long"/>, because intraday volume is genuinely
    /// fractional</b> — which is the one thing on this type that will stop a caller's deserialisation dead rather
    /// than quietly mislead them. Measured 2026-08-26: of AAPL's 390 one-minute bars, <b>64 carried a fractional
    /// volume</b>, including <c>588656.8568699993</c> on the 15:59 bar; MSFT carried 94 of 390. It is not one
    /// symbol and not one interval — 5-minute, 15-minute and hourly bars all showed it — and the recurring
    /// <c>.8568699993</c> tail across several of them suggests an aggregation artefact rather than real
    /// odd-lot arithmetic.</para>
    ///
    /// <para><b>The daily endpoints are different, and deliberately typed differently.</b> The same 502-session
    /// window on <c>historical-price-eod/light</c> and <c>/full</c> carried <b>zero</b> fractional volumes, so
    /// <see cref="EndOfDayBar.Volume"/> and <see cref="EndOfDayPrice.Volume"/> stay <see cref="long"/>. Rounding
    /// this one to match them would be inventing precision FMP did not send; widening those to match this one
    /// would suggest a fractionality that daily bars have never shown.</para></summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }
}
