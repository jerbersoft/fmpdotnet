using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The six paths that answer one envelope around an open dictionary — and the reason two of them get a
/// different dictionary from the other four.</summary>
public class AsReportedTests
{
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

    public static TheoryData<string, Func<StatementEndpoints, Task>> AsReportedCalls => new()
    {
        { "stable/income-statement-as-reported", e => e.GetIncomeStatementAsReportedAsync("AAPL") },
        { "stable/balance-sheet-statement-as-reported", e => e.GetBalanceSheetAsReportedAsync("AAPL") },
        { "stable/cash-flow-statement-as-reported", e => e.GetCashFlowAsReportedAsync("AAPL") },
        { "stable/financial-statement-full-as-reported", e => e.GetFullStatementAsReportedAsync("AAPL") },
    };

    [Theory]
    [MemberData(nameof(AsReportedCalls))]
    public async Task Each_as_reported_path_goes_through_the_shared_periodic_shape(
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
    public async Task An_as_reported_row_carries_its_envelope_and_its_xbrl_dictionary()
    {
        var (endpoints, _) = Build(Binding.Fixture("income-statement-as-reported.AAPL.json"));

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(2025, row.FiscalYear);
        Assert.Equal("FY", row.Period);
        Assert.Equal("USD", row.ReportedCurrency);
        Assert.Equal(new NodaTime.LocalDate(2025, 9, 26), row.Date);
        // The keys are lowercased, concatenated XBRL tags — not the camelCase of the modelled statements.
        Assert.Equal(416161000000m, row.Data["revenuefromcontractwithcustomerexcludingassessedtax"].GetDecimal());
    }

    [Fact]
    public async Task An_as_reported_dictionary_holds_strings_and_floats_beside_its_numbers()
    {
        // This is why Data is JsonElement rather than decimal. Measured 2026-08-27, AAPL's FY2025
        // financial-statement-full-as-reported held 234 ints, 47 strings and 19 floats in one object, and its key
        // count swings 300 -> 923 between AAPL and JPM. A Dictionary<string, decimal> throws on the 47.
        var (endpoints, _) = Build(Binding.Fixture("financial-statement-full-as-reported.AAPL.mixed.json"));

        var row = Assert.Single(await endpoints.GetFullStatementAsReportedAsync("AAPL"));

        Assert.Equal(JsonValueKind.String, row.Data["documenttype"].ValueKind);
        Assert.Equal("10-K", row.Data["documenttype"].GetString());
        // The mixed fixture keeps 14 of AAPL's 300 keys and does not carry "grossprofit" — this checks the same
        // ValueKind.Number contrast on a key the fixture actually has.
        Assert.Equal(JsonValueKind.Number, row.Data["revenuefromcontractwithcustomerexcludingassessedtax"].ValueKind);
        Assert.Equal(0.00001m, row.Data["commonstockparorstatedvaluepershare"].GetDecimal());
        // Not every number here is money. `entityaddresspostalzipcode` is a POSTAL CODE that happens to be an
        // integer, which is the other half of why this dictionary is not typed as decimal.
        Assert.Equal(95014m, row.Data["entityaddresspostalzipcode"].GetDecimal());
    }

    [Theory]
    [InlineData("stable/revenue-product-segmentation")]
    [InlineData("stable/revenue-geographic-segmentation")]
    public async Task Segmentation_sends_no_limit_because_the_endpoint_ignores_it(string path)
    {
        // Measured 2026-08-27: both segmentation paths transfer the full set regardless of `limit`, the
        // behaviour already recorded for etf-list and its siblings.
        var (endpoints, handler) = Build();

        if (path.Contains("product", StringComparison.Ordinal))
            await endpoints.GetRevenueByProductAsync("AAPL");
        else
            await endpoints.GetRevenueByGeographyAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal($"/{path}", uri.AbsolutePath);
        Assert.Contains("period=annual", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Fact]
    public async Task Segmentation_does_not_send_the_structure_parameter_fmp_documents()
    {
        // Measured 2026-08-27 on AAPL and on JPM — a filer with genuinely nested segments — `structure=flat` and
        // `structure=hierarchical` returned payloads identical to sending nothing. A parameter that does nothing
        // still costs a caller the belief that it does something.
        var (endpoints, handler) = Build();

        await endpoints.GetRevenueByProductAsync("AAPL");

        Assert.DoesNotContain("structure", handler.Requests.Single().Query);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Segmentation_rejects_a_blank_symbol_before_spending_a_request(bool byProduct)
    {
        // Envelope() is a separate helper from Periodic() and does not inherit Periodic()'s guard coverage —
        // this proves Envelope()'s own ArgumentException.ThrowIfNullOrWhiteSpace(symbol) actually runs, and runs
        // before a request goes out.
        var (endpoints, handler) = Build();

        Func<Task> call = byProduct
            ? () => endpoints.GetRevenueByProductAsync("  ")
            : () => endpoints.GetRevenueByGeographyAsync("  ");

        await Assert.ThrowsAsync<ArgumentException>(call);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_segmentation_row_reads_its_segments_as_numbers()
    {
        var (endpoints, _) = Build(Binding.Fixture("revenue-product-segmentation.AAPL.json"));

        var rows = await endpoints.GetRevenueByProductAsync("AAPL");

        var row = rows[0];
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(209586000000m, row.Data["iPhone"]);
        // Segment names are the company's own, so they carry spaces and commas — they are not identifiers.
        Assert.Equal(35686000000m, row.Data["Wearables, Home and Accessories"]);
    }

    [Fact]
    public void A_string_segment_value_throws_rather_than_binding_as_zero()
    {
        // Deliberate. Measured across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B and KO, both segmentation endpoints and
        // both cadences — every row, not a sample — the values were 3,201 ints and 36 floats and not one string.
        // A non-numeric segment revenue would be a defect worth hearing about, so the decimal dictionary is the
        // right type and this throw is the right outcome.
        const string body = """[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","data":{"Mac":"lots"}}]""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(body, FmpJsonContext.Default.ListRevenueSegmentation));
    }

    [Fact]
    public async Task An_empty_data_object_binds_to_an_empty_dictionary_not_null()
    {
        var (endpoints, _) = Build("""[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","data":{}}]""");

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.NotNull(row.Data);
        Assert.Empty(row.Data);
    }

    [Fact]
    public async Task An_absent_data_object_binds_to_an_empty_dictionary_not_null()
    {
        // The property initialiser has to survive a missing key, not just an empty one — a null here would make
        // every caller null-check a dictionary that is documented never to be null.
        var (endpoints, _) = Build("""[{"symbol":"AAPL","fiscalYear":2025,"period":"FY"}]""");

        var row = Assert.Single(await endpoints.GetIncomeStatementAsReportedAsync("AAPL"));

        Assert.NotNull(row.Data);
        Assert.Empty(row.Data);
    }
}
