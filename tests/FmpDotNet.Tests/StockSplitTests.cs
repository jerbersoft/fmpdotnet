using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/splits</c> and <c>stable/splits-calendar</c>, checked against captures taken live
/// 2026-08-28.
///
/// <para>One record serves both — five fields, measured identical. The calendar truncates, but not the way the
/// dividend calendar does: it clamps to a <b>90-day window measured from <c>to</c></b> and drops everything
/// earlier. A request for the whole of 2024 answered Q4 of 2024, at <b>737 rows</b> — nowhere near any cap, so
/// no row count could have seen it.</para></summary>
public class StockSplitTests
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

    // ---- binding ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_per_symbol_row_binds_all_five_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("splits.AAPL.json"));

        var rows = await endpoints.GetSplitsAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2020, 8, 31), rows[0].Date);
        Assert.Equal(4, rows[0].Numerator);
        Assert.Equal(1, rows[0].Denominator);
        Assert.Equal("stock-split", rows[0].SplitType);
    }

    [Fact]
    public async Task A_reverse_split_is_a_numerator_below_its_denominator_and_nothing_else()
    {
        // CYCU at 1-for-8 in the captured calendar page. The SDK does not compute a ratio, flag a direction or
        // normalise the pair: it reports the two integers FMP sent, and the caller divides if they want to.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.head.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 1, 1), Day(2026, 8, 28));

        var reverse = Assert.Single(rows, r => r.Symbol == "CYCU");
        Assert.Equal(1, reverse.Numerator);
        Assert.Equal(8, reverse.Denominator);
    }

    [Fact]
    public async Task A_null_split_type_binds_as_null_and_costs_nothing_else_on_the_row()
    {
        // 16 of 961 rows measured 2026-08-28. The other four fields on those rows are fully populated, so a
        // null here is FMP declining to classify the event, not a broken row.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.split-types.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal(5, rows.Count);
        Assert.Null(rows[0].SplitType);
        Assert.Equal(["SplitType"], Binding.Unbound(rows[0]));
        Assert.Equal("GAME", rows[0].Symbol);
        Assert.Equal(1, rows[0].Numerator);
        Assert.Equal(8, rows[0].Denominator);
    }

    [Fact]
    public async Task Every_split_type_FMP_sends_is_carried_through_verbatim()
    {
        // The complete measured set across 961 rows: stock-split x934, JSON-null x16, stock-dividend x10,
        // spin-off x1. Four values counting null, and no enum, because the set is a sample from one response.
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.split-types.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal(
            new string?[] { null, null, "stock-dividend", "stock-dividend", "spin-off" },
            rows.Select(r => r.SplitType));
    }

    [Fact]
    public void The_literal_string_None_is_carried_through_as_a_string_if_it_ever_arrives()
    {
        // Recorded rather than asserted as measured. FMP sends no "None" on this field — re-measured field by
        // field across all 961 rows on 2026-08-28 — and an earlier draft of the spec said it did, having
        // confused it with the sentinel on the previous slice's classification paths. Typed `string?`, the SDK
        // is right either way: a value it has never seen reaches the caller unchanged instead of throwing.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"X","splitType":"None"}]""", FmpJsonContext.Default.ListStockSplit)![0];

        Assert.Equal("None", row.SplitType);
    }

    [Fact]
    public void The_split_ratio_stays_int_because_the_measured_maxima_fit()
    {
        // 1,011,977 and 1,000,000 were the largest values across 961 rows, against an int.MaxValue of
        // 2,147,483,647, and 961 of 961 were whole. This is the opposite ruling from IpoCalendarEntry.MarketCap,
        // from the same kind of evidence: that field was measured at 74,999,999,925 and does NOT fit.
        var row = JsonSerializer.Deserialize(
            """[{"numerator":1011977,"denominator":1000000}]""", FmpJsonContext.Default.ListStockSplit)![0];

        Assert.Equal(1_011_977, row.Numerator);
        Assert.Equal(1_000_000, row.Denominator);
    }

    // ---- requests -----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_per_symbol_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSplitsAsync("AAPL");

        Assert.Equal("stable/splits", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_per_symbol_path_offers_no_date_range_because_the_endpoint_ignores_one()
    {
        // Measured 2026-08-28: splits?symbol=AAPL answers 5 rows with and without
        // from=2024-01-01&to=2024-12-31 — and AAPL had no split in 2024, so a working filter would have
        // answered zero.
        var method = typeof(CalendarEndpoints).GetMethod(nameof(CalendarEndpoints.GetSplitsAsync))!;

        Assert.DoesNotContain(method.GetParameters(), p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task The_calendar_path_sends_both_bounds()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Equal("stable/splits-calendar", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?from=2026-06-01&to=2026-08-28&apikey=k", handler.Requests.Single().Query);
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_blank_symbol_is_refused_before_a_request_is_spent(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetSplitsAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_before_a_request_is_spent()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetSplitsAsync(null!));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_is_refused_through_the_shared_guard()
    {
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetSplitsCalendarAsync(Day(2026, 8, 28), Day(2026, 6, 1)));

        Assert.Equal("to", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    // ---- the 90-day window --------------------------------------------------------------------------------

    [Fact]
    public async Task The_calendar_reports_a_ninety_day_window_and_no_row_cap()
    {
        var (endpoints, _) = Build(Binding.Fixture("splits-calendar.head.json"));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 8, 28), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.Equal(90, result.LookbackLimitDays);
        // Null, and deliberately: no row cap was measured on this path, and an invented one would be a number
        // nobody checked. 947 rows came back for the widest range tried.
        Assert.Null(result.RowCap);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_wider_than_ninety_days_reports_itself_truncated_at_a_row_count_no_cap_would_catch()
    {
        // The measured case, and the reason this task exists as written: from=2024-01-01&to=2024-12-31 answered
        // 737 rows whose earliest date was 2024-10-02. Nine months absent. AtRowCap is structurally blind here —
        // RowCap is null — so both surviving tells have to carry it.
        var (endpoints, _) = Build(SyntheticCalendar(737, Day(2024, 10, 2)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2024, 1, 1), Day(2024, 12, 31));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.AtRowCap);
        Assert.True(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_of_exactly_ninety_days_is_caught_by_the_start_of_range_tell_alone()
    {
        // Measured 2026-08-28 against a fixed to=2026-08-28: from at -88 days was honoured exactly, from at -90
        // answered an earliest row of 2026-05-31 against a requested 2026-05-30. So a 90-day span does not trip
        // ExceedsLookbackLimit and still loses a day, which is what MissesStartOfRange is for.
        var (endpoints, _) = Build(SyntheticCalendar(947, Day(2026, 5, 31)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 5, 30), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task A_range_inside_the_window_reports_itself_complete()
    {
        // from = to - 88 days, honoured exactly when measured: 946 rows, earliest 2026-06-01.
        var (endpoints, _) = Build(SyntheticCalendar(946, Day(2026, 6, 1)));

        var rows = await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        var result = Assert.IsType<CalendarResult<StockSplit>>(rows);
        Assert.False(result.LikelyTruncated);
        Assert.False(CalendarResult<StockSplit>.IsLikelyTruncated(rows));
    }

    /// <summary>A splits-calendar payload of a given size, every row on one date. Synthetic for the same reason
    /// as in <see cref="DividendTests"/>: what these tests exercise is a row count and an earliest date, and a
    /// 947-row capture would add noise without adding evidence.</summary>
    private static string SyntheticCalendar(int rowCount, LocalDate earliest)
    {
        var json = new System.Text.StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(System.Globalization.CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{i}}","date":"{{earliest:uuuu-MM-dd}}","numerator":2,"denominator":1,"splitType":"stock-split"}""");
        }
        return json.Append(']').ToString();
    }
}
