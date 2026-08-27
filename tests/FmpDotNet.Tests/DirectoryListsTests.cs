using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary>The eleven list endpoints on <see cref="DirectoryEndpoints"/> that answer "what exists", checked
/// against responses captured live from FMP on 2026-08-27.
///
/// <para>Separate from <see cref="DirectoryEndpointsTests"/>, which pins the two reference vocabularies, and from
/// <see cref="DirectorySymbolsTests"/>, which pins the two symbol directories.</para></summary>
public class DirectoryListsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (DirectoryEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DirectoryEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task A_country_list_unwraps_to_iso_two_letter_codes()
    {
        var (endpoints, _) = Build(Fixture("available-countries.json"));

        var countries = await endpoints.GetCountriesAsync();

        // Codes, not names. FMP calls the key `country` and sends "FK", not "Falkland Islands" — a caller
        // building a display label needs a lookup, and the measured 117 rows are all two characters.
        Assert.Equal(["FK", "MT", "SG", "PH", "US"], countries);
    }

    [Fact]
    public async Task The_etf_list_reads_the_name_key_that_stock_list_spells_differently()
    {
        var (endpoints, _) = Build(Fixture("etf-list.head.json"));

        var etfs = await endpoints.GetEtfListAsync();

        // The point of the assertion is Name being populated at all. etf-list sends `name`; if this bound
        // StockListRow (`companyName`) every name would be null and the row count would still be 3.
        Assert.Equal(3, etfs.Count);
        Assert.Equal("BREM", etfs[0].Symbol);
        Assert.Equal("iShares Emerging Markets Bond Active ETF", etfs[0].Name);
    }

    [Fact]
    public async Task The_etf_list_asks_for_the_path_fmp_serves()
    {
        var (endpoints, handler) = Build("[]");

        await endpoints.GetEtfListAsync();

        Assert.Equal("/stable/etf-list", handler.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task A_crypto_supply_beyond_long_max_is_read_rather_than_refused()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // SHIBDOGEUSD is the single row of 4,793 measured 2026-08-27 that exceeds long.MaxValue on both supply
        // fields. Typed `long?` this throws a JsonException and costs the whole 4,793-row response, not one field.
        Assert.Equal("SHIBDOGEUSD", coins[0].Symbol);
        Assert.Equal(9223372036854776000m, coins[0].CirculatingSupply);
        Assert.Equal(183985283821237380000000m, coins[0].TotalSupply);
    }

    [Fact]
    public async Task A_fractional_crypto_supply_is_read_rather_than_refused()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // 953 of 4,792 circulating values carried a fractional part on 2026-08-27. A whole-number type refuses
        // every one of them.
        Assert.Equal(6304286374.701883m, coins[1].CirculatingSupply);
    }

    [Fact]
    public async Task A_missing_crypto_supply_reads_as_null_rather_than_zero()
    {
        var (endpoints, _) = Build(Fixture("cryptocurrency-list.overflow.json"));

        var coins = await endpoints.GetCryptocurrencyListAsync();

        // 1,474 of 4,793 rows omitted totalSupply. Zero would be a claim; null is the absence of one.
        Assert.Null(coins[2].TotalSupply);
        Assert.Null(coins[2].IcoDate);
    }

    [Fact]
    public async Task A_commodity_carries_no_exchange_and_that_is_not_a_fault()
    {
        var (endpoints, _) = Build(Fixture("commodities-list.json"));

        var commodities = await endpoints.GetCommodityListAsync();

        // Null on all 40 measured rows. Pinned so the day it starts arriving is a visible change rather than a
        // silent one, and so the smoke baseline recording it empty reads as correct rather than as drift.
        Assert.All(commodities, c => Assert.Null(c.Exchange));
        Assert.Equal("Dec", commodities[0].TradeMonth);
        // USX is US cents, not a typo for USD. A caller converting prices must not treat the two alike.
        Assert.Equal("USX", commodities[1].Currency);
    }

    [Fact]
    public async Task A_forex_pair_carries_both_sides_of_the_cross()
    {
        var (endpoints, _) = Build(Fixture("forex-list.head.json"));

        var pairs = await endpoints.GetForexListAsync();

        Assert.Equal("ARSMXN", pairs[0].Symbol);
        Assert.Equal("ARS", pairs[0].FromCurrency);
        Assert.Equal("Mexican Peso", pairs[0].ToName);
    }

    [Fact]
    public async Task An_index_carries_its_exchange_and_currency()
    {
        var (endpoints, _) = Build(Fixture("index-list.head.json"));

        var indexes = await endpoints.GetIndexListAsync();

        Assert.Equal("^TTIN", indexes[0].Symbol);
        Assert.Equal("CAD", indexes[0].Currency);
    }

    public static TheoryData<string, Func<DirectoryEndpoints, Task>> AssetClassCalls => new()
    {
        { "/stable/commodities-list", e => e.GetCommodityListAsync() },
        { "/stable/cryptocurrency-list", e => e.GetCryptocurrencyListAsync() },
        { "/stable/forex-list", e => e.GetForexListAsync() },
        { "/stable/index-list", e => e.GetIndexListAsync() },
    };

    [Theory]
    [MemberData(nameof(AssetClassCalls))]
    public async Task Each_asset_class_list_asks_for_the_path_fmp_serves(
        string path, Func<DirectoryEndpoints, Task> call)
    {
        var (endpoints, handler) = Build("[]");

        await call(endpoints);

        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task An_exchange_delay_is_kept_as_the_prose_fmp_sends()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // Free text, not a duration. Four spellings measured across 63 rows — "Real-time", "20 min", "15 min",
        // "10 min" — with no published mapping, so parsing to a Duration would mean inventing one.
        Assert.Equal("Real-time", exchanges[0].Delay);
        Assert.Equal("20 min", exchanges[1].Delay);
    }

    [Fact]
    public async Task An_exchange_with_no_delay_reads_as_null()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // FSX was the only one of 63 with a null delay on 2026-08-27.
        Assert.Equal("FSX", exchanges[3].Exchange);
        Assert.Null(exchanges[3].Delay);
    }

    [Fact]
    public async Task A_symbol_suffix_of_not_applicable_arrives_as_that_literal_string()
    {
        var (endpoints, _) = Build(Fixture("available-exchanges.json"));

        var exchanges = await endpoints.GetExchangesAsync();

        // 5 of 63 rows carry the literal "N/A" rather than null. The SDK does not normalise it — see the model —
        // so this test exists to make the hazard visible rather than to assert a fix.
        Assert.Equal("N/A", exchanges[0].SymbolSuffix);
        Assert.Equal(".AX", exchanges[1].SymbolSuffix);
    }

    [Fact]
    public async Task A_statement_symbol_distinguishes_trading_from_reporting_currency()
    {
        var (endpoints, _) = Build(Fixture("financial-statement-symbol-list.head.json"));

        var symbols = await endpoints.GetFinancialStatementSymbolsAsync();

        // TOELY trades in USD and reports in JPY. Reading either field as "the currency" is wrong for one of them.
        Assert.Equal("USD", symbols[0].TradingCurrency);
        Assert.Equal("JPY", symbols[0].ReportingCurrency);
        // Null on 149 of 68,200 measured rows.
        Assert.Null(symbols[2].ReportingCurrency);
    }

    [Fact]
    public async Task A_transcript_count_arrives_as_a_string_and_reads_as_a_number()
    {
        var (endpoints, _) = Build(Fixture("earnings-transcript-list.head.json"));

        var symbols = await endpoints.GetTranscriptSymbolsAsync();

        // The wire sends "6", quoted, on all 11,178 rows. This passes only because FmpJsonContext sets
        // NumberHandling = AllowReadingFromString — load-bearing here rather than incidental.
        Assert.Equal(6, symbols[0].TranscriptCount);
        Assert.Equal(16, symbols[1].TranscriptCount);
    }

    public static TheoryData<string, Func<DirectoryEndpoints, Task>> ReferenceCalls => new()
    {
        { "/stable/available-countries", e => e.GetCountriesAsync() },
        { "/stable/available-exchanges", e => e.GetExchangesAsync() },
        { "/stable/financial-statement-symbol-list", e => e.GetFinancialStatementSymbolsAsync() },
        { "/stable/earnings-transcript-list", e => e.GetTranscriptSymbolsAsync() },
    };

    [Theory]
    [MemberData(nameof(ReferenceCalls))]
    public async Task Each_reference_list_asks_for_the_path_fmp_serves(
        string path, Func<DirectoryEndpoints, Task> call)
    {
        var (endpoints, handler) = Build("[]");

        await call(endpoints);

        Assert.Equal(path, handler.Requests.Single().AbsolutePath);
    }
}
