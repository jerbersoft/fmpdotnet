using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The <c>Quote</c> group, checked against responses captured live from FMP on 2026-08-27.
///
/// <para>Two things here are worth more than the rest: that the two <c>timestamp</c> units are read with the right
/// converter each, and that the eight paired endpoints actually send <c>short=false</c> on one side of the pair
/// and not the other. Both are mistakes that compile, deserialise, and produce plausible output.</para></summary>
public class QuoteEndpointsTests
{
    private static readonly DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (QuoteEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new QuoteEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    // ---- the two timestamp units --------------------------------------------------------------------------------

    [Fact]
    public async Task A_quote_timestamp_is_read_as_epoch_seconds()
    {
        // The captured value is 1787774400. As seconds that is 2026-08-26 20:00:00 UTC — 16:00 in New York, the
        // closing print, which is exactly where a daily quote's stamp belongs. As milliseconds it would be
        // 1970-01-21, which is perfectly representable and therefore would NOT throw: the wrong converter here
        // produces a silently wrong answer rather than an error, which is why this is asserted at all.
        var quote = await Build(Fixture("quote.AAPL.json")).Endpoints.GetQuoteAsync("AAPL");

        var wallClock = quote!.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDate(2026, 8, 26), wallClock.Date);
        Assert.Equal(new LocalTime(16, 0, 0), wallClock.TimeOfDay);
    }

    [Fact]
    public async Task An_aftermarket_timestamp_is_read_as_epoch_milliseconds()
    {
        // The same field NAME as the quote above, on a sibling endpoint, in a different unit: the captured value
        // is 1787821614000. As milliseconds that is 05:06:54 ET — pre-market, where an extended-hours print
        // belongs. Read as SECONDS it is the year 58623, which throws; the reverse mistake does not, which is the
        // asymmetry that keeps the two converters separate types.
        var trade = await Build(Fixture("aftermarket-trade.AAPL.json"))
            .Endpoints.GetAftermarketTradeAsync("AAPL");

        var wallClock = trade!.Timestamp!.Value.InZone(Eastern);
        Assert.Equal(new LocalDate(2026, 8, 27), wallClock.Date);
        Assert.Equal(new LocalTime(5, 6, 54), wallClock.TimeOfDay);
        Assert.Equal(309.97m, trade.Price);

        // A single share. Extended-hours prints are small, which is the context that makes one of them weak
        // evidence of where the security is actually bid.
        Assert.Equal(1L, trade.TradeSize);
    }

    [Fact]
    public async Task The_aftermarket_quote_is_stamped_independently_of_the_trade()
    {
        // An earlier probe caught these two carrying the same millisecond and it was written down as evidence that
        // they were one snapshot. They are not: the captured fixtures are 25 seconds apart and a later live read
        // was 8, so the lag is real and variable rather than a constant. The equality assertion that a single
        // lucky probe would have justified is replaced by one that holds: these stamps are read separately, and
        // 25 seconds is what THIS capture recorded.
        //
        // It also exercises the millisecond converter on the second of the two models that use it.
        var trade = await Build(Fixture("aftermarket-trade.AAPL.json"))
            .Endpoints.GetAftermarketTradeAsync("AAPL");
        var quote = await Build(Fixture("aftermarket-quote.AAPL.json"))
            .Endpoints.GetAftermarketQuoteAsync("AAPL");

        var gap = trade!.Timestamp!.Value - quote!.Timestamp!.Value;
        Assert.Equal(Duration.FromSeconds(25), gap);

        Assert.Equal(309.91m, quote.BidPrice);
        Assert.Equal(310m, quote.AskPrice);
        Assert.True(quote.BidPrice < quote.AskPrice, "The bid must not be above the ask.");
    }

    // ---- fractional volume, which is why volume is not a long ---------------------------------------------------

    [Fact]
    public async Task A_fractional_volume_is_read_rather_than_refused()
    {
        // This is a regression test for a real defect, not a hypothetical. Volume was first typed `long?` on the
        // strength of AAPL and every other liquid symbol answering a whole number — and it deserialised fine
        // against every single-symbol fixture. The whole-universe sweep then failed on eleven endpoints at once
        // with "The JSON value could not be converted", because across 4,778 crypto rows 496 are fractional.
        //
        // A `long?` here does not degrade gracefully: System.Text.Json throws, so ONE fractional row costs the
        // caller the entire response. That is why the fixture keeps a whole-number row beside the fractional ones
        // — the type has to carry both, and a test that only proved the fractional case would be satisfied by
        // something that had broken the ordinary one.
        var rows = await Build(Fixture("batch-crypto-quotes.fractional-volume.json"))
            .Endpoints.GetCryptoQuotesAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal(10.492659228892249m, rows[0].Volume);
        Assert.Equal(124.95809276108218m, rows[1].Volume);
        Assert.Equal(145551m, rows[2].Volume);
    }

    [Fact]
    public async Task A_market_cap_with_a_floating_point_tail_is_read_rather_than_refused()
    {
        // The same failure on a different field, and worth its own test because the cause is different: this is
        // not real precision but a double that could not represent the integer it came from. Measured 2026-08-27,
        // GOOG answered marketCap 4115284521472.9995 on stable/batch-exchange-quote. Nothing observed exceeded
        // long.MaxValue, so the range was never the problem — only the fraction.
        const string body = """
            [{"symbol":"GOOG","name":"Alphabet Inc.","price":330.5,"marketCap":4115284521472.9995,"volume":12345}]
            """;
        var rows = await Build(body).Endpoints.GetExchangeQuotesFullAsync("NASDAQ");

        Assert.Equal(4115284521472.9995m, Assert.Single(rows).MarketCap);
    }

    [Fact]
    public async Task A_percentage_too_large_for_a_decimal_costs_one_field_not_the_response()
    {
        // Measured 2026-08-27 on stable/batch-etf-quotes: BMJJF answered
        //   {"price":177.34,"changePercentage":6.3878959205932735e+35,"change":177.34,"previousClose":0}
        // — a 6.4e35 percent move, which is what a 177.34 change against a zero previous close computes to. It is
        // nonsense, it is well-formed JSON, and decimal tops out near 7.9e28.
        //
        // The point of the test is the SECOND row. Without the tolerant converter that one silly value throws and
        // the caller loses all 14,537 ETFs, so what has to be asserted is not just that the outlier reads as null
        // but that everything after it still arrives — that is the difference between "one field is missing" and
        // "this endpoint is down".
        const string body = """
            [{"symbol":"BMJJF","name":"BMO Junior Gold Index ETF","price":177.34,
              "changePercentage":6.3878959205932735e+35,"change":177.34,"volume":0,"previousClose":0},
             {"symbol":"SPY","name":"SPDR S&P 500 ETF Trust","price":712.35,
              "changePercentage":0.4213,"change":2.99,"volume":41234567,"previousClose":709.36}]
            """;

        var rows = await Build(body).Endpoints.GetEtfQuotesFullAsync();

        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].ChangePercentage);
        Assert.Equal(177.34m, rows[0].Price);          // the rest of the bad row survives too
        Assert.Equal("SPY", rows[1].Symbol);
        Assert.Equal(0.4213m, rows[1].ChangePercentage);
    }

    // ---- the short/full pairs -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("stable/batch-etf-quotes")]
    [InlineData("stable/batch-mutualfund-quotes")]
    [InlineData("stable/batch-commodity-quotes")]
    [InlineData("stable/batch-crypto-quotes")]
    [InlineData("stable/batch-forex-quotes")]
    [InlineData("stable/batch-index-quotes")]
    public async Task Each_asset_class_pair_sends_short_false_on_exactly_one_side(string path)
    {
        // The pair exists ONLY because of this parameter — the paths are identical. A Full method that forgot it
        // would return the short shape deserialised into the wide model: every one of the thirteen extra
        // properties null, no exception, and a caller left thinking FMP had stopped sending them.
        var (shortEndpoints, shortHandler) = Build();
        var (fullEndpoints, fullHandler) = Build();

        await CallAsync(shortEndpoints, path, full: false);
        await CallAsync(fullEndpoints, path, full: true);

        Assert.Contains(path, shortHandler.Requests[0].ToString());
        Assert.DoesNotContain("short=", shortHandler.Requests[0].ToString());

        Assert.Contains(path, fullHandler.Requests[0].ToString());
        Assert.Contains("short=false", fullHandler.Requests[0].ToString());
    }

    private static Task CallAsync(QuoteEndpoints endpoints, string path, bool full) => path switch
    {
        "stable/batch-etf-quotes" => full ? endpoints.GetEtfQuotesFullAsync() : endpoints.GetEtfQuotesAsync(),
        "stable/batch-mutualfund-quotes" =>
            full ? endpoints.GetMutualFundQuotesFullAsync() : endpoints.GetMutualFundQuotesAsync(),
        "stable/batch-commodity-quotes" =>
            full ? endpoints.GetCommodityQuotesFullAsync() : endpoints.GetCommodityQuotesAsync(),
        "stable/batch-crypto-quotes" =>
            full ? endpoints.GetCryptoQuotesFullAsync() : endpoints.GetCryptoQuotesAsync(),
        "stable/batch-forex-quotes" =>
            full ? endpoints.GetForexQuotesFullAsync() : endpoints.GetForexQuotesAsync(),
        "stable/batch-index-quotes" =>
            full ? endpoints.GetIndexQuotesFullAsync() : endpoints.GetIndexQuotesAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Not a paired endpoint."),
    };

    [Fact]
    public async Task The_exchange_pair_sends_the_exchange_and_short_false_together()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetExchangeQuotesFullAsync("NASDAQ");

        var uri = handler.Requests[0].ToString();
        Assert.Contains("stable/batch-exchange-quote?", uri);
        Assert.Contains("exchange=NASDAQ", uri);
        Assert.Contains("short=false", uri);
    }

    [Fact]
    public async Task The_short_exchange_call_omits_the_flag()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetExchangeQuotesAsync("NASDAQ");

        Assert.DoesNotContain("short=", handler.Requests[0].ToString());
    }

    // ---- price change, whose wire names are not legal C# identifiers ---------------------------------------------

    [Fact]
    public async Task Every_price_change_window_is_mapped_from_its_wire_name()
    {
        // Ten of the eleven wire names begin with a digit, so no casing convention reaches them and a missing
        // [JsonPropertyName] reads as null rather than failing. Asserting one window would leave the other ten
        // free to be silently absent, so all eleven are checked.
        var change = await Build(Fixture("stock-price-change.AAPL.json"))
            .Endpoints.GetPriceChangeAsync("AAPL");

        Assert.NotNull(change);
        Assert.Equal("AAPL", change.Symbol);
        Assert.Equal(1.14553m, change.OneDay);
        Assert.Equal(0.44864605m, change.FiveDay);
        Assert.Equal(-6.96328m, change.OneMonth);
        Assert.Equal(0.83641628m, change.ThreeMonth);
        Assert.Equal(18.65016m, change.SixMonth);
        Assert.Equal(15.29832m, change.YearToDate);
        Assert.Equal(35.99288m, change.OneYear);
        Assert.Equal(73.95527m, change.ThreeYear);
        Assert.Equal(110.9354m, change.FiveYear);
        Assert.Equal(1073.53051m, change.TenYear);
        Assert.Equal(244115.03701m, change.Max);
    }

    [Fact]
    public async Task The_one_day_change_agrees_with_the_quotes_percentage()
    {
        // Two endpoints, one number — captured minutes apart on 2026-08-27. Not a tautology: they are different
        // paths with different field names (`1D` against `changePercentage`), and this is the cheapest available
        // check that both mappings landed on the same concept.
        var change = await Build(Fixture("stock-price-change.AAPL.json"))
            .Endpoints.GetPriceChangeAsync("AAPL");
        var quote = await Build(Fixture("quote.AAPL.json")).Endpoints.GetQuoteAsync("AAPL");

        Assert.Equal(quote!.ChangePercentage, change!.OneDay);
    }

    // ---- single-symbol shape ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_full_quote_reads_every_field_it_carries()
    {
        var quote = await Build(Fixture("quote.AAPL.json")).Endpoints.GetQuoteAsync("AAPL");

        Assert.NotNull(quote);
        Assert.Equal("AAPL", quote.Symbol);
        Assert.Equal("Apple Inc.", quote.Name);
        Assert.Equal(313.45m, quote.Price);
        Assert.Equal(3.55m, quote.Change);
        Assert.Equal(33_571_543m, quote.Volume);
        Assert.Equal(308.8001m, quote.DayLow);
        Assert.Equal(315.43m, quote.DayHigh);
        Assert.Equal(344.57m, quote.YearHigh);
        Assert.Equal(225.95m, quote.YearLow);
        Assert.Equal(4_603_751_738_200m, quote.MarketCap);
        Assert.Equal(311.2182m, quote.PriceAvg50);
        Assert.Equal(282.12024m, quote.PriceAvg200);
        Assert.Equal("NASDAQ", quote.Exchange);
        Assert.Equal(310.245m, quote.Open);
        Assert.Equal(309.9m, quote.PreviousClose);
    }

    [Fact]
    public async Task An_unknown_symbol_reads_as_null_rather_than_an_empty_row()
    {
        // Measured 2026-08-27: HTTP 200 with the body []. Every single-symbol method has to turn that into null
        // rather than throwing or handing back a default-constructed record.
        //
        // A stub each, because FmpTransport disposes the response once it has read it — one canned message cannot
        // serve five calls, and the second would fail with an ObjectDisposedException pointing at the stream
        // rather than at the lifetime that ended it.
        Assert.Null(await Build("[]").Endpoints.GetQuoteAsync("NOSUCHTICKERXYZ"));
        Assert.Null(await Build("[]").Endpoints.GetShortQuoteAsync("NOSUCHTICKERXYZ"));
        Assert.Null(await Build("[]").Endpoints.GetAftermarketTradeAsync("NOSUCHTICKERXYZ"));
        Assert.Null(await Build("[]").Endpoints.GetAftermarketQuoteAsync("NOSUCHTICKERXYZ"));
        Assert.Null(await Build("[]").Endpoints.GetPriceChangeAsync("NOSUCHTICKERXYZ"));
    }

    // ---- batches ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Several_symbols_are_joined_with_commas()
    {
        var (endpoints, handler) = Build(Fixture("batch-quote-short.AAPL-MSFT.json"));
        var rows = await endpoints.GetShortQuotesAsync(["AAPL", "MSFT"]);

        Assert.Contains("symbols=AAPL%2CMSFT", handler.Requests[0].ToString());
        Assert.Equal(2, rows.Count);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("MSFT", rows[1].Symbol);
    }

    [Fact]
    public async Task Blank_symbols_are_dropped_rather_than_sent()
    {
        // A trailing comma reaches FMP as a request for a symbol named "", which it answers by returning one fewer
        // row — indistinguishable from that symbol not existing. Dropping blanks here keeps the row count
        // meaningful.
        var (endpoints, handler) = Build();
        await endpoints.GetQuotesAsync(["AAPL", "  ", "MSFT"]);

        Assert.Contains("symbols=AAPL%2CMSFT", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task A_list_with_nothing_in_it_throws_rather_than_asking_for_nothing()
    {
        // An all-blank list would produce `symbols=` and an empty array, which reads as "none of these symbols are
        // known" — a wrong answer to a question that was never asked properly.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetQuotesAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetQuotesAsync(["", "   "]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetQuotesAsync(null!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_exchange_is_rejected_before_it_costs_a_call()
    {
        // Measured 2026-08-27: omitting the exchange answers HTTP 400 "Query Error: Invalid or missing query
        // parameter - exchange". Catching it here saves the round trip and names the parameter.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetExchangeQuotesAsync("  "));
        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetExchangeQuotesFullAsync(""));

        Assert.Empty(handler.Requests);
    }

    // ---- paths --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Each_single_symbol_endpoint_asks_for_its_own_path()
    {
        foreach (var (path, call) in new (string, Func<QuoteEndpoints, Task>)[]
        {
            ("stable/quote", e => e.GetQuoteAsync("AAPL")),
            ("stable/quote-short", e => e.GetShortQuoteAsync("AAPL")),
            ("stable/aftermarket-trade", e => e.GetAftermarketTradeAsync("AAPL")),
            ("stable/aftermarket-quote", e => e.GetAftermarketQuoteAsync("AAPL")),
            ("stable/stock-price-change", e => e.GetPriceChangeAsync("AAPL")),
        })
        {
            var (endpoints, handler) = Build();
            await call(endpoints);
            Assert.Contains($"{path}?symbol=AAPL", handler.Requests[0].ToString());
        }
    }
}
