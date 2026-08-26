using System.Net;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three analyst-and-surprise bulk downloads (#15), checked against responses captured live from FMP
/// on 2026-08-26 through the SDK's own bulk pipeline.
///
/// <para>Each fixture is the real header plus the rows that carry the edge cases the models document — not a
/// hand-written sample. The full responses were 314 kB / 5,277 rows, 326 kB / 13,363 rows and 3.1 MB / 65,945
/// rows respectively.</para></summary>
public class BulkAnalystDataTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (BulkEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new BulkEndpoints(new FmpBulkTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> rows)
    {
        var drained = new List<T>();
        await foreach (var row in rows) drained.Add(row);
        return drained;
    }

    // ───────────────────────── price-target-summary-bulk ─────────────────────────

    [Fact]
    public async Task Price_target_summary_maps_all_ten_columns()
    {
        var (endpoints, handler) = Build(StubHandler.Csv(Fixture("price-target-summary-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPriceTargetSummariesAsync());

        var aapl = rows[0];
        Assert.Equal("AAPL", aapl.Symbol);
        Assert.Equal(6, aapl.LastMonthCount);
        Assert.Equal(311.44m, aapl.LastMonthAvgPriceTarget);
        Assert.Equal(17, aapl.LastQuarterCount);
        Assert.Equal(331.69m, aapl.LastQuarterAvgPriceTarget);
        Assert.Equal(71, aapl.LastYearCount);
        Assert.Equal(307.39m, aapl.LastYearAvgPriceTarget);
        Assert.Equal(259, aapl.AllTimeCount);
        Assert.Equal(232.31m, aapl.AllTimeAvgPriceTarget);
        // The endpoint takes no parameters at all.
        Assert.DoesNotContain("&", handler.Requests[0].Query.TrimStart('?').Replace("apikey=k", ""));
    }

    [Fact]
    public async Task Publishers_is_a_json_array_inside_a_csv_field_and_is_parsed()
    {
        // The only column on the endpoint that is not a scalar. On the wire the JSON is CSV-quoted, so every
        // inner quote arrives doubled; "Investor's Business Daily" is in the fixture because an apostrophe inside
        // a doubled-quote field is exactly where a hand-rolled unescape goes wrong.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("price-target-summary-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPriceTargetSummariesAsync());

        Assert.Equal(
            ["StreetInsider", "Benzinga", "Pulse 2.0", "MarketWatch", "Investing", "Barrons", "Investor's Business Daily"],
            rows[0].Publishers);
    }

    [Fact]
    public async Task An_empty_publisher_array_is_empty_and_not_null()
    {
        // 874 of the 5,277 measured rows carry "[]". That is FMP saying there are none, which must stay
        // distinguishable from the SDK failing to read the field.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("price-target-summary-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPriceTargetSummariesAsync());

        var aadi = rows.Single(r => r.Symbol == "AADI");
        Assert.NotNull(aadi.Publishers);
        Assert.Empty(aadi.Publishers);
    }

    [Fact]
    public async Task An_unreadable_publisher_field_is_null_rather_than_empty_and_does_not_kill_the_stream()
    {
        // Throwing would abandon a 5,000-row download over one malformed row; returning empty would make a format
        // change upstream look like "nobody publishes on anything".
        const string Csv = """
            "symbol","lastMonthCount","lastMonthAvgPriceTarget","lastQuarterCount","lastQuarterAvgPriceTarget","lastYearCount","lastYearAvgPriceTarget","allTimeCount","allTimeAvgPriceTarget","publishers"
            "BAD",1,2,3,4,5,6,7,8,"not json at all"
            "GOOD",1,2,3,4,5,6,7,8,"[""OK""]"
            """;
        var (endpoints, _) = Build(StubHandler.Csv(Csv));

        var rows = await DrainAsync(endpoints.StreamPriceTargetSummariesAsync());

        Assert.Null(rows[0].Publishers);
        Assert.Equal(["OK"], rows[1].Publishers);   // the stream carried on
    }

    [Fact]
    public async Task A_window_with_no_coverage_reads_as_zero_because_the_payload_never_sends_a_blank()
    {
        // Not one blank field appeared in the 5,277 measured rows, so "no targets this month" and "average of
        // zero" are the same bytes. The count is the only usable gate, and this pins that the zero is preserved
        // rather than quietly turned into null.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("price-target-summary-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPriceTargetSummariesAsync());

        var a = rows.Single(r => r.Symbol == "A");
        Assert.Equal(0, a.LastMonthCount);
        Assert.Equal(0m, a.LastMonthAvgPriceTarget);   // zero, not null
        Assert.Equal(7, a.LastQuarterCount);           // and the populated window is intact beside it
        Assert.Equal(155.57m, a.LastQuarterAvgPriceTarget);
    }

    // ─────────────────── upgrades-downgrades-consensus-bulk ───────────────────

    [Fact]
    public async Task Analyst_consensus_maps_the_distribution_and_the_label()
    {
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("upgrades-downgrades-consensus-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamAnalystConsensusAsync());

        var hk = rows.Single(r => r.Symbol == "0005.HK");
        Assert.Equal(0, hk.StrongBuy);
        Assert.Equal(5, hk.Buy);
        Assert.Equal(10, hk.Hold);
        Assert.Equal(2, hk.Sell);
        Assert.Equal(0, hk.StrongSell);
        Assert.Equal("Hold", hk.Consensus);
    }

    [Fact]
    public async Task The_consensus_universe_is_global_and_symbol_ordered()
    {
        // 13,363 rows against price-target-summary's 5,277, and the first are Shenzhen and Hong Kong. A caller
        // assuming US tickers is wrong about most of what it reads.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("upgrades-downgrades-consensus-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamAnalystConsensusAsync());

        Assert.Equal("000550.SZ", rows[0].Symbol);
        Assert.Contains(rows, r => r.Symbol.EndsWith(".HK"));
    }

    [Fact]
    public async Task The_consensus_label_keeps_the_upstream_spelling_including_its_space()
    {
        // "Strong Buy", not "StrongBuy" — the label vocabulary does not match the column names it is derived
        // from, which is why this stays a string rather than becoming an enum.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("upgrades-downgrades-consensus-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamAnalystConsensusAsync());

        Assert.Equal("Strong Buy", rows.Single(r => r.Symbol == "ALTI").Consensus);
    }

    // ───────────────────────── earnings-surprises-bulk ─────────────────────────

    [Fact]
    public async Task Earnings_surprises_sends_the_year_and_maps_every_column()
    {
        var (endpoints, handler) = Build(StubHandler.Csv(Fixture("earnings-surprises-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEarningsSurprisesAsync(2025));

        Assert.Contains("year=2025", handler.Requests[0].Query);
        var first = rows[0];
        Assert.Equal("AMD.NE", first.Symbol);
        Assert.Equal(new LocalDate(2025, 12, 31), first.Date);
        Assert.Equal(1.26m, first.EpsActual);
        Assert.Equal(1.46m, first.EpsEstimated);
        Assert.Equal(new LocalDate(2026, 3, 26), first.LastUpdated);
    }

    [Fact]
    public async Task One_symbol_can_carry_five_rows_in_a_single_year()
    {
        // Fiscal quarters straddle the calendar year, so a "year" of results is not four rows per symbol. Anything
        // assuming otherwise silently drops results — and symbol+date is not unique either: 210 pairs repeated
        // within the measured year.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("earnings-surprises-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEarningsSurprisesAsync(2025));

        Assert.Equal(5, rows.Count(r => r.Symbol == "AMD.NE"));
    }

    [Fact]
    public async Task A_sub_cent_loss_survives_as_decimal()
    {
        // -0.0031 is four decimal places on a negative. decimal holds it exactly; the endpoint showed no exponent
        // notation at all, unlike eod-bulk where crypto forces it.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("earnings-surprises-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEarningsSurprisesAsync(2025));

        var fpc = rows.Single(r => r.Symbol == "FPC.V");
        Assert.Equal(-0.0031m, fpc.EpsActual);
        Assert.Equal(-0.02m, fpc.EpsEstimated);
    }

    [Fact]
    public async Task The_bulk_throttle_refusal_arrives_as_a_200_and_is_still_an_error()
    {
        // FMP answers a refused bulk call with HTTP 200 and a JSON envelope, not a 429. Unhandled, that would be
        // parsed as CSV and yield a stream of garbage rows rather than a failure.
        var (endpoints, _) = Build(StubHandler.Csv(
            """{"Error Message":"Frequent abuse on this API Endpoint may result in restrictions placed on this API Key"}""",
            HttpStatusCode.OK));

        var ex = await Assert.ThrowsAsync<FmpApiException>(
            () => DrainAsync(endpoints.StreamPriceTargetSummariesAsync()));

        Assert.Contains("price-target-summary-bulk", ex.Message);
        Assert.DoesNotContain("apikey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
