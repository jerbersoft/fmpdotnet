using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The filing row and the two date fields on it, checked against captures taken live 2026-08-28.
///
/// <para><b>The two dates arrive in the same format and mean different things.</b> Across 2,115 rows sampled
/// from three paths, <c>filingDate</c>'s time component was <c>00:00:00</c> in 2,115 of 2,115 cases — it is a
/// date wearing a dummy time. <c>acceptedDate</c> was 19 characters in all 2,115 and is a real EDGAR wall clock
/// in US Eastern. Reading either with the other's converter compiles, binds, and is wrong by hours or by a
/// meaningless midnight.</para></summary>
public class SecFilingsTests
{
    // ---- the filingDate converter ------------------------------------------------------------------------------

    [Fact]
    public void A_filing_date_loses_its_dummy_midnight()
    {
        var row = JsonSerializer.Deserialize(
            """[{"filingDate":"2025-03-06 00:00:00"}]""", FmpJsonContext.Default.ListSecFiling)![0];

        Assert.Equal(new LocalDate(2025, 3, 6), row.FilingDate);
    }

    [Fact]
    public void A_filing_date_that_is_null_or_unreadable_costs_one_field_not_the_row()
    {
        // House rule for every date converter in this file: a single bad stamp must not abort the response and
        // take the other seven fields with it. The bare-ISO case is NOT a measured wire form — 2,115 of 2,115
        // rows carried the time — it is here to pin that an unexpected shape reads as null rather than throwing.
        var rows = JsonSerializer.Deserialize(
            """
            [{"symbol":"A","filingDate":null},
             {"symbol":"B","filingDate":""},
             {"symbol":"C","filingDate":"2025-03-06"}]
            """, FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Null(r.FilingDate));
        Assert.Equal("C", rows[2].Symbol);
    }

    // ---- binding -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_captured_eight_k_row_binds_seven_of_its_eight_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(5, rows.Count);
        // hasFinancials is explicitly null on all five: measured 2026-08-28 over 1,000 sec-filings-8k rows it was
        // null 107 times, false 725 and true 168, so a null here is the field FMP sent, not a field it omitted.
        Assert.Equal(["HasFinancials"], Binding.Unbound(rows[0]));
        Assert.Equal("SUNE", rows[0].Symbol);
        Assert.Equal("0000022701", rows[0].Cik);
        Assert.Equal("8-K", rows[0].FormType);
        Assert.Null(rows[0].HasFinancials);
        Assert.EndsWith("0000897101-24-000091-index.htm", rows[0].Link);
        Assert.EndsWith("pegy240248_8k.htm", rows[0].FinalLink);
    }

    [Fact]
    public void The_accepted_date_is_read_as_eastern_wall_clock_not_as_utc()
    {
        // The silent one. 2024-03-01 falls before that year's DST switch, so Eastern is UTC-5 and
        // "2024-03-01 22:47:48" is 2024-03-02T03:47:48Z. Read with NullableFmpInstantJsonConverter — the UTC twin,
        // one identifier away and the same wire format — every value would land five hours early, still sort
        // correctly, and still look entirely plausible.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 47, 48), rows[0].AcceptedDate);
        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 27, 32), rows[2].AcceptedDate);
    }

    [Fact]
    public void Filing_date_cannot_be_derived_from_accepted_date()
    {
        // The trap, in one response. Rows 1 and 2 were accepted at 22:47 and 22:45 on 2024-03-01 and carry a
        // filingDate of 2024-03-04. Rows 3 to 5 were accepted at 22:27 and 22:22 the same evening and carry a
        // filingDate of 2024-03-01. Same endpoint, same page, same acceptance hour, two different answers — so
        // neither field is computable from the other, and a caller filtering on the wrong one is not told.
        //
        // It matters because `from` and `to` filter acceptedDate, NOT filingDate: measured 2026-08-28,
        // sec-filings-financials over 2025-03-01..2025-03-05 answered 722 rows, of which 16 carried a filingDate
        // past the requested `to` — and all 16 of those carried an acceptedDate inside it, with zero rows in the
        // whole response falling outside. 722 is comfortably under the 1,000 cap, so truncation cannot explain it.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        var acceptedOn = new LocalDate(2024, 3, 1);
        Assert.All(rows, r => Assert.Equal(acceptedOn, r.AcceptedDate!.Value.InZone(
            DateTimeZoneProviders.Tzdb["America/New_York"]).Date));

        Assert.Equal(new LocalDate(2024, 3, 4), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 4), rows[1].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 1), rows[2].FilingDate);
    }

    // ---- the two feeds -----------------------------------------------------------------------------------------

    private static (SecFilingsEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SecFilingsEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task The_two_feeds_return_the_same_shape_and_differ_by_what_they_filter()
    {
        // Measured 2026-08-28 over 1,000 rows each. sec-filings-8k: formType "8-K" 1,000 times, hasFinancials
        // null 107 / false 725 / true 168. sec-filings-financials: formType "8-K" 861, "6-K" 137, "10-K" 2, and
        // hasFinancials true 1,000 times. One filters by form; the other by whether financials are attached —
        // which is why hasFinancials carries no information on the financials feed.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("sec-filings-8k.head.json")),
            StubHandler.Json(Binding.Fixture("sec-filings-financials.head.json")));

        var eightK = await endpoints.Get8KFilingsAsync(limit: 5);
        var financials = await endpoints.GetFilingsWithFinancialsAsync(limit: 5);

        Assert.All(eightK, r => Assert.Equal("8-K", r.FormType));
        Assert.All(eightK, r => Assert.Null(r.HasFinancials));

        Assert.All(financials, r => Assert.True(r.HasFinancials));
        Assert.Contains(financials, r => r.FormType == "6-K");
        Assert.Empty(Binding.Unbound(financials[0]));
    }

    [Fact]
    public async Task The_feeds_send_page_and_limit_and_omit_an_unset_range()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.Get8KFilingsAsync(page: 2, limit: 50);
        await endpoints.GetFilingsWithFinancialsAsync(
            new LocalDate(2025, 3, 1), new LocalDate(2025, 3, 5), page: 0, limit: 1000);

        Assert.Equal("/stable/sec-filings-8k", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("from=", handler.Requests[0].Query);
        Assert.DoesNotContain("to=", handler.Requests[0].Query);

        Assert.Equal("/stable/sec-filings-financials", handler.Requests[1].AbsolutePath);
        Assert.Contains("from=2025-03-01", handler.Requests[1].Query);
        Assert.Contains("to=2025-03-05", handler.Requests[1].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    [InlineData(5000)]
    public async Task A_limit_above_the_measured_cap_is_refused_on_both_feeds(int limit)
    {
        // Measured 2026-08-28: limit=2000 and limit=5000 each answered exactly 1,000 rows, HTTP 200, with
        // nothing in the response to say so. These feeds DO paginate — page 0 and page 1 return disjoint rows —
        // so a caller who asked for 5,000 and stepped `page` by 5,000 would read a fifth of the archive and be
        // told nothing at all.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        var first = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.Get8KFilingsAsync(limit: limit));
        var second = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFilingsWithFinancialsAsync(limit: limit));

        Assert.Equal("limit", first.ParamName);
        Assert.Equal("limit", second.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task A_negative_page_or_a_non_positive_limit_is_refused(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.Get8KFilingsAsync(page: page, limit: limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_filing_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, SecFilingsEndpoints.MaxSecFilingPageSize);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_on_both_feeds()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));
        var from = new LocalDate(2025, 3, 5);
        var to = new LocalDate(2025, 3, 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.Get8KFilingsAsync(from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFilingsWithFinancialsAsync(from, to));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task One_end_of_the_range_alone_is_allowed()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.Get8KFilingsAsync(from: new LocalDate(2025, 3, 1));
        await endpoints.Get8KFilingsAsync(to: new LocalDate(2025, 3, 5));

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_limit_exactly_at_the_measured_cap_succeeds_rather_than_being_refused()
    {
        // Task 2's review found the gap this closes: nothing asserted that the documented maximum itself is
        // accepted. ThrowIfGreaterThan is correct, but ThrowIfGreaterThanOrEqual would pass every other test
        // here while silently rejecting the one value callers are told is safe to send.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.Get8KFilingsAsync(limit: SecFilingsEndpoints.MaxSecFilingPageSize);

        Assert.Contains("limit=1000", handler.Requests.Single().Query);
    }
}
