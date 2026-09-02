using System.Net;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpClientFactoryTests
{
    /// <summary>Answers stable/available-sectors the way FMP does — one-property objects under "sector" — and
    /// counts.</summary>
    private sealed class SectorsUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"sector\":\"Technology\"}]", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = req,
            });
        }
    }

    /// <summary>Answers 503 every time, and counts.</summary>
    private sealed class FailingUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain"),
                RequestMessage = req,
            });
        }
    }

    [Fact]
    public async Task Create_yields_a_client_that_answers_through_the_chain_AddFmp_wires()
    {
        var upstream = new SectorsUpstream();
        using var client = FmpClientFactory.Create(o => o.ApiKey = "k",
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));

        var sectors = await client.Directory.GetSectorsAsync();

        Assert.Equal(new[] { "Technology" }, sectors);
        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task Dispose_disposes_what_the_client_owns_and_it_refuses_to_send_afterwards()
    {
        var upstream = new SectorsUpstream();
        var client = FmpClientFactory.Create(o => o.ApiKey = "k",
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));
        Assert.NotEmpty(await client.Directory.GetSectorsAsync());

        client.Dispose();
        client.Dispose();                                                   // safe to call twice

        // The private container and the two HttpClients are gone. A disposed HttpClient throws
        // ObjectDisposedException; if the transport wraps it, the cause is still that exception.
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.Directory.GetSectorsAsync());
        Assert.True(failure is ObjectDisposedException || failure.InnerException is ObjectDisposedException,
            $"expected ObjectDisposedException, got {failure.GetType().Name}: {failure.Message}");
        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task Dispose_on_a_container_resolved_client_is_a_no_op()
    {
        var upstream = new SectorsUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k");
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<FmpClient>();

        client.Dispose();

        // The container owns the transports, not the client: it stays usable, and so does the next resolve.
        Assert.NotEmpty(await client.Directory.GetSectorsAsync());
        Assert.NotEmpty(await provider.GetRequiredService<FmpClient>().Directory.GetSectorsAsync());
    }

    [Fact]
    public void Create_with_only_an_api_key_and_no_logger_factory_does_not_throw()
    {
        using var client = FmpClientFactory.Create("k");

        Assert.NotNull(client.Company);
    }

    [Fact]
    public void Create_validates_the_options_before_returning()
    {
        // The host path validates on start. The factory validates on Create, so a bad BaseUrl is an exception
        // here rather than a UriFormatException on the first request.
        Assert.Throws<OptionsValidationException>(() =>
            FmpClientFactory.Create(o => { o.ApiKey = "k"; o.BaseUrl = "not a uri"; }));
    }

    [Fact]
    public async Task A_container_and_a_factory_built_client_handed_one_registry_share_a_reservoir_pair()
    {
        var shared = new FmpBucketRegistry();
        var upstream = new FailingUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; }, fmp => fmp.UseBucketRegistry(shared));
        using var provider = services.BuildServiceProvider();
        using var side = FmpClientFactory.Create(
            o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); },
            registry: shared,
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));

        // Three failing attempts through the side client empty the capacity-3 reservoir…
        await Assert.ThrowsAnyAsync<Exception>(() => side.Directory.GetSectorsAsync());
        Assert.Equal(3, upstream.Sends);

        // …and the container, which never sent anything, finds its reservoir empty too. Without the shared
        // registry the two would emit at twice the cap.
        var now = SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
        Assert.True(provider.GetRequiredService<FmpBuckets>().Standard.Acquire(now) > Duration.Zero,
            "the container's reservoir still had tokens — the side client drew from a different pair");
    }
}
