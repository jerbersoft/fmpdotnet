using System.Net;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class ApiKeyProviderTests
{
    /// <summary>Records the apikey header of every attempt, answering from a script whose last status repeats. A
    /// 200 is FMP's available-sectors shape.</summary>
    private sealed class KeyRecordingUpstream(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly List<string> _keys = [];

        public IReadOnlyList<string> Keys
        {
            get { lock (_keys) return [.. _keys]; }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            HttpStatusCode status;
            lock (_keys)
            {
                status = statuses[Math.Min(_keys.Count, statuses.Length - 1)];
                _keys.Add(req.Headers.TryGetValues("apikey", out var values) ? string.Join(",", values) : "<none>");
            }
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = status == HttpStatusCode.OK
                    ? new StringContent("[{\"sector\":\"Technology\"}]", System.Text.Encoding.UTF8, "application/json")
                    : new StringContent("", System.Text.Encoding.UTF8, "text/plain"),
                RequestMessage = req,
            });
        }
    }

    [Fact]
    public async Task The_registered_client_asks_the_provider_once_per_request_and_a_retry_resends_that_key()
    {
        // The retry sits inside the HttpClient, below the transport that reads the provider, so one request's
        // attempts carry one key. A provider that answers differently on every call proves it was not re-read.
        var upstream = new KeyRecordingUpstream(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var calls = 0;
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(
            o =>
            {
                o.ApiKey = "configured";
                o.ApiKeyProvider = () => $"key-{Interlocked.Increment(ref calls)}";
                o.MaxAttempts = 2;
                o.RetryBaseDelay = Duration.FromMilliseconds(1);
            },
            fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<FmpClient>();

        Assert.Equal(new[] { "Technology" }, await client.Directory.GetSectorsAsync());   // 503, then 200
        Assert.Equal(new[] { "Technology" }, await client.Directory.GetSectorsAsync());   // 200

        Assert.Equal(new[] { "key-1", "key-1", "key-2" }, upstream.Keys);
        Assert.Equal(2, Volatile.Read(ref calls));
    }
}
