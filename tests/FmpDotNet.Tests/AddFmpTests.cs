using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FmpDotNet.DependencyInjection;
using FmpDotNet.Http;

using NodaTime;

namespace FmpDotNet.Tests;

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

        // The list above was three short when SecFilings was added — Search, Quote and Chart had never been
        // named here. A missing line is invisible: the test passes, and the group it forgot is untested for
        // resolution. This makes the omission fail instead.
        Assert.Equal(22, typeof(FmpClient)
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
            (await bulk.GetAsync("stable/profile-bulk?part=0&apikey=k")).Dispose();
            (await bulk.GetAsync("stable/profile-bulk?part=0&apikey=k")).Dispose();
            Assert.Equal(1, upstream.Sends);          // second call replayed from disk

            var standard = factory.CreateClient(FmpServiceCollectionExtensions.StandardClient);
            (await standard.GetAsync("stable/profile?symbol=AAPL&apikey=k")).Dispose();
            (await standard.GetAsync("stable/profile?symbol=AAPL&apikey=k")).Dispose();
            Assert.Equal(3, upstream.Sends);          // both went to the upstream
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Base_address_ends_in_a_slash_so_relative_paths_do_not_lose_a_segment()
    {
        using var provider = Build(("Fmp:ApiKey", "k"), ("Fmp:BaseUrl", "https://example.test"));

        var http = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient);

        Assert.Equal("https://example.test/", http.BaseAddress!.ToString());
    }
}
