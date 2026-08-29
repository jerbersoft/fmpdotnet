namespace FmpDotNet;

/// <summary>The bar size asked of <see cref="Endpoints.TechnicalIndicatorsEndpoints.GetAsync"/> on the
/// technical-indicator paths.
///
/// <para><b>Deliberately not <see cref="ChartInterval"/>, and the reason is measured.</b>
/// <see cref="OneDay"/> is valid here, while <c>stable/historical-chart/1day</c> answered HTTP 404 with the
/// body <c>[]</c> when measured on 2026-08-27. Sharing one enum would either drop the timeframe most callers
/// want, or hand <see cref="Endpoints.ChartEndpoints.GetIntradayAsync"/> a member that breaks it. The six
/// near-identical members are the price of a type whose validity does not depend on which method receives
/// it.</para>
///
/// <para>The two enums also fail differently. There the value is a path segment, so a wrong one is a 404
/// carrying <c>[]</c>. Here it is a <b>query value</b>, so a wrong one is <b>HTTP 400</b> with the body
/// <c>Invalid timeframe provided.</c> — 27 bytes of bare text under a <c>content-type: application/json</c>
/// that is a lie. Measured 2026-08-29 on <c>1week</c>, <c>1month</c> and <c>2hour</c>.</para>
///
/// <para><b>The reachable window depends on the timeframe and is not monotonic in the bar size.</b> Measured
/// 2026-08-29 with a bare call on AAPL at <c>periodLength=10</c>, each member's own summary records what came
/// back. <see cref="FifteenMinutes"/> reached back 51 days while <see cref="ThirtyMinutes"/> reached 28 — an
/// inversion that independently reproduces the one recorded on <see cref="ChartInterval"/> on 2026-08-27 (45
/// days against 30), two days earlier on a different endpoint. No explanation is offered because none was
/// established.</para></summary>
public enum TechnicalIndicatorTimeframe
{
    /// <summary>One-minute bars — wire <c>1min</c>. Measured 2026-08-29: 1170 rows spanning about
    /// <b>2 days</b>.</summary>
    OneMinute,

    /// <summary>Five-minute bars — wire <c>5min</c>. Measured 2026-08-29: 702 rows spanning about
    /// <b>10 days</b>.</summary>
    FiveMinutes,

    /// <summary>Fifteen-minute bars — wire <c>15min</c>. Measured 2026-08-29: 988 rows spanning about
    /// <b>51 days</b> — a wider window than <see cref="ThirtyMinutes"/>. See the note on
    /// <see cref="TechnicalIndicatorTimeframe"/>.</summary>
    FifteenMinutes,

    /// <summary>Thirty-minute bars — wire <c>30min</c>. Measured 2026-08-29: 273 rows spanning about
    /// <b>28 days</b> — narrower than <see cref="FifteenMinutes"/>.</summary>
    ThirtyMinutes,

    /// <summary>Hourly bars — wire <c>1hour</c>. Measured 2026-08-29: 441 rows spanning about
    /// <b>88 days</b>.</summary>
    OneHour,

    /// <summary>Four-hour bars — wire <c>4hour</c>. Measured 2026-08-29: 249 rows spanning about
    /// <b>178 days</b>.</summary>
    FourHours,

    /// <summary>Daily bars — wire <c>1day</c>. Measured 2026-08-29: 1254 rows spanning about <b>5 years</b>.
    ///
    /// <para>The one member with no counterpart on <see cref="ChartInterval"/>, and the reason these are two
    /// types. Daily rows carry <c>00:00:00</c> as their time — see the timestamp note on
    /// <see cref="Models.TechnicalIndicatorBar"/>.</para></summary>
    OneDay,
}

/// <summary>Conversions for <see cref="TechnicalIndicatorTimeframe"/>.</summary>
public static class TechnicalIndicatorTimeframeExtensions
{
    /// <summary>The value FMP expects in the <c>timeframe</c> query parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this TechnicalIndicatorTimeframe timeframe) => timeframe switch
    {
        TechnicalIndicatorTimeframe.OneMinute => "1min",
        TechnicalIndicatorTimeframe.FiveMinutes => "5min",
        TechnicalIndicatorTimeframe.FifteenMinutes => "15min",
        TechnicalIndicatorTimeframe.ThirtyMinutes => "30min",
        TechnicalIndicatorTimeframe.OneHour => "1hour",
        TechnicalIndicatorTimeframe.FourHours => "4hour",
        TechnicalIndicatorTimeframe.OneDay => "1day",
        _ => throw new ArgumentOutOfRangeException(
            nameof(timeframe), timeframe, "Not a known technical-indicator timeframe."),
    };
}
