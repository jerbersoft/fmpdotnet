using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Chart</c> group — daily and intraday price history for one symbol.
///
/// <para>Ten documented paths behind five methods: four daily variants, and one intraday method covering the six
/// bar sizes of <see cref="ChartInterval"/>.</para>
///
/// <para><b>Everything here truncates silently, and the two families do it differently.</b> Daily history is
/// capped at <see cref="MaxEndOfDayRows"/> rows and drops the <i>oldest</i> end; intraday history is capped by a
/// per-interval lookback window that ignores how much you asked for. Neither reports it: the status is 200 and the
/// array is well formed. The measurements are on each method, and the check that actually works is the same for
/// both — <b>compare the oldest row you got back against the <c>from</c> you asked for</b>.</para>
///
/// <para><b>One symbol per call.</b> Measured 2026-08-27, a comma-separated <c>symbol</c> answers an empty array
/// with HTTP 200 rather than an error, so a caller who batches by habit gets silence rather than a
/// complaint.</para>
///
/// <para>Every method here runs on the ordinary throttle. None of these are <c>*-bulk</c> paths, so a wide history
/// walk is a normal cost rather than a bulk download — but it is still a call per symbol per variant.</para></summary>
public sealed class ChartEndpoints(FmpTransport transport)
{
    /// <summary>The most daily rows <c>stable/historical-price-eod/*</c> will serve in one call, whatever range is
    /// asked for.
    ///
    /// <para>Measured 2026-08-27: <c>from=2006-08-26</c> and <c>from=1980-01-01</c> both answered exactly 5,000
    /// rows and both began at <b>2006-10-10</b> — the same answer for a twenty-year request and a forty-six-year
    /// one. The <c>to</c> end is honoured; <c>from</c> moves silently to whatever the cap allows.</para>
    ///
    /// <para><b>A row count equal to this constant is a warning, not a proof.</b> A legitimate 5,000-row answer
    /// exists — it is what a symbol with exactly that much history returns — so the SDK does not throw on it. It is
    /// still the cheapest signal available that a range was cut, which is why the number is public rather than
    /// buried in a doc comment.</para></summary>
    public const int MaxEndOfDayRows = 5000;

    /// <summary>Daily closes for one symbol — <c>stable/historical-price-eod/light</c>, the cheapest daily bar FMP
    /// serves.
    ///
    /// <para>Four fields per row: symbol, date, close and volume. Measured 2026-08-27, a five-year AAPL window
    /// answered 1,255 rows. Prices are split-adjusted and <b>not</b> dividend-adjusted — see
    /// <see cref="GetDividendAdjustedAsync"/> if that matters.</para>
    ///
    /// <para><b>Omitting the range does not mean "everything".</b> Measured 2026-08-27, a call with neither
    /// <paramref name="from"/> nor <paramref name="to"/> answered 1,253 rows reaching back about five years, not
    /// the twenty the endpoint holds. The default is a window like any other, just an undocumented one, which is
    /// why both parameters are required here rather than defaulted to null.</para>
    ///
    /// <para><b>Truncation is silent and drops the oldest end</b> — see <see cref="MaxEndOfDayRows"/> for the
    /// measurements. To detect it, compare the oldest row returned against <paramref name="from"/>: if it is well
    /// inside the range you asked for, narrow the window and ask again. Rows arrive newest first, so the oldest is
    /// the last one.</para></summary>
    /// <param name="symbol">One symbol. A comma-separated list answers an empty array, not an error.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows FMP returned, newest first, possibly truncated. Empty for an unknown symbol — measured
    /// 2026-08-27, an unknown symbol answers <c>[]</c> with HTTP 200 rather than a 404. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/> — see <see cref="GetIntradayAsync"/> for why this is checked rather than
    /// passed on.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<EndOfDayPrice>> GetEndOfDayAsync(
        string symbol, LocalDate from, LocalDate to, CancellationToken ct = default)
        => transport.GetListAsync(
            DailyRequest("light", symbol, from, to), FmpJsonContext.Default.ListEndOfDayPrice, ct);

    /// <summary>Daily OHLCV bars for one symbol — <c>stable/historical-price-eod/full</c>, with FMP's derived
    /// change, percentage change and VWAP.
    ///
    /// <para>Ten fields per row against the four of <see cref="GetEndOfDayAsync"/>, for the same rows over the
    /// same range. Prices are split-adjusted and <b>not</b> dividend-adjusted.</para>
    ///
    /// <para>The same silent truncation applies — see <see cref="MaxEndOfDayRows"/> — and so does the same
    /// check.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows FMP returned, newest first, possibly truncated. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EndOfDayBar>> GetEndOfDayFullAsync(
        string symbol, LocalDate from, LocalDate to, CancellationToken ct = default)
        => transport.GetListAsync(
            DailyRequest("full", symbol, from, to), FmpJsonContext.Default.ListEndOfDayBar, ct);

    /// <summary>Raw, as-traded daily prices for one symbol — <c>stable/historical-price-eod/non-split-adjusted</c>.
    ///
    /// <para><b>Named for what it returns rather than for its path, because the path reads backwards.</b>
    /// <c>non-split-adjusted</c> means <i>not adjusted for splits</i>, so these are the prices as they printed on
    /// the day. It does not mean "adjusted for things other than splits", which is how it reads first and is the
    /// opposite of the truth.</para>
    ///
    /// <para>Measured 2026-08-27, AAPL on 2020-08-28 — the session before its four-for-one split — answers 504.04
    /// open here against 126.01 from <see cref="GetEndOfDayFullAsync"/> and 122.12 from
    /// <see cref="GetDividendAdjustedAsync"/>. Exactly four times. <b>Volume differs four-fold too</b>, in the
    /// other direction.</para>
    ///
    /// <para><b>This returns the same shape as <see cref="GetDividendAdjustedAsync"/> and nothing on the row says
    /// which is which</b> — both arrive as <c>adjOpen</c>/<c>adjHigh</c>/<c>adjLow</c>/<c>adjClose</c>. See
    /// <see cref="AdjustedEndOfDayBar"/>, which carries the full comparison. If these are cached or stored, store
    /// which method produced them.</para>
    ///
    /// <para>Use this when you need to reconcile against a historical broker statement or a contemporaneous
    /// record, which quote the prices that actually traded. For return calculations use
    /// <see cref="GetDividendAdjustedAsync"/>.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows FMP returned, newest first, possibly truncated. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AdjustedEndOfDayBar>> GetUnadjustedAsync(
        string symbol, LocalDate from, LocalDate to, CancellationToken ct = default)
        => transport.GetListAsync(
            DailyRequest("non-split-adjusted", symbol, from, to),
            FmpJsonContext.Default.ListAdjustedEndOfDayBar, ct);

    /// <summary>Split- and dividend-adjusted daily prices for one symbol —
    /// <c>stable/historical-price-eod/dividend-adjusted</c>. The series to use for total-return calculations.
    ///
    /// <para>Measured 2026-08-27, AAPL on 2020-08-28 answers 122.12 open here against 126.01 from
    /// <see cref="GetEndOfDayFullAsync"/> — same split adjustment, and additionally back-adjusted for the
    /// dividends paid since.</para>
    ///
    /// <para><b>Shape-identical to <see cref="GetUnadjustedAsync"/>, meaning four times the value on the same
    /// session.</b> Nothing on the row distinguishes them; only the method called does. See
    /// <see cref="AdjustedEndOfDayBar"/>.</para></summary>
    /// <param name="symbol">One symbol.</param>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The rows FMP returned, newest first, possibly truncated. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<AdjustedEndOfDayBar>> GetDividendAdjustedAsync(
        string symbol, LocalDate from, LocalDate to, CancellationToken ct = default)
        => transport.GetListAsync(
            DailyRequest("dividend-adjusted", symbol, from, to),
            FmpJsonContext.Default.ListAdjustedEndOfDayBar, ct);

    /// <summary>Intraday bars for one symbol at one of six sizes — <c>stable/historical-chart/{interval}</c>.
    ///
    /// <para>One method for six paths, because the only thing that differs on the wire is a path segment and the
    /// row shape is identical across all of them. What is <i>not</i> identical is how far back each reaches, which
    /// is documented per member on <see cref="ChartInterval"/> and is the reason the interval is an enum.</para>
    ///
    /// <para><b>The lookback window is a hard limit that ignores <paramref name="from"/> entirely.</b> Measured
    /// 2026-08-27, asking <see cref="ChartInterval.OneMinute"/> for <c>2020-01-01 … 2026-08-26</c>, for
    /// <c>2026-07-26 …</c>, and for <c>2026-08-24 …</c> all returned the <b>same 1,169 rows</b> beginning
    /// 2026-08-24. Six and a half years, one month and three days are the same request as far as this endpoint is
    /// concerned. The other intervals behave the same way at their own depths — roughly 10 days for 5-minute,
    /// 45 for 15-minute, 30 for 30-minute, 90 for hourly and 180 for 4-hourly.</para>
    ///
    /// <para><b>Rows carry no symbol</b>, unlike every other list endpoint in this SDK — see
    /// <see cref="IntradayBar"/>. Bars are stamped with their <i>opening</i> time in Eastern wall clock, and the
    /// last bar of a session is short.</para>
    ///
    /// <para><b><paramref name="extended"/> widens each day to 04:00–19:59 and moves the hourly grids (#50).</b>
    /// Measured 2026-09-02 on AAPL's 2026-09-01 session: 1-minute answered 390 bars (09:30–15:59) plain and
    /// <b>960</b> with the flag — 330 pre-market from 04:00, the same 390, 240 post-market to 19:59 — the same six
    /// keys on every bar, no nulls, and a minute with no trade simply has no bar (the previous day gave 959). The
    /// window is unchanged: a wide range answered the same two days either way. On 1-, 5-, 15- and 30-minute the
    /// regular-session bars are <b>byte-identical</b> with and without the flag. On
    /// <see cref="ChartInterval.OneHour"/> and <see cref="ChartInterval.FourHours"/> they are <b>not</b>: the
    /// grid is anchored at the session's first bar, so hourly bars move from 09:30 … 15:30 (seven) to
    /// 04:00 … 19:00 (sixteen) and 4-hourly from 09:30 / 13:30 to 04:00 / 08:00 / 12:00 / 16:00, and a 09:30 bar
    /// does not exist in the extended answer. Do not join extended and plain answers at those two sizes.</para>
    ///
    /// <para><b><paramref name="nonadjusted"/> turns split adjustment off, on price and volume alike (#50).</b>
    /// Measured 2026-09-02 on MNST hourly across its 2:1 split of 2026-08-11: the 2026-08-04 09:30 bar answered
    /// <c>open 46.925 … close 46.52, volume 1379074</c> plain and <c>93.85 … 93.04, volume 689537</c> with the
    /// flag — prices exactly doubled, volume exactly halved — while a post-split bar was unchanged. The row count
    /// was the same either way. The two flags are independent: sent together they answered the extended grid at
    /// unadjusted prices. An explicit <c>false</c> on either answers byte for byte what omission answers, so this
    /// method sends neither name unless it is true.</para>
    ///
    /// <para><b>Why the backwards-range check is here rather than in a paragraph.</b> Measured 2026-08-27,
    /// <c>from=2026-08-26&amp;to=2026-08-24</c> on the intraday endpoints answers <b>390 well-formed rows dated
    /// 2026-08-24</b> — not an error, not an empty array, but a plausible session for the wrong end of the range.
    /// The daily endpoints answer <c>[]</c> for the same mistake. So the same transposition is silently wrong on
    /// one family and silently empty on the other, and neither tells the caller. Rejecting it before the request
    /// is sent costs nothing and is the only place the two can be made to behave alike.</para></summary>
    /// <param name="symbol">One symbol. A comma-separated list answers an empty array, not an error.</param>
    /// <param name="interval">The bar size. Each carries a different depth of history — see
    /// <see cref="ChartInterval"/>.</param>
    /// <param name="from">First calendar day of the range, inclusive. Silently ignored beyond the interval's
    /// lookback window.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="extended">Include pre-market (from 04:00) and post-market (to 19:59) bars. Sent as
    /// <c>extended=true</c> only when true. Re-anchors the hourly and 4-hourly grids — see above.</param>
    /// <param name="nonadjusted">Answer prices and volumes as traded, without split adjustment. Sent as
    /// <c>nonadjusted=true</c> only when true.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The bars FMP returned, newest first, truncated to the interval's window. Empty for an unknown
    /// symbol. Never null.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="interval"/> is not a declared member.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<IntradayBar>> GetIntradayAsync(
        string symbol, ChartInterval interval, LocalDate from, LocalDate to,
        bool extended = false, bool nonadjusted = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        DateRange.ThrowIfBackwards(from, to);

        // Sent only when true. Measured 2026-09-02: an explicit `extended=false` and an explicit
        // `nonadjusted=false` each answer byte for byte what omission answers, so false travels as nothing.
        return transport.GetListAsync(
            new FmpRequest($"stable/historical-chart/{interval.ToPathSegment()}")
                .With("symbol", symbol).With("from", from).With("to", to)
                .With("extended", extended ? true : (bool?)null)
                .With("nonadjusted", nonadjusted ? true : (bool?)null),
            FmpJsonContext.Default.ListIntradayBar, ct);
    }

    /// <summary>Builds a request for one of the four <c>historical-price-eod</c> variants.
    ///
    /// <para>The variant is a path segment rather than a query value, which is why it is interpolated here and why
    /// each caller passes a literal: there is no user input on this path, so nothing to escape, and a typo is a
    /// compile-time-adjacent mistake in one of four call sites rather than a runtime 404.</para></summary>
    private static FmpRequest DailyRequest(string variant, string symbol, LocalDate from, LocalDate to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        DateRange.ThrowIfBackwards(from, to);

        return new FmpRequest($"stable/historical-price-eod/{variant}")
            .With("symbol", symbol).With("from", from).With("to", to);
    }
}
