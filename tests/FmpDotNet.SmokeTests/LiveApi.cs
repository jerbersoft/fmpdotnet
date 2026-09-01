using FmpDotNet.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

// One FmpClient for the whole assembly, and therefore ONE shared token bucket. Two DI containers would each
// build their own FmpBuckets and pace themselves independently, so the ordinary suite and the bulk suite
// running together would emit at twice the configured rate against an upstream that restricts keys for
// exactly that. Disabling cross-collection parallelism is the other half of the same guarantee: the bulk
// bucket refills at two calls a minute, and a parallel runner would spend the whole suite queued behind it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FmpDotNet.SmokeTests;

/// <summary>The live API's availability, and the single client that talks to it.</summary>
internal static class LiveApi
{
    /// <summary>The key. Absent means every live test skips — see <see cref="LiveFactAttribute"/>.</summary>
    public const string KeyVariable = "FMP_API_KEY";

    /// <summary>Opt-in for the <c>*-bulk</c> endpoints, which are excluded by default.
    ///
    /// <para>A second switch rather than a filter, because the reason bulk is separate is not that it is slow.
    /// FMP's own throttle message warns that "frequent abuse on this API Endpoint may result in restrictions
    /// placed on this API Key" — the cost of running it too often is the key, not the minutes. A scheduled run
    /// therefore sets only <see cref="KeyVariable"/>; a human deciding to spend the calls sets this too.</para></summary>
    public const string BulkVariable = "FMPDOTNET_SMOKE_BULK";

    /// <summary>Set to rewrite the recorded baselines instead of asserting against them.</summary>
    public const string UpdateVariable = "FMPDOTNET_UPDATE_SMOKE_BASELINE";

    public static string? ApiKey =>
        Environment.GetEnvironmentVariable(KeyVariable) is { Length: > 0 } key ? key : null;

    public static bool BulkEnabled =>
        Environment.GetEnvironmentVariable(BulkVariable) is { Length: > 0 };

    public static bool Updating =>
        Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 };

    private static readonly Lazy<ServiceProvider> Provider = new(() =>
        new ServiceCollection()
            .AddFmp(o =>
            {
                o.ApiKey = ApiKey ?? throw new InvalidOperationException(
                    $"{KeyVariable} is not set. A live test reached the client anyway, which means its skip "
                    + "guard is broken — no request has been sent.");

                // Above the 30 s default, and the reason is payload size rather than latency. Two ordinary
                // endpoints in the sweep answer the whole universe in one response: `stock-list` measured
                // 91,844 rows and `actively-trading-list` 68,869 on 2026-08-26. Those are not bulk endpoints
                // and are not throttled like them, but they are megabytes over whatever connection a runner
                // happens to have, and a timeout there would report drift that is really a slow download.
                o.RequestTimeout = Duration.FromMinutes(2);

                // BulkPerMinuteCap is deliberately left at its default of 2/min. That default is the SDK's own
                // measured answer to FMP's bulk throttle, and it is what makes "heavily rate-limited" true of
                // this suite without a line of pacing code here: the twenty bulk probes queue behind the
                // shared bucket and take about eight minutes wall-clock. Raising it for the sake of a faster
                // test run would be spending the key's standing to make a test finish sooner.
            })
            .BuildServiceProvider());

    /// <summary>The client every live test drives. Built on first use, so an assembly whose tests all skip
    /// never constructs it and never reads the key.</summary>
    public static FmpClient Client => Provider.Value.GetRequiredService<FmpClient>();

    // ---- argument values -----------------------------------------------------------------------------------

    /// <summary>Today in UTC, from the same clock the SDK uses.</summary>
    private static LocalDate Today => SystemClock.Instance.GetCurrentInstant().InUtc().Date;

    /// <summary>A weekday far enough back that whatever happened on it has been reported and settled.
    ///
    /// <para>Relative rather than fixed, because a hard-coded date is a smoke suite with an expiry: it keeps
    /// passing until the day FMP stops serving that far back, and then reports drift that is really age. A
    /// week is enough for an earnings date to have an actual against it, and the weekend step-back matters
    /// because both calendars answer a Saturday with an empty array — which this suite would read as an
    /// endpoint that went dark.</para></summary>
    public static LocalDate SettledWeekday => Today.PlusDays(-7).DayOfWeek switch
    {
        IsoDayOfWeek.Saturday => Today.PlusDays(-8),
        IsoDayOfWeek.Sunday => Today.PlusDays(-9),
        _ => Today.PlusDays(-7),
    };

    /// <summary>The start of the range every date-ranged probe asks for — ninety days before
    /// <see cref="SettledWeekday"/>.
    ///
    /// <para><b>Named rather than falling out of the <c>LocalDate</c> type case, because that case is silently
    /// wrong for anything sparse.</b> <c>Probe.Argument</c> dispatched <c>LocalDate</c> on type alone, so
    /// <c>from</c> and <c>to</c> both became <see cref="SettledWeekday"/> — a range of one day. Measured
    /// 2026-08-28, <c>sec-filings-search/symbol?symbol=AAPL</c> over a single settled weekday answered
    /// <b>0 rows</b>; the same call over ninety days answered <b>7</b>. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties, and matches that baseline every week thereafter — the silent
    /// green this suite exists to prevent.</para>
    ///
    /// <para>Ninety days rather than a year: it is enough for one issuer's Form 4s and 8-Ks to appear, and short
    /// enough that the whole-market probes it also widens — the earnings and economic calendars — stay a
    /// download rather than an outage.</para></summary>
    public static LocalDate RangeStart => SettledWeekday.PlusDays(-90);

    /// <summary>The start of the week-long window the five new date-ranged Calendar probes ask for.
    ///
    /// <para><b>Named rather than reusing <see cref="SettledWeekday"/> for both ends, because a one-day window
    /// is one quiet week away from an empty baseline on the sparsest of them.</b> Measured 2026-08-28 with
    /// <c>to=2026-08-21</c>: over a single day, <c>ipos-prospectus</c> answered <b>1 row</b>,
    /// <c>ipos-calendar</c> 5 and <c>splits-calendar</c> 12. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties and matches that baseline every week thereafter — the silent
    /// green this suite exists to prevent, and the same failure <see cref="Exchange"/> and <see cref="Cik"/>
    /// were named for.</para>
    ///
    /// <para>Over seven days the same five answered 1652, 40, 34, 764 and 8. Seven and not fourteen because of
    /// the other direction: <c>dividends-calendar</c> caps at 4000 rows and answered 3249 over a fortnight —
    /// 81% of the cap — against 1652 over a week. Since #49 <c>GetDividendsCalendarAsync</c> walks the cursor
    /// past that cap, so a fortnight is no longer truncated — it is one request more expensive, and seven days
    /// keeps this baseline to a single request.</para>
    ///
    /// <para>Not used for <c>GetEarningsCalendarAsync</c> or for the economic calendar; both measured a 7-day
    /// window as unsafe on their own endpoints and keep <see cref="SettledWeekday"/>.</para></summary>
    public static LocalDate CalendarWeekStart => SettledWeekday.PlusDays(-6);

    /// <summary>The window <c>GetIndicatorAsync</c> is probed over — <b>fixed dates, deliberately</b>, and
    /// one of two fixed date ranges in this file. See <see cref="CotRangeStart"/> for the other — frozen for
    /// a different upstream reason, and at a different date.
    ///
    /// <para>Every other range here is relative, because <see cref="SettledWeekday"/> records that "a
    /// hard-coded date is a smoke suite with an expiry". <b>On this endpoint the reasoning inverts: the data
    /// is what is frozen.</b> Measured 2026-08-29, every one of the 21 <see cref="EconomicIndicator"/> series
    /// that carries data stops between 2025-10-01 and 2025-11-26, and
    /// <c>name=GDP&amp;from=2026-05-23&amp;to=2026-08-21</c> — precisely the window
    /// <see cref="RangeStart"/> and <see cref="SettledWeekday"/> produce — answered a well-formed
    /// <b>empty array</b> at HTTP 200. A relative window records <c>outcome empty</c> on the day it is
    /// written and matches that baseline green forever, which is the failure
    /// <see cref="Exchange"/>, <see cref="Cik"/> and <see cref="FilerCik"/> were each named for.</para>
    ///
    /// <para>2025-09-01 … 2025-11-30 is ninety days and answered 1 row for <c>GDP</c>, 2 for <c>CPI</c> and 3
    /// for <c>federalFunds</c>. Ninety days rather than wider because width does not behave monotonically
    /// here — the 183-day window containing this one answered nothing, measured the same day.</para>
    ///
    /// <para><b>If this probe starts recording <c>outcome empty</c>, FMP has moved its data</b>, and the fix
    /// is to re-measure the series' extent and move this window — not to widen it.</para></summary>
    public static readonly LocalDate IndicatorRangeStart = new(2025, 9, 1);

    /// <summary>The end of <see cref="IndicatorRangeStart"/>'s window.</summary>
    public static readonly LocalDate IndicatorRangeEnd = new(2025, 11, 30);

    /// <summary>The window the holiday probe asks for — <b>fixed dates, deliberately</b>, and the third of
    /// three fixed ranges in this file. See <see cref="IndicatorRangeStart"/> and <see cref="CotRangeStart"/>
    /// for the other two, each frozen for a different reason.
    ///
    /// <para><b>Fixed because the calendar is SPARSE, not because the data stops.</b> That is what makes this
    /// one different from the other two. Measured 2026-08-30, <c>holidays-by-exchange</c> answers about 13
    /// rows a year for NASDAQ, so the ninety-day window <see cref="RangeStart"/> and
    /// <see cref="SettledWeekday"/> produce — 2026-05-23 to 2026-08-21 — contains exactly <b>three</b>
    /// holidays, and a quiet quarter takes it to zero. An endpoint that answers zero records
    /// <c>outcome empty</c> with no properties and matches that baseline every week thereafter, which is the
    /// silent green <see cref="Exchange"/> and <see cref="Cik"/> were each named for.</para>
    ///
    /// <para>2024-01-01 to 2026-12-31 answered <b>38 rows</b> on 2026-08-30, spanning full closures and the
    /// early-close shape, so the baseline records both of the record's two states rather than one.</para>
    ///
    /// <para><b>The <c>from</c> bound is EXCLUSIVE upstream</b> — measured 2026-08-30, the window is
    /// <c>(from, to]</c>, so a range whose bounds are equal spans no days. A relative range narrowed to a
    /// day here would not merely be empty by construction rather than by drift — since the equal-bounds
    /// guard landed it would throw before the request, failing the sweep on its own arithmetic rather than
    /// on anything FMP did.</para></summary>
    public static readonly LocalDate HolidayRangeStart = new(2024, 1, 1);

    /// <summary>The end of <see cref="HolidayRangeStart"/>'s window. Inclusive upstream.</summary>
    public static readonly LocalDate HolidayRangeEnd = new(2026, 12, 31);

    /// <summary>The contract the two dated COT probes ask for — <c>NG</c>, Natural Gas.
    ///
    /// <para><b>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>.</b> <c>Probe.Argument</c> maps any unrecognised string to
    /// <see cref="Symbol"/>, and the COT paths take a <b>futures contract code</b> — FMP's own <c>NG</c>,
    /// <c>ZC</c>, <c>EURGBP</c>, listed by <c>GetSymbolsAsync</c> — not an equity ticker.
    /// <c>symbol=AAPL</c> is not an error there; it is an empty array under HTTP 200.</para>
    ///
    /// <para><c>NG</c> because it answers on both paths at the same range: measured 2026-08-29,
    /// <c>?symbol=NG&amp;from=2024-01-01&amp;to=2024-03-31</c> returned 13 rows from
    /// <c>commitment-of-traders-report</c> and 13 from <c>commitment-of-traders-analysis</c>. Deliberately
    /// not one of the 14 contracts whose <c>Other</c> block is populated: those are the exception rather than
    /// the shape, and the baseline should record the common one.</para></summary>
    public const string CotContract = "NG";

    /// <summary>The start of the window the two dated COT probes ask for — <b>fixed dates</b>, like
    /// <see cref="IndicatorRangeStart"/> and for the same kind of reason.
    ///
    /// <para><b>The COT data on this key stops at 2024-02-27.</b> Measured 2026-08-29, every response from
    /// both dated paths covered 2024-01-02 to 2024-02-27 and nothing later — about two and a half years
    /// earlier. A range computed from today returns an empty array at HTTP 200, so a relative window records
    /// <c>outcome empty</c> on the day it is written and agrees with itself forever.</para>
    ///
    /// <para>2024-01-01 … 2024-03-31 is one quarter, which is the widest window that keeps the two paths
    /// <b>agreeing</b>: measured 2026-08-29 it answered 13 rows from each, while a six-month window answered
    /// 13 from <c>analysis</c> and 26 from <c>report</c>. Thirteen is <c>analysis</c>'s hard cap, so anything
    /// wider records two probes that look inconsistent for a reason that has nothing to do with
    /// drift.</para></summary>
    public static readonly LocalDate CotRangeStart = new(2024, 1, 1);

    /// <summary>The end of <see cref="CotRangeStart"/>'s window.</summary>
    public static readonly LocalDate CotRangeEnd = new(2024, 3, 31);

    /// <summary>The last fiscal year complete enough that every company has filed for it.
    ///
    /// <para>Also drives <c>Endpoints.EsgEndpoints.GetBenchmarkAsync</c>'s and
    /// <c>Endpoints.TranscriptsEndpoints.GetTranscriptAsync</c>'s <c>year</c>, added in #40. Of the two, the
    /// benchmark is the one worth watching: its own bare-call default is fiscal year 2023, a fixed value
    /// rather than a moving one, so asking it for the relative <see cref="SettledYear"/> is the one new probe
    /// that could roll out from under a benchmark FMP has not yet published. Measured 2026-08-29,
    /// <c>esg-benchmark?year=2025</c> still answered 622 rows.</para></summary>
    public static int SettledYear => Today.Year - 1;

    /// <summary>The fiscal quarter the five 13F probes and the transcript probe ask for, paired with
    /// <see cref="SettledYear"/>.
    ///
    /// <para><b>Q3 and not Q4, and the reason is the filing deadline rather than the data.</b> A 13F is due 45
    /// days after the quarter ends, so <see cref="SettledYear"/>'s Q4 is not filed until mid-February of the
    /// following year — and <see cref="SettledYear"/> is <c>Today.Year - 1</c>, which means a run in January
    /// would ask for a quarter nobody has filed yet and record <c>rows 0</c> as the baseline for all five
    /// paths. Q3 of <see cref="SettledYear"/> was due by 14 November of that year, so it is settled on every
    /// day this suite can run.</para>
    ///
    /// <para>Measured 2026-08-28 with <c>year=2025&amp;quarter=3</c>: <c>extract</c> answered 41 rows,
    /// <c>holder-industry-breakdown</c> 33, <c>extract-analytics/holder</c> 5 (the probe's <c>limit</c>),
    /// <c>symbol-positions-summary</c> 1 and <c>industry-summary</c> 394. The same five with
    /// <c>quarter=4</c> answered 42, 34, 5, 1 and 394 — Q4 is equally good in August and unsafe in
    /// January.</para>
    ///
    /// <para><b>It suits the transcript probe for an unrelated reason, and that is worth stating rather than
    /// relying on.</b> <c>GetTranscriptAsync</c> takes a year and a quarter, and measured 2026-08-29,
    /// <c>symbol=AAPL&amp;year=2025&amp;quarter=3</c> answered one row carrying a 46,487-character
    /// transcript. Q3 of <see cref="SettledYear"/> is held eight to ten months before this probe can run, so
    /// it is settled on every day of the year the way a 13F quarter is — but a symbol with no call that
    /// quarter would answer an empty array, so the pairing depends on AAPL as much as on the quarter.</para></summary>
    public const int SettledQuarter = 3;

    /// <summary>The symbol every per-symbol probe uses. One symbol, not a list: the suite is asserting that the
    /// SDK still reads FMP's shape, not that FMP's coverage is broad. AAPL because it files everything — a
    /// symbol with sparse fundamentals would record half the model as null and detect nothing when the other
    /// half stopped arriving.</summary>
    public const string Symbol = "AAPL";

    /// <summary>The second symbol the batch-quote probes use, so that a multi-symbol endpoint is actually probed
    /// with more than one.
    ///
    /// <para>A one-element list would still exercise the path and the shape, but it would not distinguish
    /// <c>batch-quote</c> from <c>quote</c> — and the failure worth catching here is FMP quietly narrowing a batch
    /// endpoint to a single row. MSFT for the same reason AAPL was chosen: it carries every field.</para></summary>
    public const string SecondSymbol = "MSFT";

    /// <summary>The fund every ETF and mutual-fund probe uses.
    ///
    /// <para><b>Named rather than falling out of the default string case, because the default is silently
    /// wrong here in the worst way.</b> <c>Probe.Argument</c> maps any unrecognised string to
    /// <see cref="Symbol"/>, and measured 2026-08-30 <c>AAPL</c> answers an <b>empty array at HTTP 200</b> on
    /// all four ETF-only paths and on <c>funds/disclosure-dates</c> — five of the nine endpoints in that group
    /// would record <c>outcome empty</c> as their healthy baseline and agree with themselves for ever. This is
    /// the same failure <see cref="Exchange"/> and <see cref="Industry"/> exist to prevent.</para>
    ///
    /// <para><b>QQQ was chosen by measurement, not by taste.</b> Of the ETFs probed 2026-08-30 it is the
    /// smallest that answers non-empty on <b>all eight</b> symbol paths: 30 rows on
    /// <c>etf/asset-exposure</c>, 8 on <c>etf/country-weightings</c>, 107 on <c>etf/holdings</c>, 1 on
    /// <c>etf/info</c>, 11 on <c>etf/sector-weightings</c>, 28 on <c>funds/disclosure-dates</c>, 87 on
    /// <c>funds/disclosure-holders-latest</c>, and 101 on <c>funds/disclosure</c> at
    /// <see cref="SettledYear"/>/<see cref="SettledQuarter"/>. That is roughly <b>124 KB</b> across the eight,
    /// against SPY's ~500 KB — and none of these paths can be narrowed, so payload size is the whole
    /// cost.</para></summary>
    public const string EtfSymbol = "QQQ";

    /// <summary>The pairs the crypto news search is probed with.
    ///
    /// <para><b>Named for the reason <see cref="EtfSymbol"/> and <see cref="CotContract"/> are.</b>
    /// <c>Probe.Argument</c> maps every <c>IEnumerable&lt;string&gt;</c> parameter to
    /// <see cref="Symbol"/> and <see cref="SecondSymbol"/> — AAPL and MSFT — and this path's vocabulary is
    /// the currency PAIR. Measured 2026-08-29, <c>news/crypto?symbols=BTC</c> (the coin) answers <b>0
    /// rows</b> at HTTP 200 while <c>BTCUSD</c> answers 250; an equity ticker is further from that
    /// vocabulary still. A zero-row answer records <c>outcome empty</c> as this endpoint's healthy baseline
    /// and matches it green for ever.</para>
    ///
    /// <para>Two pairs rather than one, so the batch shape is probed as a batch — and these two, because
    /// <c>symbols=BTCUSD,ETHUSD</c> was measured at 250 rows on 2026-08-29.</para></summary>
    public static readonly string[] CryptoPairs = ["BTCUSD", "ETHUSD"];

    /// <summary>The pairs the forex news search is probed with. Named for the reason
    /// <see cref="CryptoPairs"/> is; <c>symbols=EURUSD,USDJPY</c> was measured at 250 rows on
    /// 2026-08-29.</summary>
    public static readonly string[] ForexPairs = ["EURUSD", "USDJPY"];

    /// <summary>The exchange the whole-exchange probe asks for.
    ///
    /// <para><b>Named rather than falling out of the default string case, because the default is silently
    /// wrong.</b> <c>Probe.Argument</c> maps any string to <see cref="Symbol"/>, which would send
    /// <c>exchange=AAPL</c> — and <c>stable/batch-exchange-quote</c> answers an unknown exchange with an empty
    /// array and HTTP 200, not an error (measured 2026-08-27). The endpoint would have recorded `rows 0` as its
    /// baseline and agreed with itself every week thereafter, which is the same silent-green failure the
    /// baseline-recording guard exists to prevent — arriving through the argument synthesiser instead.</para></summary>
    public const string Exchange = "NASDAQ";

    /// <summary>The industry the Market Performance industry paths are probed with.
    ///
    /// <para><b>Named for the reason <see cref="Exchange"/> is.</b> <c>Probe.Argument</c> maps any unrecognised
    /// string to <see cref="Symbol"/>, and <c>industry=AAPL</c> answers an empty array with HTTP 200 — so
    /// without this constant four endpoints would record <c>outcome empty</c> as their healthy baseline and
    /// agree with themselves forever.</para>
    ///
    /// <para><b>And it has to be an industry FMP actually carries.</b> Measured 2026-08-29,
    /// <c>stable/available-industries</c> lists 159 names and only 139 appear in any snapshot on either NASDAQ
    /// or NYSE; <c>Banks</c>, <c>Asset Management</c> and eighteen others answer <c>[]</c> everywhere. Picking
    /// a documented-but-empty name would reproduce exactly the silent green this constant exists to prevent.
    /// <c>Steel</c> was measured to return rows on both the snapshot and the historical paths.</para></summary>
    public const string Industry = "Steel";

    /// <summary>Apple's SEC Central Index Key, for the <c>search-cik</c> probe.
    ///
    /// <para><b>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>.</b> <c>Probe.Argument</c> maps any unrecognised string to <see cref="Symbol"/>,
    /// which would send <c>cik=AAPL</c> — and every <c>search-*</c> endpoint answers an unrecognised identifier
    /// with an empty array and HTTP 200, not an error (measured 2026-08-27). The probe would record `rows 0` as
    /// the baseline and match it every week thereafter, reporting a healthy endpoint that has never been
    /// exercised.</para>
    ///
    /// <para>Given unpadded deliberately: the endpoint accepts either form and always answers with the padded
    /// one, so this also exercises that normalisation.</para></summary>
    public const string Cik = "320193";

    /// <summary>An institutional <b>filer's</b> Central Index Key, for the four <c>cik</c>-keyed 13F probes —
    /// Berkshire Hathaway, <c>0001067983</c>.
    ///
    /// <para><b>Distinct from <see cref="Cik"/>, and the distinction is the whole point.</b> That is Apple's
    /// CIK — an <i>issuer</i>. The 13F paths want the CIK of an institution that <i>files</i>. Measured
    /// 2026-08-28, Apple's <c>320193</c> answers <b>zero rows</b> on all four of
    /// <c>institutional-ownership/dates</c>, <c>/extract</c>, <c>/holder-industry-breakdown</c> and
    /// <c>/holder-performance-summary</c>, each with HTTP 200 rather than an error — so the sweep would have
    /// recorded <c>rows 0</c> as the baseline for four endpoints and matched it every week thereafter. The same
    /// silent green <see cref="Exchange"/>, <see cref="Cik"/> and <see cref="AcquirerNameQuery"/> were named
    /// for.</para>
    ///
    /// <para>Berkshire's <c>0001067983</c> answers 53, 41, 33 and 53 rows against those four, paired with
    /// <see cref="SettledYear"/> and <see cref="SettledQuarter"/>. Given padded, because that is the form FMP
    /// returns and the endpoint accepts either.</para></summary>
    public const string FilerCik = "0001067983";

    /// <summary>An insider's Central Index Key, for <c>insider-trading/search</c>'s <c>reportingCik</c> —
    /// <c>1780525</c>, Apple's SVP and General Counsel.
    ///
    /// <para><b>Chosen to agree with <see cref="Symbol"/>, <see cref="Cik"/> and
    /// <see cref="InsiderTransactionCode"/>.</b> <c>Probe</c> supplies every parameter including the optional
    /// ones, and the four discriminators intersect — so one value that contradicts the others empties the
    /// result. Measured 2026-08-28, the four together answer 3 rows with all sixteen fields populated.</para>
    ///
    /// <para><b>Without this case the parameter falls through to <see cref="Symbol"/></b>, and
    /// <c>reportingCik=AAPL</c> answers <c>[]</c> with HTTP 200 — the silent green this suite exists to
    /// catch.</para>
    ///
    /// <para>Given unpadded deliberately, for the reason on <see cref="Cik"/>: both forms work, measured
    /// 2026-08-28 with byte-identical responses, so this also exercises the normalisation.</para></summary>
    public const string InsiderReportingCik = "1780525";

    /// <summary>The SEC transaction code the insider search is probed with — <c>"S-Sale"</c>.
    ///
    /// <para>Named for the reason on <see cref="Exchange"/>: unrecognised, <c>transactionType</c> would become
    /// <c>"AAPL"</c>, and measured 2026-08-28 that alone empties the response even when the other three
    /// discriminators are right.</para>
    ///
    /// <para><c>"S-Sale"</c> rather than any of the other seventeen because it is one
    /// <see cref="InsiderReportingCik"/> actually filed against <see cref="Symbol"/> — the four have to
    /// intersect. A code from <c>insider-trading-transaction-type</c>, so the sweep asks with a value that
    /// endpoint vouches for.</para></summary>
    public const string InsiderTransactionCode = "S-Sale";

    /// <summary>The name <c>insider-trading/reporting-name</c> is probed with — <c>"Apple"</c>.
    ///
    /// <para><b>It works, and it works by accident.</b> That endpoint matches a prefix of a surname-first
    /// person's name, so <c>"Apple"</c> hits <c>"Apple Allan Victor"</c>, <c>"Applebach Richard Jr"</c> and
    /// <c>"Applebaum Michelle Galanter"</c> — 20 rows measured 2026-08-28. It has nothing to do with the
    /// company, and a reader who assumes otherwise will assume the endpoint searches issuers.</para>
    ///
    /// <para><b>Its own constant rather than an alias of <see cref="AcquirerNameQuery"/>, for the reason
    /// <see cref="CompanyNameQuery"/> gives:</b> two endpoints spelling the same word must not share one
    /// constant, because a future change to one would silently move the other. Three constants now hold
    /// <c>"Apple"</c> for three different endpoints, and that repetition is the point.</para></summary>
    public const string InsiderNameQuery = "Apple";

    /// <summary>A Senator's Bioguide identifier for the three <c>senateID</c>-keyed Senate probes —
    /// Bill Hagerty, <c>H000601</c>.
    ///
    /// <para><b>Chosen because he answers on all three.</b> Measured 2026-08-29 he returns 57 rows from
    /// <c>senate-trades-by-id</c>, 250 from <c>senate-net-worth</c> and six from
    /// <c>senate-net-worth-aggregated</c>. The same silent green <see cref="FilerCik"/> was named for applies
    /// here with a sharper edge: the two <c>-by-id</c> paths answer 200 with the WRONG member's data rather
    /// than zero rows when the parameter does not reach them.</para>
    ///
    /// <para><b>A Senator cannot probe the House path.</b> <c>house-trades-by-id</c> takes the same
    /// <c>senateID</c> parameter and this value answers zero rows on it, because Hagerty has never sat in the
    /// House. See <see cref="HouseMemberId"/>.</para></summary>
    public const string SenateId = "H000601";

    /// <summary>A Representative's Bioguide identifier for <c>house-trades-by-id</c> — Nancy Pelosi,
    /// <c>P000197</c>.
    ///
    /// <para><b>A separate constant because a member sits in one chamber.</b> The parameter is spelled
    /// <c>senateID</c> on the House path too — FMP's naming — but <see cref="SenateId"/> answered
    /// <c>rows 0</c> against it when first recorded on 2026-08-29, which is exactly the <c>outcome empty</c>
    /// baseline that then matches itself green forever. Measured the same day, <c>P000197</c> answers 100
    /// rows, all of them hers.</para></summary>
    public const string HouseMemberId = "P000197";

    /// <summary>A surname for <c>house-trades-by-name</c> — <c>Pelosi</c>, the member
    /// <see cref="HouseMemberId"/> identifies.
    ///
    /// <para>Measured 2026-08-29, answers 142 rows. Its own constant rather than
    /// <see cref="InsiderNameQuery"/>'s, so a change to the insider probe cannot silently move this one — the
    /// same separation that constant was created for.</para>
    ///
    /// <para><b>Deliberately not a member with no disclosures.</b> <c>Nunn</c> answers zero rows and is a
    /// sitting Representative, so it would record <c>rows 0</c> as the baseline and match it green
    /// forever.</para></summary>
    public const string HouseNameQuery = "Pelosi";

    /// <summary>A surname for <c>senate-trades-by-name</c> — <c>Hagerty</c>, the member
    /// <see cref="SenateId"/> identifies.
    ///
    /// <para>Measured 2026-08-29, answers 57 rows — the same 57 <see cref="SenateId"/> reaches by identifier,
    /// so the two Senate probes agree on their subject the way the House pair does.</para>
    ///
    /// <para><b>Not <see cref="HouseNameQuery"/>.</b> Pelosi is a Representative and answers zero rows here;
    /// that was the recorded <c>outcome empty</c> this constant was added to fix.</para></summary>
    public const string SenateNameQuery = "Hagerty";

    /// <summary>Apple's CUSIP, for the <c>search-cusip</c> probe. Named for the reason on <see cref="Cik"/>.</summary>
    public const string Cusip = "037833100";

    /// <summary>Apple's ISIN, for the <c>search-isin</c> probe. Named for the reason on <see cref="Cik"/>.</summary>
    public const string Isin = "US0378331005";

    /// <summary>The text the two query-shaped searches are probed with.
    ///
    /// <para><see cref="Symbol"/> itself rather than a company name, because <c>search-symbol</c> matches tickers
    /// and <c>search-name</c> matches names — and "AAPL" is measured to return rows from both, 7 and 1
    /// respectively on 2026-08-27. A value that worked on only one of them would leave the other recording an
    /// empty baseline.</para></summary>
    public const string SearchQuery = Symbol;

    /// <summary>The company name the M&amp;A search is probed with.
    ///
    /// <para><b>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>.</b> <c>Probe.Argument</c> maps any unrecognised string to <see cref="Symbol"/>,
    /// which would send <c>name=AAPL</c> — and <c>mergers-acquisitions-search</c> matches company names, not
    /// tickers, answering an unmatched name with an empty array and HTTP 200 rather than an error (measured
    /// 2026-08-27). The probe would record <c>rows 0</c> as the baseline and match it every week
    /// thereafter.</para>
    ///
    /// <para><c>"Apple"</c> rather than <c>"Bank"</c>: measured 2026-08-27, <c>Apple</c> answers 3 rows in
    /// which all nine fields are populated at least once — everything the baseline records — while <c>Bank</c>
    /// answers 233 rows and an 84 KB response for the same information.</para></summary>
    public const string AcquirerNameQuery = "Apple";

    /// <summary>The name the SEC company search is probed with.
    ///
    /// <para>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>: <c>sec-filings-company-search/name</c> matches company names, so
    /// <c>company=AAPL</c> would answer an empty array with HTTP 200 and record <c>rows 0</c> as the baseline.
    /// <c>"Apple"</c> answered 52 rows on 2026-08-28. Separate from <see cref="AcquirerNameQuery"/> although
    /// both spell the same word — they are probing different endpoints, and a future change to one must not
    /// silently move the other.</para></summary>
    public const string CompanyNameQuery = "Apple";

    /// <summary>The word the fund share-class search is probed with.
    ///
    /// <para>Named rather than falling out of the default string case, for the reason recorded on
    /// <see cref="Exchange"/>: <c>funds/disclosure-holders-search</c> matches a <b>registrant's</b> name, so
    /// <c>name=Apple</c> — which is what <see cref="AcquirerNameQuery"/> would supply — answers an empty array
    /// with HTTP 200.</para>
    ///
    /// <para><b>One whole word, because that is all this path matches.</b> Measured 2026-08-30 the match is
    /// case-insensitive, whole-word and single-word: <c>Vanguard</c> answered 548 rows while <c>Vangua</c> and
    /// <c>Vanguard Group</c> each answered <b>0</b>. <c>"Schwab"</c> answered <b>211 rows, 90 KB</b> — chosen
    /// over <c>Vanguard</c> and <c>Fidelity</c> (2,379 rows, 1.0 MB) because the sweep measures shape rather
    /// than depth, and over <c>Trust</c>, which answers 66,065 rows and 27.4 MB and cannot be
    /// narrowed.</para>
    ///
    /// <para>Its own constant rather than reusing <see cref="CompanyNameQuery"/> or
    /// <see cref="AcquirerNameQuery"/>, for the reason those two are separate from each other: a change to one
    /// probe must not silently move another.</para></summary>
    public const string FundNameQuery = "Schwab";

    /// <summary>The EDGAR form type the form-type filing search is probed with.
    ///
    /// <para><c>"10-K"</c> because it is filed by every domestic issuer, so any window of ninety days contains
    /// some — measured 2026-08-28, a recent ninety-day window filled the default page of 100 rows. An
    /// unrecognised form type answers an empty array with HTTP 200 rather than an error.</para></summary>
    public const string FormType = "10-K";

    /// <summary>The SIC code the classification search is probed with — <c>"3571"</c>, "ELECTRONIC COMPUTERS".
    ///
    /// <para>Chosen to agree with <see cref="Symbol"/> and <see cref="Cik"/>: <c>industry-classification-search</c>
    /// takes all three and narrows on them, and measured 2026-08-28,
    /// <c>symbol=AAPL&amp;cik=320193&amp;sicCode=3571</c> answered one row. A SIC code that contradicted the
    /// other two would answer nothing and record an empty baseline.</para>
    ///
    /// <para>Four characters, which is how the classification paths spell it —
    /// <c>standard-industrial-classification-list</c> strips the leading zero on codes below 1000 and this one
    /// has none, so the two agree here.</para></summary>
    public const string SicCode = "3571";

    /// <summary>The period used for the technical-indicator sweep. Ten because that is the period the
    /// design's measurement tables are keyed to, so a baseline diff can be read against them.</summary>
    public const int IndicatorPeriodLength = 10;

    /// <summary>The CIK the Regulation Crowdfunding paths are probed with — <c>"0002010670"</c>, Finlete
    /// Funding, Inc.
    ///
    /// <para><b>Named rather than falling out of the <c>cik</c> default, for the reason recorded on
    /// <see cref="Exchange"/>, and the measurement here is unusually stark.</b> <c>Probe.Argument</c> maps an
    /// unrecognised <c>cik</c> to <see cref="Cik"/> — Apple's issuer CIK — and measured 2026-08-31 that
    /// answers <b>0 rows at HTTP 200</b> on <c>crowdfunding-offerings</c>, as does
    /// <see cref="FilerCik"/>. Both would record <c>outcome empty</c> as this endpoint's healthy baseline
    /// and match it every week thereafter.</para>
    ///
    /// <para><b>Form C and Form D filers are disjoint populations</b>, measured in both directions on
    /// 2026-08-31, which is why this constant and <see cref="FundraisingCik"/> are separate: each answers
    /// zero rows on the other's paths.</para>
    ///
    /// <para>Chosen as the filer with the <b>most filings — 12</b> — in a 1,000-row latest window rather
    /// than the first one to hand, so the constant does not rest on a single filing that could be amended
    /// away. It answered <b>48 rows</b> on 2026-08-31.</para></summary>
    public const string CrowdfundingCik = "0002010670";

    /// <summary>The name the Regulation Crowdfunding search is probed with — <c>"Finlete"</c>.
    ///
    /// <para><b>Named for the reason <see cref="Exchange"/> is.</b> <c>Probe.Argument</c> maps an
    /// unrecognised <c>name</c> to <see cref="AcquirerNameQuery"/>, and measured 2026-08-31
    /// <c>crowdfunding-offerings-search?name=Apple</c> answers <b>0 rows</b> with HTTP 200.</para>
    ///
    /// <para>Chosen to agree with <see cref="CrowdfundingCik"/> — it is the same issuer — so a diff on one
    /// can be read against the other. It answered <b>4 rows</b> on 2026-08-31. <b>Do not shorten it:</b> this
    /// endpoint's matching rule is not known and intermediate-length queries return nothing —
    /// <c>Well</c> and <c>Wellness</c> both answer 44 rows while <c>Welln</c> answers zero.</para></summary>
    public const string CrowdfundingNameQuery = "Finlete";

    /// <summary>The CIK the Regulation D paths are probed with — <c>"0001617426"</c>, Schutt Private
    /// Investment Fund, LP.
    ///
    /// <para><b>Its own constant, separate from <see cref="CrowdfundingCik"/>, because the two corpora are
    /// disjoint</b> — measured 2026-08-31, this CIK answers <b>0 rows</b> on <c>crowdfunding-offerings</c>
    /// and <see cref="CrowdfundingCik"/> answers 0 rows on <c>fundraising</c>. And separate from
    /// <see cref="Cik"/> and <see cref="FilerCik"/>, both of which answer 0 rows here.</para>
    ///
    /// <para>It answered <b>14 rows</b> on 2026-08-31, spanning 2013-2026 — a filer with enough history that
    /// a single amendment cannot empty it.</para></summary>
    public const string FundraisingCik = "0001617426";

    /// <summary>The name the Regulation D search is probed with — <c>"Apple"</c>, which answered <b>59
    /// rows</b> on 2026-08-31.
    ///
    /// <para><b>Its own constant although it holds the same literal as <see cref="AcquirerNameQuery"/> and
    /// <see cref="CompanyNameQuery"/>.</b> The value coincides by measurement, not because the three paths
    /// share a vocabulary — and a future change to one probe must not silently move the other two. That is
    /// the same reasoning those two constants carry about each other.</para>
    ///
    /// <para>Unlike its crowdfunding sibling this path <i>does</i> behave like a case-insensitive prefix
    /// match, measured 2026-08-31 — <c>Ap</c> 421, <c>App</c> 256, <c>Apple</c> 59, <c>pple</c> 0 — so the
    /// value is not fragile here for the reason <see cref="CrowdfundingNameQuery"/> is.</para></summary>
    public const string FundraisingNameQuery = "Apple";
}
