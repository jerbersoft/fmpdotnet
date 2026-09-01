using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/analyst-estimates</c>, checked against responses captured live from FMP on 2026-08-26.
///
/// <para>The two fixtures are the evidence behind the model shape and behind the ordering quirk, so the tests read
/// them from disk rather than embedding hand-written JSON — a hand-written payload can be quietly edited into
/// agreement with a wrong model, and a capture cannot.</para></summary>
public class AnalystEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>A fresh endpoint over a fresh single-response stub.
    ///
    /// <para>One per call, never shared across two calls in a test: <c>FmpTransport</c> disposes the
    /// <c>HttpResponseMessage</c> once it has read it, so a second call against the same stub gets a disposed
    /// content stream and throws <see cref="ObjectDisposedException"/> from somewhere that looks nothing like the
    /// cause.</para></summary>
    private static (AnalystEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new AnalystEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task Maps_every_field_of_the_first_captured_annual_row()
    {
        // All 22 wire fields of the 2030-09-27 row, asserted individually. The evidence note headlines this
        // endpoint as "23 fields" and then lists 22; the captures carry 22, on every row of both of them, which
        // Model_and_payload_agree_field_for_field pins independently of this count.
        var (endpoints, _) = Build(Fixture("analyst-estimates.AAPL.annual.json"));

        var rows = await endpoints.GetEstimatesAsync("AAPL");

        var row = rows[0];
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(new LocalDate(2030, 9, 27), row.Date);

        Assert.Equal(661_499_643_820m, row.RevenueLow);
        Assert.Equal(743_323_914_333m, row.RevenueHigh);
        Assert.Equal(693_145_000_000m, row.RevenueAvg);

        Assert.Equal(238_758_344_557m, row.EbitdaLow);
        Assert.Equal(268_291_584_000m, row.EbitdaHigh);
        Assert.Equal(250_180_259_784m, row.EbitdaAvg);

        Assert.Equal(221_553_951_387m, row.EbitLow);
        Assert.Equal(248_959_091_543m, row.EbitHigh);
        Assert.Equal(232_152_828_908m, row.EbitAvg);

        Assert.Equal(180_661_203_348m, row.NetIncomeLow);
        Assert.Equal(210_135_079_714m, row.NetIncomeHigh);
        Assert.Equal(192_060_121_612m, row.NetIncomeAvg);

        Assert.Equal(42_575_743_389m, row.SgaExpenseLow);
        Assert.Equal(47_842_154_606m, row.SgaExpenseHigh);
        Assert.Equal(44_612_516_313m, row.SgaExpenseAvg);

        // The wire sends this trio Avg/High/Low while the model groups it Low/High/Avg. Property order is not
        // part of deserialisation — these three values prove the [JsonPropertyName] mapping, not the ordering.
        Assert.Equal(12.04031m, row.EpsLow);
        Assert.Equal(14.00462m, row.EpsHigh);
        Assert.Equal(12.8m, row.EpsAvg);

        Assert.Equal(11, row.NumAnalystsRevenue);
        Assert.Equal(9, row.NumAnalystsEps);
    }

    [Theory]
    [InlineData("analyst-estimates.AAPL.annual.json")]
    [InlineData("analyst-estimates.AAPL.quarter.json")]
    public void Model_and_payload_agree_field_for_field(string fixture)
    {
        // Both directions matter. A wrong [JsonPropertyName] does not fail — it silently reads null — and a field
        // FMP sends that no property claims is data being thrown away. The eps trio is the live risk here: it is
        // the one group whose wire order differs from the model's, and transcribing it by position rather than by
        // name would map epsLow onto epsAvg without any test of a single value noticing.
        using var doc = JsonDocument.Parse(Fixture(fixture));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var properties = typeof(AnalystEstimate).GetProperties();

        // Period is the one property that is not a wire field — it is stamped from the request. Pinned by name so
        // that a second [JsonIgnore] added later has to be justified here rather than quietly widening the gap.
        var ignored = properties.Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            .Select(p => p.Name).ToArray();
        Assert.Equal(["Period"], ignored);

        var mapped = properties
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"AnalystEstimate.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        Assert.Equal(22, wire.Count);
        Assert.Empty(wire.Except(mapped));   // FMP sends it, the model ignores it
        Assert.Empty(mapped.Except(wire));   // the model expects it, FMP no longer sends it
    }

    [Fact]
    public void Every_captured_row_carries_the_same_22_fields()
    {
        // Not just the first row of each capture — a model built from row zero alone is a model built from a
        // sample of one, and this endpoint has no "period"/"fiscalYear" fields to fall back on if a later row
        // turned out to carry more.
        foreach (var fixture in new[] { "analyst-estimates.AAPL.annual.json", "analyst-estimates.AAPL.quarter.json" })
        {
            using var doc = JsonDocument.Parse(Fixture(fixture));
            var first = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();
            foreach (var row in doc.RootElement.EnumerateArray())
                Assert.Equal(first, row.EnumerateObject().Select(p => p.Name).ToArray());
        }
    }

    [Fact]
    public async Task Annual_rows_arrive_furthest_future_first_so_limit_takes_from_the_far_end()
    {
        // THE quirk of this endpoint, and the reason it has a test of its own rather than a doc sentence.
        // Measured 2026-08-26: period=annual&limit=3 answered 2030, 2029, 2028 — the three furthest-out annual
        // periods FMP holds, four years past the call, and NOT the next three. If FMP ever flips to ascending,
        // every caller that reached for `limit` starts getting different data with no error, so this must go red.
        var (endpoints, _) = Build(Fixture("analyst-estimates.AAPL.annual.json"));

        var rows = await endpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Annual, limit: 3);

        Assert.Equal(
            [new LocalDate(2030, 9, 27), new LocalDate(2029, 9, 27), new LocalDate(2028, 9, 27)],
            rows.Select(r => r.Date).ToArray());
    }

    [Fact]
    public async Task Quarterly_rows_arrive_furthest_future_first_too()
    {
        // period=quarter&limit=3 answered 2028-09-27, 2028-06-27, 2028-03-27 on the same date — descending on the
        // quarterly cadence as well, so the ordering is a property of the endpoint and not of the annual series.
        var (endpoints, _) = Build(Fixture("analyst-estimates.AAPL.quarter.json"));

        var rows = await endpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter, limit: 3);

        Assert.Equal(
            [new LocalDate(2028, 9, 27), new LocalDate(2028, 6, 27), new LocalDate(2028, 3, 27)],
            rows.Select(r => r.Date).ToArray());
    }

    [Theory]
    [InlineData("analyst-estimates.AAPL.annual.json")]
    [InlineData("analyst-estimates.AAPL.quarter.json")]
    public async Task The_sdk_hands_back_wire_order_untouched_rather_than_sorting_it(string fixture)
    {
        // Stated as an invariant rather than as three literal dates: the SDK must not reverse or re-sort, because
        // the wire order is the order FMP paged and limited by. Sorting ascending here would make `limit` describe
        // something other than what came back, and would hide the quirk instead of surfacing it.
        var (endpoints, _) = Build(Fixture(fixture));

        var rows = await endpoints.GetEstimatesAsync("AAPL");

        using var doc = JsonDocument.Parse(Fixture(fixture));
        var wireDates = doc.RootElement.EnumerateArray().Select(r => r.GetProperty("date").GetString()).ToArray();
        Assert.Equal(wireDates, rows.Select(r => r.Date?.ToString("uuuu-MM-dd", null)).ToArray());

        var descending = rows.Select(r => r.Date!.Value).OrderByDescending(d => d).ToArray();
        Assert.Equal(descending, rows.Select(r => r.Date!.Value).ToArray());
    }

    [Fact]
    public async Task The_same_date_appears_in_both_series_carrying_different_figures()
    {
        // 2028-09-27 is AAPL's fiscal year end AND its Q4 end, so it is in both captures with different numbers.
        // (symbol, date) is therefore not a unique key across the two series — the same trap enterprise-values
        // has — and the WIRE carries no period field to tell them apart. This is the measurement that justifies
        // the stamped Period property; the two tests below are what make it load-bearing.
        var (annualEndpoints, _) = Build(Fixture("analyst-estimates.AAPL.annual.json"));
        var (quarterEndpoints, _) = Build(Fixture("analyst-estimates.AAPL.quarter.json"));

        var annual = await annualEndpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Annual);
        var quarter = await quarterEndpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter);

        var annualRow = annual.Single(r => r.Date == new LocalDate(2028, 9, 27));
        var quarterRow = quarter.Single(r => r.Date == new LocalDate(2028, 9, 27));

        Assert.Equal(558_901_943_758m, annualRow.RevenueAvg);
        Assert.Equal(128_079_050_952m, quarterRow.RevenueAvg);
        Assert.NotEqual(annualRow.RevenueAvg, quarterRow.RevenueAvg);

        // And the wire itself says nothing about which is which.
        using var doc = JsonDocument.Parse(Fixture("analyst-estimates.AAPL.quarter.json"));
        var keys = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("period", keys);
        Assert.DoesNotContain("fiscalYear", keys);
    }

    [Theory]
    [InlineData(FiscalPeriod.Annual, "analyst-estimates.AAPL.annual.json")]
    [InlineData(FiscalPeriod.Quarter, "analyst-estimates.AAPL.quarter.json")]
    public async Task Every_returned_row_is_stamped_with_the_period_that_was_asked_for(FiscalPeriod period, string fixture)
    {
        // FMP sends no period, so the SDK echoes the request onto every row. Every row, not just the first: a
        // partial stamp would be worse than none, because the rows that missed out would read as Annual by
        // default and look like real data.
        var (endpoints, _) = Build(Fixture(fixture));

        var rows = await endpoints.GetEstimatesAsync("AAPL", period);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(period, r.Period));
    }

    [Fact]
    public async Task Concatenating_both_series_leaves_them_distinguishable()
    {
        // The bug this property exists to make unrepresentable. A consumer that calls the endpoint twice per
        // symbol and puts both lists in one collection keyed on (symbol, date) silently merges an annual row into
        // its Q4 row. With Period stamped, (symbol, period, date) separates them and nothing is lost.
        var (annualEndpoints, _) = Build(Fixture("analyst-estimates.AAPL.annual.json"));
        var (quarterEndpoints, _) = Build(Fixture("analyst-estimates.AAPL.quarter.json"));

        var annual = await annualEndpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Annual);
        var quarter = await quarterEndpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter);
        var all = annual.Concat(quarter).ToList();

        Assert.Equal(6, all.Count);

        // (symbol, date) collides on 2028-09-27 and loses a row; (symbol, period, date) does not.
        Assert.Equal(5, all.Select(r => (r.Symbol, r.Date)).Distinct().Count());
        Assert.Equal(6, all.Select(r => (r.Symbol, r.Period, r.Date)).Distinct().Count());

        var collided = all.Where(r => r.Date == new LocalDate(2028, 9, 27)).ToList();
        Assert.Equal(2, collided.Count);
        Assert.Equal([FiscalPeriod.Annual, FiscalPeriod.Quarter], collided.Select(r => r.Period).ToArray());
    }

    [Fact]
    public void The_stamped_period_is_neither_read_from_the_wire_nor_written_to_it()
    {
        // Period is [JsonIgnore] because it is not a wire field, and both halves of that are asserted here.
        //
        // Reading: a payload that carries a "period" key must NOT populate it. Written as a number because that is
        // what would successfully bind if the attribute were dropped — a string would throw and pass this test for
        // the wrong reason.
        var read = JsonSerializer.Deserialize(
            """[{"symbol":"TEST","date":"2028-09-27","period":1}]""",
            FmpJsonContext.Default.ListAnalystEstimate);

        Assert.NotNull(read);
        Assert.Equal(FiscalPeriod.Annual, read[0].Period);   // the enum default: "unstamped", not "annual"

        // Writing: the source-generated context must not emit it either, so a round-trip through the SDK's own
        // serializer cannot smuggle a request echo back out as though FMP had sent it.
        var written = JsonSerializer.Serialize(
            new List<AnalystEstimate> { new() { Symbol = "TEST", Period = FiscalPeriod.Quarter } },
            FmpJsonContext.Default.ListAnalystEstimate);

        Assert.DoesNotContain("period", written, StringComparison.OrdinalIgnoreCase);

        // And the fixtures, deserialised with no endpoint involved, are likewise unstamped — which is exactly why
        // GetEstimatesAsync has to do the stamping rather than the model.
        var fixture = JsonSerializer.Deserialize(
            Fixture("analyst-estimates.AAPL.quarter.json"), FmpJsonContext.Default.ListAnalystEstimate);
        Assert.NotNull(fixture);
        Assert.All(fixture, r => Assert.Equal(FiscalPeriod.Annual, r.Period));
    }

    [Fact]
    public async Task Money_is_decimal_so_a_fractional_estimate_survives_intact()
    {
        // These figures are computed means and extremes, not reported ones, so fractions are ordinary. The
        // guard is against a later narrowing to double: 12.04031 has no exact binary representation, and the
        // 15-digit revenue below is past where a double round-trip reproduces what FMP sent.
        var (endpoints, _) = Build(
            """
            [{"symbol":"TEST","date":"2028-09-27","revenueLow":123456789012.345,"revenueHigh":null,
              "revenueAvg":123456789012.345,"epsAvg":12.04031,"epsHigh":12.04031,"epsLow":12.04031,
              "numAnalystsRevenue":3,"numAnalystsEps":3}]
            """);

        var row = (await endpoints.GetEstimatesAsync("TEST"))[0];

        Assert.Equal(123456789012.345m, row.RevenueAvg);
        Assert.Equal(12.04031m, row.EpsLow);
        Assert.Null(row.RevenueHigh);   // an explicit JSON null stays null rather than becoming zero
    }

    [Fact]
    public async Task Defaults_to_annual_and_sends_no_limit_or_page_it_was_not_given()
    {
        // The exact URI, not a Contains: an extra guessed-at parameter is exactly the kind of thing a Contains
        // assertion cannot see, and `limit` on this endpoint silently changes WHICH end of the series arrives.
        var (endpoints, handler) = Build();

        await endpoints.GetEstimatesAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/analyst-estimates", uri.AbsolutePath);
        Assert.Equal("?symbol=AAPL&period=annual", uri.Query);
    }

    [Fact]
    public async Task Sends_every_argument_in_a_fully_specified_call()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter, limit: 3, page: 2);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/analyst-estimates", uri.AbsolutePath);
        Assert.Equal("?symbol=AAPL&period=quarter&limit=3&page=2", uri.Query);
    }

    [Theory]
    [InlineData(FiscalPeriod.Annual, "period=annual")]
    [InlineData(FiscalPeriod.Quarter, "period=quarter")]
    public async Task Period_is_sent_as_fmps_request_vocabulary(FiscalPeriod period, string expected)
    {
        // annual/quarter is the REQUEST vocabulary. FMP labels statement rows FY/Q1-Q4 in responses, and posting
        // one of those back as a request value is what FiscalPeriod exists to prevent. This endpoint's rows carry
        // no period field at all, so the parameter is the only record of which series was asked for.
        var (endpoints, handler) = Build();

        await endpoints.GetEstimatesAsync("AAPL", period);

        Assert.Contains(expected, handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Page_zero_is_sent_rather_than_dropped_because_it_is_the_first_page()
    {
        // page is zero-based, so 0 is a real request for the first page and not an "unset" sentinel. Dropping it
        // would be harmless today and wrong the moment FMP's default page stops being 0.
        var (endpoints, handler) = Build();

        await endpoints.GetEstimatesAsync("AAPL", page: 0);

        Assert.Contains("page=0", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_symbol_with_url_significant_characters_is_escaped()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetEstimatesAsync("BRK.B");

        Assert.Contains("symbol=BRK.B", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task An_empty_reply_is_an_empty_list_not_a_null()
    {
        // An unknown symbol, a symbol with no coverage, and a class-share ticker spelled with a dot all answer
        // [] with HTTP 200 rather than a 404 — the same "not found is a shape" rule the rest of FMP follows.
        var (endpoints, _) = Build("[]");

        var rows = await endpoints.GetEstimatesAsync("NOSUCH");

        Assert.NotNull(rows);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Rejects_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetEstimatesAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Rejects_a_null_symbol_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetEstimatesAsync(null!));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_non_positive_limit_before_spending_a_request(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Annual, limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Rejects_a_negative_page_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetEstimatesAsync("AAPL", FiscalPeriod.Annual, page: -1));
        Assert.Empty(handler.Requests);
    }
}
