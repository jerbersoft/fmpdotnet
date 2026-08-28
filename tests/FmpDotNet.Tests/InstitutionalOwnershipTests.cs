using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The 13F records and the facade that serves them, checked against captures taken live 2026-08-28.
///
/// <para><b>The type choices here are deliberately made against the local evidence, and that is what these tests
/// pin.</b> Every money and share field is <c>decimal?</c> although all 7,946 rows sampled from <c>extract</c>
/// and <c>extract-analytics/holder</c> carried integral values — because <c>industryValue</c> on the sibling
/// <c>industry-summary</c> path is fractional on 53 of 394 rows, and because binding a fractional value to an
/// integer property makes <c>System.Text.Json</c> throw, costing the caller the whole response rather than the
/// one field.</para></summary>
public class InstitutionalOwnershipTests
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

    // ---- institutional-ownership/dates --------------------------------------------------------------------------

    [Fact]
    public void A_captured_filing_quarter_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-dates.BRK.json"),
            FmpJsonContext.Default.ListFilingQuarter)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(2, rows[0].Quarter);
    }

    [Fact]
    public void A_filing_quarters_date_is_the_quarter_end_not_the_filing_date()
    {
        // Measured 2026-08-28 over Berkshire's 53 quarters: every `date` is a calendar quarter end, and the
        // year/quarter pair always agrees with it. That is what makes this endpoint the index for the other
        // four — a caller reads `Year` and `Quarter` off a row here and passes them straight back.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-dates.BRK.json"),
            FmpJsonContext.Default.ListFilingQuarter)!;

        Assert.All(rows, r =>
        {
            var quarterEnd = new LocalDate(r.Year!.Value, r.Quarter!.Value * 3, 1)
                .With(DateAdjusters.EndOfMonth);
            Assert.Equal(quarterEnd, r.Date);
        });
    }

    [Fact]
    public async Task The_filing_dates_call_sends_only_the_cik()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetFilingDatesAsync("0001067983");

        Assert.Equal("/stable/institutional-ownership/dates", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        // No limit and no page: measured 2026-08-28, this path ignores both.
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_blank_cik_is_refused_before_a_request_goes_out()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetFilingDatesAsync("   "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_cik_is_refused_with_the_other_exception_type()
    {
        // Two facts, two [Fact]s: ArgumentException.ThrowIfNullOrWhiteSpace(null) throws ArgumentNullException,
        // and Assert.ThrowsAsync<T> matches the type exactly rather than by assignability. Folding this into the
        // test above would pass for the blank case and silently stop checking the null one.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetFilingDatesAsync(null!));

        Assert.Empty(handler.Requests);
    }
}
