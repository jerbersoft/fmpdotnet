using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The two M&amp;A paths, measured 2026-08-27.</summary>
public class MergersAcquisitionsTests
{
    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
                handler);
    }

    [Fact]
    public async Task Binds_every_field_of_a_fully_populated_deal()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("mergers-acquisitions-latest.p0.json")));

        var deals = await endpoints.GetLatestMergersAcquisitionsAsync(0, 1000);

        Assert.Equal(5, deals.Count);
        var repligen = deals[0];
        Assert.Equal("RGEN", repligen.Symbol);
        Assert.Equal("REPLIGEN CORP", repligen.CompanyName);
        Assert.Equal("0000730272", repligen.Cik);
        Assert.Equal("BioLife Solutions, Inc.", repligen.TargetedCompanyName);
        Assert.Equal("0000834365", repligen.TargetedCik);
        Assert.Equal("BLFS", repligen.TargetedSymbol);
        Assert.Equal(new LocalDate(2026, 8, 24), repligen.TransactionDate);
        Assert.Equal(Instant.FromUtc(2026, 8, 25, 1, 50, 52), repligen.AcceptedDate);
        Assert.StartsWith("https://www.sec.gov/", repligen.Link);
        Assert.Empty(Binding.Unbound(repligen));
    }

    [Fact]
    public async Task All_three_target_fields_are_nullable_and_a_small_sample_would_not_show_it()
    {
        // Measured over the 1,000 rows of page 0 on 2026-08-27: targetedCik null on 390, targetedSymbol on 181,
        // targetedCompanyName on 1. A 10-row sample shows none of them. Typing any of the three non-nullable
        // fails to compile under Nullable=enable with TreatWarningsAsErrors, rather than binding a coerced
        // empty string.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("mergers-acquisitions-latest.p0.json")));

        var deals = await endpoints.GetLatestMergersAcquisitionsAsync(0, 1000);

        Assert.Null(deals[2].TargetedCik);
        Assert.Equal("UDFI", deals[2].TargetedSymbol);
        Assert.Null(deals[3].TargetedSymbol);
        Assert.Null(deals[4].TargetedCompanyName);
    }

    [Fact]
    public async Task Targeted_cik_says_nothing_in_two_different_ways()
    {
        // null on 390 of 1,000 rows AND the sentinel "0000000000" on others. A caller checking only for null
        // treats the sentinel as a real CIK and looks up a filer that does not exist.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("mergers-acquisitions-latest.p0.json")));

        var deals = await endpoints.GetLatestMergersAcquisitionsAsync(0, 1000);

        Assert.Equal("0000000000", deals[1].TargetedCik);
        Assert.Null(deals[2].TargetedCik);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task Rejects_a_page_or_limit_that_cannot_produce_a_complete_walk(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestMergersAcquisitionsAsync(page, limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Rejects_a_limit_above_the_measured_server_clamp_instead_of_letting_it_be_clamped()
    {
        // limit=5000 answered 1,000 rows on 2026-08-27 — silently clamped. A caller who asks for 5,000 and
        // walks pages assuming they got them skips 80% of the archive and sees no error. Same guard, same
        // reason, as CompanyEndpoints.MaxDelistedPageSize.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestMergersAcquisitionsAsync(0, CompanyEndpoints.MaxMergerAcquisitionPageSize + 1));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Latest_sends_both_paging_parameters()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestMergersAcquisitionsAsync(2, 1000);

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("page=2", query);
        Assert.Contains("limit=1000", query);
    }

    [Fact]
    public async Task Search_sends_only_the_name_because_the_endpoint_ignores_paging()
    {
        // name=Bank answered 233 rows bare AND 233 rows with page=0&limit=5, measured 2026-08-27. The endpoint
        // returns its whole result set every time. A signature accepting page and limit would let a caller
        // believe they asked for five rows while holding 233 — and the response cannot reveal that, so the
        // request is the only place to assert it.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchMergersAcquisitionsAsync("Apple");

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("name=Apple", query);
        Assert.DoesNotContain("page=", query);
        Assert.DoesNotContain("limit=", query);
    }

    [Fact]
    public async Task Search_binds_its_whole_result_set()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("mergers-acquisitions-search.Apple.json")));

        var deals = await endpoints.SearchMergersAcquisitionsAsync("Apple");

        Assert.Equal(3, deals.Count);
        Assert.Equal("PEGY", deals[0].Symbol);
        Assert.Equal("Apple Hospitality REIT, Inc.", deals[1].CompanyName);
    }

    [Fact]
    public async Task Search_rejects_a_blank_name_because_the_endpoint_answers_400()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.SearchMergersAcquisitionsAsync("  "));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_is_empty_not_an_error()
    {
        // name=zzzznope answered [] with HTTP 200 on 2026-08-27.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Empty(await endpoints.SearchMergersAcquisitionsAsync("zzzznope"));
    }
}
