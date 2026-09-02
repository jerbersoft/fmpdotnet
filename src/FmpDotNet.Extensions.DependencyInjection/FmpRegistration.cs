using FmpDotNet.Endpoints;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>The one wiring path. Every public entry point — <c>AddFmp</c> in each of its forms, the host-builder
/// sugar and <c>FmpClientFactory</c> — ends here, parameterised by registration name, so the handler order that
/// is contractual exists in exactly one place.
///
/// <para>A registration is a named pair of <c>HttpClient</c>s (see
/// <see cref="FmpServiceCollectionExtensions.StandardClientName"/>), named options validated under the same
/// name, and keyed <see cref="FmpTransport"/>, <see cref="FmpBulkTransport"/> and <see cref="FmpClient"/>
/// registrations under that name. The default registration — name <c>""</c> — additionally registers the
/// unkeyed transports and client, the endpoint groups, and <see cref="FmpBuckets"/> for compatibility.</para>
/// </summary>
internal static class FmpRegistration
{
    /// <summary>Keyed by registration name; present once the name's chain has been wired, so a second
    /// <c>AddFmp</c> for the same name re-configures its options and adds nothing else.</summary>
    private sealed class Wired;

    internal static IServiceCollection Register(IServiceCollection services, string name, Action<FmpOptions> configure)
    {
        services.AddOptions<FmpOptions>(name)
            .Configure(configure)
            // BaseUrl reaches `new Uri(...)` inside HttpClientFactory on first resolve, which throws a
            // UriFormatException with no mention of configuration. Rejecting it by name at startup is the point.
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl)
                           && Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                "Fmp:BaseUrl must be an absolute URI (e.g. https://financialmodelingprep.com).")
            // At 0 the reservoir never refills and the first Acquire blocks forever: calls hang rather than fail,
            // which is the worst of both.
            .Validate(o => o.PerMinuteCap > 0,
                "Fmp:PerMinuteCap must be > 0 — it is the shared token bucket's refill rate.")
            .Validate(o => o.BulkPerMinuteCap > 0,
                "Fmp:BulkPerMinuteCap must be > 0 — it is the bulk token bucket's refill rate.")
            .Validate(o => o.RequestTimeout > Duration.Zero,
                "Fmp:RequestTimeout must be > 0 — it bounds a single FMP HTTP attempt.")
            .Validate(o => o.BulkRequestTimeout > Duration.Zero,
                "Fmp:BulkRequestTimeout must be > 0 — it bounds a single bulk FMP HTTP attempt.")
            .Validate(o => o.MaxRetryAfter >= Duration.Zero,
                "Fmp:MaxRetryAfter must be >= 0 — it caps how long one 429 may hold the shared request budget.")
            // At 0 there is no attempt at all: the handler's loop would return nothing and the caller would meet a
            // failure the SDK never actually tried to produce. 1 is the "no retry" setting, and it is legal.
            .Validate(o => o.MaxAttempts >= 1,
                "Fmp:MaxAttempts must be >= 1 — it counts SENDS, not retries, so 1 means send once and do not retry.")
            .Validate(o => o.BulkMaxAttempts >= 1,
                "Fmp:BulkMaxAttempts must be >= 1 — it counts SENDS, not retries, so 1 means send once and do not "
                + "retry. 1 is the default for bulk.")
            // At 0 every backoff step is 0 and the jitter with it, so a retry sequence becomes an unpaced burst
            // against an upstream that is already failing.
            .Validate(o => o.RetryBaseDelay > Duration.Zero,
                "Fmp:RetryBaseDelay must be > 0 — it is the first step of the retry backoff, doubling per attempt.")
            // Unlike Fmp:MaxRetryAfter, which may be zero: that one holds the SHARED bucket and "hold it for
            // nothing" is a coherent choice, while a zero ceiling here would flatten every backoff to an
            // unpaced burst.
            .Validate(o => o.MaxRetryDelay > Duration.Zero,
                "Fmp:MaxRetryDelay must be > 0 — it caps one retry's wait, and at 0 every attempt fires immediately.")
            .ValidateOnStart();

        // The API key is deliberately NOT validated. An SDK cannot know whether its caller intends to make a
        // request; the host that does know should assert it.

        // Everything below this line is wired once per name. A second AddFmp for the same name has re-configured
        // its options above and is done: appending the chain again would put a retry inside a retry.
        if (services.Any(d => d.IsKeyedService && d.ServiceType == typeof(Wired) && Equals(d.ServiceKey, name)))
            return services;
        services.AddKeyedSingleton(name, new Wired());

        // NodaTime's clock, not TimeProvider — the SDK's time surface is NodaTime throughout, and a test
        // substitutes NodaTime.Testing.FakeClock here.
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        // One registry per container. Registrations sharing an API key share a reservoir pair through it.
        services.TryAddSingleton(sp => new FmpBucketRegistry(sp.GetRequiredService<ILogger<FmpBucketRegistry>>()));

        // The retry is added FIRST, which makes it the OUTERMOST handler, and that is the point rather than a
        // detail. FmpRateLimitHandlerBase acquires its token BEFORE delegating, so a retry placed inside it would
        // be reached after the single token had already been drawn and every attempt after the first would bypass
        // the reservoir entirely. Outside, each attempt re-acquires — and it is still outside the timeout, so
        // each attempt gets a fresh RequestTimeout rather than sharing one budget.
        // Explicit construction rather than AddHttpMessageHandler<T>: each link gets THIS registration's options,
        // and the throttle gets this registration's reservoir from the registry. Nothing is activated by reflection.
        Configure(services.AddHttpClient(FmpServiceCollectionExtensions.StandardClientName(name)), name)
            .AddHttpMessageHandler(sp => new FmpRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpTimeoutHandler(Options.Create(OptionsFor(sp, name))));

        // The developer cache is added FIRST, which makes it the OUTERMOST handler, and that placement is the
        // point rather than a detail: a replay must not consume a bulk token or start a timeout. A cache hit
        // therefore never reaches the rate limiter at all. It is inert unless
        // FmpOptions.DeveloperBulkCacheDirectory is set, so it costs a null check when it is off.
        // The retry sits INSIDE the cache here, unlike the ordinary client where it is outermost: a replay must
        // never be retried, because a cache hit cannot fail transiently and re-serving it would only multiply the
        // work. FmpOptions.BulkMaxAttempts defaults to 1, so this link is inert unless a caller opts in.
        Configure(services.AddHttpClient(FmpServiceCollectionExtensions.BulkClientName(name)), name)
            .AddHttpMessageHandler(sp => new FmpDeveloperBulkCacheHandler(
                Options.Create(OptionsFor(sp, name)), sp.GetRequiredService<ILogger<FmpDeveloperBulkCacheHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkTimeoutHandler(Options.Create(OptionsFor(sp, name))));

        // The transports and the client, keyed by registration name. The transports' constructors never learn
        // that names exist: each is handed IOptions carrying its own registration's values.
        services.TryAddKeyedTransient(name, (sp, _) => new FmpTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(FmpServiceCollectionExtensions.StandardClientName(name)),
            Options.Create(OptionsFor(sp, name))));
        services.TryAddKeyedTransient(name, (sp, _) => new FmpBulkTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(FmpServiceCollectionExtensions.BulkClientName(name)),
            Options.Create(OptionsFor(sp, name))));
        services.TryAddKeyedTransient(name, (sp, key) => new FmpClient(
            sp.GetRequiredKeyedService<FmpTransport>(key), sp.GetRequiredKeyedService<FmpBulkTransport>(key)));

        if (name.Length == 0) RegisterDefaultOnly(services);
        return services;
    }

    /// <summary>What only the default registration gets: the unkeyed transports and client, the endpoint groups,
    /// and <see cref="FmpBuckets"/> for compatibility.</summary>
    private static void RegisterDefaultOnly(IServiceCollection services)
    {
        var name = Options.DefaultName;

        // README:528 and :541 document GetRequiredService<FmpTransport>() and <FmpBulkTransport>() as the way to
        // reach an endpoint the SDK has not modelled. That escape hatch stays unkeyed.
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpTransport>(name));
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpBulkTransport>(name));
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpClient>(name));

        // COMPATIBILITY, and load-bearing for a test that cannot fail loudly without it. GetRequiredService<FmpBuckets>()
        // resolves to the SAME pair the default registration's handlers draw from, because the registry caches
        // per key. Every_retry_attempt_draws_its_own_token_because_the_retry_sits_outside_the_throttle asserts a
        // cross-handler property through this instance: that the reservoir it resolves is the one the retried
        // attempts drained. Drop this registration and that test would resolve a second, full reservoir and
        // silently assert nothing.
        services.TryAddSingleton(sp => BucketsFor(sp, name));

        // The endpoint groups, resolvable on their own for the default registration. Nothing in the repository or
        // the README resolves one directly, but removing these would be a silent break for a consumer who does.
        // Named registrations do not get them: 25 × N keyed registrations to save `client.Company` is a bad trade.
        services.TryAddTransient<CompanyEndpoints>();
        services.TryAddTransient<DirectoryEndpoints>();
        services.TryAddTransient<StatementEndpoints>();
        services.TryAddTransient<CalendarEndpoints>();
        services.TryAddTransient<AnalystEndpoints>();
        services.TryAddTransient<EconomicsEndpoints>();
        services.TryAddTransient<SearchEndpoints>();
        services.TryAddTransient<SecFilingsEndpoints>();
        services.TryAddTransient<InstitutionalOwnershipEndpoints>();
        services.TryAddTransient<InsiderTradesEndpoints>();
        services.TryAddTransient<CongressEndpoints>();
        services.TryAddTransient<TranscriptsEndpoints>();
        services.TryAddTransient<EsgEndpoints>();
        services.TryAddTransient<CotEndpoints>();
        services.TryAddTransient<QuoteEndpoints>();
        services.TryAddTransient<ChartEndpoints>();
        services.TryAddTransient<BulkEndpoints>();
        services.TryAddTransient<TechnicalIndicatorsEndpoints>();
        services.TryAddTransient<MarketPerformanceEndpoints>();
        services.TryAddTransient<EtfAndFundsEndpoints>();
        services.TryAddTransient<IndexesEndpoints>();
        services.TryAddTransient<MarketHoursEndpoints>();
        services.TryAddTransient<NewsEndpoints>();
        services.TryAddTransient<FundraisersEndpoints>();
        services.TryAddTransient<DiscountedCashFlowEndpoints>();
    }

    /// <summary>This registration's options. For the default registration this is exactly what
    /// <c>IOptions&lt;FmpOptions&gt;.Value</c> returns, validated the same way.</summary>
    private static FmpOptions OptionsFor(IServiceProvider sp, string name) =>
        sp.GetRequiredService<IOptionsMonitor<FmpOptions>>().Get(name);

    /// <summary>This registration's reservoir pair — shared with every other registration on the same API key.</summary>
    private static FmpBuckets BucketsFor(IServiceProvider sp, string name) =>
        sp.GetRequiredService<FmpBucketRegistry>().For(name, OptionsFor(sp, name));

    /// <summary>Everything both clients share.
    ///
    /// <para><c>Timeout.InfiniteTimeSpan</c> is a decision, not an omission. Timeouts belong to
    /// <see cref="FmpTimeoutHandlerBase"/> for two reasons the client-level knob cannot serve: it sits INSIDE the
    /// rate-limit handler, so a wait on the shared token bucket is not charged against the attempt; and it reports
    /// expiry as a <see cref="TimeoutException"/> rather than the <see cref="TaskCanceledException"/> HttpClient
    /// raises, which callers routinely mistake for a shutdown.</para>
    ///
    /// <para>Handler ORDER is contractual: the first added is outermost. The ordinary chain is retry → throttle →
    /// timeout → network and the bulk chain is developer cache → retry → throttle → timeout → network; the
    /// reasons are on each chain above. Swapping throttle and timeout puts the throttle wait back inside the
    /// deadline, which is the coupling the timeout exists to avoid.</para></summary>
    private static IHttpClientBuilder Configure(IHttpClientBuilder builder, string name) =>
        builder.ConfigureHttpClient((sp, client) =>
        {
            var o = OptionsFor(sp, name);
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
}
