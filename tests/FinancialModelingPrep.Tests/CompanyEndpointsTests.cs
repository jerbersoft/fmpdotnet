using Microsoft.Extensions.Options;
using FinancialModelingPrep.Endpoints;

using NodaTime;

namespace FinancialModelingPrep.Tests;

public class CompanyEndpointsTests
{
    private static CompanyEndpoints Build(HttpResponseMessage response)
    {
        var http = new HttpClient(new StubHandler(response))
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));
    }

    [Fact]
    public async Task Maps_a_profile_from_the_single_element_array_fmp_returns()
    {
        var endpoints = Build(StubHandler.Json(
            """
            [{"symbol":"AAPL","companyName":"Apple Inc.","price":309.9,"marketCap":4551611624400,
              "cik":"0000320193","sector":"Technology","industry":"Consumer Electronics","country":"US",
              "ipoDate":"1980-12-12","isActivelyTrading":true,"fullTimeEmployees":"166000"}]
            """));

        var profile = await endpoints.GetProfileAsync("AAPL");

        Assert.NotNull(profile);
        Assert.Equal("Apple Inc.", profile.CompanyName);
        Assert.Equal(309.9m, profile.Price);
        Assert.Equal("Technology", profile.Sector);
        Assert.Equal(new LocalDate(1980, 12, 12), profile.IpoDate);
        Assert.True(profile.IsActivelyTrading);
    }

    [Fact]
    public async Task Keeps_the_leading_zeros_on_a_cik()
    {
        // A CIK is an identifier, not a number. Parsing it to a long loses the padding SEC filings use.
        var endpoints = Build(StubHandler.Json("""[{"symbol":"AAPL","cik":"0000320193"}]"""));

        Assert.Equal("0000320193", (await endpoints.GetProfileAsync("AAPL"))!.Cik);
    }

    [Fact]
    public async Task Unknown_symbol_is_null_because_fmp_answers_an_empty_array_not_a_404()
    {
        var endpoints = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetProfileAsync("NOSUCH"));
    }

    [Fact]
    public async Task Rejects_a_blank_symbol_before_spending_a_request()
    {
        var endpoints = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetProfileAsync("  "));
    }
}
