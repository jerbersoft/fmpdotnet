using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Builds an <see cref="FmpClient"/> for a host that has no <c>IServiceCollection</c> at all.
///
/// <para>One wiring path. <see cref="Create(Action{FmpOptions}, ILoggerFactory, FmpBucketRegistry, Action{IFmpBuilder})"/>
/// builds a private container through <c>AddFmp</c> and the client owns it, so the handler chain — whose order is
/// contractual and whose mistakes fail silently — is never hand-wired a second time. It costs no new dependency:
/// the concrete container ships with this package through <c>Microsoft.Extensions.Http</c>. The cost is a container
/// the caller did not ask for and a few milliseconds at construction.</para>
///
/// <para><b>Logging defaults to none.</b> Without an <see cref="ILoggerFactory"/> the clamped-<c>Retry-After</c>
/// warning and the cap-conflict warning go nowhere, and a silent throttle is exactly the thing someone debugging
/// a slow run needs to see. Pass the host's factory.</para>
///
/// <para>Reads no environment variable. A host can pass its key in one line; a library that silently picks up
/// ambient credentials is worse than one that does not.</para></summary>
public static class FmpClientFactory
{
    /// <summary>A client for one API key, every other option at its default, and no logging.</summary>
    public static FmpClient Create(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        return Create(o => o.ApiKey = apiKey);
    }

    /// <summary>A client built from <paramref name="configure"/>, validated now rather than on its first call.
    /// Dispose it: it owns the container behind it.</summary>
    /// <param name="configure">Configures the options, exactly as <c>AddFmp</c> would.</param>
    /// <param name="loggerFactory">Where the SDK's warnings go. None by default.</param>
    /// <param name="registry">A registry to share reservoirs through — a container's, via
    /// <see cref="IFmpBuilder.UseBucketRegistry"/> — so a host and this client on the same key emit at the cap
    /// rather than at twice it.</param>
    /// <param name="configureBuilder">The customization surface <c>AddFmp</c> offers: a proxy, a tracing handler,
    /// a stubbed primary handler in a test.</param>
    public static FmpClient Create(Action<FmpOptions> configure, ILoggerFactory? loggerFactory = null,
        FmpBucketRegistry? registry = null, Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var services = new ServiceCollection();
        // Registered before AddLogging, whose TryAdd then keeps the caller's factory. An instance the container
        // did not create is not disposed by it, which is right: the factory is the host's.
        if (loggerFactory is not null) services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddFmp(configure, fmp =>
        {
            if (registry is not null) fmp.UseBucketRegistry(registry);
            configureBuilder?.Invoke(fmp);
        });

        var provider = services.BuildServiceProvider();
        try
        {
            // The host path validates on start. A factory-built client that threw a configuration error on its
            // first request instead would be the worse of the two, so validate here, before anything is built.
            var options = provider.GetRequiredService<IOptions<FmpOptions>>();
            _ = options.Value;

            // The HttpClients are created here rather than left inside resolved transports so that Dispose can
            // dispose them. A disposed client should refuse to send, and disposing the container alone would
            // leave the factory's pooled handlers — and with them the transports — working.
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var standard = factory.CreateClient(FmpServiceCollectionExtensions.StandardClient);
            var bulk = factory.CreateClient(FmpServiceCollectionExtensions.BulkClient);
            return new FmpClient(new FmpTransport(standard, options), new FmpBulkTransport(bulk, options),
                new Owned(provider, standard, bulk));
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>What a factory-built client owns: its two <c>HttpClient</c>s and the container behind them.</summary>
    private sealed class Owned(ServiceProvider provider, HttpClient standard, HttpClient bulk) : IDisposable
    {
        public void Dispose()
        {
            standard.Dispose();
            bulk.Dispose();
            provider.Dispose();
        }
    }
}
