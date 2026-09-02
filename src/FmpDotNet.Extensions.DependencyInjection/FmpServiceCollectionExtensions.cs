using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Registers the FMP clients. Every overload ends in the same wiring, so the handler order that is
/// contractual exists in exactly one place.</summary>
public static class FmpServiceCollectionExtensions
{
    /// <summary>Name of the <c>HttpClient</c> for ordinary endpoints on the default registration.</summary>
    public const string StandardClient = "fmp";

    /// <summary>Name of the <c>HttpClient</c> for <c>*-bulk</c> endpoints on the default registration, which
    /// carries its own throttle and its own much longer timeout.</summary>
    public const string BulkClient = "fmp-bulk";

    /// <summary>The name of the <c>HttpClient</c> behind a registration's ordinary endpoints:
    /// <see cref="StandardClient"/> for the default registration — a null or empty <paramref name="name"/> — and
    /// <c>"fmp:{name}"</c> for a named one.</summary>
    public static string StandardClientName(string? name) =>
        string.IsNullOrEmpty(name) ? StandardClient : $"{StandardClient}:{name}";

    /// <summary>The name of the <c>HttpClient</c> behind a registration's <c>*-bulk</c> endpoints:
    /// <see cref="BulkClient"/> for the default registration — a null or empty <paramref name="name"/> — and
    /// <c>"fmp-bulk:{name}"</c> for a named one.</summary>
    public static string BulkClientName(string? name) =>
        string.IsNullOrEmpty(name) ? BulkClient : $"{BulkClient}:{name}";

    /// <summary>Binds the <c>Fmp</c> configuration section and registers both clients.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddFmp(o => FmpOptionsBinder.Bind(configuration.GetSection(FmpOptions.SectionName), o));
    }

    /// <summary>Registers both clients against options configured in code. Calling it again for the same
    /// registration re-configures the options and wires nothing twice.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return FmpRegistration.Register(services, Options.DefaultName, configure, null);
    }

    /// <summary>Binds a configuration section and registers both clients, with a customization callback — see
    /// <see cref="IFmpBuilder"/>. <paramref name="sectionName"/> defaults to <c>"Fmp"</c>.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration,
        Action<IFmpBuilder> configureBuilder, string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        var section = SectionNameFor(Options.DefaultName, sectionName);
        return FmpRegistration.Register(services, Options.DefaultName,
            o => FmpOptionsBinder.Bind(configuration.GetSection(section), o), configureBuilder);
    }

    /// <summary>Registers both clients against options configured in code, with a customization callback — see
    /// <see cref="IFmpBuilder"/>.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure,
        Action<IFmpBuilder> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        return FmpRegistration.Register(services, Options.DefaultName, configure, configureBuilder);
    }

    /// <summary>The configuration section a registration binds by default: <c>"Fmp"</c> for the default
    /// registration and <c>"Fmp:{name}"</c> for a named one, unless <paramref name="sectionName"/> overrides it.</summary>
    internal static string SectionNameFor(string name, string? sectionName) =>
        sectionName ?? (name.Length == 0 ? FmpOptions.SectionName : $"{FmpOptions.SectionName}:{name}");
}
