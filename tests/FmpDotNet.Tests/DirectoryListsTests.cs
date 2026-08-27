using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary>The eleven list endpoints on <see cref="DirectoryEndpoints"/> that answer "what exists", checked
/// against responses captured live from FMP on 2026-08-27.
///
/// <para>Separate from <see cref="DirectoryEndpointsTests"/>, which pins the two reference vocabularies, and from
/// <see cref="DirectorySymbolsTests"/>, which pins the two symbol directories.</para></summary>
public class DirectoryListsTests
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
        return (new DirectoryEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_country_list_unwraps_to_iso_two_letter_codes()
    {
        var (endpoints, _) = Build(Fixture("available-countries.json"));

        var countries = await endpoints.GetCountriesAsync();

        // Codes, not names. FMP calls the key `country` and sends "FK", not "Falkland Islands" — a caller
        // building a display label needs a lookup, and the measured 117 rows are all two characters.
        Assert.Equal(["FK", "MT", "SG", "PH", "US"], countries);
    }

    [Fact]
    public async Task The_etf_list_reads_the_name_key_that_stock_list_spells_differently()
    {
        var (endpoints, _) = Build(Fixture("etf-list.head.json"));

        var etfs = await endpoints.GetEtfListAsync();

        // The point of the assertion is Name being populated at all. etf-list sends `name`; if this bound
        // StockListRow (`companyName`) every name would be null and the row count would still be 3.
        Assert.Equal(3, etfs.Count);
        Assert.Equal("BREM", etfs[0].Symbol);
        Assert.Equal("iShares Emerging Markets Bond Active ETF", etfs[0].Name);
    }

    [Fact]
    public async Task The_etf_list_asks_for_the_path_fmp_serves()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetEtfListAsync();

        Assert.Equal("/stable/etf-list", handler.Requests.Single().AbsolutePath);
    }
}
