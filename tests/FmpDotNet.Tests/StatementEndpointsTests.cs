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
        Assert.DoesNotContain("limit=", uri.Query);    // omitted rather than guessed at when the caller gives none
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
}
