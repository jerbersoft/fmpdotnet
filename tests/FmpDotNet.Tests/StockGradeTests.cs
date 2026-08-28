using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three <c>stable/grades*</c> paths, checked against captures taken live 2026-08-28.
///
/// <para><b>Two of them look like the same data and are not.</b> <c>grades-consensus</c> and
/// <c>grades-historical</c> each carry five analyst-count fields, under different names, and a caller could
/// reasonably assume the first is the current view of the second. Measured the same minute for AAPL, the
/// newest historical row totals <b>47</b> analysts and the consensus totals <b>112</b> — different populations,
/// not a stale copy. They are separate records for that reason.</para></summary>
public class StockGradeTests
{
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

    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    // ---- grades -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_grade_row_binds_all_six_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades.AAPL.json"));

        var rows = await endpoints.GetGradesAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(Day(2026, 8, 17), rows[0].Date);
        Assert.Equal("Rothschild & Co", rows[0].GradingCompany);
        Assert.Equal("Neutral", rows[0].PreviousGrade);
        Assert.Equal("Buy", rows[0].NewGrade);
        Assert.Equal("upgrade", rows[0].Action);
    }

    [Fact]
    public async Task A_maintain_carries_the_same_grade_on_both_sides()
    {
        // Two of the five captured rows are `maintain`, and on both the previous and new grades are identical.
        // A caller filtering for "the grade changed" must read `action`, not compare the two grade fields --
        // and must fold case on `action`, which is lower case while the grades are title case.
        var (endpoints, _) = Build(Binding.Fixture("grades.AAPL.json"));

        var rows = await endpoints.GetGradesAsync("AAPL");

        var maintained = rows.Where(r => r.Action == "maintain").ToList();
        Assert.Equal(2, maintained.Count);
        Assert.All(maintained, r => Assert.Equal(r.PreviousGrade, r.NewGrade));
    }

    [Fact]
    public void The_grades_method_offers_neither_a_limit_nor_a_page_because_the_endpoint_ignores_both()
    {
        // Measured 2026-08-28: grades?symbol=AAPL answers 1791 rows; limit=5 answers 1791; limit=10000 answers
        // 1791; page=1 answers 1791 with a byte-identical first row. The count varies by symbol (MSFT 967,
        // BRK-B 93), so it is the whole set each time and not a cap. A signature offering either parameter
        // would let a caller believe they had asked for less.
        var parameters = typeof(AnalystEndpoints)
            .GetMethod(nameof(AnalystEndpoints.GetGradesAsync))!
            .GetParameters();

        Assert.Equal(new[] { "symbol", "ct" }, parameters.Select(p => p.Name!));
    }

    [Fact]
    public async Task The_grades_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetGradesAsync("AAPL");

        Assert.Equal("stable/grades", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    // ---- grades-consensus ---------------------------------------------------------------------------------

    [Fact]
    public async Task The_consensus_unwraps_the_single_element_array_FMP_sends()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-consensus.AAPL.json"));

        var consensus = await endpoints.GetGradeConsensusAsync("AAPL");

        Assert.NotNull(consensus);
        Assert.Equal("AAPL", consensus.Symbol);
        Assert.Equal(1, consensus.StrongBuy);
        Assert.Equal(70, consensus.Buy);
        Assert.Equal(32, consensus.Hold);
        Assert.Equal(9, consensus.Sell);
        Assert.Equal(0, consensus.StrongSell);
        Assert.Equal("Buy", consensus.Consensus);
        // Every field bound, including strongSell. A real zero is not an absent field: Binding.Unbound flags
        // null, blank and empty collections, and correctly leaves a numeric 0 alone. Paired with the assertion
        // above that StrongSell is 0, this says the zero arrived and was read as a zero rather than as "FMP
        // does not know".
        Assert.Empty(Binding.Unbound(consensus));
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_rather_than_throwing()
    {
        // Every path in this slice answers an unknown-but-well-formed symbol with [] and HTTP 200, not a 404,
        // so "no coverage", "not found" and "misspelled class-share ticker" are one shape here.
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetGradeConsensusAsync("NOSUCHTICKER"));
    }

    [Fact]
    public void The_consensus_carries_no_date_at_all()
    {
        // Seven fields, and none of them temporal. There is no way to tell how old a consensus row is, which is
        // half the reason it cannot be treated as the head of the historical series.
        Assert.DoesNotContain(
            typeof(GradeConsensus).GetProperties(),
            p => p.PropertyType == typeof(LocalDate?) || p.PropertyType == typeof(LocalDate));
    }

    // ---- grades-historical --------------------------------------------------------------------------------

    [Fact]
    public async Task A_captured_history_row_binds_all_seven_of_its_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var rows = await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(Day(2026, 8, 1), rows[0].Date);
        Assert.Equal(6, rows[0].AnalystRatingsStrongBuy);
        Assert.Equal(22, rows[0].AnalystRatingsBuy);
        Assert.Equal(14, rows[0].AnalystRatingsHold);
        Assert.Equal(3, rows[0].AnalystRatingsSell);
        Assert.Equal(2, rows[0].AnalystRatingsStrongSell);
    }

    [Fact]
    public async Task The_history_rows_are_monthly_and_newest_first()
    {
        var (endpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var rows = await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal(
            [Day(2026, 8, 1), Day(2026, 7, 1), Day(2026, 6, 1), Day(2026, 5, 1), Day(2026, 4, 1)],
            rows.Select(r => r.Date));
    }

    [Fact]
    public async Task The_consensus_is_not_the_newest_history_row_and_the_two_fixtures_prove_it()
    {
        // The trap, asserted from two captures taken minutes apart in one pass. 47 analysts against 112: not a
        // stale copy, a different population. Merging them, or treating either as a refresh of the other, is
        // the mistake these two records exist as separate types to prevent.
        var (consensusEndpoints, _) = Build(Binding.Fixture("grades-consensus.AAPL.json"));
        var (historyEndpoints, _) = Build(Binding.Fixture("grades-historical.AAPL.json"));

        var consensus = await consensusEndpoints.GetGradeConsensusAsync("AAPL");
        var history = await historyEndpoints.GetGradeHistoryAsync("AAPL");

        var consensusTotal = consensus!.StrongBuy + consensus.Buy + consensus.Hold
                             + consensus.Sell + consensus.StrongSell;
        var newest = history[0];
        var historyTotal = newest.AnalystRatingsStrongBuy + newest.AnalystRatingsBuy + newest.AnalystRatingsHold
                           + newest.AnalystRatingsSell + newest.AnalystRatingsStrongSell;

        Assert.Equal(112, consensusTotal);
        Assert.Equal(47, historyTotal);
        // And the shape differs, not just the scale: the consensus is Buy-heavy at 70 of 112, the history row
        // is spread across StrongBuy and Buy at 6 and 22 of 47.
        Assert.NotEqual(consensus.Buy, newest.AnalystRatingsBuy);
    }

    [Fact]
    public async Task The_history_path_sends_only_a_symbol_when_no_limit_is_given()
    {
        // Absent limit returns the whole series: 92 rows for AAPL, unchanged by limit=10000. See the ruling in
        // the plan -- a default of 100 belongs on ratings-historical, which answers ONE row without it, and
        // nowhere else in this slice.
        var (endpoints, handler) = Build();

        await endpoints.GetGradeHistoryAsync("AAPL");

        Assert.Equal("stable/grades-historical", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_history_path_sends_a_limit_when_one_is_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetGradeHistoryAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5&apikey=k", handler.Requests.Single().Query);
    }

    [Fact]
    public void No_grade_method_offers_a_date_range_because_all_three_ignore_one()
    {
        // Measured 2026-08-28: grades answers 1791 rows and grades-historical 92, with and without
        // from=2024-01-01&to=2024-12-31 in each case.
        var methods = new[]
        {
            nameof(AnalystEndpoints.GetGradesAsync),
            nameof(AnalystEndpoints.GetGradeConsensusAsync),
            nameof(AnalystEndpoints.GetGradeHistoryAsync),
        };

        foreach (var name in methods)
            Assert.DoesNotContain(
                typeof(AnalystEndpoints).GetMethod(name)!.GetParameters(),
                p => p.Name is "from" or "to");
    }

    // ---- validation ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Every_grade_method_refuses_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (grades, h1) = Build();
        var (consensus, h2) = Build();
        var (history, h3) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => grades.GetGradesAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => consensus.GetGradeConsensusAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => history.GetGradeHistoryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
        Assert.Empty(h3.Requests);
    }

    [Fact]
    public async Task Every_grade_method_refuses_a_null_symbol_before_spending_a_request()
    {
        var (grades, _) = Build();
        var (consensus, _) = Build();
        var (history, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => grades.GetGradesAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => consensus.GetGradeConsensusAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => history.GetGradeHistoryAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_history_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetGradeHistoryAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }
}
