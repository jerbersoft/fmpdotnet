using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/delisted-companies</c>, checked against two pages captured live from FMP on 2026-08-26.
///
/// <para>The two captures are the ends of the archive: <c>page=0</c>, a full 100 rows, and <c>page=97</c>, the
/// short final page of 82. Between them they carry the three facts a caller has to know — that the order is
/// newest-delisting-first, that a future-dated delisting therefore sits on page 0, and that the walk ends with a
/// short page rather than an empty one. The archive held 9,782 rows.</para>
///
/// <para><b>The page-size question this endpoint arrived with is settled here.</b> It was unclear whether 100 was
/// the page size or a hard cap. It is a cap: <c>limit=1000</c> and <c>limit=100</c> answered byte-identical
/// 16,982-byte bodies of 100 rows, while <c>limit=10</c> was honoured. A caller who asked for 1,000 and paged
/// accordingly would silently read a tenth of the archive, so the SDK rejects the request instead.</para></summary>
public class DelistedCompaniesTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

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
    public async Task Maps_all_five_fields_of_a_captured_row()
    {
        var (endpoints, _) = Build(StubHandler.Json(Fixture("delisted-companies.p0.json")));

        var rows = await endpoints.GetDelistedAsync(page: 0, limit: 100);

        Assert.Equal(100, rows.Count);
        Assert.Equal("NB2.F", rows[0].Symbol);
        Assert.Equal("Northern Data AG", rows[0].CompanyName);
        Assert.Equal("FSX", rows[0].Exchange);
        Assert.Equal(new LocalDate(2018, 10, 2), rows[0].IpoDate);
        Assert.Equal(new LocalDate(2026, 12, 30), rows[0].DelistedDate);
    }

    [Fact]
    public async Task Page_zero_carries_a_delisting_dated_in_the_future()
    {
        // The trap this endpoint's name invites. The capture was taken on 2026-08-26 and its first row is dated
        // 2026-12-30 — four months ahead. Read as "has stopped trading", that row marks a live security as gone;
        // it is a scheduled delisting, and the ordering guarantees these sit at the front.
        var (endpoints, _) = Build(StubHandler.Json(Fixture("delisted-companies.p0.json")));

        var rows = await endpoints.GetDelistedAsync(page: 0, limit: 100);

        var captured = new LocalDate(2026, 8, 26);
        Assert.Contains(rows, r => r.DelistedDate > captured);
    }

    [Fact]
    public async Task Rows_arrive_newest_delisting_first()
    {
        // Not cosmetic: it is what puts the future-dated rows on page 0, and what lets a caller who only wants
        // recent delistings stop after a page or two instead of walking all 98.
        var (endpoints, _) = Build(StubHandler.Json(Fixture("delisted-companies.p0.json")));

        var dates = (await endpoints.GetDelistedAsync(0, 100)).Select(r => r.DelistedDate).ToList();

        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public async Task The_last_page_is_short_which_is_how_a_walk_knows_to_stop()
    {
        // Page 97 of 0..97. A caller walking until a page comes back short of `limit` ends here; one walking until
        // an empty page spends one more request on page 98, which answers `[]` with HTTP 200. Both terminate.
        var (endpoints, _) = Build(StubHandler.Json(Fixture("delisted-companies.p97.json")));

        var rows = await endpoints.GetDelistedAsync(page: 97, limit: 100);

        Assert.Equal(82, rows.Count);
        Assert.True(rows.Count < 100);
        Assert.Equal(new LocalDate(2002, 1, 31), rows[^1].DelistedDate);
        Assert.Equal("NMK", rows[^1].Symbol);
    }

    [Fact]
    public async Task A_page_past_the_end_is_an_empty_list_not_an_error()
    {
        // Measured at page 200, page 1,000 and page 100,000: HTTP 200 with `[]` every time. There is no status
        // code that says "you have gone too far", so the empty list is the only signal.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        var rows = await endpoints.GetDelistedAsync(page: 200, limit: 100);

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task A_missing_date_reads_as_null_rather_than_costing_the_whole_row()
    {
        // No captured row omitted either date. The tolerance is the converter's, and it matters because a single
        // unparseable date would otherwise abort the response and take the other 99 rows with it.
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"symbol":"X","companyName":"X Corp","exchange":"NYSE","ipoDate":"","delistedDate":"0000-00-00"}]"""));

        var row = (await endpoints.GetDelistedAsync(0, 1)).Single();

        Assert.Equal("X", row.Symbol);
        Assert.Null(row.IpoDate);
        Assert.Null(row.DelistedDate);
    }

    // ---- the request ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sends_page_and_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetDelistedAsync(page: 3, limit: 50);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/delisted-companies", uri.AbsolutePath);
        Assert.Contains("page=3", uri.Query);
        Assert.Contains("limit=50", uri.Query);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    public async Task Refuses_a_limit_above_the_measured_cap_instead_of_letting_fmp_clamp_it(int limit)
    {
        // The point of the whole constant. FMP answers 100 rows to `limit=1000` with HTTP 200 and no warning, so a
        // caller who trusted the parameter and stepped `page` by their own limit would read every tenth page and
        // believe they had the archive. Failing at the call site is the only place this is visible.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetDelistedAsync(page: 0, limit: limit));

        Assert.Equal("limit", error.ParamName);
        // And no request was spent finding out.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_cap_is_the_measured_one()
    {
        Assert.Equal(100, CompanyEndpoints.MaxDelistedPageSize);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task Rejects_a_negative_page_or_a_non_positive_limit(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetDelistedAsync(page, limit));

        Assert.Empty(handler.Requests);
    }
}
