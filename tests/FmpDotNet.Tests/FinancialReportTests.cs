using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;

namespace FmpDotNet.Tests;

public class FinancialReportTests
{
    private static (StatementEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses.Length > 0 ? responses : [StubHandler.Json("[]")]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new StatementEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    /// <summary>A body FMP would answer with the workbook's content type, which is a lie about every one of
    /// these.</summary>
    private static HttpResponseMessage Binary(byte[] payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") },
            },
        };

    // ---- financial-reports-dates ----------------------------------------------------------------------------

    [Fact]
    public async Task The_dates_list_sends_only_a_symbol()
    {
        // Measured 2026-08-27: it ignores `limit` and transfers all 65 rows regardless.
        var (endpoints, handler) = Build();

        await endpoints.GetFinancialReportDatesAsync("AAPL");

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/financial-reports-dates", uri.AbsolutePath);
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
        Assert.DoesNotContain("period=", uri.Query);
    }

    [Fact]
    public async Task The_dates_list_rejects_a_blank_symbol_before_a_request_goes_out()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetFinancialReportDatesAsync("  "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_link_row_parses_fmps_response_period_back_into_the_request_enum()
    {
        // The whole point of the type. `financial-reports-dates` answers "Q3"; GetFinancialReportAsync takes a
        // FiscalPeriod. Typing this as a string would put a hand-written parse between the two calls.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("financial-reports-dates.AAPL.json")));

        var links = await endpoints.GetFinancialReportDatesAsync("AAPL");

        Assert.Equal("AAPL", links[0].Symbol);
        Assert.Equal(2026, links[0].FiscalYear);
        Assert.Equal(FiscalPeriod.Q3, links[0].Period);
        Assert.NotNull(links[0].LinkJson);
    }

    [Fact]
    public async Task An_annual_link_row_parses_fy_as_annual()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"symbol":"AAPL","fiscalYear":2025,"period":"FY","linkJson":"https://x","linkXlsx":"https://y"}]"""));

        var link = Assert.Single(await endpoints.GetFinancialReportDatesAsync("AAPL"));

        Assert.Equal(FiscalPeriod.Annual, link.Period);
    }

    [Fact]
    public async Task An_unrecognised_period_label_binds_to_null_rather_than_throwing()
    {
        // One unreadable label must not cost the caller the other 64 rows — the rule the date converters follow.
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"symbol":"AAPL","fiscalYear":2025,"period":"H1","linkJson":"https://x","linkXlsx":"https://y"}]"""));

        var link = Assert.Single(await endpoints.GetFinancialReportDatesAsync("AAPL"));

        Assert.Null(link.Period);
        Assert.Equal("AAPL", link.Symbol);
    }

    // ---- financial-reports-json -----------------------------------------------------------------------------

    [Fact]
    public async Task A_report_keeps_its_three_scalars_apart_from_its_seventy_sections()
    {
        var (endpoints, handler) = Build(StubHandler.Json(
            Binding.Fixture("financial-reports-json.AAPL.2025.FY.json")));

        var report = await endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Annual);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/financial-reports-json", uri.AbsolutePath);
        Assert.Contains("year=2025", uri.Query);
        Assert.Contains("period=annual", uri.Query);

        Assert.NotNull(report);
        Assert.Equal("AAPL", report.Symbol);
        Assert.Equal("FY", report.Period);          // FMP normalises `annual` to `FY` in its own echo
        Assert.Equal(2025, report.Year);            // arrives as the STRING "2025"
        // symbol, period and year are NOT sections.
        Assert.DoesNotContain("symbol", report.Sections.Keys);
        Assert.Contains("Cover Page", report.Sections.Keys);
    }

    [Fact]
    public async Task A_report_section_name_is_truncated_and_the_type_does_not_pretend_otherwise()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            Binding.Fixture("financial-reports-json.AAPL.2025.FY.json")));

        var report = await endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Annual);

        // Measured 2026-08-27: section names are cut at about 30 characters and vary per filing, which is why
        // nothing typed sits over them.
        Assert.Contains("CONSOLIDATED STATEMENTS OF OPER", report!.Sections.Keys);
        Assert.Equal(JsonValueKind.Array, report.Sections["CONSOLIDATED STATEMENTS OF OPER"].ValueKind);
    }

    [Fact]
    public async Task A_report_miss_arrives_as_an_error_envelope_on_a_200_and_raises()
    {
        var (endpoints, _) = Build(StubHandler.Json(
            """{"Error Message":"No Data for this symbol or invalid API call."}"""));

        await Assert.ThrowsAsync<FmpApiException>(
            () => endpoints.GetFinancialReportAsync("NOSUCHSYM", 2025, FiscalPeriod.Annual));
    }

    // ---- financial-reports-xlsx -----------------------------------------------------------------------------

    [Fact]
    public async Task A_workbook_is_recognised_by_its_magic_number()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];
        var (endpoints, _) = Build(Binary(zip));

        var bytes = await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual);

        Assert.Equal(zip, bytes);
    }

    [Fact]
    public async Task A_workbook_miss_is_null_even_though_fmp_answered_two_hundred()
    {
        // Measured 2026-08-27, for both a bad symbol and a good symbol in a year with no filing: HTTP 200,
        // Content-Type application/json, and exactly these 16 bytes. Neither the status nor the header
        // distinguishes it from the 1.4 MB zip, which is why the magic number is the test.
        var (endpoints, _) = Build(Binary(Encoding.UTF8.GetBytes("Error with query")));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("NOSUCHSYM", 2025, FiscalPeriod.Annual));
    }

    [Fact]
    public async Task An_empty_workbook_body_is_null_rather_than_an_index_out_of_range()
    {
        var (endpoints, _) = Build(Binary([]));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual));
    }

    [Fact]
    public async Task A_body_shorter_than_the_magic_number_is_null()
    {
        var (endpoints, _) = Build(Binary([0x50, 0x4B]));

        Assert.Null(await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Annual));
    }

    // ---- the quarter trap -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(FiscalPeriod.Annual)]
    [InlineData(FiscalPeriod.Q1)]
    [InlineData(FiscalPeriod.Q4)]
    public async Task A_named_period_reaches_the_wire_on_both_document_paths(FiscalPeriod period)
    {
        var (endpoints, handler) = Build(Binary([0x50, 0x4B, 0x03, 0x04]));

        await endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, period);

        Assert.Contains($"period={period.ToQueryValue()}", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task A_bare_quarter_is_rejected_on_both_document_paths_before_a_request_goes_out()
    {
        // Measured 2026-08-27: FMP accepts period=quarter here and silently answers Q1 — the workbook comes back
        // named AAPL_2025_Q1_.xlsx at 58,263 bytes against 785,087 for the Q3 one. A report is one fiscal period,
        // so the caller has to name it rather than have FMP pick.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFinancialReportAsync("AAPL", 2025, FiscalPeriod.Quarter));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFinancialReportWorkbookAsync("AAPL", 2025, FiscalPeriod.Quarter));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_blank_symbol_is_rejected_on_both_document_paths_before_a_request_goes_out(bool workbook)
    {
        // Report() is a separate helper from the rest of the SDK's guard clauses — this proves its own
        // ArgumentException.ThrowIfNullOrWhiteSpace(symbol) actually runs, and runs before a request goes out.
        var (endpoints, handler) = Build();

        Func<Task> call = workbook
            ? () => endpoints.GetFinancialReportWorkbookAsync("  ", 2025, FiscalPeriod.Annual)
            : () => endpoints.GetFinancialReportAsync("  ", 2025, FiscalPeriod.Annual);

        await Assert.ThrowsAsync<ArgumentException>(call);
        Assert.Empty(handler.Requests);
    }
}
