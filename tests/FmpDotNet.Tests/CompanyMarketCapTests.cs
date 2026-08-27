using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three market-capitalisation paths, and the three traps measured on them 2026-08-27.</summary>
public class CompanyMarketCapTests
{
    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
                handler);
    }

    [Fact]
    public async Task Binds_the_fractional_market_cap_that_a_long_would_have_thrown_on()
    {
        // GOOG answered 4098415617064.9995 on 2026-08-27 — one fractional row in twenty. A `long?` binding
        // throws JsonException on it and loses the other nineteen rows with it.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("market-capitalization-batch.20.json")));

        var rows = await endpoints.GetMarketCapBatchAsync(["AAPL", "GOOG"]);

        Assert.Equal(20, rows.Count);
        Assert.Equal(4098415617064.9995m, Assert.Single(rows, r => r.Symbol == "GOOG").MarketCap);
    }

    [Fact]
    public async Task Batch_answers_fewer_rows_than_it_was_asked_for_so_rows_must_be_matched_by_symbol()
    {
        // symbols=AAPL,ZZZZNOPE answered one row on 2026-08-27, and 100 real tickers answered 99 — WDSP,
        // a symbol FMP's own stock-list carries, has no market-cap row. Zipping request against response
        // corrupts every row after the first gap.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("market-capitalization-batch.partial.json")));

        var rows = await endpoints.GetMarketCapBatchAsync(["AAPL", "ZZZZNOPE"]);

        Assert.Equal("AAPL", Assert.Single(rows).Symbol);
    }

    [Fact]
    public async Task Batch_rejects_an_empty_symbol_list_before_spending_a_request()
    {
        // Empty `symbols` answers 400, measured 2026-08-27.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetMarketCapBatchAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetMarketCapBatchAsync(["  "]));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Batch_joins_the_symbols_with_commas()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetMarketCapBatchAsync(["AAPL", "MSFT"]);

        Assert.Contains("symbols=AAPL%2CMSFT", Assert.Single(handler.Requests).Query);
    }

    [Fact]
    public async Task Single_symbol_binds_one_row_out_of_the_array_fmp_wraps_it_in()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("market-capitalization.AAPL.json")));

        var row = await endpoints.GetMarketCapAsync("AAPL");

        Assert.NotNull(row);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(new LocalDate(2026, 8, 27), row.Date);
        Assert.Equal(4620348450480m, row.MarketCap);
    }

    [Fact]
    public async Task Unknown_symbol_is_null_because_fmp_answers_an_empty_array_not_a_404()
    {
        // ZZZZNOPE answered [] with HTTP 200 on 2026-08-27.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetMarketCapAsync("ZZZZNOPE"));
    }

    [Fact]
    public async Task Historical_omits_the_range_and_limit_parameters_that_were_not_supplied()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHistoricalMarketCapAsync("AAPL");

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("symbol=AAPL", query);
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.DoesNotContain("limit=", query);
    }

    [Fact]
    public async Task Historical_sends_the_range_and_limit_when_they_are_supplied()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHistoricalMarketCapAsync(
            "AAPL", new LocalDate(2024, 1, 1), new LocalDate(2024, 1, 10), 5);

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("from=2024-01-01", query);
        Assert.Contains("to=2024-01-10", query);
        Assert.Contains("limit=5", query);
    }

    [Fact]
    public async Task Historical_rejects_a_backwards_range_before_spending_a_request()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => endpoints.GetHistoricalMarketCapAsync(
            "AAPL", new LocalDate(2024, 1, 10), new LocalDate(2024, 1, 1)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Historical_accepts_one_end_of_the_range_alone()
    {
        // One end cannot be backwards, so the guard must not fire on it.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetHistoricalMarketCapAsync("AAPL", from: new LocalDate(2024, 1, 1));
        await endpoints.GetHistoricalMarketCapAsync("AAPL", to: new LocalDate(2024, 1, 1));

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Historical_binds_a_window_newest_first()
    {
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("historical-market-capitalization.AAPL.limit5.json")));

        var rows = await endpoints.GetHistoricalMarketCapAsync("AAPL", limit: 5);

        Assert.Equal(5, rows.Count);
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 21), rows[^1].Date);
        Assert.Equal(4625850192660m, rows[0].MarketCap);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Rejects_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetMarketCapAsync(symbol));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetHistoricalMarketCapAsync(symbol));
        Assert.Empty(handler.Requests);
    }
}
