using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary><c>stable/shares-float</c>, checked against responses captured live from FMP on 2026-08-26.
///
/// <para>The fixtures are the evidence behind the model shape, so the tests read them rather than hand-written
/// JSON. AAPL is the ordinary case; SPY is an ETF, which reports a zero float against a real share count.</para></summary>
public class SharesFloatTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Fact]
    public async Task Maps_every_field_of_the_captured_aapl_row()
    {
        var (endpoints, _) = Build(Fixture("shares-float.AAPL.json"));

        var row = await endpoints.GetSharesFloatAsync("AAPL");

        Assert.NotNull(row);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal(Instant.FromUtc(2026, 8, 26, 14, 13, 45), row.AsOf);
        Assert.Equal(99.87879921341867m, row.FreeFloat);
        Assert.Equal(14_669_554_809m, row.FloatShares);
        Assert.Equal(14_687_356_000m, row.OutstandingShares);
        Assert.Equal(
            "https://www.sec.gov/Archives/edgar/data/320193/000032019326000020/aapl-20260627.htm",
            row.Source);
    }

    [Theory]
    [InlineData("shares-float.AAPL.json")]
    [InlineData("shares-float.SPY.json")]
    public void Model_and_payload_agree_field_for_field(string fixture)
    {
        // Both directions matter. A wrong [JsonPropertyName] does not fail — it silently reads null — and a field
        // FMP sends that no property claims is data being thrown away. Probing 40 symbols found exactly these six
        // wire names, none missing and none extra, so a change on either side should turn this red.
        using var doc = JsonDocument.Parse(Fixture(fixture));
        var wire = doc.RootElement[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        var mapped = typeof(SharesFloat).GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? throw new Xunit.Sdk.XunitException($"SharesFloat.{p.Name} has no [JsonPropertyName]."))
            .ToHashSet();

        Assert.Empty(wire.Except(mapped));   // FMP sends it, the model ignores it
        Assert.Empty(mapped.Except(wire));   // the model expects it, FMP no longer sends it
    }

    [Fact]
    public async Task Share_counts_survive_a_fractional_value_because_they_are_decimal_not_long()
    {
        // FMP serializes share counts as JSON floating-point, not integers: floatShares has been observed as
        // 25595002.125, a computation artifact of outstanding x free float %. System.Text.Json THROWS reading
        // that into a long?, which would abort the whole response rather than one field. Nothing in today's
        // 40-symbol sample is fractional, which is exactly why this needs its own test.
        var (endpoints, _) = Build(
            """
            [{"symbol":"TEST","date":"2026-08-26 14:13:45","freeFloat":100,
              "floatShares":25595002.125,"outstandingShares":25595002.125,"source":null}]
            """);

        var row = await endpoints.GetSharesFloatAsync("TEST");

        Assert.NotNull(row);
        Assert.Equal(25595002.125m, row.FloatShares);
        Assert.Equal(25595002.125m, row.OutstandingShares);
    }

    [Fact]
    public async Task The_refresh_stamp_is_read_as_utc_and_not_as_eastern()
    {
        // "2026-08-26 14:13:45" is the same shape the statement endpoints' acceptedDate uses, and that one is
        // EASTERN — so the string cannot tell you which converter is right. UTC was measured, not assumed: 40
        // stamps captured that day ran from 00:09:20 to 14:13:45, the latest 26 minutes BEFORE UTC-now and never
        // ahead of it. Read as Eastern the latest would sit 3.5 hours in the future, impossible for a value
        // recording when a row was last refreshed.
        var (endpoints, _) = Build(Fixture("shares-float.AAPL.json"));

        var row = await endpoints.GetSharesFloatAsync("AAPL");

        Assert.Equal(Instant.FromUtc(2026, 8, 26, 14, 13, 45), row!.AsOf);
        // What NullableEasternInstantJsonConverter would have produced — 26 August is EDT, so UTC-4.
        Assert.NotEqual(Instant.FromUtc(2026, 8, 26, 18, 13, 45), row.AsOf);
    }

    [Fact]
    public async Task An_etf_reports_a_zero_float_against_a_real_share_count()
    {
        // SPY, QQQ, VOO and IWM all answered freeFloat 0 and floatShares 0 on 2026-08-26 while still reporting
        // outstandingShares. Zero means "not computed for this security", not "no shares are freely tradable" —
        // and it is a measured zero, not an absent field, so it must not read as null.
        var (endpoints, _) = Build(Fixture("shares-float.SPY.json"));

        var row = await endpoints.GetSharesFloatAsync("SPY");

        Assert.NotNull(row);
        Assert.Equal(0m, row.FreeFloat);
        Assert.Equal(0m, row.FloatShares);
        Assert.Equal(1_065_238_226m, row.OutstandingShares);
        Assert.Null(row.Source);   // null source is normal on ETFs, not an error

        using var doc = JsonDocument.Parse(Fixture("shares-float.SPY.json"));
        var wire = doc.RootElement[0];
        Assert.Equal(JsonValueKind.Number, wire.GetProperty("freeFloat").ValueKind);   // present and zero,
        Assert.Equal(JsonValueKind.Null, wire.GetProperty("source").ValueKind);        // present and null
    }

    [Fact]
    public async Task Unknown_symbol_is_null_because_fmp_answers_an_empty_array_not_a_404()
    {
        var (endpoints, _) = Build("[]");

        Assert.Null(await endpoints.GetSharesFloatAsync("NOSUCH"));
    }

    [Fact]
    public async Task Rejects_a_blank_symbol_before_spending_a_request()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetSharesFloatAsync("  "));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Hits_its_own_path_carrying_only_the_symbol_and_the_key()
    {
        // No limit is sent, and there is no parameter to send one with: measured 2026-08-26, limit= is accepted
        // and ignored, so the endpoint returns exactly one row however it is asked.
        var (endpoints, handler) = Build();

        await endpoints.GetSharesFloatAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/shares-float", uri.AbsolutePath);
        Assert.Equal("?symbol=AAPL&apikey=k", uri.Query);
    }
}
