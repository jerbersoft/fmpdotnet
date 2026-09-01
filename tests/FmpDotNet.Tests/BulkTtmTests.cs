using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The two trailing-twelve-month bulk downloads (#13), checked against responses captured live on
/// 2026-08-26 — 44.0 MB / 71,500 rows and 69.5 MB / 71,504 rows.
///
/// <para>Memory behaviour is covered separately in <see cref="BulkStreamingMemoryTests"/>, which is where the
/// claim that these stream is actually enforced. For the record, streaming the two complete cached responses —
/// 113.5 MB and 143,004 rows — held peak live memory at 0.5 MB and 0.0 MB.</para></summary>
public class BulkTtmTests
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
    public async Task Key_metrics_ttm_maps_and_drops_the_redundant_suffix()
    {
        // Every column but `symbol` and `marketCap` ends TTM on the wire. Keeping it on the property would repeat
        // what the type name says, 42 times.
        var (endpoints, handler) = Build(Fixture("key-metrics-ttm-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamKeyMetricsTtmAsync()));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(4_589_211_255_760m, row.MarketCap);          // no TTM suffix upstream either
        Assert.Equal(4_633_974_255_760m, row.EnterpriseValue);    // enterpriseValueTTM
        Assert.Equal(9.926619416266979m, row.EvToSales);
        Assert.Equal(33.90307686954486m, row.EvToFreeCashFlow);
        // The endpoint takes no parameters at all.
        Assert.Equal("", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Ratios_ttm_maps_the_acronym_columns_without_shouting_them()
    {
        var (endpoints, _) = Build(Fixture("ratios-ttm-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamRatiosTtmAsync()));

        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(0.4865291555900202m, row.GrossProfitMargin);
        Assert.Equal(0.3327471011496863m, row.EbitMargin);        // ebitMarginTTM
        Assert.Equal(0.36092052019716253m, row.EbitdaMargin);     // ebitdaMarginTTM
    }

    [Fact]
    public async Task Full_precision_survives_the_decimal_mapping()
    {
        // 17 significant digits on a ratio. double would round these; the models are decimal throughout.
        var (endpoints, _) = Build(Fixture("ratios-ttm-bulk.head.csv"));

        var row = Assert.Single(await DrainAsync(endpoints.StreamRatiosTtmAsync()));

        Assert.Equal("0.4865291555900202", row.GrossProfitMargin!.Value.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Neither_ttm_model_carries_a_date_because_the_payload_has_none()
    {
        // A row is identified by symbol alone and describes the twelve months ending whenever FMP last recomputed
        // it — which the response does not say. Two rows fetched days apart are not a time series, and nothing in
        // the payload reveals that. Asserted structurally so a future "helpful" date property has to argue with
        // this test first.
        var metrics = typeof(KeyMetricsTtm).GetProperties().Select(p => p.Name).ToList();
        var ratios = typeof(RatiosTtm).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(metrics, n => n.Contains("Date", StringComparison.Ordinal));
        Assert.DoesNotContain(ratios, n => n.Contains("Date", StringComparison.Ordinal));
        Assert.Equal(43, metrics.Count);
        Assert.Equal(62, ratios.Count);
    }
}
