using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's macroeconomic surface — the release calendar, indicator series, the Treasury yield curve and
/// country equity risk premia.
///
/// <para>Unlike the company endpoints, nothing here is keyed on a symbol. Every path is <b>global</b>: a
/// request is a date range, an indicator name, or nothing at all, and the answer covers every country FMP
/// tracks. Narrowing that is the caller's job, not the SDK's.</para>
///
/// <para><b>Three of the four paths silently return less than they were asked for, in three different
/// ways.</b> <see cref="GetEconomicCalendarAsync"/> truncates a wide window to fewer rows than the narrow
/// window inside it. <see cref="GetTreasuryRatesAsync"/> truncates to about three months, keeping the newest.
/// <see cref="GetIndicatorAsync"/> answers an empty array for windows the data does not cover — and, measured
/// 2026-08-29, the data covers nothing after 2025-11-26. Each method documents its own case; none of them is
/// guarded by a row count, for the reason <see cref="GetEconomicCalendarAsync"/> sets out.</para>
///
/// <para>Only <see cref="GetTreasuryRatesAsync"/> answered current data on 2026-08-29.
/// <see cref="GetMarketRiskPremiumsAsync"/> carries no dates at all, so its currency cannot be
/// checked.</para></summary>
public sealed class EconomicsEndpoints(FmpTransport transport)
{
    /// <summary>Every economic release FMP has scheduled or published between <paramref name="from"/> and
    /// <paramref name="to"/>, both ends inclusive, for every country — or for one.
    ///
    /// <para>Measured on 2026-08-26: <c>from=2026-08-25&amp;to=2026-09-01</c> answered 713 rows spanning 81
    /// countries, and the single day 2026-08-26 answered 78 across 19. Rows came back <b>newest first</b> in both,
    /// and both range ends were included — <c>from</c> returned rows dated 2026-08-25 and <c>to</c> returned rows
    /// dated 2026-09-01. There is no paging parameter, no country parameter and no impact parameter; a range is
    /// the entire query surface.</para>
    ///
    /// <para><b>"No country parameter" above was wrong (#51).</b> Measured 2026-09-01 and again 2026-09-02 over
    /// 2026-08-17…24: 529 rows unfiltered, <c>country=US</c> <b>74</b> with every row <c>US</c>,
    /// <c>country=DE</c> 9, <c>country=EU</c> 24. The match is exact and case-sensitive — <c>country=us</c>
    /// answers <b>0</b> — and it takes one value: <c>country=US,DE</c> answers 0 too, both at HTTP 200. The
    /// vocabulary is what <see cref="EconomicRelease.Country"/> carries, which is not ISO-3166 (<c>EU</c> and
    /// <c>UK</c> are in it), so the SDK sends the string as given and validates only what would be silently
    /// wrong: a blank and a comma. The window's truncation below is measured on the unfiltered call; a filter
    /// answers fewer rows and has not been measured to change the window it truncates at.</para>
    ///
    /// <para><b>Wide windows are silently truncated, and the SDK cannot page around it — which is now measured
    /// rather than assumed.</b> Re-probed 2026-09-01 (#46) because the same claim on
    /// <see cref="CalendarEndpoints.GetEarningsCalendarAsync"/> turned out to be false: there, <c>page</c> is a
    /// working cursor that had never been tried. Here it is not. <c>from=2025-01-01&amp;to=2025-12-31</c> answers
    /// 7301 rows, and <c>page=1</c>, <c>page=2</c> and <c>limit=10000</c> each answer the identical 7301 — same
    /// first and last row, 6741 shared <c>(date, event)</c> pairs against 6741 distinct ones. So this endpoint
    /// really does have a range as its entire query surface. This is the one thing on it that will cost a caller
    /// data without telling them. Measured against the live API:</para>
    /// <list type="table">
    ///   <listheader><term>range</term><description>rows returned</description></listheader>
    ///   <item><term>2026-08-01 … 2026-08-31 (1 month)</term><description>1855</description></item>
    ///   <item><term>2026-08-01 … 2026-10-31 (3 months)</term><description>4051</description></item>
    ///   <item><term>2026-08-01 … 2027-01-31 (6 months)</term><description><b>535</b> — fewer than the three-month
    ///     window it wholly contains</description></item>
    ///   <item><term>−3 months … +12 months</term><description><b>0</b></description></item>
    /// </list>
    /// <para>A six-month window returning less than a quarter of the three-month window inside it is not sparse
    /// data, it is truncation, and the widest window collapses to nothing at all. FMP reports none of this: the
    /// status is 200 and the array is well-formed. So <b>chunking is the caller's responsibility</b> — 30-day
    /// chunks are what the consumer application this SDK replaces uses, and the widest range verified intact here
    /// is one week.</para>
    ///
    /// <para><b>Do not guard this with a row count.</b> It is the obvious instinct and it is wrong on this
    /// endpoint specifically, because macro density varies enormously and legitimately across the calendar: the
    /// week of 2026-08-25 carries 713 rows while <c>from=2027-01-25&amp;to=2027-02-01</c> carries a complete and
    /// entirely correct <b>2</b>. Set a count threshold and it rejects that genuinely quiet week while happily
    /// accepting the truncated 535-row half-year. The honest test is <b>edge coverage</b> — did the returned rows
    /// actually reach both ends of the range you asked for? A caller has everything needed for it: the range they
    /// passed, and <see cref="EconomicRelease.Timestamp"/> on every row. If the earliest row is well inside
    /// <paramref name="from"/> or the latest well inside <paramref name="to"/>, narrow the window and ask again.
    /// (Note that "well inside" has to allow for real quiet days at the edges, and that the timestamps are UTC
    /// while the range bounds are calendar dates.) This is a different failure from
    /// <c>stable/earnings-calendar</c>, which truncates at a hard, testable 4000 rows; here there is no constant
    /// to compare against, which is exactly why the check has to be positional.</para>
    ///
    /// <para>Results are returned exactly as FMP sends them: unsorted, unfiltered, and not clamped to the
    /// requested range.</para></summary>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="country">One country code in FMP's spelling — <c>US</c>, <c>DE</c>, <c>EU</c> — or
    /// <see langword="null"/> for every country. Exact and case-sensitive upstream; sent as given.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The releases in the range, newest first as measured, or an empty list — never null.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>. Checked before the request is sent: FMP answers a backwards range with an empty
    /// array and HTTP 200, so an argument the caller has plainly got wrong would otherwise read as "no releases
    /// that week" and cost a call from the key's quota to say nothing.</exception>
    /// <exception cref="ArgumentException"><paramref name="country"/> is given but blank, or contains a comma.
    /// A blank goes out as no filter and answers every country; a list answers nothing, at HTTP 200 either
    /// way.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<EconomicRelease>> GetEconomicCalendarAsync(
        LocalDate from, LocalDate to, string? country = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);
        ThrowIfNotOneCountry(country);

        return await transport.GetListAsync(
            new FmpRequest("stable/economic-calendar").With("from", from).With("to", to).With("country", country),
            FmpJsonContext.Default.ListEconomicRelease, ct).ConfigureAwait(false);
    }

    /// <summary>Rejects a <c>country</c> FMP would answer wrongly at HTTP 200 — measured 2026-09-02 (#51),
    /// <c>country=US,DE</c> answers 0 rows and <c>country=</c> answers all of them. Nothing else is checked:
    /// the vocabulary is upstream's, it is not ISO-3166, and a client-side list would go stale.</summary>
    private static void ThrowIfNotOneCountry(string? country)
    {
        if (country is null) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        if (country.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "stable/economic-calendar takes one country. Measured 2026-09-02, country=US,DE answers an "
                + "empty array at HTTP 200. Call once per country, or omit it and filter EconomicRelease.Country.",
                nameof(country));
        }
    }

    /// <summary>One macroeconomic series, oldest observation last — <c>stable/economic-indicators</c>.
    ///
    /// <para><b>Read this before choosing a range, because the obvious range returns nothing.</b> Measured
    /// 2026-08-29, every one of the 21 series that carries data stops between 2025-10-01 and 2025-11-26 —
    /// about nine months before that date. A window computed from today therefore answers a well-formed
    /// <b>empty array</b> with HTTP 200: <c>name=GDP&amp;from=2026-05-23&amp;to=2026-08-21</c> returned no
    /// rows, while <c>from=2025-09-01&amp;to=2025-11-30</c> returned one. Nothing in the response says the
    /// window was outside the data.</para>
    ///
    /// <para><b>Widening the window can return fewer rows, and no width rule predicts it.</b> Measured
    /// 2026-08-29 on <c>name=GDP</c>: a 90-day window answered 1 row, the 183-day window containing it
    /// answered <b>0</b>, and a 335-day window answered 1. A ~90-day range over a span the data actually
    /// covers is the only shape measured to work every time; anything wider is worth checking rather than
    /// trusting.</para>
    ///
    /// <para><b>The check is positional, not a row count</b>, for the reason
    /// <see cref="GetEconomicCalendarAsync"/> sets out at length: these series are legitimately sparse —
    /// <see cref="EconomicIndicator.Gdp"/> is quarterly and a correct answer for a quarter is one row — so a
    /// threshold rejects real answers while accepting truncated ones. Compare
    /// <see cref="EconomicObservation.Date"/> against the range you asked for.</para>
    ///
    /// <para><b>Omitting the range is a different query, not a wider one.</b> Measured 2026-08-29 the bare
    /// call answered the newest ~3 months of the series — 61 rows on <c>inflationRate</c>, 1 on
    /// <c>GDP</c> — which is usually what a caller wants and is what the live smoke sweep would use if it
    /// could.</para>
    ///
    /// <para><b>No <c>limit</c> parameter, because FMP ignores it.</b> Measured 2026-08-29,
    /// <c>name=CPI&amp;limit=100</c> answered the same 2 rows as <c>name=CPI</c>, byte-identical.</para>
    ///
    /// <para>Two indicators answer an empty array on every call — see <see cref="EconomicIndicator.Inflation"/>
    /// and <see cref="EconomicIndicator.ThreeMonthCertificateOfDepositRate"/>. They are valid names with no
    /// data behind them.</para></summary>
    /// <param name="indicator">The series. An <see cref="EconomicIndicator"/> rather than a string because
    /// the name is case-sensitive and a wrong one answers HTTP 200 with a body that is not JSON.</param>
    /// <param name="from">First day of the range, inclusive. Omit for FMP's default window, which starts
    /// exactly one year back from today — it only looks like the ~3-month truncation
    /// <see cref="GetTreasuryRatesAsync"/> applies, because every series stops in late 2025.</param>
    /// <param name="to">Last day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The observations in the range, or an empty list — never null. An empty list means the window
    /// falls outside the data at least as often as it means the series is quiet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>, or <paramref name="indicator"/> is not a declared member.</exception>
    /// <exception cref="FmpApiException">FMP answered 200 with a body that is not JSON, which is how it
    /// reports an unrecognised name.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EconomicObservation>> GetIndicatorAsync(
        EconomicIndicator indicator, LocalDate? from = null, LocalDate? to = null,
        CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/economic-indicators")
                .With("name", indicator.ToQueryValue()).With("from", from).With("to", to),
            FmpJsonContext.Default.ListEconomicObservation, ct);
    }

    /// <summary>Every country's equity risk premium — <c>stable/market-risk-premium</c>.
    ///
    /// <para>A full download with no query surface: no country parameter, no date parameter, no paging.
    /// Measured 2026-08-29 it answered <b>192 rows</b>, reverse-alphabetically by country, with all four
    /// fields populated on every one.</para>
    ///
    /// <para><b>The rows carry no date.</b> There is no way to tell from a response when these premia were
    /// computed, and no historical series is offered, so this cannot be checked for staleness the way the
    /// dated paths on this facade can.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every country FMP publishes a premium for. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<MarketRiskPremium>> GetMarketRiskPremiumsAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/market-risk-premium"),
            FmpJsonContext.Default.ListMarketRiskPremium, ct);

    /// <summary>The US Treasury yield curve day by day, newest first — <c>stable/treasury-rates</c>.
    ///
    /// <para><b>Truncates to about three months, keeping the newest, and reports nothing.</b> Measured
    /// 2024 data on 2026-08-29: a one-month range answered 21 rows complete, a three-month range answered 61
    /// complete, and a <b>two-year</b> range answered 61 rows spanning only 2024-10-02 to 2024-12-31 — 21
    /// months silently missing under HTTP 200 and a well-formed array. A 90-day range measured the same day
    /// answered 62 rows, complete, which is how the limit is known to be a window rather than a row count: 61
    /// is simply the number of trading days in those two spans.</para>
    ///
    /// <para><b>Chunk by quarter and the SDK will not do it for you</b>, for the reason
    /// <see cref="GetEconomicCalendarAsync"/> sets out: this endpoint is dense and regular, so a row-count
    /// guard would work here and would still be the wrong shape to teach, since the sibling paths on this
    /// facade are sparse and it would be wrong on those. The honest check is the same one everywhere — did
    /// <see cref="TreasuryRate.Date"/> reach both ends of the range you asked for?</para>
    ///
    /// <para><b>The one current path in issue #40's group.</b> Measured 2026-08-29 the bare call answered
    /// 2026-05-29 through 2026-08-27.</para></summary>
    /// <param name="from">First day of the range, inclusive. Omit for the newest ~3 months.</param>
    /// <param name="to">Last day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>One row per trading day in the range, newest first, truncated to about three months. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<TreasuryRate>> GetTreasuryRatesAsync(
        LocalDate? from = null, LocalDate? to = null, CancellationToken ct = default)
    {
        DateRange.ThrowIfBackwards(from, to);

        return transport.GetListAsync(
            new FmpRequest("stable/treasury-rates").With("from", from).With("to", to),
            FmpJsonContext.Default.ListTreasuryRate, ct);
    }
}
