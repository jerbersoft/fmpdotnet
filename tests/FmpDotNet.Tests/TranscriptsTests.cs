using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The three earnings-transcript paths, checked against captures taken live 2026-08-29.</summary>
public class TranscriptsTests
{
    private static (TranscriptsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new TranscriptsEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_transcript_binds_all_five_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript.AAPL.2025.Q3.json"),
            FmpJsonContext.Default.ListEarningsTranscript)!;

        Assert.Single(rows);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("Q3", rows[0].Period);
        Assert.Equal(2025, rows[0].Year);
        Assert.Equal(new LocalDate(2025, 7, 31), rows[0].Date);
        Assert.StartsWith("Suhasini Chandramouli: Good afternoon", rows[0].Content);
    }

    [Fact]
    public void The_three_transcript_records_each_keep_their_own_field_names()
    {
        // THE trap of this slice. FMP spells the same two facts three different ways across three paths:
        //
        //   earning-call-transcript          period: "Q3"   year: 2025
        //   earning-call-transcript-dates    quarter: 3     fiscalYear: 2026
        //   earning-call-transcript-latest   period: "Q2"   fiscalYear: 2025
        //
        // Harmonising the records would mean inventing values FMP did not send — an int where it sent a
        // string, or a `year` where it sent `fiscalYear`. This test fails the moment one record is
        // "corrected" to match its siblings.
        var transcript = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript.AAPL.2025.Q3.json"),
            FmpJsonContext.Default.ListEarningsTranscript)![0];
        var dates = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-dates.AAPL.head.json"),
            FmpJsonContext.Default.ListTranscriptDate)![0];
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-latest.head.json"),
            FmpJsonContext.Default.ListLatestTranscript)![0];

        Assert.Equal("Q3", transcript.Period);   // string, from `period`
        Assert.Equal(2025, transcript.Year);     // from `year`
        Assert.Equal(3, dates.Quarter);          // int, from `quarter`
        Assert.Equal(2026, dates.FiscalYear);    // from `fiscalYear`
        Assert.Equal("Q2", latest.Period);       // string, from `period`
        Assert.Equal(2025, latest.FiscalYear);   // from `fiscalYear`
    }

    [Fact]
    public void A_transcript_date_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-dates.AAPL.head.json"),
            FmpJsonContext.Default.ListTranscriptDate)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 7, 30), rows[0].Date);
        Assert.Equal(1, rows[2].Quarter);
    }

    [Fact]
    public void A_latest_row_binds_all_four_of_its_fields_including_a_non_US_ticker()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("earning-call-transcript-latest.head.json"),
            FmpJsonContext.Default.ListLatestTranscript)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));

        // The feed is global and the tickers carry exchange suffixes. Nothing splits on the dot.
        Assert.Equal("7011.T", rows[0].Symbol);
        Assert.Equal("601939.SS", rows[1].Symbol);
        Assert.Equal("PRS.OL", rows[2].Symbol);

        // Not sorted by date: row 0 is dated after row 1. Measured 2026-08-29 and captured deliberately, so
        // nothing downstream assumes an ordering the feed does not promise.
        Assert.True(rows[0].Date > rows[1].Date);
    }

    [Fact]
    public async Task A_miss_is_null_rather_than_an_empty_list()
    {
        // Single-row endpoints on this SDK return T?, following CompanyEndpoints.GetProfileAsync.
        var (endpoints, _) = Build();

        Assert.Null(await endpoints.GetTranscriptAsync("NOSUCH", 2025, 3));
    }

    [Fact]
    public async Task An_empty_answer_is_an_empty_list_rather_than_null_on_the_list_methods()
    {
        // Pins the promise on GetDatesAsync's and GetLatestAsync's doc comments: "Never null; empty for a
        // symbol with none, not an error" and "Never null" respectively. Both are driven through a bare `[]`
        // response here, unlike GetTranscriptAsync above, which collapses an empty answer to null instead.
        // Separate Build() calls, each good for one request — see Latest_sends_paging_only_when_it_is_given_some.
        var (datesEndpoints, _) = Build();
        var (latestEndpoints, _) = Build();

        var dates = await datesEndpoints.GetDatesAsync("NOSUCH");
        var latest = await latestEndpoints.GetLatestAsync();

        Assert.NotNull(dates);
        Assert.Empty(dates);
        Assert.NotNull(latest);
        Assert.Empty(latest);
    }

    [Fact]
    public async Task The_transcript_is_queried_with_quarter_even_though_it_answers_period()
    {
        // The request parameter and the response field disagree on this one endpoint: it is QUERIED with
        // `quarter=3` and ANSWERS `period: "Q3"`. A future reader who renames the parameter to match the
        // response gets HTTP 400.
        var (endpoints, handler) = Build();

        await endpoints.GetTranscriptAsync("AAPL", 2025, 3);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/earning-call-transcript", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", query);
        Assert.Contains("year=2025", query);
        Assert.Contains("quarter=3", query);
        Assert.DoesNotContain("period=", query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_symbol_is_refused_before_the_request_goes_out(string? symbol)
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => endpoints.GetDatesAsync(symbol!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Latest_sends_paging_only_when_it_is_given_some()
    {
        var (bare, bareHandler) = Build();
        await bare.GetLatestAsync();

        var (paged, pagedHandler) = Build();
        await paged.GetLatestAsync(limit: 50, page: 1);

        // The bare call is its own query, NOT a synonym for page=0. Measured 2026-08-29 they were issued at
        // the same instant and shared 71 of 100 rows.
        Assert.Equal("/stable/earning-call-transcript-latest", bareHandler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("page=", bareHandler.Requests[0].Query);
        Assert.DoesNotContain("limit=", bareHandler.Requests[0].Query);
        Assert.Contains("limit=50", pagedHandler.Requests[0].Query);
        Assert.Contains("page=1", pagedHandler.Requests[0].Query);
    }

    [Fact]
    public async Task Latest_refuses_a_limit_above_the_measured_cap()
    {
        // Measured 2026-08-29: limit=500 answered exactly 100 rows at HTTP 200, byte-identical to the bare
        // call, with nothing saying the request was trimmed. A caller who asks for 500 and pages by 500
        // reads a fifth of the feed and is never told.
        var (endpoints, handler) = Build();

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(
                limit: TranscriptsEndpoints.MaxLatestTranscriptPageSize + 1));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Latest_refuses_a_negative_page()
    {
        var (endpoints, handler) = Build();

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(page: -1));

        Assert.Equal("page", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }
}
