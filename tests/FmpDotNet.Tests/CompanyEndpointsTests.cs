using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

using NodaTime;

namespace FmpDotNet.Tests;

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

    [Fact]
    public async Task Binds_the_fractional_market_cap_that_stable_profile_actually_serves()
    {
        // Measured live 2026-08-27: `stable/profile?symbol=GOOG` answers 4098415617064.9995. This property was
        // `long?` until then, so GetProfileAsync("GOOG") threw JsonException against the live API — every one of
        // the profile's other 35 fields lost to one fraction on one field. GOOGL answered the integral
        // 4122584209576 the same minute, which is why no fixture and no smoke sweep had ever shown it.
        var endpoints = Build(StubHandler.Json(
            """[{"symbol":"GOOG","companyName":"Alphabet Inc.","marketCap":4098415617064.9995}]"""));

        var profile = await endpoints.GetProfileAsync("GOOG");

        Assert.NotNull(profile);
        Assert.Equal(4098415617064.9995m, profile.MarketCap);
    }

    [Fact]
    public async Task Looks_a_profile_up_by_cik_through_the_same_model()
    {
        // stable/profile-cik answers the identical 36 fields as stable/profile, in the same order, wrapped in a
        // single-element array — measured 2026-08-27, which is why it adds no model.
        var endpoints = Build(StubHandler.Json(
            """[{"symbol":"AAPL","companyName":"Apple Inc.","cik":"0000320193","marketCap":4620348450480}]"""));

        var profile = await endpoints.GetProfileByCikAsync("0000320193");

        Assert.NotNull(profile);
        Assert.Equal("AAPL", profile.Symbol);
        Assert.Equal("0000320193", profile.Cik);
    }

    [Fact]
    public async Task Sends_the_cik_exactly_as_given_because_fmp_accepts_either_padding()
    {
        // Both 0000320193 and 320193 answered the same AAPL row on 2026-08-27, so the SDK does not normalise —
        // padding or stripping here would be a silent transformation of the caller's identifier.
        var handler = new StubHandler(StubHandler.Json("[]"), StubHandler.Json("[]"));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new CompanyEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        await endpoints.GetProfileByCikAsync("0000320193");
        await endpoints.GetProfileByCikAsync("320193");

        Assert.Contains("cik=0000320193", handler.Requests[0].Query);
        Assert.Contains("cik=320193", handler.Requests[1].Query);
    }

    [Fact]
    public async Task Unknown_cik_is_null_because_fmp_answers_an_empty_array_not_a_404()
    {
        // cik=9999999999 answered [] with HTTP 200 on 2026-08-27; cik=notacik answered 400.
        var endpoints = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetProfileByCikAsync("9999999999"));
    }

    [Fact]
    public async Task Rejects_a_blank_cik_before_spending_a_request()
    {
        var endpoints = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetProfileByCikAsync("  "));
    }
}
