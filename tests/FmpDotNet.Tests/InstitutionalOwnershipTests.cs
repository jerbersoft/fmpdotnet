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

    // ---- institutional-ownership/extract ------------------------------------------------------------------------

    [Fact]
    public void A_captured_holding_binds_twelve_of_its_fourteen_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.Equal(3, rows.Count);
        // The two absences are both measured, and neither is a defect: `symbol` is null on 30.1% of rows and
        // `putCallShare` was blank on all 7,346 rows of this path.
        Assert.Equal(["PutCallShare", "Symbol"], Binding.Unbound(rows[0]));
        Assert.Equal("0000093751", rows[0].Cik);
        Assert.Equal("10170A100", rows[0].SecurityCusip);
        Assert.Equal("BOUNDLESS BIO INC", rows[0].NameOfIssuer);
        Assert.Equal("COM", rows[0].TitleOfClass);
        Assert.Equal("SH", rows[0].SharesType);
        Assert.Equal(15962m, rows[0].Shares);
        Assert.Equal(39905m, rows[0].Value);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].AcceptedDate);
    }

    [Fact]
    public void A_holding_without_a_ticker_keeps_every_other_field()
    {
        // The trap. Measured 2026-08-28, `symbol` was null on 2,209 of 7,346 rows — 30.1%. Bonds, warrants and
        // private placements are 13F-reportable and have no ticker. A consumer keying holdings by symbol drops
        // three rows in ten and is told nothing, so the property is `string?` and this pins that the rest of the
        // row still arrives.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.Null(rows[0].Symbol);
        Assert.Null(rows[1].Symbol);
        Assert.Equal("SAM", rows[2].Symbol);
        Assert.Equal("BOSTON BEER INC", rows[2].NameOfIssuer);
        Assert.Equal(314732m, rows[2].Shares);
    }

    [Fact]
    public void A_blank_put_call_share_stays_blank_rather_than_becoming_null()
    {
        // Modelled although it was `""` on all 7,346 rows of this path and never once populated. The same field
        // on extract-analytics/holder IS populated ("Share"), so omitting it here would leave a consumer no way
        // to reach it if FMP starts sending it. This asserts the measured emptiness rather than assuming it.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract.head.json"),
            FmpJsonContext.Default.ListInstitutionalHolding)!;

        Assert.All(rows, r => Assert.Equal("", r.PutCallShare));
    }

    [Fact]
    public async Task The_holdings_call_sends_cik_year_and_quarter_and_no_limit()
    {
        // The guard for the ignored parameter. Measured 2026-08-28, `extract` returns all 4,177 rows for
        // `limit=5` — byte-identical to no limit at all. A `limit` parameter here would be accepted, ignored,
        // and invisible in the response, which is worse than not offering one. This test fails the moment
        // somebody adds it back.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHoldingsAsync("0001067983", 2025, 3);

        Assert.Equal("/stable/institutional-ownership/extract", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("page=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_refused(int quarter)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHoldingsAsync("0001067983", 2025, quarter));

        Assert.Equal("quarter", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(2099)]
    public async Task A_year_far_outside_the_filed_range_is_sent_rather_than_refused(int year)
    {
        // Deliberate. Measured 2026-08-28, an out-of-range year answers `[]` with HTTP 200 — a legitimate
        // "no data", not an error. Guessing a floor would invent a fact the measurements do not have, and would
        // break the day FMP backfills. The endpoint is the authority on which years exist; GetFilingDatesAsync
        // is how a caller asks it.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var rows = await endpoints.GetHoldingsAsync("0001067983", year, 3);

        Assert.Empty(rows);
        Assert.Contains($"year={year}", handler.Requests[0].Query);
    }

    // ---- institutional-ownership/extract-analytics/holder --------------------------------------------------------

    [Fact]
    public void A_captured_holder_analytics_row_binds_all_thirty_nine_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(2, rows.Count);
        // Nothing unbound: this is the widest record in the slice and the one most likely to lose a field to a
        // typo'd [JsonPropertyName], which binds null rather than failing.
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BLACKROCK, INC.", rows[0].InvestorName);
        Assert.Equal("0002012383", rows[0].Cik);
        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("APPLE INC", rows[0].SecurityName);
        Assert.Equal("COM", rows[0].TypeOfSecurity);
        Assert.Equal("Share", rows[0].PutCallShare);
        Assert.Equal("SOLE", rows[0].InvestmentDiscretion);
        Assert.Equal("ELECTRONIC COMPUTERS", rows[0].IndustryTitle);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 8, 7), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2024, 9, 30), rows[0].FirstAdded);
        Assert.False(rows[0].IsNew);
        Assert.False(rows[0].IsSoldOut);
        Assert.True(rows[0].IsCountedForPerformance);
        Assert.Equal(8, rows[0].HoldingPeriod);
    }

    [Fact]
    public void A_market_value_past_two_billion_binds_rather_than_throwing()
    {
        // The overflow guard. int.MaxValue is 2,147,483,647; BlackRock's AAPL position is 336,524,794,350 —
        // 157 times that. Typing MarketValue as int? makes System.Text.Json throw, and FmpTransport does not
        // wrap DeserializeAsync, so the caller loses the whole response rather than the field. Retyping it as
        // int? fails this test; retyping it as long? does not, which is why the fractional-value guard in
        // Task 6 exists as well.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(336524794350m, rows[0].MarketValue);
        Assert.Equal(290512251859m, rows[0].LastMarketValue);
        Assert.Equal(40716816267m, rows[0].Performance);
        Assert.Equal(-20864809759m, rows[0].LastPerformance);
        // 1,162,996,939 — 54% of int's ceiling and rising. `sharesNumber` is the field that gets retyped by
        // somebody who checks one row and concludes it fits.
        Assert.Equal(1162996939m, rows[0].SharesNumber);
    }

    [Fact]
    public void A_zero_performance_is_a_measured_value_and_not_a_missing_one()
    {
        // Vanguard's row carries lastPerformance: 0 because it first held AAPL in the previous quarter. Zero is
        // FMP's answer, not an absence — Binding.Unbound counts zero as populated for exactly this reason
        // (see its doc), and a caller must not read it as "not reported".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-extract-analytics.AAPL.json"),
            FmpJsonContext.Default.ListHolderAnalytics)!;

        Assert.Equal(0m, rows[1].LastPerformance);
        Assert.Empty(Binding.Unbound(rows[1]));
        Assert.Equal(2, rows[1].HoldingPeriod);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[1].FirstAdded);
    }

    [Fact]
    public async Task The_holder_analytics_call_sends_page_and_limit()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, page: 2, limit: 50);

        Assert.Equal(
            "/stable/institutional-ownership/extract-analytics/holder", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_refused_on_holder_analytics(int quarter)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderAnalyticsAsync("AAPL", 2025, quarter));

        Assert.Equal("quarter", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    [InlineData(2000)]
    public async Task A_holder_analytics_limit_above_one_hundred_is_refused(int limit)
    {
        // Measured 2026-08-28: limit=200, 1000, 1001 and 2000 each answered exactly 100 rows with HTTP 200 and
        // byte-identical bodies. The path DOES paginate, so a caller who asked for 1,000 and stepped `page` by
        // 1,000 would read a tenth of the holder list and be told nothing at all. This is the one path in the
        // slice whose cap is 100 rather than 1,000.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, limit: limit));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_holder_analytics_limit_exactly_at_the_cap_is_accepted()
    {
        // The boundary the last slice's review had to add three times, for the same reason each time:
        // ThrowIfGreaterThan and ThrowIfGreaterThanOrEqual differ by one value, the whole suite stays green
        // when they are swapped, and the documented maximum starts throwing.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderAnalyticsAsync(
            "AAPL", 2025, 3, limit: InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);

        Assert.Contains("limit=100", handler.Requests[0].Query);
    }

    [Fact]
    public void The_holder_analytics_page_cap_is_the_measured_one()
    {
        Assert.Equal(100, InstitutionalOwnershipEndpoints.MaxHolderAnalyticsPageSize);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task A_negative_page_or_a_non_positive_limit_is_refused_on_holder_analytics(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderAnalyticsAsync("AAPL", 2025, 3, page: page, limit: limit));

        Assert.Empty(handler.Requests);
    }
}
