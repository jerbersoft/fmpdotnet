using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Proves the five CSV-built models bind the JSON forms of their endpoints.
///
/// <para><b>Not every one of these fails the same way if a <c>[JsonPropertyName]</c> is deleted.</b> Measured
/// 2026-08-27, only 108 of the 237 attributes across the five are load-bearing. <c>RatiosTtm</c> breaks hardest:
/// its C# property names deliberately drop FMP's <c>TTM</c> suffix — <c>GrossProfitMargin</c> for
/// <c>grossProfitMarginTTM</c> — and the serializer context sets <c>PropertyNameCaseInsensitive</c> with no
/// naming policy, so without the attribute, binding falls back to the property name, misses the suffixed wire
/// name, and leaves the field null with nothing throwing: <c>symbol</c> populates and all 61 metrics do not.
/// <c>KeyMetricsTtm</c> is close behind at 41 of 42. <c>BalanceSheetGrowth</c> is the other extreme — none of its
/// 56 attributes are load-bearing, because its wire names are already the camelCase of its property names — and
/// the remaining two records land in between. Either way, an assertion that spot-checked one field would still
/// pass.</para>
///
/// <para>So each test asserts the WHOLE record populated, against a row captured live on 2026-08-27 in which
/// every field carried a value — measured, not assumed: all five captures were checked for nulls and had
/// none.</para></summary>
public class StatementReuseBindingTests
{
    [Fact]
    public void Ratios_ttm_binds_all_sixty_two_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("ratios-ttm.AAPL.json"), FmpJsonContext.Default.ListRatiosTtm)![0];

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal("AAPL", row.Symbol);
        // The suffixed name specifically. `GrossProfitMargin` would bind from a hypothetical `grossProfitMargin`
        // by case-insensitive fallback; it is the TTM suffix that needs the attribute.
        Assert.NotNull(row.GrossProfitMargin);
    }

    [Fact]
    public void Key_metrics_ttm_binds_all_forty_three_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("key-metrics-ttm.AAPL.json"), FmpJsonContext.Default.ListKeyMetricsTtm)![0];

        Assert.Empty(Binding.Unbound(row));
        // `marketCap` carries NO suffix while `enterpriseValueTTM` does, on the same response. That inconsistency
        // is FMP's and is why the attribute values are copied from FromCsv rather than derived by a rule.
        Assert.NotNull(row.MarketCap);
        Assert.NotNull(row.EnterpriseValue);
    }

    [Fact]
    public void Income_statement_growth_binds_all_thirty_four_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("income-statement-growth.AAPL.json"), FmpJsonContext.Default.ListIncomeStatementGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
        Assert.Equal(2025, row.FiscalYear);          // arrives as the STRING "2025"; see the fiscal-year test below
        Assert.Equal("FY", row.Period);
        Assert.Equal(new NodaTime.LocalDate(2025, 9, 27), row.Date);
    }

    [Fact]
    public void Balance_sheet_growth_binds_all_fifty_six_fields()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("balance-sheet-statement-growth.AAPL.json"), FmpJsonContext.Default.ListBalanceSheetGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
        Assert.NotNull(row.GrowthTotalAssets);
    }

    [Fact]
    public void Cash_flow_growth_binds_all_forty_two_fields_including_fmps_typo()
    {
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("cash-flow-statement-growth.AAPL.json"), FmpJsonContext.Default.ListCashFlowGrowth)![0];

        Assert.Empty(Binding.Unbound(row));
        // FMP spells this `growthNetCashProvidedByOperatingActivites` — one `i` short of `Activities`. The C#
        // name is corrected and the attribute is not, which is the whole reason the attribute exists.
        Assert.NotNull(row.GrowthNetCashProvidedByOperatingActivities);
    }

    [Fact]
    public void A_fiscal_year_binds_from_both_wire_forms()
    {
        // `fiscalYear` is an int on six of the nineteen paths and a string on seven, measured 2026-08-27. One
        // `int?` property reads both ONLY because FmpJsonContext sets JsonNumberHandling.AllowReadingFromString,
        // which makes that option load-bearing rather than incidental. This is the test that says so.
        const string quoted = """[{"symbol":"AAPL","fiscalYear":"2025"}]""";
        const string bare = """[{"symbol":"AAPL","fiscalYear":2025}]""";

        Assert.Equal(2025,
            JsonSerializer.Deserialize(quoted, FmpJsonContext.Default.ListIncomeStatementGrowth)![0].FiscalYear);
        Assert.Equal(2025,
            JsonSerializer.Deserialize(bare, FmpJsonContext.Default.ListIncomeStatementGrowth)![0].FiscalYear);
    }

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

    public static TheoryData<string, Func<StatementEndpoints, Task>> GrowthCalls => new()
    {
        { "stable/income-statement-growth", e => e.GetIncomeStatementGrowthAsync("AAPL") },
        { "stable/balance-sheet-statement-growth", e => e.GetBalanceSheetGrowthAsync("AAPL") },
        { "stable/cash-flow-statement-growth", e => e.GetCashFlowGrowthAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(GrowthCalls))]
    public async Task Each_growth_path_goes_through_the_shared_periodic_shape(
        string path, Func<StatementEndpoints, Task> call)
    {
        var (endpoints, handler) = Build();

        await call(endpoints);

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("period=annual", uri.Query);
        Assert.Contains($"limit={StatementEndpoints.FullHistoryLimit}", uri.Query);
    }

    [Fact]
    public async Task A_growth_row_arrives_through_the_endpoint_fully_bound()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-growth.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementGrowthAsync("AAPL"));

        Assert.Empty(Binding.Unbound(row));
    }

    [Theory]
    [InlineData("stable/key-metrics-ttm")]
    [InlineData("stable/ratios-ttm")]
    public async Task The_ttm_snapshots_send_neither_period_nor_limit(string path)
    {
        // Measured 2026-08-27: each answers a single row and ignores both parameters. GetScoresAsync set the
        // precedent — an endpoint that discards a parameter should not be sent one.
        var (endpoints, handler) = Build();

        if (path.EndsWith("key-metrics-ttm", StringComparison.Ordinal))
            await endpoints.GetKeyMetricsTtmAsync("AAPL");
        else
            await endpoints.GetRatiosTtmAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Fact]
    public async Task A_ratios_ttm_snapshot_comes_back_as_one_record_not_a_list()
    {
        var (endpoints, _) = Build(Binding.Fixture("ratios-ttm.AAPL.json"));

        var row = await endpoints.GetRatiosTtmAsync("AAPL");

        Assert.NotNull(row);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task A_key_metrics_ttm_snapshot_comes_back_as_one_record_not_a_list()
    {
        var (endpoints, _) = Build(Binding.Fixture("key-metrics-ttm.AAPL.json"));

        var row = await endpoints.GetKeyMetricsTtmAsync("AAPL");

        Assert.NotNull(row);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task An_unknown_symbol_is_null_rather_than_an_exception_on_the_ttm_snapshots()
    {
        // FMP answers `[]` at HTTP 200 for an unknown symbol on all eleven list-shaped paths in this group,
        // measured 2026-08-27 — "not found" is a shape here, not a status code. Same rule as GetScoresAsync.
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetRatiosTtmAsync("NOSUCHSYM"));
    }
}
