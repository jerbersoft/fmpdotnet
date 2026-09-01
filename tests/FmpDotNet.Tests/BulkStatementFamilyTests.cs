using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The six statement-family bulk downloads (#12), checked against responses captured live on 2026-08-26
/// for <c>year=2025&amp;period=Q1</c>.
///
/// <para>Each fixture is the real header plus real rows. AAPL appears throughout as the US case; <c>000001.SZ</c>
/// is in the income fixture because it is the other half of the <c>acceptedDate</c> story.</para></summary>
public class BulkStatementFamilyTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (BulkEndpoints Endpoints, StubHandler Handler) Build(string csv)
    {
        var handler = new StubHandler(StubHandler.Csv(csv));
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
    public async Task The_income_bulk_csv_maps_onto_the_same_model_the_per_symbol_endpoint_returns()
    {
        // The two carry exactly the same 39 field names, so duplicating the model for the CSV path would be 39
        // chances to drift for no gain. That was a hand-comparison of the header against [JsonPropertyName],
        // made on 2026-08-26 and asserted by nothing; BulkCsvColumnParityTests now proves it on every run, for
        // this model and the other seventeen (#55). This test stays as it is — it checks that the VALUES bind,
        // which is the half a column-name comparison cannot see.
        var (endpoints, _) = Build(Fixture("income-statement-bulk.head.csv"));

        var rows = await DrainAsync(endpoints.StreamIncomeStatementsAsync(2025, BulkFiscalPeriod.Q1));

        var aapl = rows.Single(r => r.Symbol == "AAPL");
        Assert.Equal(new LocalDate(2024, 12, 28), aapl.Date);
        Assert.Equal(2025, aapl.FiscalYear);
        Assert.Equal("Q1", aapl.Period);
        Assert.Equal("USD", aapl.ReportedCurrency);
        Assert.Equal(124_300_000_000m, aapl.Revenue);
        Assert.Equal(2.41m, aapl.Eps);
        Assert.Equal(15_081_724_000m, aapl.WeightedAverageSharesOutstanding);
    }

    [Fact]
    public async Task AcceptedDate_on_the_csv_path_is_eastern_exactly_as_it_is_on_the_json_path()
    {
        // The trap this closes: the CSV reader's ordinary GetInstant reads this identical wire shape as UTC,
        // because shares-float's `date` really is UTC. Using it here would put every filing four or five hours
        // out — small enough to look like data. AAPL's "2025-01-31 06:01:27" is EST, so 11:01:27Z.
        var (endpoints, _) = Build(Fixture("income-statement-bulk.head.csv"));

        var rows = await DrainAsync(endpoints.StreamIncomeStatementsAsync(2025, BulkFiscalPeriod.Q1));

        Assert.Equal(Instant.FromUtc(2025, 1, 31, 11, 1, 27), rows.Single(r => r.Symbol == "AAPL").AcceptedDate);
    }

    [Fact]
    public async Task The_offset_comes_from_the_tz_database_and_not_from_a_fixed_number()
    {
        // 000001.SZ's row is dated 2025-03-31, which is EDT (-04), against AAPL's January row at EST (-05). A
        // hardcoded offset would be right for one of these and wrong for the other.
        var (endpoints, _) = Build(Fixture("income-statement-bulk.head.csv"));

        var rows = await DrainAsync(endpoints.StreamIncomeStatementsAsync(2025, BulkFiscalPeriod.Q1));

        Assert.Equal(Instant.FromUtc(2025, 3, 31, 4, 0, 0), rows.Single(r => r.Symbol == "000001.SZ").AcceptedDate);
    }

    [Fact]
    public async Task A_midnight_accepted_date_is_a_date_with_no_time_and_is_preserved_as_sent()
    {
        // 23,056 of 43,124 rows in the measured response end 00:00:00 — a date padded to midnight, not a filing
        // accepted at midnight, and 80% of them carry an exchange suffix. It is kept rather than nulled because
        // midnight is a legal instant and discarding it would hide the pattern; this pins that choice.
        var (endpoints, _) = Build(Fixture("income-statement-bulk.head.csv"));

        var rows = await DrainAsync(endpoints.StreamIncomeStatementsAsync(2025, BulkFiscalPeriod.Q1));

        var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
        var sz = rows.Single(r => r.Symbol == "000001.SZ");
        Assert.Equal(new LocalTime(0, 0), sz.AcceptedDate!.Value.InZone(eastern).TimeOfDay);
        Assert.Equal("CNY", sz.ReportedCurrency);
    }

    [Theory]
    [InlineData(BulkFiscalPeriod.Annual, "annual")]
    [InlineData(BulkFiscalPeriod.Q1, "Q1")]
    [InlineData(BulkFiscalPeriod.Q2, "Q2")]
    [InlineData(BulkFiscalPeriod.Q3, "Q3")]
    [InlineData(BulkFiscalPeriod.Q4, "Q4")]
    public async Task The_year_and_period_reach_the_query(BulkFiscalPeriod period, string expected)
    {
        var (endpoints, handler) = Build(Fixture("income-statement-bulk.head.csv"));

        await DrainAsync(endpoints.StreamIncomeStatementsAsync(2025, period));

        var query = handler.Requests[0].Query;
        Assert.Contains("year=2025", query);
        Assert.Contains($"period={expected}", query);
    }

    [Fact]
    public void The_bulk_period_vocabulary_does_not_offer_the_per_symbol_spelling()
    {
        // Measured by response size on 2026-08-26: period=quarter returned byte-identical data to period=Q1
        // (12,525,406 bytes), NOT all four quarters. A caller carrying the per-symbol vocabulary across would get
        // a valid 200 with twelve megabytes of real data and silently be reading Q1 alone. The enum has no member
        // that renders "quarter", which is the only way to make that unwritable.
        var rendered = Enum.GetValues<BulkFiscalPeriod>().Select(p => p.ToQueryValue()).ToList();

        Assert.DoesNotContain("quarter", rendered);
        Assert.Equal(["annual", "Q1", "Q2", "Q3", "Q4"], rendered);
    }

    [Fact]
    public async Task Balance_sheets_and_cash_flows_share_their_per_symbol_models_too()
    {
        var (balance, _) = Build(Fixture("balance-sheet-statement-bulk.head.csv"));
        var (cash, _) = Build(Fixture("cash-flow-statement-bulk.head.csv"));

        var bs = Assert.Single(await DrainAsync(balance.StreamBalanceSheetsAsync(2025, BulkFiscalPeriod.Q1)));
        var cf = Assert.Single(await DrainAsync(cash.StreamCashFlowsAsync(2025, BulkFiscalPeriod.Q1)));

        Assert.Equal(344_085_000_000m, bs.TotalAssets);
        Assert.Equal(66_500_000_000m, bs.NetDebt);
        Assert.Equal(26_995_000_000m, cf.FreeCashFlow);
        Assert.Equal(0m, cf.InterestPaid);   // a real zero, not an absent field
    }

    // ───────────────────────────── growth variants ─────────────────────────────

    [Fact]
    public async Task Growth_values_are_fractions_and_the_upstream_spellings_are_honoured()
    {
        // growthEBITDA and growthEPS are the upstream's own casing. The C# names are corrected; the strings
        // handed to the CSV reader are FMP's, because that is what arrives.
        var (endpoints, _) = Build(Fixture("income-statement-growth-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamIncomeStatementGrowthAsync(2025, BulkFiscalPeriod.Q1)));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(0.40413886411856953m, row.GrowthEbitda);
        Assert.Equal(1.484536082474227m, row.GrowthEps);   // 148%, a fraction not a percentage
    }

    [Fact]
    public async Task A_misspelled_upstream_column_still_maps()
    {
        // growthNetCashProvidedByOperatingActivites — FMP is missing the second "i" in Activities. Reading it
        // requires spelling it FMP's way; exposing it should not.
        var (endpoints, _) = Build(Fixture("cash-flow-statement-growth-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamCashFlowGrowthAsync(2025, BulkFiscalPeriod.Q1)));

        Assert.Equal(0.11651933907724442m, row.GrowthNetCashProvidedByOperatingActivities);
        Assert.Equal(0.12935614776387902m, row.GrowthFreeCashFlow);
    }

    [Fact]
    public async Task The_balance_sheet_growth_variant_maps_its_own_odd_casing()
    {
        // growthOthertotalStockholdersEquity — lowercase "t" in the middle, upstream.
        var (endpoints, _) = Build(Fixture("balance-sheet-statement-growth-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamBalanceSheetGrowthAsync(2025, BulkFiscalPeriod.Q1)));

        Assert.Equal(0m, row.GrowthOtherTotalStockholdersEquity);
        Assert.Equal(-0.05724971231300345m, row.GrowthTotalAssets);
    }
}
