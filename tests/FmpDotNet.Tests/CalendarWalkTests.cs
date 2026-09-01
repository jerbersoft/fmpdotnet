using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The page walk's three terminators, exercised on the helper itself rather than through a calendar
/// method (#49).
///
/// <para><b>Because the interesting cases are the expensive ones.</b> Proving the page ceiling bounds the loop
/// needs 100 <i>full</i> pages, and a full page at the real cap is 4000 rows — 400,000 parsed models for one
/// assertion. Called here with <c>rowCap: 2</c>, the identical proof costs 100 pages of two rows. The row cap
/// is a parameter of the walk, so nothing about production behaviour is being lowered: the two calendar
/// methods pass 4000, and <c>CalendarEndpointsTests</c> covers them at that width.</para></summary>
public class CalendarWalkTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(params string[] pages)
    {
        var handler = new StubHandler([.. pages.Select(p => StubHandler.Json(p))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    /// <summary>A page of <paramref name="rowCount"/> earnings rows, all dated the same day, with symbols
    /// numbered from <paramref name="startIndex"/> — which is what decides whether two pages share rows.</summary>
    private static string Page(int rowCount, int startIndex = 0) =>
        "[" + string.Join(",", Enumerable.Range(startIndex, rowCount).Select(i =>
            $$"""{"symbol":"S{{i}}","date":"2026-05-13","epsActual":1,"epsEstimated":1,"revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"}""")) + "]";

    private static Task<(List<EarningsCalendarEntry> Rows, CalendarWalk Walk)> RunWalkAsync(
        CalendarEndpoints endpoints, int rowCap) =>
        endpoints.WalkAsync(
            page => new FmpRequest("stable/earnings-calendar").With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListEarningsCalendarEntry,
            rowCap,
            CancellationToken.None);

    [Fact]
    public async Task A_short_page_ends_the_walk()
    {
        var (endpoints, handler) = Build(Page(2), Page(1, startIndex: 2));

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 2);

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new CalendarWalk(RowsReturned: 3, PagesFetched: 2, LastPageRowCount: 1, SeamDuplicateRows: 0), walk);
    }

    [Fact]
    public async Task The_page_ceiling_bounds_a_feed_that_never_shortens_and_never_repeats()
    {
        // The guard. No page ceiling was measured on either calendar path -- page=1000 answers [] -- so this
        // is never reached in practice. It exists because a sibling path already breaks the "a short page
        // ends it" reasoning: ipos-calendar serves every page full and every page identical.
        var pages = Enumerable.Range(0, CalendarEndpoints.MaxCalendarPages + 20)
            .Select(i => Page(2, startIndex: i * 2))
            .ToArray();
        var (endpoints, handler) = Build(pages);

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 2);

        Assert.Equal(CalendarEndpoints.MaxCalendarPages, handler.Requests.Count);
        Assert.Equal(CalendarEndpoints.MaxCalendarPages, walk.PagesFetched);
        Assert.Equal(CalendarEndpoints.MaxCalendarPages * 2, rows.Count);
        Assert.Equal(2, walk.LastPageRowCount);          // full, so AtRowCap will fire on the result type
    }

    [Fact]
    public async Task A_repeated_page_ends_the_walk_before_the_ceiling_and_is_not_appended()
    {
        // The ipos-calendar shape: page 1 and page 5 byte-identical to page 0, every page full. StubHandler
        // repeats its last response once its queue runs dry, which reproduces it exactly.
        var (endpoints, handler) = Build(Page(2));

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 2);

        Assert.Equal(2, rows.Count);                     // once, not a hundred times
        Assert.Equal(2, handler.Requests.Count);         // fetched, recognised, discarded
        Assert.Equal(1, walk.PagesFetched);
        Assert.Equal(0, walk.SeamDuplicateRows);         // a repeat is not a seam overlap
    }

    [Fact]
    public async Task A_partial_overlap_is_a_seam_and_does_not_end_the_walk()
    {
        // Two rows in common is not the same page: the walk must count them and keep going. This is the case
        // that separates the repeat terminator from the seam counter, and getting it wrong either truncates a
        // good walk or lets a repeating feed run to the ceiling.
        var (endpoints, handler) = Build(
            Page(4),                        // S0..S3
            Page(4, startIndex: 2),         // S2..S5 -- two shared
            Page(1, startIndex: 6));

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 4);

        Assert.Equal(9, rows.Count);                     // 4 + 4 + 1, nothing removed
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, walk.SeamDuplicateRows);
        Assert.Equal(1, walk.LastPageRowCount);
    }

    [Fact]
    public async Task A_row_duplicated_within_one_page_is_not_counted_as_a_seam_duplicate()
    {
        // Pins the distinct-vs-multiset choice in WalkAsync (`new HashSet<T>(rows)`). Page 0 carries S1 TWICE --
        // FMP's own duplicate, within one page, not a paging artefact -- and page 1 carries S1 once more, so the
        // two pages share exactly one DISTINCT row. A multiset-style count that matches each physical occurrence
        // of S1 in page 0 against page 1's single S1 would report 2; the distinct-set intersection this method
        // actually uses must report 1.
        string Row(int i) =>
            $$"""{"symbol":"S{{i}}","date":"2026-05-13","epsActual":1,"epsEstimated":1,"revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"}""";
        var page0 = $"[{Row(0)},{Row(1)},{Row(1)}]"; // S0, S1, S1 -- S1 duplicated within the page
        var page1 = $"[{Row(1)},{Row(2)}]";           // S1, S2 -- shares only S1 (one distinct row) with page 0
        var (endpoints, handler) = Build(page0, page1);

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 3);

        Assert.Equal(5, rows.Count);              // 3 + 2, nothing removed
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(1, walk.SeamDuplicateRows);  // one DISTINCT row shared, not two physical occurrences
        Assert.Equal(2, walk.LastPageRowCount);
    }

    [Fact]
    public async Task An_empty_first_page_costs_one_request_and_reports_one_page()
    {
        var (endpoints, handler) = Build("[]");

        var (rows, walk) = await RunWalkAsync(endpoints, rowCap: 2);

        Assert.Empty(rows);
        Assert.Single(handler.Requests);
        Assert.Equal(new CalendarWalk(RowsReturned: 0, PagesFetched: 1, LastPageRowCount: 0, SeamDuplicateRows: 0), walk);
    }
}
