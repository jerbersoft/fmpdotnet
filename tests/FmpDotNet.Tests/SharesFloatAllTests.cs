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
    /// one canned response cannot serve two requests.
    ///
    /// <para>Takes a queue rather than a single response because the walk tests need a different answer per page.
    /// <see cref="StubHandler"/> repeats its last entry forever, so a walk that failed to terminate would spin
    /// rather than fail — which is why those tests assert on the request count as well as the row count.</para></summary>
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
    public async Task Hits_its_own_path_carrying_the_page_and_the_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetAllSharesFloatAsync(page: 3, limit: 1000);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/shares-float-all", uri.AbsolutePath);
        Assert.Equal("?page=3&limit=1000", uri.Query);
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

    // ---- the measured page-size cap -----------------------------------------------------------------------------

    [Fact]
    public async Task Rejects_a_limit_above_the_measured_cap_rather_than_letting_fmp_clamp_it()
    {
        // Measured 2026-09-05: limit=5001, 6000, 10001 and 100000 all answer exactly 5,000 rows in a
        // byte-identical 836,819-byte body, while 2,001 and 4,999 are honoured exactly. A caller who asks for
        // 10,000 and advances the page index by 10,000 therefore reads rows 0-4,999, then 10,000-14,999, and
        // walks off the end having seen 45,000 of 85,821 rows — with HTTP 200 throughout and no error anywhere.
        // Same treatment as MaxDelistedPageSize and MaxCikListPageSize.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAllSharesFloatAsync(page: 0, limit: CompanyEndpoints.MaxSharesFloatPageSize + 1));

        // Rejected locally: no request went out.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Accepts_a_limit_exactly_at_the_measured_cap()
    {
        // The boundary is inclusive: 5,000 is the largest page FMP serves, not the first one it refuses.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetAllSharesFloatAsync(page: 0, limit: CompanyEndpoints.MaxSharesFloatPageSize);

        Assert.Equal("?page=0&limit=5000", handler.Requests.Single().Query);
    }

    // ---- the universe walk --------------------------------------------------------------------------------------

    /// <summary>A page of exactly <see cref="CompanyEndpoints.MaxSharesFloatPageSize"/> rows — what the walk has
    /// to read as "there is more", carrying only the one field the assertions need.</summary>
    private static HttpResponseMessage FullPage()
    {
        var rows = string.Join(",", Enumerable.Range(0, CompanyEndpoints.MaxSharesFloatPageSize)
            .Select(i => $"{{\"symbol\":\"S{i}\"}}"));
        return StubHandler.Json($"[{rows}]");
    }

    [Fact]
    public async Task The_stream_walks_every_page_until_one_comes_back_short()
    {
        // Two full pages at the cap, then a short one that ends the walk. A fourth response is queued to prove it
        // is never requested — StubHandler repeats its last response forever, so a walk that failed to stop would
        // spin rather than fail, and the request count is what catches that.
        var (endpoints, handler) = Build(
            FullPage(),
            FullPage(),
            StubHandler.Json("""[{"symbol":"ZZZ.TO"}]"""),
            StubHandler.Json("[]"));

        var symbols = new List<string>();
        await foreach (var row in endpoints.StreamAllSharesFloatAsync()) symbols.Add(row.Symbol!);

        Assert.Equal(CompanyEndpoints.MaxSharesFloatPageSize * 2 + 1, symbols.Count);
        Assert.Equal("ZZZ.TO", symbols[^1]);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task The_stream_asks_for_the_cap_on_every_page_so_the_page_ceiling_stays_out_of_reach()
    {
        // FMP saturates `page` at 1000: measured 2026-09-05, page 1001 and page 5000 re-serve page 1000's rows
        // rather than answering empty, and stable/cik-list does the same, so it is an FMP-wide ceiling rather than
        // a fact about this endpoint. At the 5,000 cap that ceiling sits at row 5,000,000, far past the
        // 85,821-row universe. At limit=50 it would sit at row 50,000, leaving 35,821 rows permanently
        // unreachable and a walk-until-empty loop that never terminates. Sending the cap is what keeps it
        // out of reach, so the page size the walk asks for is behaviour, not a tuning choice.
        var (endpoints, handler) = Build(FullPage(), StubHandler.Json("[]"));

        await foreach (var _ in endpoints.StreamAllSharesFloatAsync()) { }

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("?page=0&limit=5000", handler.Requests[0].Query);
        Assert.Equal("?page=1&limit=5000", handler.Requests[1].Query);
    }

    [Fact]
    public async Task The_stream_stops_on_an_empty_first_page_without_a_second_request()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var count = 0;
        await foreach (var _ in endpoints.StreamAllSharesFloatAsync()) count++;

        Assert.Equal(0, count);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_plan_refusal_throws_out_of_the_stream_rather_than_ending_it_quietly(HttpStatusCode status)
    {
        // A caller degrading to the per-symbol path has to be able to tell "refused" from "the universe is
        // empty". A stream that simply stopped would make those two indistinguishable — which is the same
        // conflation the nullable return on GetAllSharesFloatAsync was deleted for.
        var (endpoints, _) = Build(StubHandler.Status(status));

        var ex = await Assert.ThrowsAsync<FmpPlanRestrictedException>(async () =>
        {
            await foreach (var _ in endpoints.StreamAllSharesFloatAsync()) { }
        });

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task A_429_mid_walk_surfaces_rather_than_truncating_the_stream()
    {
        // 18 requests on the ordinary throttle is where a rate limit is most likely to bite, and a truncated
        // stream would look exactly like a complete universe to the caller.
        var throttled = StubHandler.Json("", HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter =
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        var (endpoints, _) = Build(FullPage(), throttled);

        var seen = 0;
        var ex = await Assert.ThrowsAsync<FmpRateLimitedException>(async () =>
        {
            await foreach (var _ in endpoints.StreamAllSharesFloatAsync()) seen++;
        });

        // The first page was delivered before the throttle hit: the rows already yielded are real.
        Assert.Equal(CompanyEndpoints.MaxSharesFloatPageSize, seen);
        Assert.Equal(Duration.FromSeconds(30), ex.RetryAfter);
    }
}
