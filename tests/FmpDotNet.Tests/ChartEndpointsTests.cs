using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The <c>Chart</c> group, checked against responses captured live from FMP on 2026-08-27.
///
/// <para>The AAPL fixtures around its 2020 four-for-one split are the ones that matter. All three daily variants
/// were captured for the same seven-session window, so the tests can assert the relationship <i>between</i> the
/// endpoints rather than just that each parses — and that relationship is the only thing distinguishing two
/// endpoints whose payloads are shape-identical.</para></summary>
public class ChartEndpointsTests
{
    private static readonly DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (ChartEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new ChartEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static readonly LocalDate SplitFrom = new(2020, 8, 27);
    private static readonly LocalDate SplitTo = new(2020, 9, 2);

    // ---- the split, which is the whole reason two of these endpoints are told apart -----------------------------

    [Fact]
    public async Task The_unadjusted_endpoint_returns_prices_four_times_the_split_adjusted_ones()
    {
        // THE test on this group. `non-split-adjusted` and `dividend-adjusted` return byte-identical field names,
        // so nothing in a payload — and nothing the compiler can see — distinguishes a bar from one endpoint from
        // a bar from the other. What separates them is the value, and the split is where that separation is
        // unmistakable rather than a rounding difference: exactly 4x, on a known corporate action.
        //
        // Asserting only that each endpoint parses would be satisfied by wiring both methods to the same path.
        // This is not.
        var raw = await Build(Fixture("historical-price-eod-non-split-adjusted.AAPL.2020-split.json"))
            .Endpoints.GetUnadjustedAsync("AAPL", SplitFrom, SplitTo);
        var split = await Build(Fixture("historical-price-eod-full.AAPL.2020-split.json"))
            .Endpoints.GetEndOfDayFullAsync("AAPL", SplitFrom, SplitTo);

        var preSplit = new LocalDate(2020, 8, 28);
        var rawBar = Assert.Single(raw, b => b.Date == preSplit);
        var splitBar = Assert.Single(split, b => b.Date == preSplit);

        Assert.Equal(504.04m, rawBar.AdjOpen);
        Assert.Equal(126.01m, splitBar.Open);
        Assert.Equal(4m, rawBar.AdjClose!.Value / splitBar.Close!.Value);

        // Volume moves the other way by the same factor, and it is the field most likely to be assumed comparable
        // because its name carries no `adj` prefix.
        Assert.Equal(46_907_500L, rawBar.Volume);
        Assert.Equal(187_630_000L, splitBar.Volume);
        Assert.Equal(4L, splitBar.Volume!.Value / rawBar.Volume!.Value);
    }

    [Fact]
    public async Task Dividend_adjusted_differs_from_split_adjusted_after_the_split_is_accounted_for()
    {
        // The second half of the same point: `dividend-adjusted` is not simply the split-adjusted series under
        // another name. On the session AFTER the split both are already split-adjusted, so any remaining gap is
        // the dividend back-adjustment alone — 129.04 against 125.06 on 2020-08-31.
        var dividend = await Build(Fixture("historical-price-eod-dividend-adjusted.AAPL.2020-split.json"))
            .Endpoints.GetDividendAdjustedAsync("AAPL", SplitFrom, SplitTo);
        var split = await Build(Fixture("historical-price-eod-full.AAPL.2020-split.json"))
            .Endpoints.GetEndOfDayFullAsync("AAPL", SplitFrom, SplitTo);

        var postSplit = new LocalDate(2020, 8, 31);
        var dividendBar = Assert.Single(dividend, b => b.Date == postSplit);
        var splitBar = Assert.Single(split, b => b.Date == postSplit);

        Assert.Equal(129.04m, splitBar.Close);
        Assert.Equal(125.06m, dividendBar.AdjClose);
        Assert.True(dividendBar.AdjClose < splitBar.Close,
            "Dividend-adjusted history is back-adjusted downward, so it must sit below the split-only series.");

        // Volume is identical here, unlike the unadjusted comparison — both are split-adjusted.
        Assert.Equal(splitBar.Volume, dividendBar.Volume);
    }

    [Fact]
    public async Task The_two_adjusted_endpoints_request_different_paths()
    {
        // They share a return type, so a copy-paste that pointed both at one path would still compile, still
        // deserialise, and still pass any test that only looked at the rows.
        var (unadjusted, unadjustedHandler) = Build();
        await unadjusted.GetUnadjustedAsync("AAPL", SplitFrom, SplitTo);
        var (dividend, dividendHandler) = Build();
        await dividend.GetDividendAdjustedAsync("AAPL", SplitFrom, SplitTo);

        Assert.Contains("historical-price-eod/non-split-adjusted", unadjustedHandler.Requests[0].ToString());
        Assert.Contains("historical-price-eod/dividend-adjusted", dividendHandler.Requests[0].ToString());
    }

    // ---- intraday ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_intraday_bar_is_read_as_eastern_wall_clock()
    {
        // Same "uuuu-MM-dd HH:mm:ss" string the economic calendar uses, and that one is UTC — so the string cannot
        // say which converter is right and the compiler will never object. What settles it is the session: the
        // captured 2026-08-26 session runs 09:30 to 15:59, which is the US regular session in New York. Read as
        // UTC it would open at 05:30 ET.
        //
        // The assertion therefore goes the whole way a caller would, through tzdb to a wall clock, rather than
        // pinning an Instant — pinning the Instant produced by the wrong converter would be just as green.
        var bars = await Build(Fixture("historical-chart-1min.AAPL.head.json"))
            .Endpoints.GetIntradayAsync("AAPL", ChartInterval.OneMinute,
                new LocalDate(2026, 8, 26), new LocalDate(2026, 8, 26));

        var newest = bars[0];
        var wallClock = newest.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDate(2026, 8, 26), wallClock.Date);
        Assert.Equal(new LocalTime(15, 59, 0), wallClock.TimeOfDay);

        // 15:59 is the last bar of a session that closes at 16:00, which is what makes "bars are stamped with
        // their OPEN, and the final one is short" checkable rather than asserted.
        Assert.Equal(313.52m, newest.Open);
        Assert.Equal(313.23m, newest.Low);
        Assert.Equal(313.52m, newest.High);
    }

    [Theory]
    [InlineData(ChartInterval.OneMinute, "1min")]
    [InlineData(ChartInterval.FiveMinutes, "5min")]
    [InlineData(ChartInterval.FifteenMinutes, "15min")]
    [InlineData(ChartInterval.ThirtyMinutes, "30min")]
    [InlineData(ChartInterval.OneHour, "1hour")]
    [InlineData(ChartInterval.FourHours, "4hour")]
    public async Task Every_interval_maps_to_the_path_segment_fmp_serves(ChartInterval interval, string segment)
    {
        // A wrong segment is not a compile error and not an exception: FMP answers an unknown interval with HTTP
        // 404 and the body `[]`, which the transport reports as "no explanation in the body" — so a typo here
        // would surface to a caller as "this symbol has no intraday history".
        var (endpoints, handler) = Build();
        await endpoints.GetIntradayAsync("AAPL", interval, SplitFrom, SplitTo);

        Assert.Contains($"stable/historical-chart/{segment}?", handler.Requests[0].ToString());
    }

    [Fact]
    public void An_undeclared_interval_throws_rather_than_reaching_fmp_as_a_bad_path()
    {
        var undeclared = (ChartInterval)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => undeclared.ToPathSegment());
    }

    // ---- the guards -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_backwards_range_is_rejected_before_it_costs_a_call()
    {
        // Measured 2026-08-27: intraday answers a transposed range with 390 well-formed rows dated to the `to`
        // day — plausible data for the wrong end of the range — while the daily endpoints answer []. Neither
        // reports anything. Rejecting it here is the only place the two can be made to behave alike.
        var (endpoints, handler) = Build();
        var from = new LocalDate(2026, 8, 26);
        var to = new LocalDate(2026, 8, 24);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIntradayAsync("AAPL", ChartInterval.OneMinute, from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetEndOfDayAsync("AAPL", from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetEndOfDayFullAsync("AAPL", from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetUnadjustedAsync("AAPL", from, to));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetDividendAdjustedAsync("AAPL", from, to));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_single_day_range_is_allowed()
    {
        // The guard rejects `to < from`, not `to == from`, and a one-session request is an ordinary thing to ask.
        var (endpoints, handler) = Build(Fixture("historical-price-eod-light.AAPL.json"));
        var day = new LocalDate(2026, 8, 26);

        await endpoints.GetEndOfDayAsync("AAPL", day, day);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_blank_symbol_is_rejected_before_it_costs_a_call()
    {
        var (endpoints, handler) = Build();
        var day = new LocalDate(2026, 8, 26);

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetEndOfDayAsync("   ", day, day));
        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetIntradayAsync("", ChartInterval.OneHour, day, day));

        Assert.Empty(handler.Requests);
    }

    // ---- request shape ----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_range_is_sent_in_fmps_iso_form()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetEndOfDayAsync("AAPL", new LocalDate(2026, 8, 24), new LocalDate(2026, 8, 26));

        var uri = handler.Requests[0].ToString();
        Assert.Contains("stable/historical-price-eod/light?", uri);
        Assert.Contains("symbol=AAPL", uri);
        Assert.Contains("from=2026-08-24", uri);
        Assert.Contains("to=2026-08-26", uri);
    }

    [Fact]
    public async Task The_light_endpoint_reads_the_close_and_the_session_date()
    {
        var rows = await Build(Fixture("historical-price-eod-light.AAPL.json"))
            .Endpoints.GetEndOfDayAsync("AAPL", new LocalDate(2026, 8, 24), new LocalDate(2026, 8, 26));

        // Newest first, as measured — the SDK does not re-sort, so this pins the order FMP sent.
        Assert.Equal(new LocalDate(2026, 8, 26), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 24), rows[^1].Date);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(313.45m, rows[0].Price);
    }

    [Fact]
    public async Task An_unknown_symbol_reads_as_an_empty_list_rather_than_an_error()
    {
        // Measured 2026-08-27: HTTP 200 with the body []. "Not found" is a shape on this API, not a status code.
        var rows = await Build("[]").Endpoints.GetEndOfDayAsync(
            "NOSUCHTICKERXYZ", new LocalDate(2026, 8, 24), new LocalDate(2026, 8, 26));

        Assert.Empty(rows);
    }
}
