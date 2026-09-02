using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpHostBuilderTests
{
    private static HostApplicationBuilder Builder(params (string Key, string Value)[] settings)
    {
        // The full builder rather than the empty one, so logging and the rest of the defaults are present the way
        // they are in a real host. The in-memory values are added last and win.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        return builder;
    }

    [Fact]
    public void AddFmp_binds_the_Fmp_section_from_the_hosts_configuration()
    {
        var builder = Builder(("Fmp:ApiKey", "host-key"));
        builder.AddFmp();
        using var host = builder.Build();

        Assert.Equal("host-key", host.Services.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
        Assert.NotNull(host.Services.GetRequiredService<FmpClient>());
    }

    [Fact]
    public void AddFmp_with_a_name_binds_Fmp_colon_name()
    {
        var builder = Builder(("Fmp:research:ApiKey", "research-key"));
        builder.AddFmp("research");
        using var host = builder.Build();

        Assert.Equal("research-key",
            host.Services.GetRequiredService<IOptionsMonitor<FmpOptions>>().Get("research").ApiKey);
        Assert.NotNull(host.Services.GetRequiredKeyedService<FmpClient>("research"));
    }

    [Fact]
    public void AddFmp_with_a_configure_delegate_takes_the_options_from_code()
    {
        var builder = Builder();
        string? seen = null;
        builder.AddFmp(o => o.ApiKey = "code-key", configureBuilder: fmp => seen = fmp.Name);
        using var host = builder.Build();

        Assert.Equal("code-key", host.Services.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
        Assert.Equal("", seen);
    }
}
