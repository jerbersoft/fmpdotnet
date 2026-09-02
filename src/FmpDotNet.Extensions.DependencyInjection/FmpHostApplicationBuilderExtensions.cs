using Microsoft.Extensions.Hosting;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>One-line registration on an <see cref="IHostApplicationBuilder"/> — ASP.NET Core, a Worker Service,
/// or a console app built on <c>Host.CreateApplicationBuilder</c>. Delegates to the <c>IServiceCollection</c>
/// overloads with the builder's configuration; nothing is wired here.</summary>
public static class FmpHostApplicationBuilderExtensions
{
    /// <summary>Registers FMP from the builder's configuration: the <c>"Fmp"</c> section for the default
    /// registration, <c>"Fmp:{name}"</c> for a named one, or <paramref name="sectionName"/> if given. See
    /// <see cref="FmpServiceCollectionExtensions.AddFmp(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Microsoft.Extensions.Configuration.IConfiguration, Action{IFmpBuilder}, string)"/>.</summary>
    public static IHostApplicationBuilder AddFmp(this IHostApplicationBuilder builder, string? name = null,
        string? sectionName = null, Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddFmp(name ?? "", builder.Configuration, configureBuilder, sectionName);
        return builder;
    }

    /// <summary>Registers FMP against options configured in code, under <paramref name="name"/> if given.</summary>
    public static IHostApplicationBuilder AddFmp(this IHostApplicationBuilder builder, Action<FmpOptions> configure,
        string? name = null, Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddFmp(name ?? "", configure, configureBuilder);
        return builder;
    }
}
