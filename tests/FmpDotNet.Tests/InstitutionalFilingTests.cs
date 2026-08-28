using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The two market-wide 13F paths, and the two traps they carry.
///
/// <para><b>The date trap fails silently, which is why it is a test rather than a comment.</b>
/// <c>institutional-ownership/latest</c> sends <c>filingDate</c> as <c>"2026-08-28 00:00:00"</c> — midnight on
/// 1000 of 1000 rows measured 2026-08-28 — and <c>acceptedDate</c> as <c>"2026-08-28 15:47:03"</c>, midnight on
/// 0 of 1000. Every other path in this slice sends bare ISO dates and uses
/// <see cref="NullableLocalDateJsonConverter"/>, which parses with <c>LocalDatePattern.Iso</c> and returns null
/// on failure rather than throwing. Point it at either field here and every date reads null: no exception, no
/// failing assertion elsewhere, nothing in a diff.</para>
///
/// <para><b>The fractional trap is the evidence behind every <c>decimal?</c> in this slice.</b>
/// <c>industryValue</c> is fractional on 53 of 394 rows, while every money field on every other path measured
/// was integral. <c>System.Text.Json</c> throws on a fractional value bound to an integer property and
/// <c>FmpTransport</c> does not wrap the deserialiser, so one such value costs the caller the response.</para></summary>
public class InstitutionalFilingTests
{
    private static (InstitutionalOwnershipEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InstitutionalOwnershipEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- institutional-ownership/industry-summary ----------------------------------------------------------------

    [Fact]
    public void A_fractional_industry_value_binds_rather_than_throwing()
    {
        // THE test for the decimal? ruling. 523,604,028,974.8208 is a dollar aggregate with four decimal places,
        // and 53 of 394 rows in this quarter carry one. Retype IndustryValue as long? or int? and
        // System.Text.Json throws — costing the caller all 394 rows, not the one field. Every money and share
        // field in this slice is decimal? because of these 53 rows, even though the other 7,946 rows measured
        // were integral and would have justified long?.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-industry-summary.2025Q4.json"),
            FmpJsonContext.Default.ListIndustryOwnershipSummary)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(8775759887m, rows[0].IndustryValue);
        Assert.Equal(523604028974.8208m, rows[1].IndustryValue);
        Assert.Equal(1769618150.15m, rows[2].IndustryValue);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
    }

    [Fact]
    public void An_industry_summary_row_carries_its_quarter_end_and_its_label()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-industry-summary.2025Q4.json"),
            FmpJsonContext.Default.ListIndustryOwnershipSummary)!;

        Assert.Equal("BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)", rows[1].IndustryTitle);
        Assert.All(rows, r => Assert.Equal(new LocalDate(2025, 12, 31), r.Date));
    }

    [Fact]
    public async Task The_industry_summary_call_sends_year_and_quarter_and_nothing_else()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetIndustrySummaryAsync(2025, 4);

        Assert.Equal("/stable/institutional-ownership/industry-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=4", handler.Requests[0].Query);
        Assert.DoesNotContain("cik=", handler.Requests[0].Query);
        Assert.DoesNotContain("symbol=", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task An_industry_summary_quarter_outside_one_to_four_is_refused(int quarter)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustrySummaryAsync(2025, quarter));

        Assert.Empty(handler.Requests);
    }

    // ---- institutional-ownership/latest --------------------------------------------------------------------------

    [Fact]
    public void The_filing_feeds_two_dates_use_two_different_converters()
    {
        // The silent one, and the reason this file exists. filingDate is "2026-08-28 00:00:00" — a date wearing
        // a datetime's clothes, midnight on 1000 of 1000 rows — and reads as a LocalDate through
        // NullableDateAtMidnightJsonConverter. acceptedDate is "2026-08-28 15:47:03" — a real clock, midnight on
        // 0 of 1000 — and keeps its time as a LocalDateTime.
        //
        // Point NullableLocalDateJsonConverter at either and LocalDatePattern.Iso rejects the trailing time,
        // and the converter returns null rather than throwing (NodaConverters.cs:35-48). Every date in every row
        // would read null and nothing would say so.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].FilingDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 47, 3), rows[0].AcceptedDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 30, 34), rows[1].AcceptedDate);
        Assert.Equal(new LocalDateTime(2026, 8, 28, 15, 19, 1), rows[2].AcceptedDate);
    }

    [Fact]
    public void The_accepted_time_is_information_and_the_filing_time_is_not()
    {
        // Why they are two types rather than one. All three rows share a filingDate to the second — the dummy
        // midnight — while their acceptedDate values are 16 and 11 minutes apart. Reading acceptedDate as a
        // LocalDate would discard the only field that orders three filings made on the same day; reading
        // filingDate as a LocalDateTime would leak a meaningless 00:00:00 into every comparison a caller writes.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.All(rows, r => Assert.Equal(new LocalDate(2026, 8, 28), r.FilingDate));
        Assert.True(rows[0].AcceptedDate > rows[1].AcceptedDate);
        Assert.True(rows[1].AcceptedDate > rows[2].AcceptedDate);
    }

    [Fact]
    public void A_captured_latest_filing_binds_all_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-latest.head.json"),
            FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0002110329", rows[0].Cik);
        Assert.Equal("CORNERSTONE FINANCIAL MANAGEMENT LLC", rows[0].Name);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal("13F-HR/A", rows[0].FormType);
        Assert.EndsWith("-index.htm", rows[0].Link);
        Assert.EndsWith("primary_doc.xml", rows[0].FinalLink);
    }

    [Fact]
    public void A_date_that_is_null_or_in_the_wrong_shape_costs_one_field_not_the_row()
    {
        // House rule for every date converter: one bad stamp must not abort the response and take the other
        // seven fields with it. The bare-ISO case is NOT a measured wire form on this path — 1000 of 1000 rows
        // carried the time — it is here to pin that an unexpected shape reads null rather than throwing.
        var rows = JsonSerializer.Deserialize(
            """
            [{"cik":"A","filingDate":null,"acceptedDate":null},
             {"cik":"B","filingDate":"","acceptedDate":""},
             {"cik":"C","filingDate":"2026-08-28","acceptedDate":"2026-08-28"}]
            """, FmpJsonContext.Default.ListInstitutionalFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Null(r.FilingDate));
        Assert.All(rows, r => Assert.Null(r.AcceptedDate));
        Assert.Equal("C", rows[2].Cik);
    }

    [Fact]
    public async Task The_latest_filings_call_sends_page_and_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestFilingsAsync(page: 3, limit: 250);

        Assert.Equal("/stable/institutional-ownership/latest", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=3", handler.Requests[0].Query);
        Assert.Contains("limit=250", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    public async Task A_latest_filings_limit_above_the_measured_cap_is_refused(int limit)
    {
        // Measured 2026-08-28: limit=2000 answered exactly 1,000 rows with HTTP 200 and nothing in the body to
        // say it had been trimmed. The feed paginates, so a caller stepping `page` by 2,000 reads half the
        // archive and is never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestFilingsAsync(limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_latest_filings_limit_exactly_at_the_cap_is_accepted()
    {
        // The off-by-one boundary. See the note on the holder-analytics twin in InstitutionalOwnershipTests.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestFilingsAsync(
            limit: InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
    }

    [Fact]
    public void The_ownership_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, InstitutionalOwnershipEndpoints.MaxOwnershipPageSize);
        // And it is NOT the same as the holder-analytics cap, which is 100. One constant for both would have
        // let a caller ask extract-analytics/holder for 1,000 and silently receive 100.
        Assert.NotEqual(
            InstitutionalOwnershipEndpoints.MaxOwnershipPageSize,
            InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);
    }
}
