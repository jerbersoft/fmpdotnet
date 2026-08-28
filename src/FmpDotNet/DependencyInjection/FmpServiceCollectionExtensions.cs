using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Http;
using NodaTime;

namespace FmpDotNet.DependencyInjection;

/// <summary>Registers the FMP clients.</summary>
public static class FmpServiceCollectionExtensions
{
    /// <summary>Name of the typed client for ordinary endpoints.</summary>
    public const string StandardClient = "fmp";

    /// <summary>Name of the typed client for <c>*-bulk</c> endpoints, which carries its own throttle and its own
    /// much longer timeout.</summary>
    public const string BulkClient = "fmp-bulk";

    /// <summary>Binds the <c>Fmp</c> configuration section and registers both clients.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddFmp(o => Bind(configuration.GetSection(FmpOptions.SectionName), o));
    }

    /// <summary>Binds the section by name rather than by reflection.
    ///
    /// <para><c>ConfigurationBinder.Bind</c> is neither trim- nor AOT-safe, and this assembly declares itself
    /// AOT-compatible. Seven explicit reads cost less than the alternatives — a source generator, or an SDK that
    /// quietly breaks when a consumer publishes trimmed.</para></summary>
    private static void Bind(IConfiguration section, FmpOptions o)
    {
        if (section[nameof(FmpOptions.ApiKey)] is { } apiKey) o.ApiKey = apiKey;
        if (section[nameof(FmpOptions.BaseUrl)] is { } baseUrl) o.BaseUrl = baseUrl;
        if (Int32(section[nameof(FmpOptions.PerMinuteCap)]) is { } cap) o.PerMinuteCap = cap;
        if (Int32(section[nameof(FmpOptions.BulkPerMinuteCap)]) is { } bulkCap) o.BulkPerMinuteCap = bulkCap;
        if (Span(section[nameof(FmpOptions.RequestTimeout)]) is { } timeout) o.RequestTimeout = timeout;
        if (Span(section[nameof(FmpOptions.BulkRequestTimeout)]) is { } bulkTimeout) o.BulkRequestTimeout = bulkTimeout;
        if (Span(section[nameof(FmpOptions.MaxRetryAfter)]) is { } retry) o.MaxRetryAfter = retry;
        if (section[nameof(FmpOptions.DeveloperBulkCacheDirectory)] is { } cache)
            o.DeveloperBulkCacheDirectory = cache;

        static int? Int32(string? raw) =>
            int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        // Accepts the "00:00:30" form configuration usually carries and a bare number of seconds, which is what
        // anyone setting this from an environment variable reaches for first.
        //
        // The bare-number case is tested FIRST and deliberately: TimeSpan.TryParse("45") succeeds and yields
        // FORTY-FIVE DAYS. Trying the clock form first therefore turns "RequestTimeout=45" — the most natural
        // thing anyone would write — into a timeout that never fires, silently, with no parse error to notice.
        // The TimeSpan hop is confined to this parse; the option itself is a NodaTime Duration.
        static Duration? Span(string? raw) => raw switch
        {
            null or "" => null,
            var s when !s.Contains(':') && double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds) => Duration.FromSeconds(seconds),
            var s when TimeSpan.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v)
                => Duration.FromTimeSpan(v),
            _ => null,
        };
    }

    /// <summary>Registers both clients against options configured in code.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<FmpOptions>()
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
            .ValidateOnStart();

        // The API key is deliberately NOT validated. An SDK cannot know whether its caller intends to make a
        // request; the host that does know should assert it.

        // NodaTime's clock, not TimeProvider — the SDK's time surface is NodaTime throughout, and a test
        // substitutes NodaTime.Testing.FakeClock here.
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        // TryAdd so exactly one reservoir pair exists regardless of how many times AddFmp is called — the
        // invariant is structural, not dependent on call order.
        services.TryAddSingleton(sp => new FmpBuckets(sp.GetRequiredService<IOptions<FmpOptions>>().Value));
        services.TryAddTransient<FmpRateLimitHandler>();
        services.TryAddTransient<FmpBulkRateLimitHandler>();
        services.TryAddTransient<FmpTimeoutHandler>();
        services.TryAddTransient<FmpBulkTimeoutHandler>();
        services.TryAddTransient<FmpDeveloperBulkCacheHandler>();

        Configure(services.AddHttpClient<FmpTransport>(StandardClient))
            .AddHttpMessageHandler<FmpRateLimitHandler>()
            .AddHttpMessageHandler<FmpTimeoutHandler>();

        // The developer cache is added FIRST, which makes it the OUTERMOST handler, and that placement is the
        // point rather than a detail: a replay must not consume a bulk token or start a timeout. A cache hit
        // therefore never reaches the rate limiter at all. It is inert unless
        // FmpOptions.DeveloperBulkCacheDirectory is set, so it costs a null check when it is off.
        Configure(services.AddHttpClient<FmpBulkTransport>(BulkClient))
            .AddHttpMessageHandler<FmpDeveloperBulkCacheHandler>()
            .AddHttpMessageHandler<FmpBulkRateLimitHandler>()
            .AddHttpMessageHandler<FmpBulkTimeoutHandler>();

        services.TryAddTransient<CompanyEndpoints>();
        services.TryAddTransient<DirectoryEndpoints>();
        services.TryAddTransient<StatementEndpoints>();
        services.TryAddTransient<CalendarEndpoints>();
        services.TryAddTransient<AnalystEndpoints>();
        services.TryAddTransient<EconomicsEndpoints>();
        services.TryAddTransient<SearchEndpoints>();
        services.TryAddTransient<SecFilingsEndpoints>();
        services.TryAddTransient<QuoteEndpoints>();
        services.TryAddTransient<ChartEndpoints>();
        services.TryAddTransient<BulkEndpoints>();
        services.TryAddTransient<FmpClient>();

        return services;
    }

    /// <summary>Everything both clients share.
    ///
    /// <para><c>Timeout.InfiniteTimeSpan</c> is a decision, not an omission. Timeouts belong to
    /// <see cref="FmpTimeoutHandlerBase"/> for two reasons the client-level knob cannot serve: it sits INSIDE the
    /// rate-limit handler, so a wait on the shared token bucket is not charged against the attempt; and it reports
    /// expiry as a <see cref="TimeoutException"/> rather than the <see cref="TaskCanceledException"/> HttpClient
    /// raises, which callers routinely mistake for a shutdown.</para>
    ///
    /// <para>Handler ORDER is contractual: the first added is outermost, so the chain is throttle → timeout →
    /// network. Swapping them puts the throttle wait back inside the deadline, which is the coupling the timeout
    /// exists to avoid.</para></summary>
    private static IHttpClientBuilder Configure(IHttpClientBuilder builder) =>
        builder.ConfigureHttpClient((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<FmpOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
}
