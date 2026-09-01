using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;

namespace FmpDotNet.Tests;

/// <summary><c>stable/financial-scores</c>, checked against responses captured live from FMP on 2026-08-26.
///
/// <para>The fixtures are the evidence behind the model shape, so the tests read them rather than hand-written
/// JSON. AAPL is the ordinary case; SPY is an ETF, and an ETF answers the same empty array an unknown symbol
/// does.</para></summary>
public class FinancialScoresTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // One canned response per handler: FmpTransport disposes the HttpResponseMessage once it has read the body,
    // so a second call against the same stub would fail with ObjectDisposedException rather than with anything
    // that points at the reuse. Every test builds its own.
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

    [Fact]
    public async Task Maps_every_field_of_the_captured_aapl_row()
    {
        var (endpoints, _) = Build(Fixture("financial-scores.AAPL.json"));

        var row = await endpoints.GetScoresAsync("AAPL");

        Assert.NotNull(row);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal("USD", row.ReportedCurrency);
        Assert.Equal(12.553407594048608m, row.AltmanZScore);
        Assert.Equal(9m, row.PiotroskiScore);
        Assert.Equal(492_000_000m, row.WorkingCapital);
        Assert.Equal(383_266_000_000m, row.TotalAssets);
        Assert.Equal(11_326_000_000m, row.RetainedEarnings);
        Assert.Equal(155_386_000_000m, row.Ebit);
        Assert.Equal(4_574_891_083_660m, row.MarketCap);
        Assert.Equal(275_746_000_000m, row.TotalLiabilities);
        Assert.Equal(466_823_000_000m, row.Revenue);
    }

    [Fact]
    public void Model_and_payload_agree_field_for_field()
    {
        // Both directions matter. A wrong [JsonPropertyName] does not fail — it silently reads null — and a field
        // FMP sends that no property claims is data being thrown away. The AAPL capture carried exactly these
        // eleven wire names, none missing and none extra, so a change on either side should turn this red.
        using var doc = JsonDocument.Parse(Fixture("financial-scores.AAPL.json"));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = typeof(FinancialScores).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"FinancialScores.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        Assert.Equal(11, wire.Count);
        Assert.Empty(wire.Except(mapped));   // FMP sends it, the model ignores it
        Assert.Empty(mapped.Except(wire));   // the model expects it, FMP no longer sends it
    }

    [Fact]
    public void The_response_carries_no_date_no_period_and_no_fiscal_year()
    {
        // The single most surprising thing about this endpoint, and the reason it is asserted rather than only
        // documented: a caller storing these rows has nothing in the payload to key or order them by, and has to
        // stamp them at fetch time. If FMP ever starts sending one, this test says so.
        using var doc = JsonDocument.Parse(Fixture("financial-scores.AAPL.json"));
        var wire = doc.RootElement[0];

        Assert.False(wire.TryGetProperty("date", out _));
        Assert.False(wire.TryGetProperty("period", out _));
        Assert.False(wire.TryGetProperty("fiscalYear", out _));
        Assert.False(wire.TryGetProperty("calendarYear", out _));
    }

    [Fact]
    public async Task The_seven_figures_are_exactly_the_altman_z_inputs_and_reproduce_the_reported_score()
    {
        // Verified against the AAPL capture rather than assumed: the classic public-manufacturer weighting
        //   1.2*(workingCapital/totalAssets) + 1.4*(retainedEarnings/totalAssets) + 3.3*(ebit/totalAssets)
        //   + 0.6*(marketCap/totalLiabilities) + 1.0*(revenue/totalAssets)
        // reproduces 12.553407594048608 exactly in double, and to 1.3e-15 here where the divisions round at
        // decimal's 28 digits. That is what makes these seven fields the reason the response exists — and it
        // rules out the private-firm and non-manufacturer variants, which weight the terms differently and would
        // land nowhere near.
        var (endpoints, _) = Build(Fixture("financial-scores.AAPL.json"));

        var row = await endpoints.GetScoresAsync("AAPL");

        Assert.NotNull(row);
        var assets = row.TotalAssets!.Value;
        var recomputed =
            1.2m * (row.WorkingCapital!.Value / assets)
            + 1.4m * (row.RetainedEarnings!.Value / assets)
            + 3.3m * (row.Ebit!.Value / assets)
            + 0.6m * (row.MarketCap!.Value / row.TotalLiabilities!.Value)
            + 1.0m * (row.Revenue!.Value / assets);

        Assert.True(Math.Abs(recomputed - row.AltmanZScore!.Value) < 0.000_000_000_001m,
            $"recomputed {recomputed} against reported {row.AltmanZScore}");
    }

    [Fact]
    public async Task Figures_are_decimal_so_a_value_past_double_precision_survives_intact()
    {
        // Nothing captured needs the headroom — the largest figure measured was marketCap at 4,574,891,083,660,
        // which a double holds exactly — which is precisely why this needs its own test. Reported currency is not
        // always USD, and totalAssets in a currency worth a fraction of a cent runs orders of magnitude higher.
        // Past 2^53 a double silently rounds: 12345678901234567 comes back as ...568, and the row would look
        // right while being wrong in its last digit.
        var (endpoints, _) = Build(
            """
            [{"symbol":"TEST","reportedCurrency":"JPY","altmanZScore":1.2345678901234567,"piotroskiScore":5,
              "workingCapital":1,"totalAssets":12345678901234567,"retainedEarnings":1,"ebit":1,
              "marketCap":12345678901234567,"totalLiabilities":1,"revenue":1}]
            """);

        var row = await endpoints.GetScoresAsync("TEST");

        Assert.NotNull(row);
        Assert.Equal(12345678901234567m, row.TotalAssets);
        Assert.Equal(12345678901234567m, row.MarketCap);
        Assert.NotEqual(12345678901234568m, row.TotalAssets);   // what a double would have read
        Assert.Equal(1.2345678901234567m, row.AltmanZScore);
    }

    [Theory]
    [InlineData("9", 9)]
    [InlineData("9.0", 9)]     // the SAME score, written through a float
    [InlineData("8.5", 8.5)]   // a score that should not exist, and must still not cost the other ten fields
    [InlineData("\"9\"", 9)]   // quoted, which AllowReadingFromString already covered
    public async Task A_fractional_piotroski_score_does_not_abort_the_whole_response(string wire, double expected)
    {
        // Why PiotroskiScore is decimal and not int, even though it counts nine binary tests and is integral by
        // construction. No fractional value has been observed - this guards an unobserved risk, deliberately.
        // The measurement that decides it is about System.Text.Json, not about FMP: reading into an int? throws
        // on 8.5 AND EQUALLY ON 9.0, and the throw aborts the entire deserialisation rather than one field, so a
        // purely cosmetic serializer change upstream would cost all eleven properties. AllowReadingFromString
        // rescues the quoted "9" and does nothing for an unquoted 9.0. Same failure mode that made
        // SharesFloat.FloatShares decimal rather than long after floatShares was seen as 25595002.125.
        var (endpoints, _) = Build(
            $$"""
            [{"symbol":"TEST","reportedCurrency":"USD","altmanZScore":1.5,"piotroskiScore":{{wire}},
              "workingCapital":1,"totalAssets":2,"retainedEarnings":3,"ebit":4,
              "marketCap":5,"totalLiabilities":6,"revenue":7}]
            """);

        var row = await endpoints.GetScoresAsync("TEST");

        Assert.NotNull(row);
        Assert.Equal((decimal)expected, row.PiotroskiScore);
        Assert.Equal(7m, row.Revenue);   // the fields after it survived, which is the whole point
    }

    [Fact]
    public async Task Symbol_is_read_from_the_response_and_not_echoed_back_from_the_argument()
    {
        // FMP's spelling is the authoritative one and is not always the caller's - class-share tickers have to be
        // hyphenated, and a caller that echoes its own argument would store a spelling FMP may not accept on the
        // next request while never seeing the two disagree. The body here is synthetic: it differs from the
        // argument only to prove which of the two the property carries.
        var (endpoints, _) = Build(
            """[{"symbol":"BRK-B","reportedCurrency":"USD","piotroskiScore":6}]""");

        var row = await endpoints.GetScoresAsync("brk-b");

        Assert.Equal("BRK-B", row!.Symbol);
    }

    [Fact]
    public async Task An_etf_is_null_because_it_answers_the_same_empty_array_an_unknown_symbol_does()
    {
        // SPY measured [] with HTTP 200 on 2026-08-26. Both scores are built from issuer accounts an ETF does not
        // file, so "not applicable" and "not found" are the same shape and the caller cannot tell them apart from
        // the response alone.
        Assert.Equal("[]", Fixture("financial-scores.SPY.json"));

        var (endpoints, _) = Build(Fixture("financial-scores.SPY.json"));

        Assert.Null(await endpoints.GetScoresAsync("SPY"));
    }

    [Fact]
    public async Task Unknown_symbol_is_null_because_fmp_answers_an_empty_array_not_a_404()
    {
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetScoresAsync("NOSUCH"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_a_blank_symbol_before_spending_a_request(string symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetScoresAsync(symbol));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Hits_its_own_path_carrying_only_the_symbol()
    {
        // Deliberately not routed through the shared periodic query shape: measured 2026-08-26, this endpoint
        // takes neither period nor limit, and sending either would be sending FMP a parameter it does not accept.
        var (endpoints, handler) = Build();

        await endpoints.GetScoresAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/financial-scores", uri.AbsolutePath);
        Assert.Equal("?symbol=AAPL", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }
}
