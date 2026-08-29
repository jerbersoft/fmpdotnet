using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The <c>TechnicalIndicators</c> group, against responses captured live on 2026-08-29.</summary>
public class TechnicalIndicatorsEndpointsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static (TechnicalIndicatorsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    [Theory]
    [InlineData(TechnicalIndicator.Adx, "adx")]
    [InlineData(TechnicalIndicator.Dema, "dema")]
    [InlineData(TechnicalIndicator.Ema, "ema")]
    [InlineData(TechnicalIndicator.Rsi, "rsi")]
    [InlineData(TechnicalIndicator.Sma, "sma")]
    [InlineData(TechnicalIndicator.StandardDeviation, "standarddeviation")]
    [InlineData(TechnicalIndicator.Tema, "tema")]
    [InlineData(TechnicalIndicator.WilliamsR, "williams")]
    [InlineData(TechnicalIndicator.Wma, "wma")]
    public async Task Each_indicator_reaches_its_own_path(TechnicalIndicator indicator, string segment)
    {
        // One method over nine paths, so the path is the only thing distinguishing the calls. Without this,
        // wiring every indicator to `sma` would pass every other test in this file.
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", indicator, 10, TechnicalIndicatorTimeframe.OneDay);

        Assert.Contains($"stable/technical-indicators/{segment}?", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task The_three_required_parameters_are_always_sent()
    {
        // Measured 2026-08-29: omitting any one answers HTTP 400 with
        // `Query Error: Invalid or missing query parameter - <name>`. There are no server-side defaults.
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", TechnicalIndicator.Rsi, 14, TechnicalIndicatorTimeframe.OneHour);

        var request = handler.Requests[0].ToString();
        Assert.Contains("symbol=AAPL", request);
        Assert.Contains("periodLength=14", request);
        Assert.Contains("timeframe=1hour", request);
    }

    [Fact]
    public async Task An_omitted_range_sends_no_range_parameters()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay);

        var request = handler.Requests[0].ToString();
        Assert.DoesNotContain("from=", request);
        Assert.DoesNotContain("to=", request);
    }

    [Fact]
    public async Task A_supplied_range_is_sent_in_FMPs_date_form()
    {
        var (endpoints, handler) = Build();
        await endpoints.GetAsync(
            "AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay,
            new LocalDate(2026, 8, 17), new LocalDate(2026, 8, 28));

        var request = handler.Requests[0].ToString();
        Assert.Contains("from=2026-08-17", request);
        Assert.Contains("to=2026-08-28", request);
    }

    [Fact]
    public async Task The_response_binds_through_the_shared_record()
    {
        var (endpoints, _) = Build(Fixture("technical-indicators-sma.AAPL.head.json"));
        var rows = await endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay);

        Assert.Equal(3, rows.Count);
        Assert.Equal(TechnicalIndicator.Sma, rows[0].Indicator);
        Assert.Equal(319.7m, rows[0].Close);
        Assert.NotNull(rows[0].Value);
    }

    [Fact]
    public async Task Indicator_is_resolved_from_the_body_not_the_argument_that_was_sent()
    {
        // TechnicalIndicatorBar.Indicator's doc promises it is "resolved from the column that arrived, not
        // stamped from the argument that was sent." Every other test in this file asks for the same indicator
        // the stub answers, so none of them would notice a future change that stamped Indicator from the
        // caller's argument instead of reading the column FMP actually sent. Here the stub answers `adx` while
        // the call asks for `sma`, and the resolved value is required to be the one that arrived.
        var (endpoints, _) = Build(Fixture("technical-indicators-adx.AAPL.head.json"));

        var rows = await endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay);

        Assert.Equal(TechnicalIndicator.Adx, rows[0].Indicator);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_period_below_one_throws_before_any_call_is_made(int periodLength)
    {
        // Measured 2026-08-29: FMP answers periodLength=0 and periodLength=-5 with HTTP 200 and `[]`. A caller
        // whose computed period lands on zero would read that as "this symbol has no data" — a plausible,
        // wrong answer bought with a call from their quota.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, periodLength,
                                     TechnicalIndicatorTimeframe.OneDay));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backwards_range_throws_before_any_call_is_made()
    {
        // Measured 2026-08-29: `from` after `to` answers HTTP 200 with 1254 rows — `to` honoured, `from`
        // silently discarded. A plainly wrong argument would otherwise return a plausible result.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetAsync(
                "AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay,
                new LocalDate(2026, 8, 28), new LocalDate(2026, 8, 1)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_unknown_segment_answering_404_with_an_empty_array_still_throws()
    {
        // Measured 2026-08-29: `stable/technical-indicators/macd` answers HTTP 404 with the body `[]` — the
        // SUCCESS shape on a failure status. Passing that through would surface as "no data" instead of
        // "no such indicator". Guards FmpTransport.ReadFailureAsync's array branch for this endpoint.
        var handler = new StubHandler(StubHandler.Json("[]", System.Net.HttpStatusCode.NotFound));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        var error = await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay));
        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task An_invalid_timeframe_answering_400_with_bare_text_keeps_the_sentence()
    {
        // Measured 2026-08-29 on `1week`, `1month` and `2hour`: HTTP 400 with the body
        // `Invalid timeframe provided.` — 27 bytes of bare text under a `content-type: application/json` that
        // is a lie. EnsureSuccessStatusCode would throw that sentence away and report only the status.
        var handler = new StubHandler(
            StubHandler.Json("Invalid timeframe provided.", System.Net.HttpStatusCode.BadRequest));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var endpoints = new TechnicalIndicatorsEndpoints(
            new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" })));

        var error = await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetAsync("AAPL", TechnicalIndicator.Sma, 10, TechnicalIndicatorTimeframe.OneDay));
        Assert.Contains("Invalid timeframe provided.", error.Message);
    }
}
