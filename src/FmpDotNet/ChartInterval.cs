namespace FmpDotNet;

/// <summary>The bar size asked of <see cref="Endpoints.ChartEndpoints.GetIntradayAsync"/>.
///
/// <para>Deliberately not a string. FMP spells these as path segments rather than query values, so a typo would
/// produce a 404 rather than an empty list — but the more useful reason is that <b>the choice of interval silently
/// changes how far back you can see</b>, and an enum gives each window a place to be documented. Asking for a year
/// of 1-minute bars is not a finer-grained version of asking for a year of 4-hour bars; it is a request that
/// quietly returns three days.</para>
///
/// <para><b>The windows below were measured on 2026-08-27</b> by asking each interval for
/// <c>2020-01-01 … 2026-08-26</c> and recording the oldest bar that came back. FMP documents none of this and
/// reports no truncation: the status is 200 and the array is well formed. See
/// <see cref="Endpoints.ChartEndpoints.GetIntradayAsync"/> for what a caller can do about it.</para>
///
/// <para><b>The windows are not monotonic in the bar size</b>, and that is measured rather than mistyped:
/// <see cref="FifteenMinutes"/> reached back about 45 days while <see cref="ThirtyMinutes"/> reached back about
/// 30. No explanation is offered here because none was established — inventing one would be worse than recording
/// the oddity. It does mean a caller who needs six weeks of history should reach for 15-minute bars rather than
/// assuming the coarser interval keeps more.</para></summary>
public enum ChartInterval
{
    /// <summary>One-minute bars, from <c>stable/historical-chart/1min</c>. Measured 2026-08-27: about
    /// <b>3 calendar days</b> of history (1169 bars), regardless of how much more is asked for.</summary>
    OneMinute,

    /// <summary>Five-minute bars, from <c>stable/historical-chart/5min</c>. Measured 2026-08-27: about
    /// <b>10 calendar days</b> (624 bars).</summary>
    FiveMinutes,

    /// <summary>Fifteen-minute bars, from <c>stable/historical-chart/15min</c>. Measured 2026-08-27: about
    /// <b>45 calendar days</b> (858 bars) — the widest window of any interval below an hour, and wider than
    /// <see cref="ThirtyMinutes"/>. See the note on <see cref="ChartInterval"/>.</summary>
    FifteenMinutes,

    /// <summary>Thirty-minute bars, from <c>stable/historical-chart/30min</c>. Measured 2026-08-27: about
    /// <b>30 calendar days</b> (286 bars) — narrower than <see cref="FifteenMinutes"/>.</summary>
    ThirtyMinutes,

    /// <summary>Hourly bars, from <c>stable/historical-chart/1hour</c>. Measured 2026-08-27: about
    /// <b>90 calendar days</b> (434 bars). The session's last bar is partial — 15:30 covers the half hour to the
    /// close.</summary>
    OneHour,

    /// <summary>Four-hour bars, from <c>stable/historical-chart/4hour</c>. Measured 2026-08-27: about
    /// <b>180 calendar days</b> (247 bars), the deepest intraday history FMP serves.</summary>
    FourHours,
}

/// <summary>Conversions for <see cref="ChartInterval"/>.</summary>
public static class ChartIntervalExtensions
{
    /// <summary>The segment FMP expects after <c>stable/historical-chart/</c>.
    ///
    /// <para>A path segment rather than a query value, which is why an unmapped member must throw rather than fall
    /// back to anything: an unrecognised interval reaches FMP as a path that does not exist and answers HTTP 404
    /// with the body <c>[]</c> — measured 2026-08-27 on <c>1day</c> and <c>2hour</c>. That is a success shape on
    /// a failure status, and it would surface as "this symbol has no intraday history" rather than as a bug in
    /// the SDK.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToPathSegment(this ChartInterval interval) => interval switch
    {
        ChartInterval.OneMinute => "1min",
        ChartInterval.FiveMinutes => "5min",
        ChartInterval.FifteenMinutes => "15min",
        ChartInterval.ThirtyMinutes => "30min",
        ChartInterval.OneHour => "1hour",
        ChartInterval.FourHours => "4hour",
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Not a known chart interval."),
    };
}
