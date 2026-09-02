using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's technical-indicator surface — nine indicators over one price series.
///
/// <para><b>Nine paths, one method.</b> Every path returns the same six price fields plus one column named
/// after the path segment, measured 2026-08-29 across 88 non-empty responses carrying exactly nine distinct
/// key tuples. <see cref="TechnicalIndicator"/> selects the path;
/// <see cref="Models.TechnicalIndicatorBar"/> is the shape they share.</para>
///
/// <para><b>This facade computes nothing.</b> It reports what FMP returned, including where that is wrong —
/// see the warm-up note on <see cref="GetAsync"/>.</para>
///
/// <para><b>Plan tier — Starter, second-hand.</b> fmpsdk 20260824.0, the independent client this SDK is cross-checked
/// against, recorded every path in this class as 402 on free, needing Starter or higher; it gives no date for that
/// beyond its release, 2026-08-24. Not verified here: every path answered 200 on the Ultimate key this SDK is
/// measured with (2026-09-02), which says nothing about the plans below it. A dated observation, not a contract —
/// catch <see cref="FmpPlanRestrictedException"/> rather than gating on it.</para></summary>
public sealed class TechnicalIndicatorsEndpoints(FmpTransport transport)
{
    /// <summary>One indicator's series for one symbol —
    /// <c>stable/technical-indicators/{indicator}</c>.
    ///
    /// <para><b>The value FMP returns for a given date depends on the range you ask for.</b> Measured
    /// 2026-08-29 on AAPL at <c>periodLength=10</c>, a ten-row window compared against the same dates in the
    /// 1254-row series: <see cref="TechnicalIndicator.Sma"/>, <see cref="TechnicalIndicator.Wma"/>,
    /// <see cref="TechnicalIndicator.WilliamsR"/>, <see cref="TechnicalIndicator.StandardDeviation"/> and
    /// <see cref="TechnicalIndicator.Rsi"/> were exact on every row, while
    /// <see cref="TechnicalIndicator.Adx"/> was out by <b>264% on the newest row and 277% at worst</b>. The
    /// four that drift warm up from the start of the returned range rather than from a buffer of prior data.
    /// <see cref="TechnicalIndicatorExtensions.SuggestedWarmUpBars"/> says how much history to prepend;
    /// this method does not prepend it for you.</para>
    ///
    /// <para><b>A range wider than the timeframe's ceiling is silently truncated.</b> Each
    /// <see cref="TechnicalIndicatorTimeframe"/> member records its own measured window. On
    /// <see cref="TechnicalIndicatorTimeframe.OneDay"/> the ceiling is a span of about <b>five years anchored
    /// at <paramref name="to"/></b>: measured 2026-08-29, <c>2010-01-01 … 2020-01-01</c> answered 1257 rows
    /// covering only 2015-01-05 onward, and <c>2010-01-01 … 2026-08-28</c> answered 1255 rows covering only
    /// 2021-08-30 onward. There is <b>no history floor</b> — <c>2010-01-01 … 2015-01-01</c> returned that
    /// range in full — so it is a span limit, and the half that vanishes is the older one.</para>
    ///
    /// <para><b>Not guarded, deliberately</b>, for the reason
    /// <see cref="EconomicsEndpoints.GetEconomicCalendarAsync"/> sets out: no row count distinguishes a
    /// truncated window from a genuinely short one. The honest check is positional — did
    /// <see cref="Models.TechnicalIndicatorBar.Timestamp"/> reach both ends of the range you asked
    /// for?</para>
    ///
    /// <para><b>Two more silent answers.</b> A wholly future range returns five years of the past — measured
    /// 2026-08-29, <c>2027-01-01 … 2027-06-01</c> answered byte-identically to a bare call. And a
    /// <paramref name="periodLength"/> longer than the available history is quietly satisfied with less:
    /// <c>periodLength=100000</c> against 1254 bars answered 1254 distinct non-null values, which are
    /// expanding-window averages rather than the average that was asked for. The SDK cannot know how many
    /// bars FMP holds for a symbol, so it sets no upper bound.</para>
    ///
    /// <para>An unknown symbol answers <b>HTTP 200 with an empty array</b>, measured 2026-08-29. Equities,
    /// ETFs, indices, forex and crypto all work; the row count follows the trading calendar, so BTCUSD
    /// returned 1825 daily rows over five years where AAPL returned 1254.</para>
    ///
    /// <para>Rows arrive <b>newest first</b> and are returned exactly as FMP sent them — unsorted,
    /// unfiltered, and not clamped to the requested range.</para></summary>
    /// <param name="symbol">The ticker, futures code or pair to ask about.</param>
    /// <param name="indicator">Which indicator to compute. Selects the path segment.</param>
    /// <param name="periodLength">The indicator's period, in bars. Must be 1 or greater.</param>
    /// <param name="timeframe">The bar size. Determines how far back the data reaches.</param>
    /// <param name="from">First calendar day of the range, inclusive. Omit for the timeframe's default
    /// window.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The bars in the range, newest first, truncated to the timeframe's ceiling. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="periodLength"/> is less than 1;
    /// <paramref name="to"/> is earlier than <paramref name="from"/>; or <paramref name="indicator"/> or
    /// <paramref name="timeframe"/> is not a declared member. All are checked before the request is sent:
    /// FMP answers a zero or negative period, and a backwards range, with HTTP 200 and a plausible body.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status — including the HTTP 404 carrying
    /// <c>[]</c> that an unrecognised indicator segment produces, and the HTTP 400 carrying
    /// <c>Invalid timeframe provided.</c> that an unrecognised timeframe produces.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<TechnicalIndicatorBar>> GetAsync(
        string symbol,
        TechnicalIndicator indicator,
        int periodLength,
        TechnicalIndicatorTimeframe timeframe,
        LocalDate? from = null,
        LocalDate? to = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfLessThan(periodLength, 1);
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest($"stable/technical-indicators/{indicator.ToPathSegment()}")
                .With("symbol", symbol)
                .With("periodLength", periodLength)
                .With("timeframe", timeframe.ToQueryValue())
                .With("from", from)
                .With("to", to),
            FmpJsonContext.Default.ListTechnicalIndicatorBar, ct);
    }
}
