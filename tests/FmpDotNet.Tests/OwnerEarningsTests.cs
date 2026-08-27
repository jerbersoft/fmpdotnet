using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class OwnerEarningsTests
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

    [Fact]
    public async Task It_asks_for_the_whole_history_and_sends_no_period()
    {
        // Measured 2026-08-27: owner-earnings accepts `period` and ignores it — the series is quarterly only.
        var (endpoints, handler) = Build();

        await endpoints.GetOwnerEarningsAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/owner-earnings", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task A_row_binds_all_ten_fields()
    {
        var (endpoints, _) = Build(Binding.Fixture("owner-earnings.AAPL.json"));

        var rows = await endpoints.GetOwnerEarningsAsync("AAPL");

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal(2026, rows[0].FiscalYear);        // arrives as the STRING "2026"
        Assert.Equal("Q3", rows[0].Period);
        Assert.Equal(new NodaTime.LocalDate(2026, 6, 27), rows[0].Date);
        // Two of the ten are routinely negative — they are capital SPENDING, and reading them as positive
        // outflows double-counts the sign.
        Assert.True(rows[0].MaintenanceCapex < 0);
        Assert.True(rows[0].GrowthCapex < 0);
    }

    [Fact]
    public void The_measured_row_ceiling_is_recorded_as_a_constant()
    {
        // Not a tautology: the constant is the only place the SDK records that a full-length answer may be
        // truncated, and a caller comparing rows.Count against it is the only way to suspect it. Measured
        // 2026-08-27 — AAPL, MSFT, GE, KO, JPM, IBM and PG all returned exactly 50 at limit=100000 while
        // income-statement-ttm returned 164 for the same filers; SHOP returned 46, which is its real history.
        Assert.Equal(50, StatementEndpoints.MaxOwnerEarningsRows);
    }
}
