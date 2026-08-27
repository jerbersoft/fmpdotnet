using Microsoft.Extensions.Options;
using NodaTime;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class LatestStatementsTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses.Length > 0 ? responses : [StubHandler.Json("[]")]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_page_is_requested_by_page_and_limit()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetLatestStatementsAsync(page: 2, limit: 250);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/latest-financial-statements", uri.AbsolutePath);
        Assert.Contains("page=2", uri.Query);
        Assert.Contains("limit=250", uri.Query);
    }

    [Fact]
    public async Task A_row_binds_and_is_keyed_on_calendar_year_not_fiscal_year()
    {
        // The only path in this group keyed on calendarYear, measured 2026-08-27. A caller joining these rows to
        // the statement endpoints on "year" is joining two different years for any filer whose fiscal year does
        // not end in December.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("latest-financial-statements.p0.json")));

        var rows = await endpoints.GetLatestStatementsAsync(0, 250);

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(2026, rows[0].CalendarYear);
        Assert.Equal("Q2", rows[0].Period);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        // Space-separated, not ISO-T — the shape that would silently fail an Instant parse expecting a `T`.
        Assert.Equal(new LocalDateTime(2026, 8, 27, 11, 3, 21), rows[0].DateAdded);
    }

    [Theory]
    [InlineData(-1, 250)]
    [InlineData(101, 250)]
    [InlineData(0, 0)]
    [InlineData(0, 251)]
    public async Task An_out_of_range_page_or_limit_throws_before_a_request_goes_out(int page, int limit)
    {
        // Measured 2026-08-27: page=101 is HTTP 400 ("Maxmium Query Parameter…", FMP's spelling) and limit=1000
        // silently answers 250. A caller who asks for 1,000 a page and advances by 1,000 skips three quarters of
        // the feed and never sees an error — which is why the limit is refused here rather than clamped upstream.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestStatementsAsync(page, limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_walk_stops_at_the_first_short_page()
    {
        var full = string.Join(",", Enumerable.Range(0, 250).Select(i => $$"""{"symbol":"S{{i}}","calendarYear":2026}"""));
        var (endpoints, handler) = Build(
            StubHandler.Json($"[{full}]"),
            StubHandler.Json("""[{"symbol":"LAST","calendarYear":2026}]"""));

        var rows = new List<Models.LatestFinancialStatement>();
        await foreach (var row in endpoints.StreamLatestStatementsAsync()) rows.Add(row);

        Assert.Equal(251, rows.Count);
        Assert.Equal(2, handler.Requests.Count);      // it did not ask for a third page
        Assert.Contains("page=0", handler.Requests[0].Query);
        Assert.Contains("page=1", handler.Requests[1].Query);
    }

    [Fact]
    public void The_measured_ceilings_are_recorded_as_constants()
    {
        Assert.Equal(100, StatementEndpoints.MaxLatestStatementsPage);
        Assert.Equal(250, StatementEndpoints.MaxLatestStatementsPageSize);
    }
}
