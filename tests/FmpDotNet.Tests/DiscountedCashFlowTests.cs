using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The four Discounted Cash Flow paths, checked against captures taken live 2026-08-31.</summary>
public class DiscountedCashFlowTests
{
    [Fact]
    public void Both_plain_valuations_bind_all_four_keys_including_the_one_with_a_space_in_it()
    {
        // `Stock Price` is capitalised and contains a space. It is already documented for dcf-bulk's CSV on
        // BulkDiscountedCashFlow; it appears here in JSON. The Python fmpsdk had to abandon class-body
        // TypedDict syntax for this field because a Python identifier cannot contain a space — an
        // independent confirmation that the space is real and not a transcription slip.
        var unlevered = JsonSerializer.Deserialize(
            Binding.Fixture("discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListDcfValuation)!;
        var levered = JsonSerializer.Deserialize(
            Binding.Fixture("levered-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListLeveredDcfValuation)!;

        var u = Assert.Single(unlevered);
        var l = Assert.Single(levered);

        Assert.Empty(Binding.Unbound(u));
        Assert.Empty(Binding.Unbound(l));
        Assert.Equal("AAPL", u.Symbol);
        Assert.Equal("AAPL", l.Symbol);
        Assert.NotNull(u.StockPrice);
        Assert.NotNull(l.StockPrice);

        // The wire name, spelled exactly. A [JsonPropertyName("stockPrice")] binds nothing and leaves the
        // property null on every row, silently.
        Assert.Equal("Stock Price", typeof(DcfValuation)
            .GetProperty(nameof(DcfValuation.StockPrice))!
            .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
        Assert.Equal("Stock Price", typeof(LeveredDcfValuation)
            .GetProperty(nameof(LeveredDcfValuation.StockPrice))!
            .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
    }

    [Fact]
    public void The_levered_and_unlevered_valuations_are_two_types_that_cannot_be_assigned_to_each_other()
    {
        // The split is the point. Measured 2026-08-27/31, KO reads 83.71 unlevered against 49.77 levered —
        // a 41% gap — and JPM 728.00 against 907.85. Neither is "the" DCF, and a single record would let a
        // variable that has drifted from its call site pass silently for the other model's answer.
        //
        // This is a compile-time guarantee, so the test asserts what reflection can see: two distinct types,
        // neither assignable to the other, carrying the same four wire names.
        Assert.NotEqual(typeof(DcfValuation), typeof(LeveredDcfValuation));
        Assert.False(typeof(DcfValuation).IsAssignableFrom(typeof(LeveredDcfValuation)));
        Assert.False(typeof(LeveredDcfValuation).IsAssignableFrom(typeof(DcfValuation)));

        static string[] WireNames(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)];

        // Sorted, because GetProperties does not promise declaration order.
        Assert.Equal(["Stock Price", "date", "dcf", "symbol"],
            WireNames(typeof(DcfValuation)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(
            WireNames(typeof(DcfValuation)).OrderBy(n => n, StringComparer.Ordinal),
            WireNames(typeof(LeveredDcfValuation)).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void The_custom_projection_year_arrives_as_a_JSON_string_and_binds_as_an_int()
    {
        // The wire sends "2030", quoted. FmpJsonContext sets NumberHandling = AllowReadingFromString
        // globally, so it binds to int? with no converter at all — and this test is what proves the global
        // setting is doing that work, because deleting it would null this field on every row.
        //
        // int? rather than decimal? here, against the rule the rest of this slice follows, precisely BECAUSE
        // the value is quoted: a quoted year cannot arrive as 9.0 the way an unquoted number can.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("custom-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListCustomDcfProjection)!;

        Assert.Equal(2, rows.Count);
        Assert.Equal(2030, rows[0].Year);
        Assert.Equal(2029, rows[1].Year);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));

        var levered = JsonSerializer.Deserialize(
            Binding.Fixture("custom-levered-discounted-cash-flow.AAPL.json"),
            FmpJsonContext.Default.ListCustomLeveredDcfProjection)!;

        Assert.Equal(2, levered.Count);
        Assert.Equal(2030, levered[0].Year);
        Assert.All(levered, r => Assert.Empty(Binding.Unbound(r)));

        // Ten rows per response measured 2026-08-31, descending 2030 -> 2021; the fixture holds the first
        // two. Nothing on the wire marks which rows are history and which are forecast — two fields imply
        // two different boundaries — so the SDK surfaces Year and lets the caller decide.
        Assert.True(rows[0].Year > rows[1].Year);
    }

    [Fact]
    public void The_lowercase_o_in_costofDebt_is_reproduced_exactly()
    {
        // The only field in this group that breaks camelCase — `costofDebt`, with a lowercase o in "of".
        // Confirmed on the wire AND in the Python fmpsdk's type. A [JsonPropertyName("costOfDebt")] binds
        // nothing and leaves the property null on every row, on a nullable decimal that gives no hint.
        //
        // Note the contrast with `costOfEquity` beside it, which IS camelCase. The two sit next to each
        // other in the response and only one of them is misspelled.
        foreach (var (type, property) in new (Type, string)[]
                 {
                     (typeof(CustomDcfProjection), nameof(CustomDcfProjection.CostOfDebt)),
                     (typeof(CustomLeveredDcfProjection), nameof(CustomLeveredDcfProjection.CostOfDebt)),
                 })
        {
            Assert.Equal("costofDebt", type.GetProperty(property)!
                .GetCustomAttribute<JsonPropertyNameAttribute>()!.Name);
        }

        var row = JsonSerializer.Deserialize(
            """[{"costofDebt":4.48,"costOfEquity":8.31,"taxRateCash":16785417,"taxRate":15.61}]""",
            FmpJsonContext.Default.ListCustomDcfProjection)![0];

        Assert.Equal(4.48m, row.CostOfDebt);
        Assert.Equal(8.31m, row.CostOfEquity);

        // taxRateCash is a CASH TAX AMOUNT in dollars, not a rate — 13.3M to 24.1M for AAPL measured
        // 2026-08-31 — while taxRate beside it reads 15.61. The SDK keeps FMP's name and says so in the doc
        // rather than renaming a field the caller will look up in FMP's own documentation.
        Assert.Equal(16785417m, row.TaxRateCash);
        Assert.Equal(15.61m, row.TaxRate);
    }

    [Fact]
    public void The_two_custom_shapes_share_twenty_nine_keys_and_disagree_on_twenty_three()
    {
        // 47 and 34 keys, confirmed twice on 2026-08-31: against the live captures, and against the
        // independent Python fmpsdk, whose TypedDicts carry 47 and 34 fields with identical key sets.
        static HashSet<string> WireNames(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)];

        var unlevered = WireNames(typeof(CustomDcfProjection));
        var levered = WireNames(typeof(CustomLeveredDcfProjection));

        Assert.Equal(47, unlevered.Count);
        Assert.Equal(34, levered.Count);
        Assert.Equal(29, unlevered.Intersect(levered, StringComparer.Ordinal).Count());
        Assert.Equal(18, unlevered.Except(levered, StringComparer.Ordinal).Count());
        Assert.Equal(5, levered.Except(unlevered, StringComparer.Ordinal).Count());

        // The five levered-only names, spelled out — this is the half of the split a merged record would
        // have to make nullable-and-meaningless on the other path.
        Assert.Equal(
            ["freeCashFlow", "operatingCashFlow", "operatingCashFlowPercentage", "pvLfcf", "sumPvLfcf"],
            levered.Except(unlevered, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static (DiscountedCashFlowEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DiscountedCashFlowEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Each_of_the_four_paths_is_asked_exactly_once()
    {
        var (dcf, handler) = Build();

        await dcf.GetValuationAsync("AAPL");
        await dcf.GetLeveredValuationAsync("AAPL");
        await dcf.GetCustomValuationAsync("AAPL");
        await dcf.GetCustomLeveredValuationAsync("AAPL");

        Assert.Equal(
            [
                "/stable/discounted-cash-flow",
                "/stable/levered-discounted-cash-flow",
                "/stable/custom-discounted-cash-flow",
                "/stable/custom-levered-discounted-cash-flow",
            ],
            handler.Requests.Select(u => u.AbsolutePath));

        // No limit and no page on any of the four. Measured 2026-08-31,
        // custom-discounted-cash-flow?symbol=AAPL&limit=3 returned the full 10 rows — the parameter is
        // ignored, so offering it would be worse than not offering it.
        Assert.All(handler.Requests, u =>
        {
            Assert.DoesNotContain("limit=", u.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("page=", u.Query, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Omitting_the_assumptions_sends_nothing_but_the_symbol()
    {
        // An absent assumptions object and an empty one are the same request, deliberately: every property
        // is nullable and FmpRequest.With drops nulls, so "use FMP's default for that assumption" has one
        // spelling. The smoke sweep depends on this — it probes both custom paths with an empty record and
        // baselines FMP's own default valuation rather than an arbitrary set of overrides.
        var (dcf, handler) = Build();

        await dcf.GetCustomValuationAsync("AAPL");
        await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions());
        await dcf.GetCustomLeveredValuationAsync("AAPL");
        await dcf.GetCustomLeveredValuationAsync("AAPL", new CustomLeveredDcfAssumptions());

        Assert.All(handler.Requests, u =>
            Assert.Equal(["symbol", "apikey"], HttpUtility.ParseQueryString(u.Query)
                .AllKeys.Where(k => k is not null).Select(k => k!).ToArray()));
        Assert.Equal(handler.Requests[0].Query, handler.Requests[1].Query);
        Assert.Equal(handler.Requests[2].Query, handler.Requests[3].Query);
    }

    [Fact]
    public async Task Every_set_assumption_reaches_the_query_under_its_own_wire_name()
    {
        var (dcf, handler) = Build();

        await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions
        {
            RevenueGrowthPct = 12.5m,
            EbitdaPct = 30m,
            DepreciationAndAmortizationPct = 3m,
            CashAndShortTermInvestmentsPct = 20m,
            ReceivablesPct = 15m,
            InventoriesPct = 2m,
            PayablePct = 18m,
            EbitPct = 28m,
            CapitalExpenditurePct = -3m,
            TaxRate = 16m,
            LongTermGrowthRate = 3m,
            CostOfDebt = 4.5m,
            CostOfEquity = 8.31m,
            MarketRiskPremium = 4.72m,
            Beta = 1.1m,
            RiskFreeRate = 4.48m,
        });

        var query = HttpUtility.ParseQueryString(handler.Requests[0].Query);

        Assert.Equal("AAPL", query["symbol"]);
        Assert.Equal("12.5", query["revenueGrowthPct"]);
        Assert.Equal("30", query["ebitdaPct"]);
        Assert.Equal("3", query["depreciationAndAmortizationPct"]);
        Assert.Equal("20", query["cashAndShortTermInvestmentsPct"]);
        Assert.Equal("15", query["receivablesPct"]);
        Assert.Equal("2", query["inventoriesPct"]);
        Assert.Equal("18", query["payablePct"]);
        Assert.Equal("28", query["ebitPct"]);
        Assert.Equal("-3", query["capitalExpenditurePct"]);
        Assert.Equal("16", query["taxRate"]);
        Assert.Equal("3", query["longTermGrowthRate"]);
        Assert.Equal("4.5", query["costOfDebt"]);
        Assert.Equal("8.31", query["costOfEquity"]);
        Assert.Equal("4.72", query["marketRiskPremium"]);
        Assert.Equal("1.1", query["beta"]);
        Assert.Equal("4.48", query["riskFreeRate"]);

        // 16 overrides plus symbol plus the key.
        Assert.Equal(18, query.AllKeys.Length);

        // The response-side misspelling does NOT appear on the request side: the wire wants `costOfDebt`
        // here and sends `costofDebt` back. Both spellings are reproduced as they are.
        Assert.DoesNotContain("costofDebt", handler.Requests[0].Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_assumption_is_formatted_invariantly_whatever_the_ambient_culture_is()
    {
        // The culture is load-bearing, for exactly the reason ScreenerCriteria records: a value formatted
        // under a comma-decimal culture becomes `beta=1,1` in the query string and FMP does not reject it.
        // Measured 2026-08-31, custom-discounted-cash-flow?symbol=AAPL&notARealParam=99 returned HTTP 200
        // with longTermGrowthRate, beta and equityValuePerShare identical to the baseline — an unparseable
        // value is treated like an unrecognised one, so a German or French host would silently receive FMP's
        // DEFAULT valuation while believing it applied the caller's assumptions.
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var (dcf, handler) = Build();
            await dcf.GetCustomValuationAsync("AAPL", new CustomDcfAssumptions { Beta = 1.1m });
            await dcf.GetCustomLeveredValuationAsync(
                "AAPL", new CustomLeveredDcfAssumptions { Beta = 1.1m });

            Assert.All(handler.Requests, u =>
                Assert.Equal("1.1", HttpUtility.ParseQueryString(u.Query)["beta"]));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void The_two_assumption_vocabularies_are_pinned_and_neither_carries_the_dead_parameter()
    {
        // The reason there are two records rather than one. An unrecognised or wrong-path parameter is
        // SILENT: measured 2026-08-31, a wrong-path override returns HTTP 200 with a valuation identical to
        // the baseline, so a caller who hands ebitdaPct to the levered endpoint gets a number that ignored
        // their assumption. Two records make that a compile error.
        //
        // This is not hypothetical. The independent Python fmpsdk assembles BOTH custom calls through one
        // shared 18-parameter helper, which means eight of its eighteen levered parameters do nothing and two
        // of its eighteen unlevered ones do nothing.
        static HashSet<string> Names(Type t) =>
            [.. t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)];

        var unlevered = Names(typeof(CustomDcfAssumptions));
        var levered = Names(typeof(CustomLeveredDcfAssumptions));

        Assert.Equal(
            ["Beta", "CapitalExpenditurePct", "CashAndShortTermInvestmentsPct", "CostOfDebt", "CostOfEquity",
             "DepreciationAndAmortizationPct", "EbitPct", "EbitdaPct", "InventoriesPct", "LongTermGrowthRate",
             "MarketRiskPremium", "PayablePct", "ReceivablesPct", "RevenueGrowthPct", "RiskFreeRate",
             "TaxRate"],
            unlevered.OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(
            ["Beta", "CapitalExpenditurePct", "CostOfDebt", "CostOfEquity", "LongTermGrowthRate",
             "MarketRiskPremium", "OperatingCashFlowPct", "RevenueGrowthPct", "RiskFreeRate", "TaxRate"],
            levered.OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(9, unlevered.Intersect(levered, StringComparer.Ordinal).Count());
        Assert.Equal(7, unlevered.Except(levered, StringComparer.Ordinal).Count());
        Assert.Single(levered.Except(unlevered, StringComparer.Ordinal));

        // sellingGeneralAndAdministrativeExpensesPct is FMP's eighteenth override and it moved NOTHING on
        // either path, measured 2026-08-31. A property for it would be a control that does nothing, so it is
        // on neither record — and this assertion is what stops it being "helpfully" added back.
        Assert.DoesNotContain("SellingGeneralAndAdministrativeExpensesPct", unlevered);
        Assert.DoesNotContain("SellingGeneralAndAdministrativeExpensesPct", levered);

        // Every property on both is decimal?, which is what lets one Number() helper serve all of them.
        foreach (var t in new[] { typeof(CustomDcfAssumptions), typeof(CustomLeveredDcfAssumptions) })
            Assert.All(t.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                p => Assert.Equal(typeof(decimal?), p.PropertyType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_rejected_on_all_four_paths(string? blank)
    {
        // All four answer a naked request with HTTP 400 and a plain-text body naming `symbol`, measured
        // 2026-08-31. Rejecting locally saves a call against the key's quota.
        //
        // There is deliberately NO uppercase guard: measured 2026-08-31, symbol=aapl returned "AAPL" with
        // values byte-identical to the uppercase call on the plain path, and the custom path normalised and
        // returned all 10 rows. The News slice guards case because lowercase THERE returns 0 rows at HTTP
        // 200; that reasoning does not transfer, and a guard invented here would reject a request FMP serves.
        var (dcf, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetLeveredValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetCustomValuationAsync(blank!));
        await Assert.ThrowsAsync<ArgumentException>(() => dcf.GetCustomLeveredValuationAsync(blank!));
        Assert.Empty(handler.Requests);

        // And a lowercase symbol goes through untouched, which is the absence this asserts.
        await dcf.GetValuationAsync("aapl");
        Assert.Equal("aapl", HttpUtility.ParseQueryString(handler.Requests[0].Query)["symbol"]);
    }

    [Fact]
    public async Task A_null_symbol_is_refused_with_ArgumentNullException_on_all_four_paths()
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace(null) raises ArgumentNullException, and
        // Assert.ThrowsAsync matches the type EXACTLY rather than by assignment — the repo splits null from
        // blank for that reason, and DividendTests.cs:182 records it. Both branches refuse before a request
        // is built, which is the guarantee that matters to a caller.
        var (dcf, handler) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => dcf.GetValuationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dcf.GetLeveredValuationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dcf.GetCustomValuationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dcf.GetCustomLeveredValuationAsync(null!));
        Assert.Empty(handler.Requests);
    }
}
