using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Collects a registration's customizations for <see cref="FmpRegistration"/> to apply at one defined
/// point. Nothing here touches the service collection; that is the point of collecting rather than proxying.</summary>
internal sealed class FmpBuilder(IServiceCollection services, string name) : IFmpBuilder
{
    public IServiceCollection Services { get; } = services;
    public string Name { get; } = name;

    internal List<Action<IHttpClientBuilder>> Standard { get; } = [];
    internal List<Action<IHttpClientBuilder>> Bulk { get; } = [];
    internal FmpBucketRegistry? Registry { get; private set; }

    public IFmpBuilder ConfigureStandardClient(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Standard.Add(configure);
        return this;
    }

    public IFmpBuilder ConfigureBulkClient(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Bulk.Add(configure);
        return this;
    }

    public IFmpBuilder ConfigureAllClients(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Standard.Add(configure);
        Bulk.Add(configure);
        return this;
    }

    public IFmpBuilder UseBucketRegistry(FmpBucketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        return this;
    }
}
