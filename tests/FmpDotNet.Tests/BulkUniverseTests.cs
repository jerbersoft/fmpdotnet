using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The whole-universe downloads (#14) and the ETF holdings (#16), checked against responses captured
/// live on 2026-08-26.</summary>
public class BulkUniverseTests
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

    [Fact]
    public async Task Ratings_map_and_the_scale_runs_above_A_plus()
    {
        // Measured across all 45,008 rows: C, B+, C+, B, A-, B-, C-, D+, A, A+, S- (363) and S (26). An A-to-F
        // enum would have had nowhere to put the top two, and D- and F never appeared at all — so a scale
        // inferred from this snapshot would be wrong at both ends. The string stays a string.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("rating-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamRatingsAsync());

        var sz = rows[0];
        Assert.Equal("000001.SZ", sz.Symbol);
        Assert.Equal(new LocalDate(2026, 8, 26), sz.Date);
        Assert.Equal("B-", sz.Rating);
        Assert.Equal(1, sz.DiscountedCashFlowScore);
        Assert.Equal(4, sz.PriceToBookScore);
        Assert.Contains(rows, r => r.Rating == "S-");
    }

    [Fact]
    public async Task The_dcf_price_column_is_spelled_with_a_capital_and_a_space()
    {
        // "Stock Price", unlike every other column on every other endpoint the SDK models. Getting this wrong
        // yields a silently null price rather than an error, which is why it has its own test.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("dcf-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamDiscountedCashFlowsAsync());

        Assert.All(rows, r => Assert.NotNull(r.StockPrice));
        Assert.Equal(2.5m, rows[0].StockPrice);
    }

    [Fact]
    public async Task A_negative_or_absent_dcf_is_ordinary()
    {
        // 1,664 of 33,583 rows carried no dcf at all, and a negative valuation is routine. Any ratio of dcf to
        // price needs a null check and has to tolerate a negative.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("dcf-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamDiscountedCashFlowsAsync());

        Assert.Equal(-6.207502473403145m, rows[0].Dcf);
        Assert.Contains(rows, r => r.Dcf is null && r.StockPrice is not null);
    }

    [Fact]
    public async Task Scores_bulk_maps_onto_the_same_model_the_per_symbol_endpoint_returns()
    {
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("scores-bulk.head.csv")));

        var row = Assert.Single(await DrainAsync(endpoints.StreamScoresAsync()));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal("USD", row.ReportedCurrency);
        Assert.Equal(12.553407594048608m, row.AltmanZScore);
        Assert.Equal(9m, row.PiotroskiScore);
        Assert.Equal(383_266_000_000m, row.TotalAssets);
    }

    [Fact]
    public async Task Peers_are_split_out_of_the_single_csv_field_that_holds_them()
    {
        // The CSV quoting hides that this is a list: "3698.HK,600000.SS,600015.SS" is ONE field.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("peers-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPeersAsync());

        Assert.Equal(["3698.HK", "600000.SS", "600015.SS", "600016.SS", "600036.SS", "601166.SS", "601658.SS"],
            rows[0].Peers);
    }

    [Fact]
    public async Task A_company_with_no_peers_gets_an_empty_list_not_a_list_containing_nothing()
    {
        // 965 of 82,930 rows have an empty peers field, and measured rows also end with a dangling comma — so a
        // naive split yields a phantom empty peer in both cases.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("peers-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamPeersAsync());

        var none = rows.Single(r => r.Symbol == "009730.KQ");
        Assert.Empty(none.Peers);
        Assert.All(rows, r => Assert.DoesNotContain(r.Peers, p => p.Length == 0));
    }

    // ───────────────────────────── ETF holdings (#16) ─────────────────────────────

    [Fact]
    public async Task Etf_holdings_put_the_fund_in_symbol_and_the_holding_in_asset()
    {
        // The opposite way round from every other bulk endpoint, where the symbol is the subject of the row.
        var (endpoints, handler) = Build(StubHandler.Csv(Fixture("etf-holder-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEtfHoldingsAsync(0));

        var clb = rows.Single(r => r.Asset == "CLB");
        Assert.Equal("Core Laboratories Inc", clb.Name);
        Assert.Equal(48089m, clb.SharesNumber);
        Assert.Equal(0.19m, clb.WeightPercentage);
        Assert.Equal("21867A105", clb.Cusip);
        Assert.Equal("US21867A1051", clb.Isin);
        Assert.Equal(465812.45006999996m, clb.MarketValue);
        Assert.Equal(new LocalDate(2026, 8, 26), clb.LastUpdated);
        Assert.Contains("part=0", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_cash_position_has_no_ticker_and_that_is_normal()
    {
        // Blank `asset` on roughly three quarters of rows — cash, bonds and unlisted positions have no ticker,
        // and `name` carries "Other/Cash" for them.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("etf-holder-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEtfHoldingsAsync(0));

        var cash = rows.Single(r => r.Name == "Other/Cash");
        Assert.Null(cash.Asset);
        Assert.Equal(0m, cash.SharesNumber);
        Assert.Equal(64976.46462m, cash.MarketValue);
    }

    [Fact]
    public async Task The_double_dash_fund_placeholder_is_preserved_rather_than_nulled()
    {
        // 26 rows carry the ETF symbol as the literal string " -- ", spaces included. It is a placeholder for an
        // unidentified fund; keeping it lets a caller grouping by ETF see it for what it is.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("etf-holder-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEtfHoldingsAsync(0));

        Assert.All(rows, r => Assert.Equal(" -- ", r.Symbol));
    }

    [Fact]
    public async Task The_part_walk_stops_on_the_400_that_ends_it_and_rethrows_one_on_part_zero()
    {
        // An out-of-range part answers HTTP 400 with a plain-text body under a content-type of application/json
        // that is a lie. There is no empty-response terminator, so the 400 IS the terminator — except on part 0,
        // where it means the request was wrong rather than the universe being exhausted.
        var csv = Fixture("etf-holder-bulk.head.csv");
        var refusal = StubHandler.Csv("Query Error: Invalid or missing query parameter - part",
            System.Net.HttpStatusCode.BadRequest);

        var (walks, _) = Build(StubHandler.Csv(csv), refusal);
        var rows = await DrainAsync(walks.StreamAllEtfHoldingsAsync());
        Assert.Equal(3, rows.Count);   // part 0 delivered, part 1 refused, walk ended cleanly

        var (fails, _) = Build(StubHandler.Csv("Query Error: Invalid or missing query parameter - part",
            System.Net.HttpStatusCode.BadRequest));
        await Assert.ThrowsAsync<FmpApiException>(() => DrainAsync(fails.StreamAllEtfHoldingsAsync()));
    }

    [Fact]
    public async Task End_of_day_bars_keep_exponent_notation_which_is_why_they_are_double()
    {
        // Crypto rows arrive as 6.344979e-10. decimal cannot hold that magnitude, which is the one place in the
        // SDK where double is the right choice rather than the lazy one.
        var (endpoints, _) = Build(StubHandler.Csv(Fixture("eod-bulk.head.csv")));

        var rows = await DrainAsync(endpoints.StreamEndOfDayAsync(new LocalDate(2026, 8, 25)));

        var corgi = rows.Single(r => r.Symbol == "CORGIBUSD");
        Assert.Equal(6.344979e-10, corgi.Open);
        Assert.Equal(6.35949e-10, corgi.AdjustedClose);
        Assert.Equal(5, corgi.Volume);
        Assert.Equal(9.377582044223232e-11, rows.Single(r => r.Symbol == "DINUUSD").High);
    }
}
