using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class StatementEndpointsTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    public static TheoryData<string, Func<StatementEndpoints, Task>> Calls => new()
    {
        { "stable/income-statement", e => e.GetIncomeStatementAsync("AAPL") },
        { "stable/balance-sheet-statement", e => e.GetBalanceSheetAsync("AAPL") },
        { "stable/cash-flow-statement", e => e.GetCashFlowAsync("AAPL") },
        { "stable/ratios", e => e.GetRatiosAsync("AAPL") },
        { "stable/key-metrics", e => e.GetKeyMetricsAsync("AAPL") },
        { "stable/financial-growth", e => e.GetFinancialGrowthAsync("AAPL") },
        { "stable/enterprise-values", e => e.GetEnterpriseValuesAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Each_of_the_seven_hits_its_own_path_with_the_shared_query(string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("period=annual", uri.Query);   // the default, and FMP's request vocabulary not its response one
        // NOT omitted. FMP's undocumented default is 5, so sending nothing returned 5 rows of a 41-row history
        // — measured 2026-08-27 on all seven of these paths. See StatementEndpoints.FullHistoryLimit.
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
    }

    [Theory]
    [InlineData(FiscalPeriod.Annual, "period=annual")]
    [InlineData(FiscalPeriod.Quarter, "period=quarter")]
    public async Task Period_is_sent_as_fmps_request_vocabulary(FiscalPeriod period, string expected)
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementAsync("AAPL", period);

        Assert.Contains(expected, handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Period_is_still_sent_for_enterprise_values_even_though_the_reply_drops_it()
    {
        // Measured 2026-08-26: period= genuinely changes which dates come back on this endpoint, but the rows
        // carry no period field to say which series they are. Dropping the parameter because the response
        // looks period-less would silently return annual data to a caller asking for quarters.
        var (endpoints, handler) = Build();

        await endpoints.GetEnterpriseValuesAsync("AAPL", FiscalPeriod.Quarter);

        Assert.Contains("period=quarter", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_limit_is_passed_through_when_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementAsync("AAPL", FiscalPeriod.Quarter, limit: 8);

        Assert.Contains("limit=8", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_symbol_with_url_significant_characters_is_escaped()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetRatiosAsync("BRK.B");

        Assert.Contains("symbol=BRK.B", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Rejects_a_blank_symbol_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetKeyMetricsAsync("  "));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_non_positive_limit_before_spending_a_request(int limit)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetCashFlowAsync("AAPL", FiscalPeriod.Annual, limit));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_empty_reply_is_an_empty_list_not_a_null()
    {
        var (endpoints, _) = Build("[]");

        Assert.Empty(await endpoints.GetIncomeStatementAsync("NOSUCH"));
    }

    [Theory]
    [InlineData(FiscalPeriod.Annual, "period=annual")]
    [InlineData(FiscalPeriod.Quarter, "period=quarter")]
    [InlineData(FiscalPeriod.Q1, "period=Q1")]
    [InlineData(FiscalPeriod.Q2, "period=Q2")]
    [InlineData(FiscalPeriod.Q3, "period=Q3")]
    [InlineData(FiscalPeriod.Q4, "period=Q4")]
    public async Task All_five_period_values_reach_the_wire(FiscalPeriod period, string expected)
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementAsync("AAPL", period);

        Assert.Contains(expected, handler.Requests.Single().Query);
    }

    [Fact]
    public void An_undeclared_period_throws_rather_than_reaching_the_wire()
    {
        // The throw is the point. An unrecognised period is silently read as annual by FMP (measured 2026-08-27,
        // `period=bogus` answered FY rows at HTTP 200), so a value that got past this would produce a well-formed
        // answer to a question nobody asked.
        Assert.Throws<ArgumentOutOfRangeException>(() => ((FiscalPeriod)99).ToQueryValue());
    }

    [Fact]
    public void The_two_original_period_ordinals_did_not_move()
    {
        // Q1-Q4 were appended, not inserted. A caller who persisted the underlying int keeps reading what they
        // stored — which is the whole reason the enum was widened at the end rather than in fiscal order.
        Assert.Equal(0, (int)FiscalPeriod.Annual);
        Assert.Equal(1, (int)FiscalPeriod.Quarter);
    }
}
