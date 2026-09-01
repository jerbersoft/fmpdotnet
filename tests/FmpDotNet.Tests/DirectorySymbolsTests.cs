using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary>The two symbol directories — <c>stable/stock-list</c> and <c>stable/actively-trading-list</c> — checked
/// against responses captured live from FMP on 2026-08-26.
///
/// <para>The fixtures are the first five rows of each, not the whole payloads: unlike the sector and industry
/// vocabularies, these run to 91,844 and 68,869 rows and 7.7 MB and 5.3 MB. The counts and the relationship
/// between the two lists were measured over the full captures and are recorded on the endpoint's XML docs; what is
/// pinned here is the mapping, which is where the two endpoints actually differ.</para>
///
/// <para><b>What makes this pair worth its own file:</b> the same value arrives under two different keys —
/// <c>companyName</c> on the stock list, <c>name</c> on the actively-trading list — while the values themselves
/// agreed character for character on all 68,869 shared symbols. One public
/// <see cref="CompanySymbol"/> is fed by two internal wire shapes, so both directions need holding.</para></summary>
public class DirectorySymbolsTests
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
        return (new DirectoryEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- the two key spellings --------------------------------------------------------------------------------

    [Fact]
    public async Task Reads_the_stock_list_where_the_name_is_spelled_companyName()
    {
        var (endpoints, _) = Build(Fixture("stock-list.head.json"));

        var symbols = await endpoints.GetStockListAsync();

        Assert.Equal(5, symbols.Count);
        Assert.Equal("LAAA", symbols[0].Symbol);
        Assert.Equal("Lakeshore Acquisition I Corp.", symbols[0].Name);
        Assert.Equal("PMEH.PA", symbols[2].Symbol);
    }

    [Fact]
    public async Task Reads_the_actively_trading_list_where_the_same_value_is_spelled_name()
    {
        var (endpoints, _) = Build(Fixture("actively-trading-list.head.json"));

        var symbols = await endpoints.GetActivelyTradingAsync();

        Assert.Equal(5, symbols.Count);
        Assert.Equal("ITRN", symbols[0].Symbol);
        Assert.Equal("Ituran Location and Control Ltd.", symbols[0].Name);
    }

    [Fact]
    public async Task The_two_wire_spellings_land_on_one_shape_so_a_caller_never_learns_which_is_which()
    {
        // ITRN sits in both captures. This is the assertion the whole two-model design exists to make true: the
        // same symbol read through either endpoint produces an equal CompanySymbol. A record's value equality
        // makes that a single comparison.
        var fromStockList = await Build(Fixture("stock-list.head.json")).Endpoints.GetStockListAsync();
        var fromTrading = await Build(Fixture("actively-trading-list.head.json")).Endpoints.GetActivelyTradingAsync();

        Assert.Equal(
            fromStockList.Single(s => s.Symbol == "ITRN"),
            fromTrading.Single(s => s.Symbol == "ITRN"));
    }

    [Fact]
    public async Task Reading_the_stock_list_wire_shape_does_not_pick_up_the_other_endpoints_key()
    {
        // Guards the failure this design is exposed to: if StockListRow ever bound `name` as well, a caller of one
        // endpoint would silently start reading a key that endpoint does not send. `name` here must be ignored.
        var (endpoints, _) = Build("""[{"symbol":"AAPL","name":"Apple Inc."}]""");

        var symbols = await endpoints.GetStockListAsync();

        Assert.Equal("AAPL", symbols[0].Symbol);
        Assert.Null(symbols[0].Name);
    }

    [Fact]
    public async Task Reading_the_actively_trading_wire_shape_does_not_pick_up_companyName_either()
    {
        var (endpoints, _) = Build("""[{"symbol":"AAPL","companyName":"Apple Inc."}]""");

        var symbols = await endpoints.GetActivelyTradingAsync();

        Assert.Equal("AAPL", symbols[0].Symbol);
        Assert.Null(symbols[0].Name);
    }

    // ---- the mapping rules ------------------------------------------------------------------------------------

    [Fact]
    public async Task Drops_a_row_with_no_symbol_because_a_directory_entry_with_no_key_is_not_an_entry()
    {
        var (endpoints, _) = Build(
            """
            [{"symbol":null,"companyName":"A"},{"symbol":"","companyName":"B"},
             {"symbol":"   ","companyName":"C"},{"symbol":"AAPL","companyName":"Apple Inc."},null,{}]
            """);

        var symbols = await endpoints.GetStockListAsync();

        Assert.Equal(["AAPL"], symbols.Select(s => s.Symbol));
    }

    [Fact]
    public async Task Keeps_a_row_with_no_name_because_dropping_it_would_shrink_the_universe_silently()
    {
        // The deliberate asymmetry with the rule above, and the one that matters more: callers use these lists to
        // decide what is listed. A missing name is a cosmetic gap; a missing symbol is a security that appears not
        // to exist. Nothing measured needed the tolerance — zero of 160,713 rows lacked a name.
        var (endpoints, _) = Build(
            """
            [{"symbol":"AAPL","companyName":null},{"symbol":"MSFT","companyName":"  "},{"symbol":"NVDA"}]
            """);

        var symbols = await endpoints.GetStockListAsync();

        Assert.Equal(["AAPL", "MSFT", "NVDA"], symbols.Select(s => s.Symbol));
        Assert.All(symbols, s => Assert.Null(s.Name));
    }

    [Fact]
    public async Task Trims_both_fields_because_a_padded_ticker_is_a_silent_equality_miss()
    {
        var (endpoints, _) = Build("""[{"symbol":"  AAPL  ","companyName":"  Apple Inc.  "}]""");

        var symbols = await endpoints.GetStockListAsync();

        Assert.Equal("AAPL", symbols[0].Symbol);
        Assert.Equal("Apple Inc.", symbols[0].Name);
    }

    [Fact]
    public async Task Preserves_the_wire_order_and_keeps_duplicates()
    {
        // Neither list is sorted and neither contained a duplicate on 2026-08-26 — 68,869 and 91,844 rows, all
        // unique. Sorting would spend an O(n log n) pass imposing an order the caller may not want, and collapsing
        // a duplicate would hide the day FMP starts sending one.
        var (endpoints, _) = Build(
            """[{"symbol":"ZZZ"},{"symbol":"AAA"},{"symbol":"ZZZ"}]""");

        Assert.Equal(["ZZZ", "AAA", "ZZZ"], (await endpoints.GetStockListAsync()).Select(s => s.Symbol));
    }

    [Fact]
    public async Task An_empty_payload_is_an_empty_list_never_null()
    {
        var stock = await Build("[]").Endpoints.GetStockListAsync();
        var trading = await Build("[]").Endpoints.GetActivelyTradingAsync();

        Assert.NotNull(stock);
        Assert.Empty(stock);
        Assert.NotNull(trading);
        Assert.Empty(trading);
    }

    // ---- the request ------------------------------------------------------------------------------------------

    public static TheoryData<string, Func<DirectoryEndpoints, Task<IReadOnlyList<CompanySymbol>>>> Calls => new()
    {
        { "stable/stock-list", e => e.GetStockListAsync() },
        { "stable/actively-trading-list", e => e.GetActivelyTradingAsync() },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Sends_no_query_parameter_at_all(
        string path, Func<DirectoryEndpoints, Task<IReadOnlyList<CompanySymbol>>> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        // Equality rather than Contains, and load-bearing here: `limit` is accepted and then ignored by both
        // endpoints — measured, `limit=5` still returned all 68,869 and 91,845 rows — so sending one would look
        // like a sampling call while transferring megabytes. There is no page or limit parameter to send.
        Assert.Equal("", uri.Query);
    }

    [Fact]
    public async Task A_wrong_path_is_an_error_even_though_its_body_is_a_valid_empty_list()
    {
        // The shape of the trap, reproduced: `stable/company-symbol-list` reads like the right name for a symbol
        // directory and answers HTTP 404 whose body is `[]` — measured 2026-08-26, content-length 2. Anything that
        // classifies on the body alone reads that as "FMP knows no symbols". The status is checked first, so it
        // throws; and the message must name the status, because the body has nothing to say.
        var handler = new StubHandler(StubHandler.Json("[]", System.Net.HttpStatusCode.NotFound));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var transport = new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }));

        var error = await Assert.ThrowsAsync<FmpApiException>(() => transport.GetListAsync(
            new FmpRequest("stable/company-symbol-list"),
            Serialization.FmpJsonContext.Default.ListStockListRow));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, error.StatusCode);
        Assert.Contains("404", error.Message);
        // Not the body. `FmpApiException: []` names neither the status nor the path — it is the least useful
        // sentence available about a request that went to a path FMP does not serve.
        Assert.DoesNotContain("[]", error.Message);
    }
}
