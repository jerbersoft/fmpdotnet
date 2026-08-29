using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>Form 3, 4 and 5 insider transactions, checked against captures taken live 2026-08-28.
///
/// <para><b>Share counts here are fractional, and that is what forced <c>decimal?</c> across the whole
/// slice.</b> Measured over 1,000 rows of <c>insider-trading/latest</c>: <c>securitiesOwned</c> was fractional
/// on 59 (5.9%) and <c>securitiesTransacted</c> on 40 (4.0%). Phantom stock, deferred units and dividend
/// reinvestment all produce fractions. Typing either as <c>long?</c> makes <c>System.Text.Json</c> throw, and
/// <c>FmpTransport</c> does not wrap the deserialiser — so one such row costs the caller all 1,000.</para>
///
/// <para><b>Blank and null are both wire values here and mean different things.</b> <c>transactionType</c> is
/// <c>""</c> on 8 rows of 100 — Form 3 initial statements have no transaction — while
/// <c>directOrIndirect</c> is explicitly <c>null</c> on 3. Neither is normalised to the other.</para></summary>
public class InsiderTradesTests
{
    private static (InsiderTradesEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new InsiderTradesEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    // ---- the record --------------------------------------------------------------------------------------------

    [Fact]
    public void A_fractional_share_count_binds_rather_than_throwing()
    {
        // THE test for the insider half of the decimal? ruling, and the one whose values are real rather than
        // constructed: IBM's Arvind Krishna holds 28,447.467 phantom shares and transacted 8,375.5601 of them.
        // Retype either property as long? or int? and System.Text.Json throws, costing the caller every row in
        // the response rather than the one field.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(28447.467m, rows[0].SecuritiesOwned);
        Assert.Equal(8375.5601m, rows[0].SecuritiesTransacted);
        Assert.Equal("Phantom Stock", rows[0].SecurityName);
    }

    [Fact]
    public void A_blank_transaction_type_stays_blank_and_a_null_direct_flag_stays_null()
    {
        // Two different absences on two different fields, both measured, neither normalised. transactionType
        // was "" on 8 of 100 rows — a Form 3 initial statement reports a holding, not a transaction — while
        // directOrIndirect was explicitly null on 3. Mapping "" to null would erase the distinction between
        // "FMP sent an empty value" and "FMP sent nothing", and an enum over transactionType would have no
        // member for the blank at all.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal("", rows[1].TransactionType);
        Assert.Equal("", rows[1].AcquisitionOrDisposition);
        Assert.Equal("I", rows[1].DirectOrIndirect);

        Assert.Equal("", rows[2].TransactionType);
        Assert.Null(rows[2].DirectOrIndirect);
        Assert.Equal("", rows[2].SecurityName);
        // And the rest of the row still arrives.
        Assert.Equal("TREX", rows[2].Symbol);
        Assert.Equal("Taylor Brian J.", rows[2].ReportingName);
        Assert.Equal("3", rows[2].FormType);
    }

    [Fact]
    public void A_captured_trade_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("IBM", rows[0].Symbol);
        Assert.Equal("0001629898", rows[0].ReportingCik);
        Assert.Equal("0000051143", rows[0].CompanyCik);
        Assert.Equal("I-Discretionary", rows[0].TransactionType);
        Assert.Equal("KRISHNA ARVIND", rows[0].ReportingName);
        Assert.Equal("director, officer: Chairman, President & CEO", rows[0].TypeOfOwner);
        Assert.Equal("A", rows[0].AcquisitionOrDisposition);
        Assert.Equal("D", rows[0].DirectOrIndirect);
        Assert.Equal("4", rows[0].FormType);
        Assert.Equal(0m, rows[0].Price);
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].TransactionDate);
    }

    [Fact]
    public void The_transaction_date_is_not_the_filing_date()
    {
        // Two distinct dates and the gap is real: two days on the IBM row and 59 on the POLA row. A consumer
        // that reads either as the other misdates the transaction, and neither is derivable from the other.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.All(rows, r => Assert.Equal(new LocalDate(2026, 8, 28), r.FilingDate));
        Assert.Equal(new LocalDate(2026, 8, 27), rows[0].TransactionDate);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[1].TransactionDate);
        Assert.Equal(new LocalDate(2026, 8, 24), rows[2].TransactionDate);
    }

    // ---- insider-trading/latest --------------------------------------------------------------------------------

    [Fact]
    public async Task The_latest_call_sends_page_and_limit_and_no_filters()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetLatestAsync(page: 2, limit: 50);

        Assert.Equal("/stable/insider-trading/latest", handler.Requests[0].AbsolutePath);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("limit=50", handler.Requests[0].Query);
        Assert.DoesNotContain("symbol=", handler.Requests[0].Query);
        Assert.DoesNotContain("transactionType=", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(2000)]
    [InlineData(5000)]
    public async Task An_insider_limit_above_the_measured_cap_is_refused_on_both_paths(int limit)
    {
        // Measured 2026-08-28: insider-trading/latest at limit=2000 and limit=5000 each answered exactly 1,000
        // rows with HTTP 200 and byte-identical bodies; insider-trading/search at limit=2000 answered 1,000 as
        // well. Both feeds paginate, so a caller stepping `page` by 5,000 reads a fifth of the archive and is
        // never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        var first = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(limit: limit));
        var second = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchAsync(symbol: "AAPL", limit: limit));

        Assert.Equal("limit", first.ParamName);
        Assert.Equal("limit", second.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_insider_limit_exactly_at_the_cap_is_accepted_on_both_paths()
    {
        // The off-by-one boundary, on the shared guard — so one swapped comparison would break both feeds and
        // this is the only test that would say so.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetLatestAsync(limit: InsiderTradesEndpoints.MaxInsiderTradePageSize);
        await endpoints.SearchAsync(symbol: "AAPL", limit: InsiderTradesEndpoints.MaxInsiderTradePageSize);

        Assert.Contains("limit=1000", handler.Requests[0].Query);
        Assert.Contains("limit=1000", handler.Requests[1].Query);
    }

    [Fact]
    public void The_insider_page_cap_is_the_measured_one()
    {
        Assert.Equal(1000, InsiderTradesEndpoints.MaxInsiderTradePageSize);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    public async Task A_negative_page_or_a_non_positive_limit_is_refused_on_both_insider_paths(int page, int limit)
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetLatestAsync(page: page, limit: limit));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.SearchAsync(symbol: "AAPL", page: page, limit: limit));

        Assert.Empty(handler.Requests);
    }

    // ---- insider-trading/search --------------------------------------------------------------------------------

    [Fact]
    public async Task Every_search_discriminator_that_is_supplied_reaches_the_query()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync(
            symbol: "AAPL", reportingCik: "1780525", companyCik: "320193", transactionType: "S-Sale",
            page: 0, limit: 5);

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/insider-trading/search", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", query);
        Assert.Contains("reportingCik=1780525", query);
        Assert.Contains("companyCik=320193", query);
        Assert.Contains("transactionType=S-Sale", query);
    }

    [Fact]
    public async Task A_search_with_no_criteria_is_a_valid_call()
    {
        // Deliberate. With nothing supplied the endpoint degenerates to the same feed GetLatestAsync answers,
        // which is a legitimate thing to ask for and not a caller error. FmpRequest.With drops the null values
        // passed here, so nothing reaches FMP but page and limit.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync();

        var query = handler.Requests[0].Query;
        Assert.Equal("/stable/insider-trading/search", handler.Requests[0].AbsolutePath);
        Assert.DoesNotContain("symbol=", query);
        Assert.DoesNotContain("reportingCik=", query);
        Assert.DoesNotContain("companyCik=", query);
        Assert.DoesNotContain("transactionType=", query);
        Assert.Contains("page=0", query);
    }

    [Fact]
    public async Task A_blank_discriminator_is_treated_as_absent_rather_than_refused()
    {
        // The four are optional, so blank means "not filtering on this" rather than "the caller made a
        // mistake". FmpRequest.With does not drop a whitespace-only string on its own — it checks
        // IsNullOrEmpty, not IsNullOrWhiteSpace — so it is SearchAsync's private NullIfBlank helper that turns
        // "   " into null before it ever reaches FmpRequest; this pins that the method does not throw on it,
        // which would make `SearchAsync(symbol: userInput)` unusable against an empty form field.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchAsync(symbol: "AAPL", transactionType: "   ");

        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.DoesNotContain("transactionType=", handler.Requests[0].Query);
    }

    [Fact]
    public void The_four_search_discriminators_narrow_together_rather_than_widen()
    {
        // Measured 2026-08-28, and worth recording because a first reading of the row counts suggests
        // otherwise. `reportingCik=1780525` alone answers a default page of 100 rows whose head is all AAPL —
        // which looks as though adding `symbol=AAPL` should change nothing, yet it drops the count to 10.
        //
        // Asking for the whole set explains it: `reportingCik=1780525&limit=1000` answers 553 rows across five
        // symbols (META 518, FB 20, AAPL 10, RJET 3, EMR 2) — the reporting person moved employers. Exactly 10
        // are AAPL, and `symbol=AAPL&reportingCik=1780525` answers exactly those 10. The filters intersect
        // correctly; the 100-row default page was the misleading part.
        //
        // This fixture is the four-way intersection: every row satisfies all four discriminators.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-search.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("AAPL", r.Symbol);
            Assert.Equal("0001780525", r.ReportingCik);
            Assert.Equal("0000320193", r.CompanyCik);
            Assert.Equal("S-Sale", r.TransactionType);
        });
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
    }

    [Fact]
    public void The_search_and_latest_paths_return_the_same_sixteen_fields()
    {
        // Verified rather than assumed: the two paths send the same keys in the same order, which is why one
        // record serves both. If they diverge, this fails on the record that stopped binding.
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;
        var search = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-search.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        Assert.Empty(Binding.Unbound(latest[0]));
        Assert.Empty(Binding.Unbound(search[0]));
    }

    // ---- insider-trading/statistics -----------------------------------------------------------------------------

    [Fact]
    public void The_statistics_ratios_and_averages_are_usually_fractional()
    {
        // The third place in this slice where a long? would throw, and the only one where fractional is the
        // normal case rather than the exception. Measured 2026-08-28 over AAPL's 94 quarters:
        // acquiredDisposedRatio fractional on 87, averageDisposed on 85, averageAcquired on 76. The totals and
        // the transaction counts were fractional on 0 — which is why those stay int?.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(0.175m, rows[1].AcquiredDisposedRatio);
        Assert.Equal(43314.1429m, rows[1].AverageAcquired);
        Assert.Equal(23184.5m, rows[1].AverageDisposed);
        Assert.Equal(1.5m, rows[2].AcquiredDisposedRatio);
        Assert.Equal(5113.0667m, rows[2].AverageAcquired);
    }

    [Fact]
    public void A_quarter_with_no_acquisitions_reports_zeroes_rather_than_nulls()
    {
        // Row 1 is 2026 Q3: no acquisitions, three disposals. Every acquired figure is 0 and the ratio is 0 —
        // all of them FMP's answers, not absences. Binding.Unbound counts zero as populated for this reason,
        // so the whole-record check still holds.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(0, rows[0].AcquiredTransactions);
        Assert.Equal(3, rows[0].DisposedTransactions);
        Assert.Equal(0m, rows[0].TotalAcquired);
        Assert.Equal(4317m, rows[0].TotalDisposed);
        Assert.Equal(0m, rows[0].AcquiredDisposedRatio);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(3, rows[0].Quarter);
    }

    [Fact]
    public void Total_sales_counts_filings_and_total_disposed_counts_shares()
    {
        // Two fields whose names read alike and whose units are not. On 2026 Q2: totalSales is 14 and
        // totalDisposed is 927,380. One counts transactions, the other counts shares — which is why the first
        // is int? and the second decimal?, and why the doc comments say which is which.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-statistics.AAPL.json"),
            FmpJsonContext.Default.ListInsiderTradeStatistics)!;

        Assert.Equal(14, rows[1].TotalSales);
        Assert.Equal(927380m, rows[1].TotalDisposed);
        Assert.Equal(40, rows[1].DisposedTransactions);
    }

    [Fact]
    public async Task The_statistics_call_sends_only_the_symbol()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetStatisticsAsync("AAPL");

        Assert.Equal("/stable/insider-trading/statistics", handler.Requests[0].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[0].Query);
        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.DoesNotContain("year=", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_blank_statistics_symbol_is_refused()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.GetStatisticsAsync(" "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_null_statistics_symbol_is_refused_with_ArgumentNullException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetStatisticsAsync(null!));

        Assert.Empty(handler.Requests);
    }

    // ---- insider-trading/reporting-name -------------------------------------------------------------------------

    [Fact]
    public void A_reporting_name_row_is_a_cik_and_a_name_and_nothing_else()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-reporting-name.head.json"),
            FmpJsonContext.Default.ListInsiderReportingName)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
        Assert.Equal("0001706288", rows[0].ReportingCik);
        Assert.Equal("Cook Adam T", rows[0].ReportingName);
    }

    [Fact]
    public void The_name_lookup_matches_a_prefix_of_a_surname_first_name()
    {
        // Measured 2026-08-28 on two queries: name=Cook answered 133 rows, every one beginning "Cook";
        // name=Apple answered 20, including "Applebach Richard Jr" and "Applebaum Michelle Galanter". So it is
        // a prefix match against a name EDGAR spells surname-first, not a substring match and not a match on a
        // company. Searching for a given name finds nothing.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-reporting-name.head.json"),
            FmpJsonContext.Default.ListInsiderReportingName)!;

        Assert.All(rows, r => Assert.StartsWith("Cook", r.ReportingName));
    }

    [Fact]
    public async Task The_reporting_name_call_sends_the_name_as_typed()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.SearchReportingNameAsync("Cook");

        Assert.Equal("/stable/insider-trading/reporting-name", handler.Requests[0].AbsolutePath);
        Assert.Contains("name=Cook", handler.Requests[0].Query);
    }

    [Fact]
    public async Task A_null_name_is_refused_with_ArgumentNullException()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.SearchReportingNameAsync(null!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_name_is_refused()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.SearchReportingNameAsync(" "));

        Assert.Empty(handler.Requests);
    }

    // ---- insider-trading-transaction-type -----------------------------------------------------------------------

    [Fact]
    public void The_transaction_type_list_is_the_eighteen_codes_search_accepts()
    {
        // The whole response, not a head: the list IS the answer. These eighteen are what
        // SearchAsync(transactionType:) draws from, and they are served by an endpoint rather than fixed in the
        // SDK — which is exactly why InsiderTrade.TransactionType is a string and not an enum. FMP can add a
        // nineteenth without an SDK release, and a closed enum would also have no member for the blank that
        // appears on 40 of 1,000 rows.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-transaction-type.json"),
            FmpJsonContext.Default.ListInsiderTransactionType)!;

        Assert.Equal(18, rows.Count);
        Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)));
        Assert.Equal("A-Award", rows[0].TransactionType);
        Assert.Equal("Z-Trust", rows[^1].TransactionType);
        Assert.Contains(rows, r => r.TransactionType == "S-Sale");
        Assert.Contains(rows, r => r.TransactionType == "P-Purchase");
    }

    [Fact]
    public void Every_measured_transaction_type_on_a_trade_row_is_in_the_list_or_blank()
    {
        // The two fixtures agree, which is the point of modelling the list at all. Measured over 1,000 rows of
        // insider-trading/latest, every transactionType was drawn from these eighteen or was the empty string.
        var codes = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-transaction-type.json"),
            FmpJsonContext.Default.ListInsiderTransactionType)!
            .Select(r => r.TransactionType ?? "").ToHashSet(StringComparer.Ordinal);
        var trades = JsonSerializer.Deserialize(
            Binding.Fixture("insider-trading-latest.head.json"),
            FmpJsonContext.Default.ListInsiderTrade)!;

        // `?? ""` on both sides rather than `!`: TransactionType is string?, and ToHashSet/Contains over a
        // nullable element type would warn under TreatWarningsAsErrors. The blank is a legal value here anyway,
        // which is what the assertion allows for.
        Assert.All(trades, t => Assert.True(
            t.TransactionType == "" || codes.Contains(t.TransactionType ?? ""),
            $"'{t.TransactionType}' is not one of the eighteen codes and is not blank."));
    }

    [Fact]
    public async Task The_transaction_type_call_sends_no_parameters_at_all()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetTransactionTypesAsync();

        Assert.Equal("/stable/insider-trading-transaction-type", handler.Requests[0].AbsolutePath);
        // The path is NOT under insider-trading/ — it is a sibling. Getting that wrong answers 404, which
        // FmpTransport surfaces as an exception rather than an empty list, so it would be loud; the assertion
        // above is here so it is loud at build time instead.
        Assert.DoesNotContain("insider-trading/transaction-type", handler.Requests[0].AbsolutePath);
    }
}
