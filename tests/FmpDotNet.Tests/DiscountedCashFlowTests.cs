using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
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
}
