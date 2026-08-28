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

    /// <summary>The last fiscal year complete enough that every company has filed for it.</summary>
    public static int SettledYear => Today.Year - 1;

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

    /// <summary>The exchange the whole-exchange probe asks for.
    ///
    /// <para><b>Named rather than falling out of the default string case, because the default is silently
    /// wrong.</b> <c>Probe.Argument</c> maps any string to <see cref="Symbol"/>, which would send
    /// <c>exchange=AAPL</c> — and <c>stable/batch-exchange-quote</c> answers an unknown exchange with an empty
    /// array and HTTP 200, not an error (measured 2026-08-27). The endpoint would have recorded `rows 0` as its
    /// baseline and agreed with itself every week thereafter, which is the same silent-green failure the
    /// baseline-recording guard exists to prevent — arriving through the argument synthesiser instead.</para></summary>
    public const string Exchange = "NASDAQ";

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
}
