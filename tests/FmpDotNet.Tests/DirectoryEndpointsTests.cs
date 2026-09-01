using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FmpDotNet.DependencyInjection;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

/// <summary>The two reference vocabularies, checked against responses captured live from FMP on 2026-08-26.
///
/// <para>The fixtures are the whole payloads, not one row each: for these endpoints the row COUNT and the row
/// ORDER are part of what is being pinned. Sectors arrive alphabetically and industries do not — they are grouped
/// by sector — so a stray <c>OrderBy</c> would look harmless on the first and destroy the only sector signal the
/// second carries.</para></summary>
public class DirectoryEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (DirectoryEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DirectoryEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    public static TheoryData<string, Func<DirectoryEndpoints, Task<IReadOnlyList<string>>>> Calls => new()
    {
        { "stable/available-sectors", e => e.GetSectorsAsync() },
        { "stable/available-industries", e => e.GetIndustriesAsync() },
    };

    [Fact]
    public async Task Unwraps_the_eleven_sectors_in_the_order_fmp_sends_them()
    {
        var (endpoints, _) = Build(Fixture("available-sectors.json"));

        var sectors = await endpoints.GetSectorsAsync();

        Assert.Equal(
        [
            "Basic Materials", "Communication Services", "Consumer Cyclical", "Consumer Defensive", "Energy",
            "Financial Services", "Healthcare", "Industrials", "Real Estate", "Technology", "Utilities",
        ], sectors);
    }

    [Fact]
    public async Task Unwraps_all_hundred_and_fifty_nine_industries_without_sorting_them()
    {
        var (endpoints, _) = Build(Fixture("available-industries.json"));

        var industries = await endpoints.GetIndustriesAsync();

        Assert.Equal(159, industries.Count);
        // The ends are what prove no sort was applied: alphabetically "Steel" would be near the back and
        // "Advertising Agencies" at the front. What FMP actually sends is grouped by sector — the basic-materials
        // block first, the utilities block last — and that grouping is the only thing in the payload that says
        // which sector an industry belongs to, since no row carries a sector field.
        Assert.Equal(["Steel", "Silver", "Other Precious Metals", "Gold", "Copper"], industries.Take(5));
        Assert.Equal(
            ["Regulated Gas", "Regulated Electric", "Independent Power Producers", "Diversified Utilities",
             "General Utilities"],
            industries.TakeLast(5));
    }

    [Fact]
    public async Task Drops_blank_labels_because_an_empty_category_poisons_a_lookup_table()
    {
        // Neither live payload contained one. The filter is still not optional: a caller cannot see the payload,
        // and an empty string entering a sector lookup becomes a row that matches nothing and reads as real.
        var (endpoints, _) = Build(
            """[{"sector":null},{"sector":""},{"sector":"   "},{"sector":"Energy"},null,{}]""");

        Assert.Equal(["Energy"], await endpoints.GetSectorsAsync());
    }

    [Fact]
    public async Task Drops_blank_industry_labels_too_so_the_two_endpoints_cannot_drift_apart()
    {
        var (endpoints, _) = Build("""[{"industry":null},{"industry":"  "},{"industry":"Biotechnology"}]""");

        Assert.Equal(["Biotechnology"], await endpoints.GetIndustriesAsync());
    }

    [Fact]
    public async Task Trims_a_padded_label_because_a_trailing_space_is_a_silent_equality_miss()
    {
        var (endpoints, _) = Build("""[{"sector":"  Technology  "}]""");

        Assert.Equal(["Technology"], await endpoints.GetSectorsAsync());
    }

    [Fact]
    public async Task Keeps_duplicates_because_changing_the_cardinality_would_hide_an_upstream_change()
    {
        // Deliberate, and the opposite of the blank rule. A blank label carries no information; a repeated one
        // carries the fact that FMP now repeats it. Collapsing the pair here would make the SDK look correct while
        // concealing a directory that had changed, and whether duplicates are a fault or two spellings to merge is
        // the caller's call — Distinct() is one call away.
        var (endpoints, _) = Build("""[{"sector":"Energy"},{"sector":"Utilities"},{"sector":"Energy"}]""");

        Assert.Equal(["Energy", "Utilities", "Energy"], await endpoints.GetSectorsAsync());
    }

    [Fact]
    public async Task An_empty_payload_is_an_empty_list_never_null()
    {
        // A stub each: FmpTransport disposes the response it read, so one canned message cannot serve two calls.
        var sectors = await Build("[]").Endpoints.GetSectorsAsync();
        var industries = await Build("[]").Endpoints.GetIndustriesAsync();

        Assert.NotNull(sectors);
        Assert.Empty(sectors);
        Assert.NotNull(industries);
        Assert.Empty(industries);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Sends_no_query_parameter_at_all(
        string path, Func<DirectoryEndpoints, Task<IReadOnlyList<string>>> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        // Equality, not Contains: these endpoints take no arguments at all, and a query parameter invented here
        // would be accepted silently by FMP rather than rejected.
        Assert.Equal("", uri.Query);
    }

    [Fact]
    public void Directory_resolves_from_dependency_injection_off_the_client()
    {
        // Mirrors AddFmpTests: registration is what makes the group reachable, and a missing TryAddTransient
        // fails only at the first resolve, which is startup in a host and never in a unit test of the endpoints.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Fmp:ApiKey", "k")])
            .Build();
        using var provider = new ServiceCollection().AddLogging().AddFmp(configuration).BuildServiceProvider();

        var client = provider.GetRequiredService<FmpClient>();

        Assert.NotNull(client.Directory);
        Assert.NotNull(provider.GetRequiredService<DirectoryEndpoints>());
    }
}
