using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary>The five <c>stable/search-*</c> lookups, checked against responses captured live from FMP on
/// 2026-08-27.
///
/// <para>Separate from <see cref="CompanyScreenerTests"/>, which covers the sixth member of FMP's Search group.</para></summary>
public class SearchEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (SearchEndpoints Endpoints, StubHandler Handler) Build(params string[] bodies)
    {
        // One response per call: FmpTransport disposes the response after reading it, so a single
        // HttpResponseMessage cannot serve two requests.
        var handler = new StubHandler([.. bodies.Select(b => StubHandler.Json(b))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SearchEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_symbol_search_reads_the_code_from_exchange_and_the_name_from_exchange_full_name()
    {
        var (endpoints, _) = Build(Fixture("search-symbol.AAPL.json"));

        var matches = await endpoints.FindBySymbolAsync("AAPL");

        // On THIS endpoint `exchange` is the code. On search-exchange-variants it is the display name and the
        // code lives in exchangeShortName. Pinned on both sides so the inversion cannot be "tidied up".
        Assert.Equal("NASDAQ", matches[0].Exchange);
        Assert.Equal("NASDAQ Global Select", matches[0].ExchangeFullName);
    }

    [Fact]
    public async Task A_symbol_search_returns_every_listing_rather_than_the_first()
    {
        var (endpoints, _) = Build(Fixture("search-symbol.AAPL.json"));

        var matches = await endpoints.FindBySymbolAsync("AAPL");

        // A list, not a T?. "AAPL" matched 7 listings across exchanges on 2026-08-27; returning one would pick a
        // listing — and therefore a currency — without saying so.
        Assert.Equal(2, matches.Count);
        Assert.Equal("EUR", matches[1].Currency);
    }

    [Fact]
    public async Task A_cik_search_echoes_the_padded_form_whichever_was_asked_for()
    {
        var (endpoints, _) = Build(Fixture("search-cik.AAPL.json"), Fixture("search-cik.AAPL.json"));

        var padded = await endpoints.FindByCikAsync("0000320193");
        var bare = await endpoints.FindByCikAsync("320193");

        // Both forms are accepted upstream and both answer with the 10-character form, so a caller can round-trip
        // through CikEntry.Cik without normalising.
        Assert.Equal("0000320193", padded[0].Cik);
        Assert.Equal("0000320193", bare[0].Cik);
    }

    [Fact]
    public async Task A_cusip_match_and_an_isin_match_agree_on_the_company_name()
    {
        var (endpoints, _) = Build(Fixture("search-cusip.AAPL.json"), Fixture("search-isin.AAPL.json"));

        var byCusip = await endpoints.FindByCusipAsync("037833100");
        var byIsin = await endpoints.FindByIsinAsync("US0378331005");

        // The wire disagrees: search-cusip sends `companyName`, search-isin sends `name`, for the identical fact.
        // Both models surface it as CompanyName so a caller never learns which endpoint spells it which way.
        Assert.Equal("Apple Inc.", byCusip[0].CompanyName);
        Assert.Equal("Apple Inc.", byIsin[0].CompanyName);
    }

    [Fact]
    public async Task An_identifier_match_carries_a_market_cap_in_an_unstated_currency()
    {
        var (endpoints, _) = Build(Fixture("search-cusip.AAPL.json"));

        var matches = await endpoints.FindByCusipAsync("037833100");

        // Both rows are Apple. The first is the Mexican listing, quoted in MXN, and NOTHING on the row says so —
        // there is no currency field and no exchange field on this shape. Sorting by MarketCap ranks currencies.
        Assert.Equal("AAPL.MX", matches[0].Symbol);
        Assert.Equal(78694853448000m, matches[0].MarketCap);
        Assert.True(matches[0].MarketCap > matches[1].MarketCap);
    }

    [Fact]
    public async Task A_symbol_search_sends_the_optional_filters_only_when_given()
    {
        var (endpoints, handler) = Build("[]", "[]");

        await endpoints.FindBySymbolAsync("AAPL");
        await endpoints.FindBySymbolAsync("AAPL", limit: 3, exchange: "NASDAQ");

        Assert.DoesNotContain("limit=", handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.DoesNotContain("exchange=", handler.Requests[0].Query, StringComparison.Ordinal);
        Assert.Contains("limit=3", handler.Requests[1].Query, StringComparison.Ordinal);
        Assert.Contains("exchange=NASDAQ", handler.Requests[1].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_identifier_searches_do_not_offer_a_limit_that_does_nothing()
    {
        // search-cusip and search-isin ignore `limit` — measured 4 -> 4 and 5 -> 5 on 2026-08-27. The guarantee is
        // that no overload offers one, which is a compile-time fact: these calls take exactly (string, ct).
        var cusip = typeof(SearchEndpoints).GetMethod(nameof(SearchEndpoints.FindByCusipAsync))!;
        var isin = typeof(SearchEndpoints).GetMethod(nameof(SearchEndpoints.FindByIsinAsync))!;

        Assert.Equal(["cusip", "ct"], cusip.GetParameters().Select(p => p.Name));
        Assert.Equal(["isin", "ct"], isin.GetParameters().Select(p => p.Name));
        await Task.CompletedTask;
    }

    public static TheoryData<string, Func<SearchEndpoints, Task<int>>> Lookups => new()
    {
        { "/stable/search-symbol", async e => (await e.FindBySymbolAsync("ZZZZQQQQ9")).Count },
        { "/stable/search-name", async e => (await e.FindByNameAsync("ZZZZQQQQ9")).Count },
        { "/stable/search-cik", async e => (await e.FindByCikAsync("9999999999")).Count },
        { "/stable/search-cusip", async e => (await e.FindByCusipAsync("000000000")).Count },
        { "/stable/search-isin", async e => (await e.FindByIsinAsync("XX0000000000")).Count },
    };

    [Theory]
    [MemberData(nameof(Lookups))]
    public async Task An_unknown_identifier_reads_as_an_empty_list(
        string path, Func<SearchEndpoints, Task<int>> call)
    {
        var (endpoints, handler) = Build("[]");

        // All five answer garbage with HTTP 200 and [], never an error — measured 2026-08-27. An empty list is
        // therefore "no match", and is indistinguishable from a query FMP did not understand.
        Assert.Equal(0, await call(endpoints));
        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }

    public static TheoryData<string, Func<SearchEndpoints, string, Task>> BlankRejectingCalls => new()
    {
        // All five guard their identifier the same way — ArgumentException.ThrowIfNullOrWhiteSpace, thrown
        // before any request is built — but FindByCikAsync, FindByCusipAsync and FindByIsinAsync are separate
        // implementations from FindBySymbolAsync and FindByNameAsync (which share QueryAsync), so each is
        // proven rather than assumed from the one already covered.
        { "", (e, q) => e.FindBySymbolAsync(q) },
        { "   ", (e, q) => e.FindBySymbolAsync(q) },
        { "", (e, q) => e.FindByNameAsync(q) },
        { "   ", (e, q) => e.FindByNameAsync(q) },
        { "", (e, q) => e.FindByCikAsync(q) },
        { "   ", (e, q) => e.FindByCikAsync(q) },
        { "", (e, q) => e.FindByCusipAsync(q) },
        { "   ", (e, q) => e.FindByCusipAsync(q) },
        { "", (e, q) => e.FindByIsinAsync(q) },
        { "   ", (e, q) => e.FindByIsinAsync(q) },
    };

    [Theory]
    [MemberData(nameof(BlankRejectingCalls))]
    public async Task A_blank_query_is_rejected_before_it_costs_a_call(
        string query, Func<SearchEndpoints, string, Task> call)
    {
        var (endpoints, handler) = Build("[]");

        await Assert.ThrowsAsync<ArgumentException>(() => call(endpoints, query));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_exchange_variant_reads_the_code_from_exchange_short_name()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // THE INVERSION. On stable/profile, `exchange` is the code and `exchangeFullName` the display name. Here
        // `exchange` is the DISPLAY NAME and the code lives in exchangeShortName. A caller filtering on
        // Exchange == "NASDAQ" against this endpoint gets nothing, with no error.
        Assert.Equal("NASDAQ", variants[0].ExchangeShortName);
        Assert.Equal("NASDAQ Global Select", variants[0].Exchange);
    }

    [Fact]
    public async Task An_exchange_variant_is_not_a_company_profile()
    {
        // The two shapes have 36 fields each and 29 in common, so a reader comparing counts would conclude they
        // are interchangeable. These four wire keys are the ones that differ, and binding CompanyProfile to this
        // payload leaves all four null while every other field populates — the worst kind of near-miss.
        var variant = typeof(ExchangeVariant).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false))
            .Cast<JsonPropertyNameAttribute>().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        var profile = typeof(CompanyProfile).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false))
            .Cast<JsonPropertyNameAttribute>().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("mktCap", variant);
        Assert.Contains("marketCap", profile);
        Assert.DoesNotContain("marketCap", variant);
        Assert.DoesNotContain("mktCap", profile);
        // And the field only this endpoint carries.
        Assert.Contains("dcf", variant);
        Assert.DoesNotContain("dcf", profile);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task An_exchange_variant_carries_a_cik_only_for_the_primary_listing()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // 5 of 6 measured rows had a null cik. This endpoint looks like a symbol -> CIK bridge and is not one;
        // FindByCikAsync goes the other way and DirectoryEndpoints.StreamCikListAsync walks the registry.
        Assert.Equal("0000320193", variants[0].Cik);
        Assert.Null(variants[1].Cik);
        Assert.Null(variants[2].Cik);
    }

    [Fact]
    public async Task An_exchange_variant_dcf_does_not_reconcile_with_its_own_price()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // dcf + dcfDiff implies a price the row does not carry: 142.85 + 170.11 = 312.96 against price 313.45.
        // Measured on every row, and the direction is not consistent — the Mexican row implies 5300.01 against
        // 5330. Pinned so a caller cannot infer price from the pair.
        var implied = variants[0].Dcf!.Value + variants[0].DcfDiff!.Value;
        Assert.NotEqual(variants[0].Price!.Value, implied);
        Assert.Equal(312.96m, Math.Round(implied, 2));
    }

    [Fact]
    public async Task An_exchange_variant_row_can_be_missing_its_price_entirely()
    {
        var (endpoints, _) = Build(Fixture("search-exchange-variants.AAPL.json"));

        var variants = await endpoints.GetExchangeVariantsAsync("AAPL");

        // AAPL.DE carried nulls for price, range, changes and dcfDiff while still reporting isActivelyTrading.
        Assert.Null(variants[2].Price);
        Assert.Null(variants[2].Changes);
        Assert.True(variants[2].IsActivelyTrading);
    }
}
