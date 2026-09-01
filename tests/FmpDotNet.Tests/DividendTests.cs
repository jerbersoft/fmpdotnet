using System.Globalization;
using System.Text;
using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/dividends</c> and <c>stable/dividends-calendar</c>, checked against captures taken live
/// 2026-08-28.
///
/// <para>One record serves both: their field sets were measured byte-identical, nine fields in the same order.
/// What differs is everything around the record — one takes a symbol and returns a whole history, the other
/// takes a date range and returns every symbol in it, and only the second truncates.</para></summary>
public class DividendTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // A response per page, in order. StubHandler repeats its last response once the queue runs dry, which is
    // the ipos-calendar shape and is what the walk's repeat terminator exists for -- so a test that wants the
    // walk to STOP must end its queue with a short page.
    private static (CalendarEndpoints Endpoints, StubHandler Handler) BuildPages(params string[] pages)
    {
        var handler = new StubHandler([.. pages.Select(p => StubHandler.Json(p))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    // ---- binding ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_per_symbol_row_binds_all_nine_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("dividends.AAPL.json"));

        var rows = await endpoints.GetDividendsAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 10), rows[0].Date);
        Assert.Equal(Day(2026, 8, 10), rows[0].RecordDate);
        Assert.Equal(Day(2026, 8, 13), rows[0].PaymentDate);
        Assert.Equal(Day(2026, 7, 30), rows[0].DeclarationDate);
        Assert.Equal(0.27m, rows[0].AdjDividend);
        Assert.Equal(0.27m, rows[0].DividendAmount);
        Assert.Equal(0.3438655680269902m, rows[0].Yield);
        Assert.Equal("Quarterly", rows[0].Frequency);
    }

    [Fact]
    public async Task A_blank_declaration_date_reads_as_null_and_costs_nothing_else_on_the_row()
    {
        // The measured shape, not an edge case: declarationDate was blank on 325 of the 622 rows this fixture's
        // request returned, and on 2232 of 4000 in a wider one. NullableLocalDateJsonConverter reads "" as null
        // because LocalDatePattern.Iso.Parse("") fails and it answers null rather than throwing — a throw here
        // would cost the whole response, since FmpTransport does not wrap DeserializeAsync.
        var (endpoints, _) = Build(Binding.Fixture("dividends-calendar.head.json"));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Null(r.DeclarationDate));
        Assert.Equal(["DeclarationDate"], Binding.Unbound(rows[0]));
        // Everything else on the row survived the blank.
        Assert.Equal("001231.SZ", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 24), rows[0].RecordDate);
        Assert.Equal(0.15m, rows[0].DividendAmount);
        Assert.Equal("Annual", rows[0].Frequency);
    }

    [Fact]
    public void The_wire_name_dividend_binds_to_the_property_named_DividendAmount()
    {
        // C# forbids a member sharing its type's name (CS0542), so the property is renamed and the wire name is
        // pinned by an explicit attribute. Without that attribute `dividend` would not bind and AdjDividend
        // would still populate — half the row correct, which is the failure worth a test of its own.
        var row = JsonSerializer.Deserialize(
            """[{"dividend":1.25,"adjDividend":9.99}]""", FmpJsonContext.Default.ListDividend)![0];

        Assert.Equal(1.25m, row.DividendAmount);
        Assert.Equal(9.99m, row.AdjDividend);
    }

    [Fact]
    public void The_four_dates_are_read_independently_and_none_is_assumed_to_precede_another()
    {
        // In the captured calendar rows a recordDate falls before its date and a paymentDate three weeks after.
        // Nothing in the SDK sorts or validates them against each other.
        var row = JsonSerializer.Deserialize(
            """
            [{"date":"2026-08-25","recordDate":"2026-08-24","paymentDate":"2026-09-10",
              "declarationDate":"2026-07-01"}]
            """, FmpJsonContext.Default.ListDividend)![0];

        Assert.Equal(Day(2026, 8, 25), row.Date);
        Assert.Equal(Day(2026, 8, 24), row.RecordDate);
        Assert.Equal(Day(2026, 9, 10), row.PaymentDate);
        Assert.Equal(Day(2026, 7, 1), row.DeclarationDate);
    }

    [Fact]
    public void Frequency_stays_a_string_because_the_observed_set_depends_on_which_path_answered()
    {
        // Measured 2026-08-28: dividends?symbol=AAPL shows 2 distinct values (Quarterly x91, Irregular x1);
        // dividends-calendar over two days shows 7 (Monthly, Quarterly, Semi-Annual, Annual, Weekly, Irregular,
        // Special) and 8 over a wider window (adding Bi-Weekly). An enum built from either sample would be
        // wrong for the other, and would turn an unseen value into a deserialisation failure.
        var rows = JsonSerializer.Deserialize(
            """[{"frequency":"Bi-Weekly"},{"frequency":"Special"},{"frequency":"Something FMP Adds In 2027"}]""",
            FmpJsonContext.Default.ListDividend)!;

        Assert.Equal(["Bi-Weekly", "Special", "Something FMP Adds In 2027"], rows.Select(r => r.Frequency));
    }

    // ---- requests -----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_per_symbol_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        // limit is omitted rather than defaulted, because an absent limit returns the whole series: 92 rows for
        // AAPL, unchanged by limit=10000. A default of 100 would silently truncate a longer history.
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsAsync("AAPL");

        Assert.Equal("stable/dividends", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_per_symbol_path_sends_a_limit_when_one_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_per_symbol_path_offers_no_date_range_because_the_endpoint_ignores_one()
    {
        // Measured 2026-08-28: dividends?symbol=AAPL answers 92 rows, and the same call with
        // from=2024-01-01&to=2024-12-31 answers the same 92. The parameters are accepted and ignored, so the
        // signature does not offer them — a caller who could pass them would believe the filter happened.
        var method = typeof(CalendarEndpoints).GetMethod(nameof(CalendarEndpoints.GetDividendsAsync))!;

        Assert.DoesNotContain(method.GetParameters(), p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task The_calendar_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal("stable/dividends-calendar", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-08-24&to=2026-08-25", handler.Requests.Single().Query);
    }

    // ---- the walk past the cap (#49) ----------------------------------------------------------------------

    [Fact]
    public async Task A_full_page_of_dividends_is_followed_by_the_next_one()
    {
        // Measured 2026-09-01: May 2026 answers 4000 / 4000 / 1325 / 0 -- 9325 rows, of which this method
        // used to return 4000. A full year is 8 requests and 28,104 rows against the same 4000.
        var (endpoints, handler) = BuildPages(
            SyntheticDividends(4000, Day(2026, 5, 1)),
            SyntheticDividends(4000, Day(2026, 5, 1), startIndex: 4000),
            SyntheticDividends(1325, Day(2026, 5, 1), startIndex: 8000));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 5, 1), Day(2026, 5, 31));

        Assert.Equal(9325, rows.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("?from=2026-05-01&to=2026-05-31", handler.Requests[0].Query);
        Assert.Equal("?from=2026-05-01&to=2026-05-31&page=1", handler.Requests[1].Query);
        Assert.Equal("?from=2026-05-01&to=2026-05-31&page=2", handler.Requests[2].Query);

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(3, result.PagesFetched);
        Assert.Equal(9325, result.RowsReturned);
        Assert.False(result.AtRowCap);
    }

    [Fact]
    public async Task A_dividend_range_that_fits_in_one_page_costs_one_request()
    {
        var (endpoints, handler) = BuildPages(SyntheticDividends(41, Day(2026, 8, 24)));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal(41, rows.Count);
        Assert.Single(handler.Requests);
        Assert.Equal(1, Assert.IsType<CalendarResult<Dividend>>(rows).PagesFetched);
    }

    [Fact]
    public async Task An_overlapping_dividend_seam_is_counted_and_reported_as_truncation()
    {
        // Measured on the 2025 dividends year: 8 pages, 913 rows served twice, and 913 different rows served
        // on neither side. The first seam duplicated 381 rows and lost exactly 381.
        var (endpoints, _) = BuildPages(
            SyntheticDividends(4000, Day(2026, 5, 1)),
            SyntheticDividends(1325, Day(2026, 5, 1), startIndex: 3619));   // 381 rows on both sides

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 5, 1), Day(2026, 5, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(5325, rows.Count);          // nothing removed
        Assert.Equal(381, result.SeamDuplicateRows);
        Assert.False(result.AtRowCap);
        Assert.True(result.LikelyTruncated);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_blank_symbol_is_refused_before_a_request_is_spent(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetDividendsAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_before_a_request_is_spent()
    {
        // Separate from the theory above: ArgumentException.ThrowIfNullOrWhiteSpace throws
        // ArgumentNullException for null, and Assert.ThrowsAsync matches the type exactly.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetDividendsAsync(null!));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetDividendsAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_through_the_shared_guard()
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetDividendsCalendarAsync(Day(2026, 8, 25), Day(2026, 8, 24)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    // ---- truncation ---------------------------------------------------------------------------------------

    [Fact]
    public async Task The_calendar_returns_a_CalendarResult_carrying_the_measured_row_cap()
    {
        var (endpoints, _) = Build(Binding.Fixture("dividends-calendar.head.json"));

        // from is the fixture's earliest row, deliberately. Ask for a day earlier than the earliest row FMP
        // returned and MissesStartOfRange fires — correctly, since the SDK cannot tell an empty day from a
        // truncated one. This test is about the cap being carried on a complete response, so it asks for a range
        // the response actually covers.
        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 25), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(4000, result.RowCap);
        // Null, and deliberately: the row cap always fires first at 340-876 rows a day, so no window limit is
        // observable on this path and asserting one would be inventing evidence.
        Assert.Null(result.LookbackLimitDays);
        Assert.Equal(Day(2026, 8, 25), result.RequestedFrom);
        Assert.Equal(Day(2026, 8, 28), result.RequestedTo);
        Assert.Equal(Day(2026, 8, 25), result.EarliestReturnedDate);
        Assert.Equal(5, result.RowsReturned);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_response_at_the_cap_reports_itself_truncated()
    {
        // The measured headline: from=2025-01-01&to=2025-12-31 answered exactly 4000 rows whose earliest date
        // was 2025-12-29 — a request for a year, answered with its last three days. Both tells fire here, and
        // they are independent: the cap is visible in the count, the missing front only in the dates.
        var (endpoints, _) = Build(SyntheticCalendar(4000, Day(2025, 12, 29)));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2025, 1, 1), Day(2025, 12, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.True(result.AtRowCap);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
        Assert.True(CalendarResult<Dividend>.IsLikelyTruncated(rows));
    }

    [Fact]
    public async Task The_truncation_signal_is_taken_from_the_raw_response_before_any_row_is_dropped()
    {
        // The ordering that makes the signal trustworthy. 4000 rows arrive, one of them undated and therefore
        // dropped, so the caller holds 3999. Count the kept rows instead of the raw ones and this response --
        // genuinely at the cap -- reports itself complete.
        var body = SyntheticCalendar(3999, Day(2025, 12, 29), undatedRows: 1);
        var (endpoints, _) = Build(body);

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2025, 1, 1), Day(2025, 12, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(4000, result.RowsReturned);
        Assert.Equal(3999, result.Count);
        Assert.True(result.AtRowCap);
    }

    [Fact]
    public async Task A_calendar_row_with_an_unparseable_date_is_dropped_rather_than_aborting_the_response()
    {
        // Same rule the earnings calendar already applies: on a calendar the date is half the row's identity,
        // so a row that cannot be placed on a timeline is dropped, and RowsReturned says how many were.
        var (endpoints, _) = Build(
            """
            [{"symbol":"BAD.X","date":"","dividend":1,"frequency":"Annual"},
             {"symbol":"0018.HK","date":"2026-08-25","dividend":0.01,"frequency":"Annual"}]
            """);

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        var row = Assert.Single(rows);
        Assert.Equal("0018.HK", row.Symbol);
        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(2, result.RowsReturned);
    }

    /// <summary>A calendar payload of a given size. Synthetic on purpose — the cap needs 4000 rows to exercise
    /// and nothing about those rows matters except how many there are and which dates they carry, so shipping a
    /// 4000-row fixture would add a megabyte of noise and prove nothing the captures do not.</summary>
    private static string SyntheticCalendar(int rowCount, LocalDate day, int undatedRows = 0)
    {
        var json = new System.Text.StringBuilder("[");
        for (var i = 0; i < rowCount + undatedRows; i++)
        {
            if (i > 0) json.Append(',');
            var date = i < undatedRows ? "" : day.ToString("uuuu-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{i}}","date":"{{date}}","dividend":1,"adjDividend":1,"yield":1,"frequency":"Annual"}""");
        }
        return json.Append(']').ToString();
    }

    /// <summary>A dividends-calendar payload of a given size. Synthetic on purpose — the cap needs 4000 rows
    /// to exercise and nothing about those rows matters except how many there are, which dates they carry and
    /// whether two pages share any. <paramref name="startIndex"/> is what decides that last one.</summary>
    private static string SyntheticDividends(int rowCount, LocalDate day, int startIndex = 0)
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{startIndex + i}}","date":"{{day:uuuu-MM-dd}}","recordDate":"{{day:uuuu-MM-dd}}","paymentDate":"{{day:uuuu-MM-dd}}","declarationDate":"","adjDividend":1,"dividend":1,"yield":1,"frequency":"Annual"}""");
        }
        return json.Append(']').ToString();
    }
}
