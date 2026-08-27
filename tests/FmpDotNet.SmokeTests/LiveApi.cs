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
}
