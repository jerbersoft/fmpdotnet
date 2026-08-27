using System.Globalization;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FmpDotNet.DependencyInjection;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

/// <summary><c>stable/company-screener</c>, checked against responses captured live from FMP on 2026-08-26.
///
/// <para><b>Most of this file is about the query, not the response</b>, and that is the point of the endpoint.
/// The screener does not reject bad input, it answers it: an unrecognised parameter <i>name</i> is ignored and
/// returns an unfiltered universe with HTTP 200, while an unrecognised parameter <i>value</i> returns an empty list
/// with HTTP 200. Neither is distinguishable downstream from a query that worked. <see cref="ScreenerCriteria"/>
/// exists to make the first impossible, so what has to be held is that every property lands on the wire name FMP
/// actually honours — a typo there would reintroduce the silent failure inside the type built to prevent
/// it.</para>
///
/// <para>Every filter below was verified against the live API by asserting the returned rows satisfy it, not by
/// reading FMP's documentation.</para></summary>
public class CompanyScreenerTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (SearchEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SearchEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    private static async Task<Dictionary<string, string>> QueryFor(ScreenerCriteria criteria)
    {
        var (endpoints, handler) = Build();
        await endpoints.ScreenAsync(criteria);
        var parsed = HttpUtility.ParseQueryString(handler.Requests.Single().Query);
        return parsed.AllKeys.Where(k => k is not null and not "apikey")
            .ToDictionary(k => k!, k => parsed[k]!);
    }

    // ---- the query --------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_empty_criteria_asks_for_the_unfiltered_universe_not_for_nothing()
    {
        // FmpRequest drops nulls, so an unset property never reaches the query string. That is what makes the
        // empty record mean "no filters" — FMP then serves its default page of the top 1,000 by market cap.
        var query = await QueryFor(new ScreenerCriteria());

        Assert.Empty(query);
    }

    [Fact]
    public async Task Every_filter_lands_on_the_wire_name_fmp_honours()
    {
        // The load-bearing test in this file. FMP ignores a parameter name it does not recognise and answers HTTP
        // 200 with an unfiltered result, so a misspelling here would not fail — it would silently widen every
        // query built through the type whose job is to prevent exactly that. Each name below was confirmed by
        // sending it live and checking the returned rows satisfied the constraint.
        var query = await QueryFor(new ScreenerCriteria
        {
            MarketCapMoreThan = 1_000_000_000m,
            MarketCapLowerThan = 5_000_000_000m,
            PriceMoreThan = 10m,
            PriceLowerThan = 500m,
            BetaMoreThan = 0.5m,
            BetaLowerThan = 2m,
            VolumeMoreThan = 100_000,
            VolumeLowerThan = 90_000_000,
            DividendMoreThan = 0.5m,
            DividendLowerThan = 8m,
            Sector = "Technology",
            Industry = "Semiconductors",
            Country = "US",
            Exchange = "NASDAQ",
            IsEtf = false,
            IsFund = false,
            IsActivelyTrading = true,
            IncludeAllShareClasses = true,
            Page = 2,
            Limit = 50,
        });

        Assert.Equal(new Dictionary<string, string>
        {
            ["marketCapMoreThan"] = "1000000000",
            ["marketCapLowerThan"] = "5000000000",
            ["priceMoreThan"] = "10",
            ["priceLowerThan"] = "500",
            ["betaMoreThan"] = "0.5",
            ["betaLowerThan"] = "2",
            ["volumeMoreThan"] = "100000",
            ["volumeLowerThan"] = "90000000",
            ["dividendMoreThan"] = "0.5",
            ["dividendLowerThan"] = "8",
            ["sector"] = "Technology",
            ["industry"] = "Semiconductors",
            ["country"] = "US",
            ["exchange"] = "NASDAQ",
            ["isEtf"] = "false",
            ["isFund"] = "false",
            ["isActivelyTrading"] = "true",
            ["includeAllShareClasses"] = "true",
            ["page"] = "2",
            ["limit"] = "50",
        }, query);
    }

    [Fact]
    public async Task A_false_flag_is_sent_rather_than_dropped()
    {
        // `isEtf=false` is a filter — "exclude ETFs" — and is not the same request as omitting isEtf. Dropping it
        // as a falsy value would turn an exclusion into no filter at all, which the endpoint would answer happily.
        var query = await QueryFor(new ScreenerCriteria { IsEtf = false });

        Assert.Equal("false", query["isEtf"]);
    }

    [Fact]
    public async Task A_zero_bound_is_sent_rather_than_dropped()
    {
        // Same shape of mistake for numbers. `betaLowerThan=0` is a real query — it returns negative betas and the
        // uncomputed zeros — so zero must not be treated as "unset".
        var query = await QueryFor(new ScreenerCriteria { BetaLowerThan = 0m, Page = 0 });

        Assert.Equal("0", query["betaLowerThan"]);
        Assert.Equal("0", query["page"]);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task Formats_bounds_invariantly_so_a_comma_decimal_host_does_not_screen_on_nothing(string culture)
    {
        // Not hypothetical, and silent if wrong: under a comma-decimal culture the default formatting sends
        // `1000000000,5`, and this endpoint answers an unparseable value the same way it answers an unrecognised
        // one — HTTP 200 with an empty list. The bug would be invisible in CI and appear only on a German or
        // French host, as "the screener returns nothing in production".
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var query = await QueryFor(new ScreenerCriteria { MarketCapMoreThan = 1_000_000_000.5m, BetaMoreThan = 1.25m });

            Assert.Equal("1000000000.5", query["marketCapMoreThan"]);
            Assert.Equal("1.25", query["betaMoreThan"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Sends_the_path_that_works()
    {
        var (endpoints, handler) = Build();

        await endpoints.ScreenAsync(new ScreenerCriteria());

        Assert.Equal("/stable/company-screener", handler.Requests.Single().AbsolutePath);
    }

    // ---- validation -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Rejects_a_null_criteria_rather_than_screening_on_everything_by_accident()
    {
        var (endpoints, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.ScreenAsync(null!));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    [InlineData(null, -10)]
    public async Task Rejects_a_negative_page_or_a_non_positive_limit_without_spending_a_request(int? page, int? limit)
    {
        // FMP would answer these too — it does not report bad input on this endpoint — and the answer would look
        // like data. The call site is the only place the mistake is visible.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.ScreenAsync(new ScreenerCriteria { Page = page, Limit = limit }));

        Assert.Empty(handler.Requests);
    }

    // ---- the response -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Maps_all_fifteen_fields_of_a_captured_row()
    {
        var (endpoints, _) = Build(Fixture("company-screener.head.json"));

        var rows = await endpoints.ScreenAsync(new ScreenerCriteria());

        Assert.Equal(5, rows.Count);
        var nvda = rows[0];
        Assert.Equal("NVDA", nvda.Symbol);
        Assert.Equal("NVIDIA Corporation", nvda.CompanyName);
        Assert.Equal(5_078_174_860_000L, nvda.MarketCap);
        Assert.Equal("Technology", nvda.Sector);
        Assert.Equal("Semiconductors", nvda.Industry);
        Assert.Equal(2.215m, nvda.Beta);
        Assert.Equal(209.66m, nvda.Price);
        Assert.Equal(0.28m, nvda.LastAnnualDividend);
        Assert.Equal(145_070_184m, nvda.Volume);
        Assert.Equal("NASDAQ Global Select", nvda.Exchange);
        Assert.Equal("NASDAQ", nvda.ExchangeShortName);
        Assert.Equal("US", nvda.Country);
        Assert.False(nvda.IsEtf);
        Assert.False(nvda.IsFund);
        Assert.True(nvda.IsActivelyTrading);
    }

    [Fact]
    public async Task A_fractional_volume_deserialises_without_throwing()
    {
        // Constructed to exercise the non-integer path, not captured from the wire. See the note on
        // ScreenerResult.Volume: a live sweep on 2026-08-27 measured a company-screener response whose volume
        // would not convert to an integer, and the exact literal was never captured — a follow-up request the
        // same day came back with only plain integers. This binds a synthetic fractional value instead, so the
        // regression is "decimal? accepts what long? refused" rather than a claim about what FMP actually sent.
        var (endpoints, _) = Build("""[{"symbol":"NVDA","volume":262507631.5}]""");

        var rows = await endpoints.ScreenAsync(new ScreenerCriteria());

        Assert.Equal(262507631.5m, rows[0].Volume);
    }

    [Fact]
    public async Task The_two_exchange_fields_are_different_values_and_only_one_can_be_sent_back()
    {
        // The trap worth a test of its own. A caller who screens, reads `Exchange` off a result and feeds it into
        // the next query gets an empty list with HTTP 200 — measured: `exchange=NASDAQ` matched,
        // `exchange=NASDAQ Global Select` did not. The fields differing is what makes that mistake available.
        var (endpoints, _) = Build(Fixture("company-screener.head.json"));

        var row = (await endpoints.ScreenAsync(new ScreenerCriteria()))[0];

        Assert.NotEqual(row.Exchange, row.ExchangeShortName);

        var query = await QueryFor(new ScreenerCriteria { Exchange = row.ExchangeShortName });
        Assert.Equal("NASDAQ", query["exchange"]);
    }

    [Fact]
    public async Task Rows_arrive_largest_market_cap_first()
    {
        var (endpoints, _) = Build(Fixture("company-screener.head.json"));

        var caps = (await endpoints.ScreenAsync(new ScreenerCriteria())).Select(r => r.MarketCap).ToList();

        Assert.Equal(caps.OrderByDescending(c => c), caps);
    }

    [Fact]
    public async Task An_empty_result_is_an_empty_list_never_null()
    {
        // And it is also what an unrecognised filter value produces, which is why the endpoint's docs say a
        // surprising empty result is a reason to check the values rather than to conclude nothing matched.
        var (endpoints, _) = Build("[]");

        var rows = await endpoints.ScreenAsync(new ScreenerCriteria { Sector = "Nonsense" });

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public void Search_resolves_from_dependency_injection_off_the_client()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Fmp:ApiKey", "k")])
            .Build();
        using var provider = new ServiceCollection().AddLogging().AddFmp(configuration).BuildServiceProvider();

        var client = provider.GetRequiredService<FmpClient>();

        Assert.NotNull(client.Search);
        Assert.NotNull(provider.GetRequiredService<SearchEndpoints>());
    }
}
