using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Proves the five CSV-built models bind the JSON forms of their endpoints.
///
/// <para><b>These are the tests that fail when someone deletes a <c>[JsonPropertyName]</c>.</b> The five records
/// here were written for the <c>*-bulk</c> CSV surface, which maps them by an explicit wire-name lookup, and
/// their C# property names deliberately drop FMP's <c>TTM</c> suffix — <c>GrossProfitMargin</c> for
/// <c>grossProfitMarginTTM</c>. The serializer context sets <c>PropertyNameCaseInsensitive</c> and no naming
/// policy, so without the attributes JSON binding falls back to the property name, misses, and leaves the field
/// null. Nothing throws. <c>symbol</c> populates and 61 metrics do not, and every assertion that spot-checked one
/// field would still pass.</para>
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
}
