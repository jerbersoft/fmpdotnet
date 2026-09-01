using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/ratings-snapshot</c> and <c>stable/ratings-historical</c>, checked against captures taken
/// live 2026-08-28.
///
/// <para>One record serves both. Their field sets differ by exactly one member: the snapshot sends nine and the
/// history sends the same nine plus <c>date</c>, so <see cref="CompanyRating.Date"/> is nullable and is null on
/// every row the snapshot returns — the same pattern as <see cref="EmployeeCount"/>.</para>
///
/// <para><b>The trap is the default.</b> <c>ratings-historical</c> with no <c>limit</c> answers <b>one row</b>,
/// from an endpoint whose name promises a series. That is the one place in this slice where a
/// <c>limit</c> is defaulted rather than omitted.</para></summary>
public class CompanyRatingTests
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

    // ---- one record, two shapes ---------------------------------------------------------------------------

    [Fact]
    public async Task The_snapshot_binds_its_nine_fields_and_leaves_the_date_null()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratings-snapshot.AAPL.json"));

        var rating = await endpoints.GetRatingAsync("AAPL");

        Assert.NotNull(rating);
        // Date is the one member this path never sends, so it is the one member reported unbound.
        Assert.Equal(["Date"], Binding.Unbound(rating));
        Assert.Null(rating.Date);
        Assert.Equal("AAPL", rating.Symbol);
        Assert.Equal("B", rating.Rating);
        Assert.Equal(3, rating.OverallScore);
        Assert.Equal(3, rating.DiscountedCashFlowScore);
        Assert.Equal(5, rating.ReturnOnEquityScore);
        Assert.Equal(5, rating.ReturnOnAssetsScore);
        Assert.Equal(1, rating.DebtToEquityScore);
        Assert.Equal(2, rating.PriceToEarningsScore);
        Assert.Equal(1, rating.PriceToBookScore);
    }

    [Fact]
    public async Task The_history_binds_all_ten_fields_including_the_date()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratings-historical.AAPL.json"));

        var rows = await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(Day(2026, 8, 27), rows[0].Date);
        Assert.Equal("B", rows[0].Rating);
        Assert.Equal(3, rows[0].OverallScore);
    }

    [Fact]
    public async Task The_history_is_per_trading_day_not_per_calendar_day()
    {
        // 2026-08-22 and 08-23 were a weekend and are simply absent. A caller stepping dates rather than
        // reading them will misalign.
        var (endpoints, _) = Build(Binding.Fixture("ratings-historical.AAPL.json"));

        var rows = await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal(
            [Day(2026, 8, 27), Day(2026, 8, 26), Day(2026, 8, 25), Day(2026, 8, 24), Day(2026, 8, 21)],
            rows.Select(r => r.Date));
    }

    [Fact]
    public void The_shipped_bulk_rating_is_not_reused_because_it_has_no_overall_score()
    {
        // The measurement that forced two records rather than one: BulkCompanyRating carries nine fields and
        // none of them is overallScore, which both ordinary paths send on every row. Reusing it would silently
        // drop a measured field; adding the property to it would put a permanently-null member on the bulk
        // shape. This test fails if someone later "deduplicates" the two.
        Assert.Null(typeof(BulkCompanyRating).GetProperty("OverallScore"));
        Assert.NotNull(typeof(CompanyRating).GetProperty(nameof(CompanyRating.OverallScore)));
    }

    // ---- the one-row default ------------------------------------------------------------------------------

    [Fact]
    public async Task The_history_sends_a_limit_of_one_hundred_when_the_caller_gives_none()
    {
        // The trap, and the one place in this slice where a limit is defaulted. Measured 2026-08-28:
        // ratings-historical?symbol=AAPL with no limit answers exactly ONE row; limit=5 answers 5; limit=100
        // answers 100; limit=10000 answers 6292, which is AAPL's whole series and not a cap. Faithfully passing
        // FMP's default through would give a caller one row from an endpoint called "historical".
        var (endpoints, handler) = Build();

        await endpoints.GetRatingHistoryAsync("AAPL");

        Assert.Equal("stable/ratings-historical", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL&limit=100", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task An_explicit_limit_replaces_the_default()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetRatingHistoryAsync("AAPL", limit: 5);

        Assert.Equal("?symbol=AAPL&limit=5", handler.Requests.Single().Query);
    }

    [Fact]
    public void The_history_limit_is_not_nullable_unlike_every_other_limit_in_this_slice()
    {
        // Deliberate asymmetry, pinned so it is not "tidied" into consistency later. Dividends, splits and
        // grade history all answer the whole series with no limit, so theirs are `int?` defaulting to null.
        // This one answers one row, so a null default would be useless.
        var parameter = typeof(AnalystEndpoints)
            .GetMethod(nameof(AnalystEndpoints.GetRatingHistoryAsync))!
            .GetParameters()
            .Single(p => p.Name == "limit");

        Assert.Equal(typeof(int), parameter.ParameterType);
        Assert.Equal(100, parameter.DefaultValue);
    }

    // ---- requests and validation --------------------------------------------------------------------------

    [Fact]
    public async Task The_snapshot_request_carries_a_symbol_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetRatingAsync("AAPL");

        Assert.Equal("stable/ratings-snapshot", handler.Requests.Single().AbsolutePath.TrimStart('/'));
        Assert.Equal("?symbol=AAPL", handler.Requests.Single().Query);
    }

    [Fact]
    public void Neither_rating_method_offers_a_date_range_because_the_history_ignores_one()
    {
        // Measured 2026-08-28: ratings-historical?symbol=AAPL&limit=1000 answers 1000 rows with and without
        // from=2024-01-01&to=2024-12-31.
        foreach (var name in new[]
                 {
                     nameof(AnalystEndpoints.GetRatingAsync),
                     nameof(AnalystEndpoints.GetRatingHistoryAsync),
                 })
            Assert.DoesNotContain(
                typeof(AnalystEndpoints).GetMethod(name)!.GetParameters(),
                p => p.Name is "from" or "to");
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_from_the_snapshot_and_an_empty_list_from_the_history()
    {
        var (snapshot, _) = Build("[]");
        var (history, _) = Build("[]");

        Assert.Null(await snapshot.GetRatingAsync("NOSUCHTICKER"));
        Assert.Empty(await history.GetRatingHistoryAsync("NOSUCHTICKER"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Both_methods_refuse_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (snapshot, h1) = Build();
        var (history, h2) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => snapshot.GetRatingAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => history.GetRatingHistoryAsync(symbol));
        Assert.Empty(h1.Requests);
        Assert.Empty(h2.Requests);
    }

    [Fact]
    public async Task Both_methods_refuse_a_null_symbol_before_spending_a_request()
    {
        var (snapshot, _) = Build();
        var (history, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => snapshot.GetRatingAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => history.GetRatingHistoryAsync(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_positive_history_limit_is_refused_before_a_request_is_spent(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetRatingHistoryAsync("AAPL", limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_letter_rating_is_a_string_because_the_observed_scale_is_not_A_to_F()
    {
        // The shipped BulkCompanyRating documents the measurement: across 45,008 bulk rows the values ran
        // C, B+, C+, B, A-, B-, C-, D+, A, A+, and then S- and S -- two grades ABOVE A+ -- while D- and F never
        // appeared at all. A scale inferred from any one snapshot is wrong at both ends.
        var rows = JsonSerializer.Deserialize(
            """[{"rating":"S"},{"rating":"S-"},{"rating":"A+"},{"rating":"Z"}]""",
            FmpJsonContext.Default.ListCompanyRating)!;

        Assert.Equal(["S", "S-", "A+", "Z"], rows.Select(r => r.Rating));
    }
}
