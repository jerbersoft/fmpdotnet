using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FmpDotNet.Extensions.DependencyInjection;
using FmpDotNet.Http;

using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class AddFmpTests
{
    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        return new ServiceCollection().AddLogging().AddFmp(configuration).BuildServiceProvider();
    }

    /// <summary>Every group on <see cref="FmpClient"/> resolves.
    ///
    /// <para>Named for the whole surface rather than for a count, because the count keeps changing: a group added
    /// to the client but never registered fails only here, and only if this test names it. The constructor is
    /// where that would surface otherwise — which is to say, at the first resolve in a consumer's application
    /// rather than in this build.</para></summary>
    [Fact]
    public void Resolves_the_client_and_every_endpoint_group()
    {
        using var provider = Build(("Fmp:ApiKey", "k"));

        var client = provider.GetRequiredService<FmpClient>();

        Assert.NotNull(client.Company);
        Assert.NotNull(client.Directory);
        Assert.NotNull(client.Statements);
        Assert.NotNull(client.Calendar);
        Assert.NotNull(client.Analyst);
        Assert.NotNull(client.Economics);
        Assert.NotNull(client.Search);
        Assert.NotNull(client.SecFilings);
        Assert.NotNull(client.InstitutionalOwnership);
        Assert.NotNull(client.InsiderTrades);
        Assert.NotNull(client.Congress);
        Assert.NotNull(client.Transcripts);
        Assert.NotNull(client.Esg);
        Assert.NotNull(client.Cot);
        Assert.NotNull(client.Quote);
        Assert.NotNull(client.Chart);
        Assert.NotNull(client.Bulk);
        Assert.NotNull(client.TechnicalIndicators);
        Assert.NotNull(client.MarketPerformance);
        Assert.NotNull(client.EtfAndFunds);
        Assert.NotNull(client.Indexes);
        Assert.NotNull(client.MarketHours);
        Assert.NotNull(client.News);
        Assert.NotNull(client.Fundraisers);
        Assert.NotNull(client.DiscountedCashFlow);

        // The list above was three short when SecFilings was added — Search, Quote and Chart had never been
        // named here. A missing line is invisible: the test passes, and the group it forgot is untested for
        // resolution. This makes the omission fail instead.
        Assert.Equal(25, typeof(FmpClient)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length);
    }

    [Fact]
    public void Binds_every_option_from_configuration()
    {
        using var provider = Build(
            ("Fmp:ApiKey", "k"),
            ("Fmp:BaseUrl", "https://example.test"),
            ("Fmp:PerMinuteCap", "500"),
            ("Fmp:BulkPerMinuteCap", "3"),
            ("Fmp:RequestTimeout", "00:00:45"),
            ("Fmp:BulkRequestTimeout", "00:20:00"),
            ("Fmp:MaxRetryAfter", "00:01:00"),
            ("Fmp:DeveloperBulkCacheDirectory", "/tmp/fmp-bulk"));

        var o = provider.GetRequiredService<IOptions<FmpOptions>>().Value;

        Assert.Equal("k", o.ApiKey);
        Assert.Equal("https://example.test", o.BaseUrl);
        Assert.Equal(500, o.PerMinuteCap);
        Assert.Equal(3, o.BulkPerMinuteCap);
        Assert.Equal(Duration.FromSeconds(45), o.RequestTimeout);
        Assert.Equal(Duration.FromMinutes(20), o.BulkRequestTimeout);
        Assert.Equal(Duration.FromMinutes(1), o.MaxRetryAfter);
        Assert.Equal("/tmp/fmp-bulk", o.DeveloperBulkCacheDirectory);
    }

    [Fact]
    public void The_developer_bulk_cache_is_off_unless_a_directory_is_configured()
    {
        // The default has to be off: an entry never expires, so a cache that switched itself on would mean an
        // application silently serving whatever FMP said the first time, forever.
        using var provider = Build(("Fmp:ApiKey", "k"));

        Assert.Null(provider.GetRequiredService<IOptions<FmpOptions>>().Value.DeveloperBulkCacheDirectory);
    }

    [Fact]
    public void Accepts_a_bare_number_of_seconds_for_a_timeout()
    {
        // What anyone setting this from an environment variable reaches for first — and TimeSpan.TryParse("45")
        // silently means 45 DAYS, so getting this order wrong disables the timeout rather than failing.
        using var provider = Build(("Fmp:ApiKey", "k"), ("Fmp:RequestTimeout", "45"));

        Assert.Equal(Duration.FromSeconds(45),
            provider.GetRequiredService<IOptions<FmpOptions>>().Value.RequestTimeout);
    }

    [Fact]
    public void Leaves_defaults_alone_when_configuration_is_silent()
    {
        using var provider = Build(("Fmp:ApiKey", "k"));

        var o = provider.GetRequiredService<IOptions<FmpOptions>>().Value;

        Assert.Equal("https://financialmodelingprep.com", o.BaseUrl);
        Assert.Equal(660, o.PerMinuteCap);
        Assert.Equal(2, o.BulkPerMinuteCap);
        Assert.Equal(3, o.MaxAttempts);
        // One, not three: bulk retries are opt-in. See FmpOptions.BulkMaxAttempts.
        Assert.Equal(1, o.BulkMaxAttempts);
        Assert.Equal(Duration.FromSeconds(1), o.RetryBaseDelay);
        Assert.Equal(Duration.FromSeconds(120), o.MaxRetryDelay);
    }

    [Fact]
    public void Does_not_require_an_api_key_because_an_sdk_cannot_know_the_caller_intends_to_call()
    {
        using var provider = Build(("Fmp:BaseUrl", "https://example.test"));

        Assert.Equal("", provider.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
    }

    [Theory]
    [InlineData("Fmp:BaseUrl", "not-a-uri")]
    [InlineData("Fmp:PerMinuteCap", "0")]
    [InlineData("Fmp:BulkPerMinuteCap", "0")]
    [InlineData("Fmp:RequestTimeout", "0")]
    [InlineData("Fmp:BulkRequestTimeout", "0")]
    [InlineData("Fmp:MaxAttempts", "0")]
    [InlineData("Fmp:BulkMaxAttempts", "0")]
    [InlineData("Fmp:RetryBaseDelay", "0")]
    [InlineData("Fmp:MaxRetryDelay", "0")]
    public void Rejects_configuration_that_would_hang_or_throw_later(string key, string value)
    {
        using var provider = Build(("Fmp:ApiKey", "k"), (key, value));

        // A zero cap means a reservoir that never refills, so the first request waits forever — failing by name
        // at startup beats hanging with a log line that says only "waiting".
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<FmpOptions>>().Value);
    }

    [Fact]
    public void Registers_exactly_one_reservoir_pair_however_many_times_AddFmp_is_called()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(configuration).AddFmp(configuration).AddFmp(configuration)
            .BuildServiceProvider();

        // Handlers are transient and HttpClientFactory rebuilds them; a second reservoir would mean an aggregate
        // emitted rate above the cap.
        Assert.Same(provider.GetRequiredService<FmpBuckets>(), provider.GetRequiredService<FmpBuckets>());
    }

    [Fact]
    public void Gives_the_two_clients_separate_reservoirs()
    {
        using var provider = Build(("Fmp:ApiKey", "k"));
        var buckets = provider.GetRequiredService<FmpBuckets>();

        Assert.NotSame(buckets.Standard, buckets.Bulk);
    }

    [Fact]
    public void Leaves_client_timeouts_infinite_so_the_handler_owns_the_deadline()
    {
        using var provider = Build(("Fmp:ApiKey", "k"));

        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // A client-level timeout surfaces as TaskCanceledException, which callers mistake for a shutdown; the
        // deadline belongs to FmpTimeoutHandler, which sits inside the throttle and raises TimeoutException.
        Assert.Equal(Timeout.InfiniteTimeSpan,
            factory.CreateClient(FmpServiceCollectionExtensions.StandardClient).Timeout);
        Assert.Equal(Timeout.InfiniteTimeSpan,
            factory.CreateClient(FmpServiceCollectionExtensions.BulkClient).Timeout);
    }

    /// <summary>Counts requests that actually reached the network.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("symbol,sector\nAAA,Technology\n", System.Text.Encoding.UTF8, "text/csv"),
                RequestMessage = req,
            });
        }
    }

    [Fact]
    public async Task The_developer_bulk_cache_is_wired_to_the_bulk_client_and_only_to_it()
    {
        // Two claims in one test, because each is only meaningful with the other. It has to be ON the bulk client
        // — that is the throttle FMP warns about — and it has to be OFF the ordinary one, where responses are
        // small, per-symbol and expected to be live. A cache silently covering `stable/profile` would make every
        // symbol answer with whichever company happened to be fetched first.
        var directory = Path.Combine(Path.GetTempPath(), "fmpdotnet-wiring-tests", Guid.NewGuid().ToString("n"));
        var upstream = new CountingHandler();
        try
        {
            var services = new ServiceCollection().AddLogging();
            services.AddFmp(o => { o.ApiKey = "k"; o.DeveloperBulkCacheDirectory = directory; });
            services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();

            var bulk = factory.CreateClient(FmpServiceCollectionExtensions.BulkClient);
            (await bulk.GetAsync("stable/profile-bulk?part=0")).Dispose();
            (await bulk.GetAsync("stable/profile-bulk?part=0")).Dispose();
            Assert.Equal(1, upstream.Sends);          // second call replayed from disk

            var standard = factory.CreateClient(FmpServiceCollectionExtensions.StandardClient);
            (await standard.GetAsync("stable/profile?symbol=AAPL")).Dispose();
            (await standard.GetAsync("stable/profile?symbol=AAPL")).Dispose();
            Assert.Equal(3, upstream.Sends);          // both went to the upstream
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Answers every request with the same status, and counts.</summary>
    private sealed class FailingHandler(System.Net.HttpStatusCode status) : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain"),
                RequestMessage = req,
            });
        }
    }

    private static ServiceProvider BuildWithUpstream(HttpMessageHandler upstream, Action<FmpOptions> configure)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(configure);
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_ordinary_client_retries_a_5xx_and_the_bulk_client_does_not()
    {
        // Two claims in one test because the asymmetry IS the design: bulk answers tens of megabytes against a
        // reservoir of 2/min, and FMP warns it will restrict a key that abuses those endpoints.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        using var provider = BuildWithUpstream(upstream, o =>
        {
            o.ApiKey = "k";
            o.RetryBaseDelay = Duration.FromMilliseconds(1);   // the policy is asserted elsewhere; keep this fast
        });
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        (await factory.CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        Assert.Equal(3, upstream.Sends);

        (await factory.CreateClient(FmpServiceCollectionExtensions.BulkClient)
            .GetAsync("stable/profile-bulk?part=0")).Dispose();
        Assert.Equal(4, upstream.Sends);                       // one more, not three more
    }

    [Fact]
    public async Task Holding_the_bucket_for_nothing_on_a_429_does_not_also_flatten_the_retry_backoff()
    {
        // MaxRetryAfter = 0 is a supported setting and means "never let a 429 hold the SHARED bucket". It must not
        // also mean "re-send with no pacing at all", which is what sourcing the retry ceiling from it would do —
        // turning the default three attempts into an immediate burst against an upstream already failing.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        using var provider = BuildWithUpstream(upstream, o =>
        {
            o.ApiKey = "k";
            o.MaxRetryAfter = Duration.Zero;
            o.MaxAttempts = 2;
            o.RetryBaseDelay = Duration.FromMilliseconds(100);
        });

        var started = System.Diagnostics.Stopwatch.StartNew();
        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        started.Stop();

        Assert.Equal(2, upstream.Sends);
        Assert.True(started.ElapsedMilliseconds >= 50,
            $"the two attempts were {started.ElapsedMilliseconds}ms apart — the backoff was flattened to nothing");
    }

    [Fact]
    public async Task Every_retry_attempt_draws_its_own_token_because_the_retry_sits_outside_the_throttle()
    {
        // The correction that shaped this handler's placement. FmpRateLimitHandlerBase acquires its token BEFORE
        // delegating, so a retry registered INSIDE it would draw one token for the whole sequence and every
        // attempt after the first would bypass the reservoir — the opposite of what a throttle is for.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        using var provider = BuildWithUpstream(upstream, o =>
        {
            o.ApiKey = "k";
            o.PerMinuteCap = 3;                                // capacity 3, so three attempts empty it exactly
            o.RetryBaseDelay = Duration.FromMilliseconds(1);
        });

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();

        Assert.Equal(3, upstream.Sends);
        var now = SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
        Assert.True(
            provider.GetRequiredService<FmpBuckets>().Standard.Acquire(now) > Duration.Zero,
            "the reservoir still had tokens to give — the retried attempts bypassed the throttle");
    }

    [Fact]
    public void Base_address_ends_in_a_slash_so_relative_paths_do_not_lose_a_segment()
    {
        using var provider = Build(("Fmp:ApiKey", "k"), ("Fmp:BaseUrl", "https://example.test"));

        var http = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient);

        Assert.Equal("https://example.test/", http.BaseAddress!.ToString());
    }

    [Fact]
    public async Task Calling_AddFmp_twice_for_one_registration_wires_the_handler_chain_once()
    {
        // Registering the same name twice is the caller re-configuring one registration, not creating two. A
        // second copy of the chain would be a retry inside a retry: 3 × 3 = 9 sends per call.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        Action<FmpOptions> configure = o => { o.ApiKey = "k"; o.RetryBaseDelay = Duration.FromMilliseconds(1); };
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(configure).AddFmp(configure);
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        using var provider = services.BuildServiceProvider();

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();

        Assert.Equal(3, upstream.Sends);
    }

    [Theory]
    [InlineData(null, "fmp", "fmp-bulk")]
    [InlineData("", "fmp", "fmp-bulk")]
    [InlineData("research", "fmp:research", "fmp-bulk:research")]
    public void Client_names_are_the_constants_for_the_default_registration_and_suffixed_for_a_named_one(
        string? name, string standard, string bulk)
    {
        Assert.Equal(standard, FmpServiceCollectionExtensions.StandardClientName(name));
        Assert.Equal(bulk, FmpServiceCollectionExtensions.BulkClientName(name));
    }
}
