using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/earnings</c> and <c>stable/earnings-calendar</c>, checked against responses captured live
/// from FMP on 2026-08-26.
///
/// <para>The calendar fixtures are a matched pair — the same request with and without
/// <c>includeReportTimes=true</c> — and they are shipped whole, all 48 rows, because what they prove is a property
/// of the <i>set</i>: both carry the same 48 symbols, and exactly one row moves date when the flag is on. Trimming
/// them to one row each would delete the evidence.</para></summary>
public class CalendarEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // A fresh stub per call, deliberately: FmpTransport disposes the HttpResponseMessage once it has read the body,
    // so a single canned response cannot serve two calls — the second fails with ObjectDisposedException.
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

    private static LocalDate Day(int year, int month, int day) => new(year, month, day);

    // ------------------------------------------------------------------ stable/earnings

    [Fact]
    public async Task Maps_every_field_of_the_captured_aapl_earnings_rows()
    {
        var (endpoints, _) = Build(Fixture("earnings.AAPL.json"));

        var rows = await endpoints.GetEarningsAsync("AAPL", limit: 8);

        Assert.Equal(8, rows.Count);

        var head = rows[0];
        Assert.Equal("AAPL", head.Symbol);
        Assert.Equal(Day(2026, 10, 29), head.Date);
        Assert.Null(head.EpsActual);
        Assert.Equal(1.98m, head.EpsEstimated);
        Assert.Null(head.RevenueActual);
        Assert.Equal(113_205_200_000m, head.RevenueEstimated);
        Assert.Equal(Day(2026, 8, 26), head.LastUpdated);

        var reported = rows[1];
        Assert.Equal("AAPL", reported.Symbol);
        Assert.Equal(Day(2026, 7, 30), reported.Date);
        Assert.Equal(2.02m, reported.EpsActual);
        Assert.Equal(1.89m, reported.EpsEstimated);
        Assert.Equal(109_417_000_000m, reported.RevenueActual);
        Assert.Equal(109_038_900_000m, reported.RevenueEstimated);
        Assert.Equal(Day(2026, 8, 26), reported.LastUpdated);
    }

    [Fact]
    public async Task The_newest_earnings_row_is_in_the_future_and_carries_no_actuals()
    {
        // The trap on this endpoint. Rows are newest first and the newest has not happened: AAPL's head row was
        // 2026-10-29 when the capture was taken on 2026-08-26, with both actuals null and both estimates set. So
        // "the last N earnings" hands back N-1 reported quarters and a forecast, and averaging EpsActual across
        // the result quietly averages the wrong count.
        var (endpoints, _) = Build(Fixture("earnings.AAPL.json"));

        var rows = await endpoints.GetEarningsAsync("AAPL", limit: 8);

        Assert.True(rows[0].Date > Day(2026, 8, 26));       // future relative to the capture date
        Assert.Null(rows[0].EpsActual);
        Assert.NotNull(rows[0].EpsEstimated);               // the estimate is what makes it a forecast row
        Assert.All(rows.Skip(1), r => Assert.NotNull(r.EpsActual));

        // Newest first, strictly descending across all eight.
        for (var i = 1; i < rows.Count; i++)
            Assert.True(rows[i].Date < rows[i - 1].Date, $"row {i} is not older than row {i - 1}");

        // The filter a caller actually wants, and the count it produces.
        Assert.Equal(7, rows.Count(r => r.EpsActual is not null));
    }

    [Fact]
    public async Task Earnings_model_and_payload_agree_field_for_field()
    {
        // Both directions matter. A wrong [JsonPropertyName] does not fail, it silently reads null; a field FMP
        // sends that no property claims is data thrown away. All 165 rows of AAPL's full history carried exactly
        // these seven names, none missing and none extra.
        using var doc = JsonDocument.Parse(Fixture("earnings.AAPL.json"));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = WireNames(typeof(EarningsReport));

        Assert.Empty(wire.Except(mapped));
        Assert.Empty(mapped.Except(wire));
        Assert.Equal(7, mapped.Count);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task An_earnings_row_with_an_unparseable_date_is_dropped_rather_than_aborting_the_response()
    {
        // The date is half this row's identity - (symbol, date) is the key a caller stores and joins on - so a row
        // with no usable date is dropped rather than returned with a null one. The rest of the response survives,
        // which is the part that matters: one bad value must not cost the caller every other row.
        var (endpoints, _) = Build(
            """
            [{"symbol":"AAPL","date":"0000-00-00","epsActual":1,"epsEstimated":1,
              "revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"},
             {"symbol":"AAPL","date":null,"epsActual":2,"epsEstimated":2,
              "revenueActual":2,"revenueEstimated":2,"lastUpdated":"2026-08-26"},
             {"symbol":"AAPL","date":"2026-07-30","epsActual":2.02,"epsEstimated":1.89,
              "revenueActual":109417000000,"revenueEstimated":109038900000,"lastUpdated":"2026-08-26"}]
            """);

        var rows = await endpoints.GetEarningsAsync("AAPL");

        var row = Assert.Single(rows);
        Assert.Equal(Day(2026, 7, 30), row.Date);
        Assert.Equal(2.02m, row.EpsActual);
    }

    [Fact]
    public async Task Earnings_sends_symbol_and_limit_and_no_period()
    {
        // Unlike the period-shaped endpoints this one takes no period. Sending one would be inventing a
        // parameter the API does not have.
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsAsync("AAPL", limit: 8);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/earnings", uri.AbsolutePath);
        Assert.Equal("?symbol=AAPL&limit=8", uri.Query);
        Assert.DoesNotContain("period", uri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Earnings_omits_limit_when_none_is_asked_for()
    {
        // And that is the whole history: measured 2026-08-26, no limit answers 165 rows spanning 1985-09-30 to
        // 2026-10-29, not a recent window.
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsAsync("AAPL");

        Assert.Equal("?symbol=AAPL", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Earnings_rejects_a_blank_symbol_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetEarningsAsync("  "));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Earnings_rejects_a_non_positive_limit_before_spending_a_request(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetEarningsAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }

    // ------------------------------------------------------------------ stable/earnings-calendar

    [Fact]
    public async Task Maps_every_field_of_a_captured_calendar_row_without_report_times()
    {
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17));

        Assert.Equal(48, rows.Count);
        var row = rows[0];
        Assert.Equal("GFH.AE", row.Symbol);
        Assert.Equal(Day(2026, 5, 17), row.Date);
        Assert.Equal(0.03708m, row.EpsActual);
        Assert.Equal(0.08026m, row.EpsEstimated);
        Assert.Equal(350_977_000m, row.RevenueActual);
        Assert.Equal(638_486_100m, row.RevenueEstimated);
        Assert.Equal(Day(2026, 8, 17), row.LastUpdated);
    }

    [Fact]
    public async Task The_five_report_time_fields_are_null_when_the_flag_was_not_sent()
    {
        // Null here means "you did not ask", not "FMP does not know" - the fields are absent from the payload
        // entirely. Asserted across all 48 rows so it reads as a property of the response rather than of one row.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17));

        Assert.All(rows, r =>
        {
            Assert.Null(r.ReportTime);
            Assert.Null(r.PeriodEnding);
            Assert.Null(r.FiscalPeriod);
            Assert.Null(r.FiscalYear);
            Assert.Null(r.Confirmed);
            Assert.NotNull(r.Symbol);        // the seven that are always there are still there
            Assert.NotNull(r.Date);
            Assert.NotNull(r.LastUpdated);
        });
    }

    [Fact]
    public async Task The_five_report_time_fields_are_populated_when_the_flag_was_sent()
    {
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true);

        Assert.Equal(48, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.NotNull(r.PeriodEnding);
            Assert.NotNull(r.FiscalPeriod);
            Assert.NotNull(r.FiscalYear);
            Assert.NotNull(r.Confirmed);
        });

        var confirmedBmo = rows.Single(r => r.Symbol == "DSCT.TA");
        Assert.Equal(Day(2026, 5, 17), confirmedBmo.Date);
        Assert.Equal(0.65m, confirmedBmo.EpsActual);
        Assert.Equal(0.606m, confirmedBmo.EpsEstimated);
        Assert.Equal(2_898_780_000m, confirmedBmo.RevenueActual);
        Assert.Equal(5_889_000_000m, confirmedBmo.RevenueEstimated);
        Assert.Equal("bmo", confirmedBmo.ReportTime);
        Assert.Equal(Day(2026, 3, 31), confirmedBmo.PeriodEnding);
        Assert.Equal("Q1", confirmedBmo.FiscalPeriod);
        Assert.Equal(2026, confirmedBmo.FiscalYear);
        Assert.True(confirmedBmo.Confirmed);
        Assert.Equal(Day(2026, 8, 17), confirmedBmo.LastUpdated);

        // A row whose fiscal year lags the calendar year, with a negative EPS - both ordinary, neither a fault.
        var negative = rows.Single(r => r.Symbol == "0AAW.L");
        Assert.Equal(-0.17m, negative.EpsActual);
        Assert.Null(negative.EpsEstimated);
        Assert.Equal(Day(2025, 12, 31), negative.PeriodEnding);
        Assert.Equal("Q4", negative.FiscalPeriod);
        Assert.Equal(2025, negative.FiscalYear);
        Assert.False(negative.Confirmed);
    }

    [Fact]
    public async Task A_null_report_time_is_the_common_case_even_with_the_flag_set()
    {
        // 41 of 48 rows are null, against 5 bmo and 2 amc, so null cannot be read as "unusual". Those three are
        // the only values seen across a 4000-row sweep, and the SDK keeps the token verbatim rather than
        // normalising it.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true);

        Assert.Equal(41, rows.Count(r => r.ReportTime is null));
        Assert.Equal(5, rows.Count(r => r.ReportTime == "bmo"));
        Assert.Equal(2, rows.Count(r => r.ReportTime == "amc"));
        Assert.DoesNotContain(rows, r => r.ReportTime is not (null or "bmo" or "amc"));
    }

    [Fact]
    public async Task Calendar_model_and_payload_agree_field_for_field_on_the_flagged_capture()
    {
        using var doc = JsonDocument.Parse(Fixture("earnings-calendar.2026-05-16.times.json"));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = WireNames(typeof(EarningsCalendarEntry));

        Assert.Empty(wire.Except(mapped));
        Assert.Empty(mapped.Except(wire));
        Assert.Equal(12, mapped.Count);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_unflagged_capture_carries_the_same_seven_fields_as_stable_earnings()
    {
        // The unflagged row is a subset, not a different shape - which is why one model with five nullable extras
        // is right rather than two unrelated ones.
        using var plain = JsonDocument.Parse(Fixture("earnings-calendar.2026-05-16.json"));
        var wire = plain.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.Empty(wire.Except(WireNames(typeof(EarningsCalendarEntry))));   // nothing the model ignores
        Assert.True(wire.SetEquals(WireNames(typeof(EarningsReport))));        // and it is exactly the earnings seven
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Rows_are_returned_in_wire_order_because_fmp_does_not_sort_them()
    {
        // The flagged capture's first element is dated 2026-05-18, after its own second element and after the end
        // of the requested range. The week-long capture's first element was its last day. Sorting would hide how
        // arbitrary that is; the SDK preserves what it was sent.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true);

        Assert.Equal("RAM.BK", rows[0].Symbol);
        Assert.Equal(Day(2026, 5, 18), rows[0].Date);
        Assert.True(rows[1].Date < rows[0].Date);
    }

    // ---- the clamp, and the measurement that decided its default ----

    [Fact]
    public async Task Unclamped_by_default_keeps_the_row_fmp_re_dated_past_the_end_of_the_range()
    {
        // includeReportTimes does not ADD rows, it RE-DATES some. The plain and flagged captures of the same
        // request are both 48 rows over the same 48 symbols; exactly one - RAM.BK - moves from 2026-05-17 to
        // 2026-05-18 when the flag is on. Selection happens on the un-shifted date, so nothing else will ever
        // return that row: clamping it away is deletion, not deduplication. Hence the default is off.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true, clampToRange: false);

        Assert.Equal(48, rows.Count);
        var shifted = rows.Single(r => r.Symbol == "RAM.BK");
        Assert.Equal(Day(2026, 5, 18), shifted.Date);         // one day past `to`, and kept
        Assert.Equal(0.2m, shifted.EpsActual);
        Assert.Null(shifted.EpsEstimated);
        Assert.Equal(5_325_000_000m, shifted.RevenueActual);
        Assert.Equal(5_255_000_000m, shifted.RevenueEstimated);
        Assert.Null(shifted.ReportTime);
        Assert.Equal(Day(2026, 3, 31), shifted.PeriodEnding);
        Assert.Equal("Q1", shifted.FiscalPeriod);
        Assert.Equal(2026, shifted.FiscalYear);
        Assert.False(shifted.Confirmed);
        Assert.Equal(Day(2026, 8, 18), shifted.LastUpdated);
    }

    [Fact]
    public async Task Clamping_drops_exactly_the_re_dated_row_and_nothing_else()
    {
        var (unclampedEndpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));
        var unclamped = await unclampedEndpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true, clampToRange: false);

        var (clampedEndpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.times.json"));
        var clamped = await clampedEndpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true, clampToRange: true);

        Assert.Equal(47, clamped.Count);
        Assert.DoesNotContain(clamped, r => r.Symbol == "RAM.BK");
        Assert.All(clamped, r => Assert.InRange(r.Date!.Value, Day(2026, 5, 16), Day(2026, 5, 17)));

        // Exactly one row went, and it is the same object graph otherwise - order included.
        Assert.Equal(
            unclamped.Where(r => r.Symbol != "RAM.BK").ToList(),
            clamped.ToList());
    }

    [Fact]
    public async Task Clamping_off_and_on_are_the_only_difference_when_no_row_overshoots()
    {
        // The unflagged capture has no shifted row, so the clamp is a no-op on it. Pinned so a future change that
        // makes the clamp over-eager - an exclusive bound, say - fails here rather than only on the flagged pair.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 16), Day(2026, 5, 17), clampToRange: true);

        Assert.Equal(48, rows.Count);   // boundaries are inclusive at both ends
        Assert.Equal(32, rows.Count(r => r.Date == Day(2026, 5, 16)));
        Assert.Equal(16, rows.Count(r => r.Date == Day(2026, 5, 17)));
    }

    // ---- the 4000-row cap, and the walk past it (#49) -----------------------------------------------------

    [Fact]
    public async Task A_full_page_is_followed_by_the_next_one_and_the_two_are_returned_as_one_list()
    {
        // Measured 2026-09-01: from=2026-05-13&to=2026-05-19 answers 4000 rows on page 0, 2496 on page 1 and
        // 0 on page 2 -- 6496 in total, of which this method used to return 4000.
        var (endpoints, handler) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(2496, Day(2026, 5, 13), startIndex: 4000));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(6496, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(6496, result.RowsReturned);
        Assert.False(result.AtRowCap);              // the walk ended on a short page
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task The_walk_omits_page_on_the_first_request_and_numbers_the_rest_from_one()
    {
        // page=0 was measured byte-identical to sending no page at all, so the first request of a walk is the
        // request this method already made. That keeps every single-page caller's URL, cache key and log line
        // exactly as they were.
        var (endpoints, handler) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(4000, Day(2026, 5, 13), startIndex: 4000),
            SyntheticCalendar(7, Day(2026, 5, 13), startIndex: 8000));

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("?from=2026-05-13&to=2026-05-19", handler.Requests[0].Query);
        Assert.Equal("?from=2026-05-13&to=2026-05-19&page=1", handler.Requests[1].Query);
        Assert.Equal("?from=2026-05-13&to=2026-05-19&page=2", handler.Requests[2].Query);
    }

    [Fact]
    public async Task A_range_that_fits_in_one_page_costs_exactly_one_request()
    {
        // The common case, and the one a walk must not make more expensive. 3999 rows is one below the cap.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(3999, Day(2026, 5, 13)));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 14));

        Assert.Equal(3999, rows.Count);
        Assert.Single(handler.Requests);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(1, result.PagesFetched);
        Assert.False(result.AtRowCap);
        Assert.False(EarningsCalendarResult.IsLikelyTruncated(rows));
    }

    [Fact]
    public async Task An_empty_page_ends_the_walk_and_contributes_nothing()
    {
        // Measured: page 2 of the earnings week answers [] rather than an error, and page 101 and page 1000
        // answer [] too. There is no ceiling response to handle on this family.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13)), "[]");

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(4000, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(0, result.SeamDuplicateRows);
    }

    [Fact]
    public async Task A_page_that_repeats_its_predecessor_ends_the_walk_and_is_not_appended()
    {
        // ipos-calendar does exactly this today: page=1 and page=5 are byte-identical to page=0, every page
        // full, no page ever short. Without this terminator such a path walks to MaxCalendarPages and returns
        // the same rows a hundred times. StubHandler repeating its last response reproduces the shape exactly.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13)));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(4000, rows.Count);                 // once, not twice and not a hundred times
        Assert.Equal(2, handler.Requests.Count);        // the repeat was fetched, recognised and discarded
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(1, result.PagesFetched);
        Assert.True(result.AtRowCap);                   // stopped with a full page in hand
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task Rows_shared_across_a_seam_are_counted_and_left_in_the_list()
    {
        // Measured 2026-09-01: an overlapping seam duplicates and loses the same number of rows. The SDK
        // reports rather than repairs -- removing a duplicate would be guessing which of two identical rows
        // is the real one, and FMP's own data carries genuine duplicate rows.
        var (endpoints, _) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(2496, Day(2026, 5, 13), startIndex: 3900));   // 100 rows on both sides

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(6496, rows.Count);                                     // nothing removed
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(100, result.SeamDuplicateRows);
        Assert.False(result.AtRowCap);                                      // ended on a short page
        Assert.True(result.LikelyTruncated);                                // and is still missing ~100 rows
    }

    [Fact]
    public async Task Undated_rows_are_dropped_across_the_whole_walk_and_the_raw_count_still_says_so()
    {
        var (endpoints, _) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            """
            [{"symbol":"BAD.X","date":"","epsActual":1,"epsEstimated":null,
              "revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-17"},
             {"symbol":"GFH.AE","date":"2026-05-17","epsActual":0.03708,"epsEstimated":0.08026,
              "revenueActual":350977000,"revenueEstimated":638486100,"lastUpdated":"2026-08-17"}]
            """);

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(4002, result.RowsReturned);        // raw, both pages
        Assert.Equal(4001, result.Count);               // the undated row is gone
    }

    [Fact]
    public async Task The_truncation_signal_does_not_fire_one_row_below_the_cap()
    {
        var (endpoints, _) = Build(SyntheticCalendar(3999, Day(2026, 5, 13)));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 14));

        Assert.Equal(3999, rows.Count);
        Assert.False(EarningsCalendarResult.IsLikelyTruncated(rows));
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.False(result.AtRowCap);
        Assert.False(result.MissesStartOfRange);
    }

    [Fact]
    public async Task The_truncation_signal_survives_clamping_because_it_is_taken_before_the_clamp()
    {
        // This is a live bug in the consumer this SDK replaces: it clamps first, then tests rows.Count >= 4000.
        // Clamping removes the overshoot rows, so a genuinely truncated response reaches the test already under
        // the cap and is judged complete. Here page 1 repeats page 0, so the walk stops with a full page in
        // hand; 12 of the 4000 rows fall outside the range and the clamp takes the count to 3988.
        var (endpoints, _) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13), overshootRows: 12));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 13), Day(2026, 5, 14), clampToRange: true);

        Assert.Equal(3988, rows.Count);                                   // what a naive count test would see
        Assert.True(rows.Count < EarningsCalendarResult.RowCap);          // and it would call this complete
        Assert.True(EarningsCalendarResult.IsLikelyTruncated(rows));      // the SDK does not
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(4000, result.RowsReturned);                          // what FMP actually sent
        Assert.True(result.AtRowCap);
    }

    [Fact]
    public async Task A_missing_first_day_is_reported_as_truncation_even_far_below_the_cap()
    {
        // The second tell. FMP drops rows from the FRONT of the range: from=2026-05-13&to=2026-05-19 returned 4000
        // rows with no 2026-05-13 row at all, though that day alone answers 2039. A 7-day peak window measured
        // 3676 rows - 92% of the cap without crossing it - so a count-only test would miss the near-cap case.
        // Here the capture's earliest row is 2026-05-16 while 05-15 was asked for.
        var (endpoints, _) = Build(Fixture("earnings-calendar.2026-05-16.json"));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 15), Day(2026, 5, 17));

        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.False(result.AtRowCap);                        // 48 rows, nowhere near it
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
        Assert.Equal(Day(2026, 5, 16), result.EarliestReturnedDate);
        Assert.Equal(Day(2026, 5, 15), result.RequestedFrom);
        Assert.Equal(Day(2026, 5, 17), result.RequestedTo);
    }

    [Fact]
    public async Task An_empty_response_is_not_reported_as_truncated()
    {
        // Nothing came back, so there is no earliest date to compare and no rows to have been cut. "Empty" and
        // "cut short" are different answers and the signal must not conflate them.
        var (endpoints, _) = Build("[]");

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17));

        Assert.Empty(rows);
        Assert.False(EarningsCalendarResult.IsLikelyTruncated(rows));
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Null(result.EarliestReturnedDate);
        Assert.False(result.MissesStartOfRange);
    }

    [Fact]
    public void The_truncation_helper_falls_back_to_a_row_count_on_a_list_it_did_not_build()
    {
        // Concatenating chunks discards the per-response evidence, so the helper can only count. Documented rather
        // than hidden, and pinned so the fallback does not quietly become "always false".
        var foreign = Enumerable.Range(0, EarningsCalendarResult.RowCap)
            .Select(i => new EarningsCalendarEntry { Symbol = $"S{i}", Date = Day(2026, 5, 13) })
            .ToList();

        Assert.True(EarningsCalendarResult.IsLikelyTruncated(foreign));
        Assert.False(EarningsCalendarResult.IsLikelyTruncated(foreign.Take(3999).ToList()));
        Assert.Throws<ArgumentNullException>(() => EarningsCalendarResult.IsLikelyTruncated(null!));
    }

    // ---- request shape and argument validation ----

    [Fact]
    public async Task A_reversed_range_throws_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetEarningsCalendarAsync(Day(2026, 5, 17), Day(2026, 5, 16)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_single_day_range_is_allowed_and_is_the_recommended_chunk_width()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 13));

        Assert.Equal("?from=2026-05-13&to=2026-05-13", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Hits_its_own_path_carrying_from_and_to_only()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17));

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/earnings-calendar", uri.AbsolutePath);
        Assert.Equal("?from=2026-05-16&to=2026-05-17", uri.Query);
        Assert.DoesNotContain("includeReportTimes", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sends_includeReportTimes_only_when_it_is_asked_for()
    {
        // Omitted rather than sent as false: the measured plain request had no such parameter, and there is no
        // evidence about how FMP reads an explicit false.
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17), includeReportTimes: true);

        Assert.Equal("?from=2026-05-16&to=2026-05-17&includeReportTimes=true",
            handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Clamping_is_a_client_side_decision_and_changes_no_query_parameter()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17), clampToRange: true);

        Assert.Equal("?from=2026-05-16&to=2026-05-17", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_calendar_row_with_an_unparseable_date_is_dropped_rather_than_aborting_the_response()
    {
        // Same rule as stable/earnings, and on the calendar it is also the only consistent answer: a null-dated
        // row cannot be clamped to a range either.
        var (endpoints, _) = Build(
            """
            [{"symbol":"BAD.X","date":"","epsActual":1,"epsEstimated":null,
              "revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-17"},
             {"symbol":"GFH.AE","date":"2026-05-17","epsActual":0.03708,"epsEstimated":0.08026,
              "revenueActual":350977000,"revenueEstimated":638486100,"lastUpdated":"2026-08-17"}]
            """);

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 16), Day(2026, 5, 17));

        var row = Assert.Single(rows);
        Assert.Equal("GFH.AE", row.Symbol);

        // And what was dropped stays visible: RowsReturned is the raw count, Count is what the caller holds.
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(2, result.RowsReturned);
        Assert.Single(result);
    }

    private static HashSet<string> WireNames(Type model) =>
        model.GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"{model.Name}.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

    /// <summary>A calendar payload of a given size. Synthetic on purpose — the cap needs 4000 rows to exercise and
    /// nothing about those rows matters except how many there are and which dates they carry, so shipping a 4000-row
    /// fixture would add a megabyte of noise and prove nothing the captures do not.</summary>
    private static string SyntheticCalendar(
        int rowCount, LocalDate day, int overshootRows = 0, int startIndex = 0)
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            // The overshoot rows sit one day past `to`, exactly as the re-dated real rows do.
            var date = i < overshootRows ? day.PlusDays(2) : day;
            if (i > 0) json.Append(',');
            json.Append(CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{startIndex + i}}","date":"{{date:uuuu-MM-dd}}","epsActual":1,"epsEstimated":1,"revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"}""");
        }
        return json.Append(']').ToString();
    }

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
}
