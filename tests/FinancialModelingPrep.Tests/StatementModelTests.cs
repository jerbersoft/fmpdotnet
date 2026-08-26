using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialModelingPrep.Models;
using FinancialModelingPrep.Serialization;
using NodaTime;

namespace FinancialModelingPrep.Tests;

/// <summary>The seven period-shaped models, checked against responses captured live from FMP.
///
/// <para>These models were generated from those captures rather than transcribed from the documentation, because a
/// wrong <c>[JsonPropertyName]</c> does not fail — it silently yields null. The coverage test below is what turns
/// that silent failure into a red build.</para></summary>
public class StatementModelTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static TheoryData<string, Type> Captures => new()
    {
        { "income-statement.AAPL.json", typeof(IncomeStatement) },
        { "balance-sheet-statement.AAPL.json", typeof(BalanceSheetStatement) },
        { "balance-sheet-statement.JPM.json", typeof(BalanceSheetStatement) },
        { "cash-flow-statement.AAPL.json", typeof(CashFlowStatement) },
        { "ratios.AAPL.json", typeof(FinancialRatios) },
        { "key-metrics.AAPL.json", typeof(KeyMetrics) },
        { "financial-growth.AAPL.json", typeof(FinancialGrowth) },
        { "enterprise-values.AAPL.json", typeof(EnterpriseValues) },
    };

    [Theory]
    [MemberData(nameof(Captures))]
    public void Model_and_payload_agree_field_for_field(string fixture, Type model)
    {
        // Both directions matter. A property FMP does not send is dead weight that reads as null forever
        // (trader carries two: enterprise-values dropped fiscalYear and period, and its mapper turns the
        // missing values into fiscal year 0 and an empty string). A field FMP sends that no property claims
        // is data being thrown away.
        using var doc = JsonDocument.Parse(Fixture(fixture));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = model.GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"{model.Name}.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        Assert.Empty(wire.Except(mapped));   // FMP sends it, the model ignores it
        Assert.Empty(mapped.Except(wire));   // the model expects it, FMP no longer sends it
    }

    [Fact]
    public void Income_statement_reads_its_headline_figures()
    {
        var row = JsonSerializer.Deserialize(Fixture("income-statement.AAPL.json"),
            FmpJsonContext.Default.ListIncomeStatement)![0];

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(new LocalDate(2025, 9, 27), row.Date);
        Assert.Equal("USD", row.ReportedCurrency);
        Assert.Equal(416_161_000_000m, row.Revenue);
        Assert.Equal(7.49m, row.Eps);
        Assert.Equal(7.46m, row.EpsDiluted);
    }

    [Fact]
    public void Balance_sheet_reads_a_figure_larger_than_a_double_holds_exactly()
    {
        var row = JsonSerializer.Deserialize(Fixture("balance-sheet-statement.JPM.json"),
            FmpJsonContext.Default.ListBalanceSheetStatement)![0];

        Assert.Equal("JPM", row.Symbol);
        Assert.Equal(4_424_900_000_000m, row.TotalAssets);
    }

    [Fact]
    public void Cash_flow_reads_its_derived_aggregates()
    {
        var row = JsonSerializer.Deserialize(Fixture("cash-flow-statement.AAPL.json"),
            FmpJsonContext.Default.ListCashFlowStatement)![0];

        Assert.Equal(row.OperatingCashFlow + row.CapitalExpenditure, row.FreeCashFlow);
    }

    [Fact]
    public void Ratios_keeps_full_precision_on_a_seventeen_digit_value()
    {
        // Ratios arrive with up to 17 significant digits. Parsed as double the trailing digits are lost;
        // this asserts the decimal round-trips the exact string FMP sent.
        var json = Fixture("ratios.AAPL.json");
        using var doc = JsonDocument.Parse(json);
        var raw = doc.RootElement[0].GetProperty("effectiveTaxRate").GetRawText();

        var row = JsonSerializer.Deserialize(json, FmpJsonContext.Default.ListFinancialRatios)![0];

        Assert.Equal(decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture), row.EffectiveTaxRate);
    }

    [Fact]
    public void Key_metrics_and_growth_read_their_own_fields()
    {
        var metrics = JsonSerializer.Deserialize(Fixture("key-metrics.AAPL.json"),
            FmpJsonContext.Default.ListKeyMetrics)![0];
        var growth = JsonSerializer.Deserialize(Fixture("financial-growth.AAPL.json"),
            FmpJsonContext.Default.ListFinancialGrowth)![0];

        Assert.Equal("AAPL", metrics.Symbol);
        Assert.NotNull(metrics.ReturnOnEquity);
        // FMP spells the wire name "Developement"; the C# name fixes it. If the wire name were "corrected"
        // to match, this would read null instead.
        Assert.NotNull(metrics.ResearchAndDevelopmentToRevenue);
        Assert.NotNull(growth.EbitGrowth);
        Assert.NotNull(growth.EpsDilutedGrowth);
    }

    [Fact]
    public void Enterprise_values_has_no_fiscal_year_or_period_at_all()
    {
        // Not an omission in the model — FMP genuinely stops sending them on this one endpoint, so a row is
        // identified by symbol and date alone. Measured 2026-08-26.
        using var doc = JsonDocument.Parse(Fixture("enterprise-values.AAPL.json"));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("fiscalYear", wire);
        Assert.DoesNotContain("period", wire);

        var row = JsonSerializer.Deserialize(Fixture("enterprise-values.AAPL.json"),
            FmpJsonContext.Default.ListEnterpriseValues)![0];
        Assert.Equal(new LocalDate(2025, 9, 27), row.Date);
        Assert.Equal(row.MarketCapitalization - row.MinusCashAndCashEquivalents + row.AddTotalDebt,
                     row.EnterpriseValue);
    }

    [Theory]
    [InlineData("income-statement.AAPL.json")]
    [InlineData("balance-sheet-statement.AAPL.json")]
    [InlineData("cash-flow-statement.AAPL.json")]
    [InlineData("ratios.AAPL.json")]
    [InlineData("key-metrics.AAPL.json")]
    [InlineData("financial-growth.AAPL.json")]
    public void Fiscal_year_arrives_quoted_and_still_reads_as_a_number(string fixture)
    {
        // FMP sends "fiscalYear":"2025" — a quoted number. Without JsonNumberHandling.AllowReadingFromString
        // on the context, the first one aborts the whole response rather than one field.
        using var doc = JsonDocument.Parse(Fixture(fixture));
        Assert.Equal(JsonValueKind.String, doc.RootElement[0].GetProperty("fiscalYear").ValueKind);

        var year = fixture switch
        {
            "income-statement.AAPL.json" => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListIncomeStatement)![0].FiscalYear,
            "balance-sheet-statement.AAPL.json" => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListBalanceSheetStatement)![0].FiscalYear,
            "cash-flow-statement.AAPL.json" => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListCashFlowStatement)![0].FiscalYear,
            "ratios.AAPL.json" => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListFinancialRatios)![0].FiscalYear,
            "key-metrics.AAPL.json" => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListKeyMetrics)![0].FiscalYear,
            _ => JsonSerializer.Deserialize(Fixture(fixture), FmpJsonContext.Default.ListFinancialGrowth)![0].FiscalYear,
        };

        Assert.Equal(2025, year);
    }

    [Fact]
    public void Accepted_date_is_eastern_wall_clock_and_becomes_a_true_instant()
    {
        // The 4-vs-5 hour split is the whole point: both rows say "16:20"-ish in their own local terms, and
        // only a real timezone gets both right. Cross-checked against SEC EDGAR's own UTC acceptance times
        // on 2026-08-26 — see NullableEasternInstantJsonConverter for the accession numbers.
        var aapl = JsonSerializer.Deserialize(Fixture("income-statement.AAPL.json"),
            FmpJsonContext.Default.ListIncomeStatement)![0];
        var jpm = JsonSerializer.Deserialize(Fixture("balance-sheet-statement.JPM.json"),
            FmpJsonContext.Default.ListBalanceSheetStatement)![0];

        // FMP said "2025-10-31 06:01:26"; 31 October is EDT, so UTC-4.
        Assert.Equal(Instant.FromUtc(2025, 10, 31, 10, 1, 26), aapl.AcceptedDate);
        // FMP said "2026-02-13 16:20:00"; 13 February is EST, so UTC-5.
        Assert.Equal(Instant.FromUtc(2026, 2, 13, 21, 20, 0), jpm.AcceptedDate);

        // Reading the same strings as UTC — which is correct for the economic calendar, and what trader's
        // adapter does — would place both 4-5 hours early.
        Assert.NotEqual(Instant.FromUtc(2025, 10, 31, 6, 1, 26), aapl.AcceptedDate);
    }

    [Fact]
    public void Filing_date_is_a_plain_date_and_keeps_no_time()
    {
        var row = JsonSerializer.Deserialize(Fixture("income-statement.AAPL.json"),
            FmpJsonContext.Default.ListIncomeStatement)![0];

        Assert.Equal(new LocalDate(2025, 10, 31), row.FilingDate);
    }

    [Fact]
    public void A_cik_keeps_its_padding_here_too()
    {
        var row = JsonSerializer.Deserialize(Fixture("income-statement.AAPL.json"),
            FmpJsonContext.Default.ListIncomeStatement)![0];

        Assert.Equal("0000320193", row.Cik);
    }
}
