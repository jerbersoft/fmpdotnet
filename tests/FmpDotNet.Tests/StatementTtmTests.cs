using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The three rolling-twelve-month statements, which reuse the base statement models exactly.</summary>
public class StatementTtmTests
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
        { "stable/income-statement-ttm", e => e.GetIncomeStatementTtmAsync("AAPL") },
        { "stable/balance-sheet-statement-ttm", e => e.GetBalanceSheetTtmAsync("AAPL") },
        { "stable/cash-flow-statement-ttm", e => e.GetCashFlowTtmAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Each_ttm_path_asks_for_the_whole_history_and_sends_no_period(
        string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
        // Measured 2026-08-27: these three accept `period` and ignore it — they are a rolling series, always
        // newest-first from the latest quarter. Sending a parameter the endpoint discards is not free.
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task An_income_statement_ttm_row_binds_every_field_the_base_model_declares()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementTtmAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal("AAPL", row.Symbol);
    }

    [Fact]
    public async Task A_cash_flow_ttm_row_binds_every_field_the_base_model_declares()
    {
        var (endpoints, _) = Build(Binding.Fixture("cash-flow-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetCashFlowTtmAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task The_balance_sheet_ttm_row_is_missing_exactly_one_field_and_it_is_the_measured_one()
    {
        // Measured 2026-08-27 across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B, KO, GE and MSFT: the TTM row carries
        // 60 keys and never `capitalLeaseObligationsNonCurrent`, while the plain balance sheet carries it for all
        // ten. That is structural, not a sparse filer — so it binds as null on every TTM row forever, and a
        // caller reading it off one is reading an absence rather than a zero.
        var (endpoints, _) = Build(Binding.Fixture("balance-sheet-statement-ttm.AAPL.json"));

        var row = Assert.Single(await endpoints.GetBalanceSheetTtmAsync("AAPL"));

        Assert.Equal(["CapitalLeaseObligationsNonCurrent"], Binding.Unbound(row));
    }

    [Fact]
    public async Task The_field_the_ttm_row_omits_is_present_on_the_plain_balance_sheet()
    {
        // The other half of the claim above. Without this, "the TTM row omits it" could equally mean "the model
        // never binds it", and the two are not the same defect.
        var (endpoints, _) = Build(Binding.Fixture("balance-sheet-statement.AAPL.json"));

        var row = Assert.Single(await endpoints.GetBalanceSheetAsync("AAPL", limit: 1));

        Assert.NotNull(row.CapitalLeaseObligationsNonCurrent);
    }

    [Fact]
    public async Task A_limit_is_passed_through_when_given()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIncomeStatementTtmAsync("AAPL", limit: 8);

        Assert.Contains("limit=8", handler.Requests.Single().Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_rejected_before_a_request_goes_out(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetIncomeStatementTtmAsync(symbol));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_zero_limit_is_rejected_before_a_request_goes_out()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIncomeStatementTtmAsync("AAPL", limit: 0));

        Assert.Empty(handler.Requests);
    }
}
