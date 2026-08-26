using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/shares-float-all</c>, checked against two pages captured live from FMP on 2026-08-26.
///
/// <para>The two captures are <c>page=0&amp;limit=5</c> and <c>page=1&amp;limit=5</c>. They are disjoint and
/// consecutive — <c>000001.SZ … 000006.SZ</c> then <c>000007.SZ … 000011.SZ</c> — which is the whole evidence for
/// how this endpoint pages, and also the whole explanation of an incident described on
/// <see cref="CompanyEndpoints.GetAllSharesFloatAsync"/>: the universe is symbol-ordered and global, so page
/// zero is Shenzhen, not a sample.</para>
///
/// <para><b>Nothing here asserts that the endpoint is available.</b> It was recorded as 402 on Premium by the
/// application this SDK replaces and answered 200 on 2026-08-26, so the tests pin how each answer is handled and
/// never which answer arrives.</para></summary>
public class SharesFloatAllTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A fresh endpoint per call — <see cref="FmpTransport"/> disposes the response after reading it, so
    /// one canned response cannot serve two requests.</summary>
    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- mapping ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Maps_the_captured_page_onto_the_per_symbol_shares_float_model()
    {
        var (endpoints, _) = Build(StubHandler.Json(Fixture("shares-float-all.p0.json")));

        var rows = await endpoints.GetAllSharesFloatAsync(page: 0, limit: 5);

        Assert.NotNull(rows);
        Assert.Equal(5, rows.Count);
        Assert.Equal("000001.SZ", rows[0].Symbol);
        Assert.Equal(Instant.FromUtc(2026, 8, 25, 21, 36, 25), rows[0].AsOf);
        Assert.Equal(41.40900000201062m, rows[0].FreeFloat);
        Assert.Equal(8_035_796_667m, rows[0].FloatShares);
        Assert.Equal(19_405_918_198m, rows[0].OutstandingShares);
    }

    [Fact]
    public async Task Source_is_null_on_every_row_because_the_bulk_rows_do_not_carry_it()
    {
        // Five wire fields here against six on the per-symbol endpoint. The null therefore means "this endpoint
        // does not carry the field", NOT "FMP names no filing" — and the per-symbol path answers null too on every
        // ETF measured, so the two nulls are indistinguishable on the value alone. Only the call you made tells
        // them apart, which is why it is documented on the method.
        var (endpoints, _) = Build(StubHandler.Json(Fixture("shares-float-all.p0.json")));

        var rows = await endpoints.GetAllSharesFloatAsync(page: 0, limit: 5);

        Assert.NotNull(rows);
        Assert.All(rows, row => Assert.Null(row.Source));
    }

    [Fact]
    public void The_bulk_row_is_the_per_symbol_row_minus_source_and_nothing_else()
    {
        // Both directions matter, as on the per-symbol endpoint. A field FMP sends that no property claims is data
        // thrown away; a property FMP no longer sends reads silently null. The one permitted gap is "source",
        // and naming it explicitly is what makes reusing SharesFloat here safe rather than lucky.
        using var doc = JsonDocument.Parse(Fixture("shares-float-all.p0.json"));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = typeof(SharesFloat).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"SharesFloat.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        Assert.Equal(5, wire.Count);
        Assert.Empty(wire.Except(mapped));                              // FMP sends it, the model ignores it
        Assert.Equal(new[] { "source" }, mapped.Except(wire).ToArray()); // the model expects it, this path omits it
    }

    [Fact]
    public async Task Share_counts_survive_a_fractional_value_because_they_are_decimal_not_long()
    {
        // Same reasoning as the per-symbol path: System.Text.Json THROWS reading a fractional value into a long?,
        // which would abort the whole page rather than one field. On a paged universe walk that is worse — one bad
        // row loses an entire page of symbols.
        var (endpoints, _) = Build(StubHandler.Json(
            """
            [{"symbol":"TEST.SZ","date":"2026-08-25 21:36:25","freeFloat":41.4,
              "floatShares":25595002.125,"outstandingShares":25595002.125}]
            """));

        var rows = await endpoints.GetAllSharesFloatAsync(page: 0, limit: 1);

        Assert.NotNull(rows);
        Assert.Equal(25595002.125m, rows[0].FloatShares);
    }

    // ---- paging -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Consecutive_pages_are_disjoint_and_symbol_ordered()
    {
        // This is the paragraph of evidence that turns an unexplained data-loss incident into an ordinary paging
        // fact. Page 0 is 000001.SZ..000006.SZ and page 1 continues 000007.SZ..000011.SZ — Shenzhen listings,
        // because the universe is ordered by symbol and it is global. Reading page zero without knowing pages
        // existed is what made the predecessor conclude the endpoint returned "a partial (mostly foreign) page"
        // and skip its entire US universe.
        var (page0, _) = Build(StubHandler.Json(Fixture("shares-float-all.p0.json")));
        var (page1, _) = Build(StubHandler.Json(Fixture("shares-float-all.p1.json")));

        var first = await page0.GetAllSharesFloatAsync(page: 0, limit: 5);
        var second = await page1.GetAllSharesFloatAsync(page: 1, limit: 5);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.All(first, row => Assert.EndsWith(".SZ", row.Symbol));
        Assert.Empty(first.Select(r => r.Symbol).Intersect(second.Select(r => r.Symbol)));
        Assert.Equal("000006.SZ", first[^1].Symbol);
        Assert.Equal("000007.SZ", second[0].Symbol);
    }

    [Fact]
    public async Task Hits_its_own_path_carrying_the_page_the_limit_and_the_key()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetAllSharesFloatAsync(page: 3, limit: 1000);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/shares-float-all", uri.AbsolutePath);
        Assert.Equal("?page=3&limit=1000&apikey=k", uri.Query);
    }

    [Fact]
    public async Task A_page_past_the_end_of_the_universe_is_an_empty_list_and_not_null()
    {
        // The distinction the whole signature turns on: empty means "entitled, nothing here", null means
        // "not entitled". A walk terminates on the first, never on the second.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        var rows = await endpoints.GetAllSharesFloatAsync(page: 999_999, limit: 100);

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Rejects_a_negative_page_or_a_non_positive_limit_before_spending_a_request()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAllSharesFloatAsync(-1, 100));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAllSharesFloatAsync(0, 0));
        Assert.Empty(handler.Requests);
    }

    // ---- plan gating ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Plan_gating_throws_rather_than_coming_back_as_a_null(HttpStatusCode status)
    {
        // This method used to be TryGetAllSharesFloatAsync and answered null here, so an optional fast path could
        // degrade in one branch. That put two error channels on one signature and overloaded a nullable return
        // with a meaning the signature could not carry. A caller that wants to degrade catches instead — and gets
        // to see WHICH refusal arrived, which the null never told it.
        var (endpoints, _) = Build(StubHandler.Status(status));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(
            () => endpoints.GetAllSharesFloatAsync(page: 0, limit: 100));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task A_429_is_still_a_rate_limit_exception_on_this_path()
    {
        // The predecessor's adapter returned null on 402/403 but did NOT special-case 429, so a throttled call
        // there surfaced as a bare HttpRequestException. The SDK's transport handles it; this pins that the new
        // non-success path did not regress it into an FmpApiException.
        var response = StubHandler.Json("", HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var (endpoints, _) = Build(response);

        var ex = await Assert.ThrowsAsync<FmpRateLimitedException>(
            () => endpoints.GetAllSharesFloatAsync(page: 0, limit: 100));

        Assert.Equal(Duration.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task A_400_on_the_json_pipeline_surfaces_its_body_without_the_api_key()
    {
        // The same transport fix the CSV pipeline needed. Both paths called EnsureSuccessStatusCode(), and both
        // discarded whatever the body said.
        var handler = new StubHandler(StubHandler.Json("Query Error: bad page", HttpStatusCode.BadRequest));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new CompanyEndpoints(new FmpTransport(
            http, Options.Create(new FmpOptions { ApiKey = "super-secret-key" })));

        var ex = await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetAllSharesFloatAsync(page: 0, limit: 100));

        Assert.Equal("Query Error: bad page", ex.ErrorMessage);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.DoesNotContain("super-secret-key", ex.ToString());
    }
}
