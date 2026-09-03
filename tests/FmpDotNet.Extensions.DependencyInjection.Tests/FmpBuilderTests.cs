using System.Net;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpBuilderTests
{
    /// <summary>Answers every request with the same status, and counts.</summary>
    private sealed class FailingUpstream(HttpStatusCode status) : HttpMessageHandler
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

    /// <summary>Answers 200 with an empty JSON array, and counts.</summary>
    private sealed class CountingUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = req,
            });
        }
    }

    /// <summary>A consumer's own link: counts how many times it is entered.</summary>
    private sealed class EntryCounter : DelegatingHandler
    {
        public int Entries;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Entries);
            return base.SendAsync(req, ct);
        }
    }

    [Fact]
    public async Task A_consumer_handler_on_the_standard_client_sits_outside_the_retry()
    {
        var upstream = new FailingUpstream(HttpStatusCode.ServiceUnavailable);
        var entries = new EntryCounter();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(
            o => { o.ApiKey = "k"; o.MaxAttempts = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); },
            fmp => fmp.ConfigureStandardClient(b => b
                .AddHttpMessageHandler(() => entries)
                .ConfigurePrimaryHttpMessageHandler(() => upstream)));
        using var provider = services.BuildServiceProvider();

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();

        // Outermost means one entry per logical call with the three attempts beneath it. Inside the retry the
        // counter would read three. No clock, no timing: the numbers differ by construction.
        Assert.Equal(3, upstream.Sends);
        Assert.Equal(1, entries.Entries);
    }

    [Fact]
    public async Task ConfigureBulkClient_reaches_the_bulk_client_only()
    {
        var everywhere = new CountingUpstream();
        var bulkOnly = new CountingUpstream();
        var services = new ServiceCollection().AddLogging();
        // Defaults first: IConfigureOptions run in registration order and the last PrimaryHandler assignment
        // wins, so the per-client override has to be registered after the default to be the one that applies.
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => everywhere));
        services.AddFmp(o => o.ApiKey = "k",
            fmp => fmp.ConfigureBulkClient(b => b.ConfigurePrimaryHttpMessageHandler(() => bulkOnly)));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        (await factory.CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        Assert.Equal(1, everywhere.Sends);
        Assert.Equal(0, bulkOnly.Sends);

        (await factory.CreateClient(FmpServiceCollectionExtensions.BulkClient)
            .GetAsync("stable/profile-bulk?part=0")).Dispose();
        Assert.Equal(1, everywhere.Sends);
        Assert.Equal(1, bulkOnly.Sends);
    }

    [Fact]
    public async Task ConfigureAllClients_reaches_both()
    {
        var upstream = new CountingUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k",
            fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        (await factory.CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        (await factory.CreateClient(FmpServiceCollectionExtensions.BulkClient)
            .GetAsync("stable/profile-bulk?part=0")).Dispose();

        Assert.Equal(2, upstream.Sends);
    }

    [Fact]
    public void The_builder_exposes_the_services_and_the_registration_name()
    {
        var services = new ServiceCollection().AddLogging();
        IServiceCollection? seen = null;
        string? name = null;

        services.AddFmp(o => o.ApiKey = "k", fmp => { seen = fmp.Services; name = fmp.Name; });

        Assert.Same(services, seen);
        Assert.Equal("", name);
    }

    [Fact]
    public void A_second_AddFmp_for_the_same_registration_with_a_builder_throws()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k");

        // The SDK's handlers are already in place; nothing added now could land outermost, so silently dropping
        // the callback would be worse than refusing it.
        Assert.Throws<InvalidOperationException>(() => services.AddFmp(o => { }, fmp => { }));
    }

    [Fact]
    public void A_second_AddFmp_for_the_same_registration_reconfigures_its_options()
    {
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(o => o.ApiKey = "k")
            .AddFmp(o => o.PerMinuteCap = 5)
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<FmpOptions>>().Value;

        Assert.Equal("k", options.ApiKey);
        Assert.Equal(5, options.PerMinuteCap);
    }

    [Fact]
    public void UseBucketRegistry_makes_the_container_draw_from_the_given_registry()
    {
        var shared = new FmpBucketRegistry();
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(o => o.ApiKey = "K1", fmp => fmp.UseBucketRegistry(shared))
            .BuildServiceProvider();

        // The compatibility FmpBuckets is the shared registry's pair for this key, and the registry the container
        // resolves is the shared one — so anything else handed the same instance joins the same reservoirs.
        Assert.Same(shared.For("", new FmpOptions { ApiKey = "K1" }), provider.GetRequiredService<FmpBuckets>());
        Assert.Same(shared, provider.GetRequiredService<FmpBucketRegistry>());
    }
}
