using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>The customization surface for one FMP registration, handed to the <c>configureBuilder</c> callback
/// of <c>AddFmp</c>.
///
/// <para>The builder collects; it does not proxy. Every callback given here is applied by <c>AddFmp</c> at one
/// defined point, before the SDK adds its own handlers, so nothing depends on the order of statements inside
/// the caller's lambda. Two things follow. <see cref="UseBucketRegistry"/> is known before any rate-limit handler
/// is built. And consumer handlers land <b>outermost</b>: a handler added through
/// <see cref="ConfigureStandardClient"/> sees one entry per logical call, with the SDK's retry, throttle wait and
/// timeout all happening beneath it. That is the right default for the things hosts actually add here — a
/// proxy, a tracing span, a stubbed primary handler in a test — and it means a handler added to observe retries
/// will not see them.</para>
///
/// <para>A registration's callbacks are given on its first <c>AddFmp</c>. A later <c>AddFmp</c> for the same
/// name may re-configure its options; one that passes a builder callback throws, because the SDK's handlers are
/// already in place and nothing added afterwards could land outermost.</para></summary>
public interface IFmpBuilder
{
    /// <summary>The service collection the registration is being added to.</summary>
    IServiceCollection Services { get; }

    /// <summary>The registration's name — <c>""</c> for the default registration.</summary>
    string Name { get; }

    /// <summary>Configures the <c>HttpClient</c> behind the ordinary endpoints. Handlers added here are outermost.
    ///
    /// <para><b>Do not add a second retry policy here.</b> The SDK already retries transient failures on this
    /// client — <see cref="FmpOptions.MaxAttempts"/>, three by default — and a retry added here multiplies with
    /// it: two policies of three attempts each make nine sends per call. A consumer of this SDK measured exactly
    /// that with <c>AddStandardResilienceHandler</c>, whose circuit breaker then cascaded a handful of 429s into
    /// thousands of skipped symbols. Tune the SDK's retry through <see cref="FmpOptions"/> instead.</para></summary>
    IFmpBuilder ConfigureStandardClient(Action<IHttpClientBuilder> configure);

    /// <summary>Configures the <c>HttpClient</c> behind the <c>*-bulk</c> endpoints. Handlers added here are
    /// outermost — outside the developer cache too, so a handler here observes cache hits. The warning on
    /// <see cref="ConfigureStandardClient"/> about stacking a second retry applies here as well.</summary>
    IFmpBuilder ConfigureBulkClient(Action<IHttpClientBuilder> configure);

    /// <summary>Configures both clients: the same as <see cref="ConfigureStandardClient"/> and
    /// <see cref="ConfigureBulkClient"/> with the same callback. The callback runs once per client, so a handler
    /// it adds must come from the factory lambda each time — a single captured <c>DelegatingHandler</c> instance
    /// would be given two inner handlers and throw after the first send.</summary>
    IFmpBuilder ConfigureAllClients(Action<IHttpClientBuilder> configure);

    /// <summary>Draws this registration's reservoirs from <paramref name="registry"/> rather than from the
    /// container's own, which is how a container and a factory-built client on the same API key share a pair
    /// instead of emitting at twice the cap. The registry also becomes the container's, unless the container
    /// already has one — in which case an earlier registration keeps drawing from the container's while this one
    /// draws from the given one, and two registrations on one key would emit at twice the cap. Hand every
    /// registration the same registry.</summary>
    IFmpBuilder UseBucketRegistry(FmpBucketRegistry registry);
}
