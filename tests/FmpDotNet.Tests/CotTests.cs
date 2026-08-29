using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The Commitment of Traders records, checked against captures taken live 2026-08-29.
///
/// <para><c>CotReport</c> is 128 properties and these tests do not assert all 128 individually. The guard
/// against a transcription error is <see cref="Binding.Unbound{T}"/> over a fixture in which every field is
/// populated: a mistyped <c>[JsonPropertyName]</c> leaves its property null and the assertion names it. The
/// explicit assertions below are the four blocks' representatives plus every field the naming rule
/// touches.</para></summary>
public class CotTests
{
    [Fact]
    public void A_captured_report_row_binds_all_one_hundred_and_twenty_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)!;

        Assert.Equal(2, rows.Count);

        // Row 1 is ZC, whose `Other` block carries non-zero values — unlike row 0 (NG), where all 36 `Other`
        // fields are legitimately zero. Binding.Unbound counts zero as bound, so this assertion would pass on
        // either row; ZC is chosen because it is the row that proves the block binds real data rather than 36
        // zeroes that would bind the same way whether or not the mapping worked.
        Assert.Empty(Binding.Unbound(rows[1]));
    }

    [Fact]
    public void One_representative_from_each_of_the_four_blocks_binds()
    {
        // positions / pct / traders / change — the four blocks CotReport is built from. Asserting all 128
        // would restate the generated property list without adding a check; asserting one per block catches a
        // whole block bound to the wrong suffix.
        var ng = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal("NG", ng.Symbol);
        Assert.Equal(1500882, ng.OpenInterestAll);              // positions
        Assert.Equal(21.7m, ng.PctOfOiNoncommLongOld);          // pct, and an `Ol` -> `Old` rename
        Assert.Equal(155, ng.TradersNoncommSpreadOld);          // traders, and the double-defect field
        Assert.Equal(-28330, ng.ChangeInNoncommSpreadAll);      // change, and a `Spead` -> `Spread` rename
        Assert.Equal("(Contracts of 10,000 MMBTU'S)", ng.ContractUnits);
    }

    [Fact]
    public void The_three_misspellings_bind_from_the_wire_spelling_and_not_the_english_one()
    {
        // If any [JsonPropertyName] is "corrected" to the English spelling, these land null. That is a silent
        // failure — System.Text.Json answers a field it cannot find with null and no error — so it needs a
        // test that names it.
        var report = JsonSerializer.Deserialize(
            """[{"changeInNoncommSpeadAll":-1,"tradersNoncommSpeadOl":-2}]""",
            FmpJsonContext.Default.ListCotReport)![0];
        var analysis = JsonSerializer.Deserialize(
            """[{"netPostion":-3}]""",
            FmpJsonContext.Default.ListCotAnalysis)![0];

        Assert.Equal(-1, report.ChangeInNoncommSpreadAll);
        Assert.Equal(-2, report.TradersNoncommSpreadOld);
        Assert.Equal(-3, analysis.NetPosition);

        // And the correctly-spelled siblings still bind, so the fix is not "spell everything Spead".
        var correct = JsonSerializer.Deserialize(
            """[{"noncommPositionsSpreadAll":4,"tradersNoncommSpreadAll":5}]""",
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal(4, correct.NoncommPositionsSpreadAll);
        Assert.Equal(5, correct.TradersNoncommSpreadAll);
    }

    [Fact]
    public void A_row_carrying_both_the_Ol_and_the_Old_suffix_binds_both()
    {
        // The suffix is `Old` in the positions block and `Ol` in the other three, on the same row. Normalising
        // the ATTRIBUTE to one or the other silently empties 26 properties; normalising the PROPERTY is what
        // this SDK does instead, and this is the test that pins the direction.
        var row = JsonSerializer.Deserialize(
            """[{"openInterestOld":1,"pctOfOpenInterestOl":2,"tradersTotOl":3,"concNetLe8TdrShortOl":4}]""",
            FmpJsonContext.Default.ListCotReport)![0];

        Assert.Equal(1, row.OpenInterestOld);
        Assert.Equal(2, row.PctOfOpenInterestOld);
        Assert.Equal(3, row.TradersTotOld);
        Assert.Equal(4m, row.ConcNetLe8TdrShortOld);
    }

    [Fact]
    public void The_Other_block_carries_real_data_on_the_symbols_that_use_it()
    {
        // 36 of the 128 properties are the `Other` block, and dropping it to save width would silently lose
        // real data: 118 of 545 rows measured 2026-08-29 carry a non-zero value in at least one Other field,
        // across 14 distinct symbols. NG is not one of them and ZC is.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)!;

        Assert.Equal(0, rows[0].TradersTotOther);            // NG — genuinely zero
        Assert.Equal(458, rows[1].TradersTotOther);          // ZC — genuinely populated
        Assert.Equal(325558, rows[1].OpenInterestOther);
        Assert.Equal(26.9m, rows[1].ConcGrossLe4TdrLongOther);
    }

    [Fact]
    public void The_COT_date_carries_a_midnight_time_and_still_parses_to_a_date()
    {
        // "2024-02-27 00:00:00" — 19 characters with a ` 00:00:00` tail, on EVERY row of both COT paths.
        // NullableDateAtMidnightJsonConverter already parses exactly this; the plain-date converter used
        // everywhere else in this slice throws on it. No new converter was written for #40 because of this.
        var report = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-report.head.json"),
            FmpJsonContext.Default.ListCotReport)![0];
        var analysis = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)![0];

        Assert.Equal(new LocalDate(2024, 2, 27), report.Date);
        Assert.Equal(new LocalDate(2024, 2, 27), analysis.Date);
    }

    [Fact]
    public void An_analysis_row_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("NG", rows[0].Symbol);
        Assert.Equal("Natural Gas (NG)", rows[0].Name);
        Assert.Equal("ENERGIES", rows[0].Sector);
        Assert.Equal("NAT GAS NYME - NEW YORK MERCANTILE EXCHANGE", rows[0].Exchange);
        Assert.Equal(41.09m, rows[0].CurrentLongMarketSituation);
        Assert.Equal(58.91m, rows[0].CurrentShortMarketSituation);
        Assert.Equal("Bearish", rows[0].MarketSituation);
        Assert.Equal(-141553, rows[0].NetPosition);
        Assert.Equal(-153872, rows[0].PreviousNetPosition);
    }

    [Fact]
    public void ChangeInNetPosition_is_a_percentage_and_the_arithmetic_proves_it()
    {
        // The field sits between two int? position counts and is NOT their difference. Measured across all
        // 545 rows on 2026-08-29, 545 match a percent reading and 4 match an absolute one. A caller who adds
        // it to a position count is wrong by three orders of magnitude and gets no signal — which is why the
        // property is decimal? while its two neighbours are int?, and why this test does the arithmetic.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        foreach (var row in rows)
        {
            var absolute = row.NetPosition!.Value - row.PreviousNetPosition!.Value;
            var percent = 100m * absolute / Math.Abs(row.PreviousNetPosition!.Value);

            Assert.Equal(percent, row.ChangeInNetPosition!.Value, precision: 1);
            Assert.NotEqual(absolute, (int)row.ChangeInNetPosition!.Value);
        }

        // Spelled out on the newest row, so the numbers are readable rather than derived:
        //   -141553 - -153872 = 12319 absolute; 12319 / 153872 = 8.01%; the field says 8.01.
        Assert.Equal(8.01m, rows[0].ChangeInNetPosition);
        Assert.Equal(-12.68m, rows[1].ChangeInNetPosition);
    }

    [Fact]
    public void ReversalTrend_binds_a_real_JSON_boolean()
    {
        // Worth its own test because #31 met the opposite case: `capitalGainsOver200USD` arrives as the
        // STRING "False", which bool? will not bind. Measured 2026-08-29, this one is a real JSON boolean on
        // all 545 rows. The two look identical in documentation and differ on the wire, so each is typed from
        // its own measurement rather than from the other's precedent.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.True(rows[0].ReversalTrend);
        Assert.False(rows[1].ReversalTrend);
    }

    [Fact]
    public void Market_sentiment_keeps_the_leading_space_FMP_sends()
    {
        // " Increasing Bearish" — with the space. Captured rather than trimmed, because trimming here would
        // be the SDK silently disagreeing with the upstream about what the value is, and a caller matching on
        // the string needs to know.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-analysis.NG.head.json"),
            FmpJsonContext.Default.ListCotAnalysis)!;

        Assert.Equal("Increasing Bullish", rows[0].MarketSentiment);
        Assert.Equal(" Increasing Bearish", rows[1].MarketSentiment);
    }

    [Fact]
    public void A_symbol_row_binds_both_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("commitment-of-traders-list.head.json"),
            FmpJsonContext.Default.ListCotSymbol)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("NG", rows[0].Symbol);
        Assert.Equal("Natural Gas (NG)", rows[0].Name);
    }

    // ---- the request surface -----------------------------------------------------------------------------

    private static (CotEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CotEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Every_parameter_on_the_two_dated_paths_is_optional()
    {
        // All three optional on both, and a bare call is legal: measured 2026-08-29 it answered 545 rows on
        // each. Omitted parameters must not reach the wire as empty values.
        var (endpoints, handler) = Build();

        await endpoints.GetReportAsync();

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/commitment-of-traders-report", handler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("symbol=", query);
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (report, reportHandler) = Build();
        await report.GetReportAsync("NG", new LocalDate(2024, 1, 1), new LocalDate(2024, 3, 31));

        var (analysis, analysisHandler) = Build();
        await analysis.GetAnalysisAsync("NG");

        var (symbols, symbolsHandler) = Build();
        await symbols.GetSymbolsAsync();

        Assert.Equal("/stable/commitment-of-traders-report", reportHandler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=NG", reportHandler.Requests[0].Query);
        Assert.Contains("from=2024-01-01", reportHandler.Requests[0].Query);
        Assert.Contains("to=2024-03-31", reportHandler.Requests[0].Query);
        Assert.Equal("/stable/commitment-of-traders-analysis", analysisHandler.Requests[0].AbsolutePath);
        Assert.Equal("/stable/commitment-of-traders-list", symbolsHandler.Requests[0].AbsolutePath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_backwards_range_is_refused_before_the_request_goes_out(bool analysis)
    {
        var (endpoints, handler) = Build();
        var from = new LocalDate(2024, 3, 31);
        var to = new LocalDate(2024, 1, 1);

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => analysis
                ? endpoints.GetAnalysisAsync("NG", from, to)
                : endpoints.GetReportAsync("NG", from, to));

        Assert.Equal("to", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_empty_answer_is_an_empty_list_rather_than_null_on_all_three_methods()
    {
        // Pins the "never null" half of each method's doc comment: GetReportAsync's "Never null; an empty
        // list usually means the range is outside the data rather than that the contract has no filings",
        // GetAnalysisAsync's "At most 13 rows per request, newest first. Never null", and GetSymbolsAsync's
        // "Every contract code and name. Never null". A separate Build() per call, since each stub handler
        // answers one request only.
        var (report, _) = Build();
        var (analysis, _) = Build();
        var (symbols, _) = Build();

        var reportRows = await report.GetReportAsync();
        var analysisRows = await analysis.GetAnalysisAsync();
        var symbolRows = await symbols.GetSymbolsAsync();

        Assert.NotNull(reportRows);
        Assert.Empty(reportRows);
        Assert.NotNull(analysisRows);
        Assert.Empty(analysisRows);
        Assert.NotNull(symbolRows);
        Assert.Empty(symbolRows);
    }
}
