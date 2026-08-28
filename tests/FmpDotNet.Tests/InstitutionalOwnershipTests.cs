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

    // ---- institutional-ownership/holder-industry-breakdown -------------------------------------------------------

    [Fact]
    public void A_captured_industry_breakdown_row_binds_all_twelve_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-industry-breakdown.BRK.json"),
            FmpJsonContext.Default.ListHolderIndustryBreakdown)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0001067983", rows[0].Cik);
        Assert.Equal("BERKSHIRE HATHAWAY INC", rows[0].InvestorName);
        Assert.Equal("ELECTRONIC COMPUTERS", rows[0].IndustryTitle);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(22.0383m, rows[0].Weight);
        Assert.Equal(8107036430m, rows[0].Performance);
    }

    [Fact]
    public void An_industry_performance_percentage_can_contradict_its_own_dollar_figure()
    {
        // Not a capture error, and not something to normalise. All three measured rows carry a positive
        // `performance` beside a negative `performancePercentage` — 8,107,036,430 against −296.8456. FMP's
        // percentage is computed against a base this endpoint does not publish, and the two figures are not
        // reconcilable from the response. The SDK reports both as sent; a consumer that assumes they agree in
        // sign is wrong on every row measured, which is what this test records.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-industry-breakdown.BRK.json"),
            FmpJsonContext.Default.ListHolderIndustryBreakdown)!;

        Assert.All(rows, r =>
        {
            Assert.True(r.Performance > 0);
            Assert.True(r.PerformancePercentage < 0);
        });
        Assert.Equal(-296.8456m, rows[0].PerformancePercentage);
        Assert.Equal(-4118474790m, rows[0].LastPerformance);
    }

    [Fact]
    public async Task The_industry_breakdown_call_sends_cik_year_and_quarter()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderIndustryBreakdownAsync("0001067983", 2025, 3);

        Assert.Equal(
            "/stable/institutional-ownership/holder-industry-breakdown", handler.Requests[0].AbsolutePath);
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
    public async Task A_quarter_outside_one_to_four_is_refused_on_industry_breakdown(int quarter)
    {
        // Task 3's review flagged this exact omission: the shared guard being exercised on two other methods
        // does not cover a third that might later stop calling it. Mirrors
        // A_quarter_outside_one_to_four_is_refused and
        // A_quarter_outside_one_to_four_is_refused_on_holder_analytics.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHolderIndustryBreakdownAsync("0001067983", 2025, quarter));

        Assert.Equal("quarter", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    // ---- institutional-ownership/holder-performance-summary ------------------------------------------------------

    [Fact]
    public void A_captured_holder_performance_row_binds_all_thirty_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-performance-summary.BRK.json"),
            FmpJsonContext.Default.ListHolderPerformance)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BERKSHIRE HATHAWAY INC", rows[0].InvestorName);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(29, rows[0].PortfolioSize);
        Assert.Equal(1, rows[0].SecuritiesAdded);
        Assert.Equal(1, rows[0].SecuritiesRemoved);
        Assert.Equal(20, rows[0].AverageHoldingPeriod);
        Assert.Equal(29, rows[0].AverageHoldingPeriodTop10);
        Assert.Equal(25, rows[0].AverageHoldingPeriodTop20);
        Assert.Equal(299253556246m, rows[0].MarketValue);
        Assert.Equal(288653953205m, rows[0].PerformanceSinceInception);
        Assert.Equal(-151.8108m, rows[0].PerformanceSinceInceptionRelativeToSP500Percentage);
    }

    [Fact]
    public void The_performance_summary_answers_every_quarter_not_just_the_latest()
    {
        // Measured 2026-08-28: 53 rows for Berkshire, newest first, one per quarter reported — the same 53
        // quarters GetFilingDatesAsync enumerates. That is why this method takes no year and no quarter: it is
        // the filer's whole history, and asking for one quarter of it is not something the endpoint offers.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-holder-performance-summary.BRK.json"),
            FmpJsonContext.Default.ListHolderPerformance)!;

        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[1].Date);
        // The quarter that flips sign, which is why these two rows were chosen.
        Assert.Equal(21069772689m, rows[0].Performance);
        Assert.Equal(-2243708176m, rows[1].Performance);
        // And each row's LastPerformance is the next row's Performance — the series is self-consistent.
        Assert.Equal(rows[1].Performance, rows[0].LastPerformance);
    }

    [Fact]
    public async Task The_performance_summary_call_sends_only_the_cik()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHolderPerformanceAsync("0001067983");

        Assert.Equal(
            "/stable/institutional-ownership/holder-performance-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("cik=0001067983", handler.Requests[0].Query);
        Assert.DoesNotContain("year=", handler.Requests[0].Query);
        Assert.DoesNotContain("quarter=", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }

    // ---- institutional-ownership/symbol-positions-summary --------------------------------------------------------

    [Fact]
    public async Task The_symbol_positions_summary_is_unwrapped_from_its_one_element_array()
    {
        // The wire shape is an array; the answer is one row of whole-market aggregates. GetProfileAsync set
        // this precedent — GetListAsync, then rows[0] — rather than GetObjectAsync, because the response really
        // is a list and pretending otherwise would fail to deserialise.
        var (endpoints, _) = Build(StubHandler.Json(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json")));

        var row = await endpoints.GetSymbolPositionsAsync("AAPL", 2026, 2);

        Assert.NotNull(row);
        Assert.Equal("AAPL", row.Symbol);
        Assert.Equal("0000320193", row.Cik);
        Assert.Equal(new LocalDate(2026, 6, 30), row.Date);
        Assert.Empty(Binding.Unbound(row));
    }

    [Fact]
    public async Task An_unknown_symbol_answers_null_rather_than_throwing()
    {
        // Measured 2026-08-28: an unrecognised symbol answers `[]` with HTTP 200, not a 404. Null is this SDK's
        // spelling of that, matching GetProfileAsync.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Null(await endpoints.GetSymbolPositionsAsync("NOSUCHTICKER", 2026, 2));
    }

    [Fact]
    public void An_ownership_percentage_over_one_hundred_is_kept_exactly_as_sent()
    {
        // Not a defect and not something to clamp. A 13F double-counts shares held through multiple reporting
        // managers, so summing filers legitimately passes shares outstanding. Measured 2026-08-28, this was
        // over 100 on two of six symbols: AAPL 110.1329 and MSFT 128.2744. A clamp, a range check or a
        // percentage wrapper type would each turn a real measurement into a lie.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(110.1329m, rows[0].OwnershipPercent);
        Assert.Equal(63.9264m, rows[0].LastOwnershipPercent);
        Assert.Equal(46.2065m, rows[0].OwnershipPercentChange);
    }

    [Fact]
    public void A_total_invested_past_two_trillion_binds_rather_than_throwing()
    {
        // 2,840,158,192,185 — 1,322 times int.MaxValue, and past long's ceiling is not the risk here; the risk
        // is somebody typing it int? because "positions" sounds like a count. numberOf13Fshares is the sharper
        // case at 16,201,347,267: seven times int's ceiling on a field whose name says "shares".
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(2840158192185m, rows[0].TotalInvested);
        Assert.Equal(16201347267m, rows[0].NumberOf13FShares);
        Assert.Equal(463018157203m, rows[0].TotalInvestedChange);
    }

    [Fact]
    public void The_position_counts_are_ints_and_the_negative_changes_survive_it()
    {
        // These six really are counts of filers, so they stay int? rather than being swept into decimal? for
        // safety — the largest measured is 6,435 and none was ever fractional. The changes go negative, which
        // is what this pins: closedPositionsChange is −18 and reducedPositionsChange is −165 on the captured
        // row, so an unsigned type would be wrong here even though the counts themselves never are.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("institutional-ownership-symbol-positions-summary.AAPL.json"),
            FmpJsonContext.Default.ListSymbolPositions)!;

        Assert.Equal(6435, rows[0].InvestorsHolding);
        Assert.Equal(43, rows[0].InvestorsHoldingChange);
        Assert.Equal(-18, rows[0].ClosedPositionsChange);
        Assert.Equal(-165, rows[0].ReducedPositionsChange);
    }

    [Fact]
    public async Task The_symbol_positions_call_sends_symbol_year_and_quarter()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetSymbolPositionsAsync("AAPL", 2025, 3);

        Assert.Equal(
            "/stable/institutional-ownership/symbol-positions-summary", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[0].Query);
        Assert.Contains("quarter=3", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_refused_on_symbol_positions(int quarter)
    {
        // Task 3's review flagged this exact omission on a sibling method: the shared guard being exercised
        // elsewhere does not cover a third caller that might later stop calling it. Mirrors
        // A_quarter_outside_one_to_four_is_refused, ..._on_holder_analytics and ..._on_industry_breakdown.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetSymbolPositionsAsync("AAPL", 2025, quarter));

        Assert.Equal("quarter", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }
}
