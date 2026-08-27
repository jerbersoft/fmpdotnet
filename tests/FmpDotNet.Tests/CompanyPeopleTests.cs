using Microsoft.Extensions.Options;
using FmpDotNet.Endpoints;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>Headcounts, executives and compensation — the Company group's people-shaped endpoints.</summary>
public class CompanyPeopleTests
{
    private static (CompanyEndpoints Endpoints, StubHandler Handler) Build(params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CompanyEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
                handler);
    }

    [Fact]
    public async Task Binds_every_field_of_an_employee_count_row()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("employee-count.AAPL.json")));

        var rows = await endpoints.GetEmployeeCountAsync("AAPL");

        Assert.Equal(3, rows.Count);
        var latest = rows[0];
        Assert.Equal("AAPL", latest.Symbol);
        Assert.Equal("0000320193", latest.Cik);
        Assert.Equal("Apple Inc.", latest.CompanyName);
        Assert.Equal("10-K", latest.FormType);
        Assert.Equal(new LocalDate(2025, 9, 27), latest.PeriodOfReport);
        Assert.Equal(new LocalDate(2025, 10, 31), latest.FilingDate);
        Assert.Equal(166000, latest.Employees);
        Assert.StartsWith("https://www.sec.gov/", latest.Source);
        Assert.Empty(Binding.Unbound(latest));
    }

    [Fact]
    public async Task Reads_the_acceptance_stamp_as_edgars_eastern_wall_clock()
    {
        // "2025-10-31 06:01:26" is space-separated with no offset and no `T`. EDGAR reports Eastern, so
        // 06:01:26 EDT is 10:01:26 UTC. Read as UTC — the other converter in this file — every stamp would be
        // four or five hours early and nothing would throw.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("employee-count.AAPL.json")));

        var latest = (await endpoints.GetEmployeeCountAsync("AAPL"))[0];

        Assert.Equal(
            Instant.FromUtc(2025, 10, 31, 10, 1, 26),
            latest.AcceptanceTime);
    }

    [Fact]
    public async Task The_two_employee_count_methods_call_two_different_paths()
    {
        // The responses are byte-identical — AAPL 32 rows, JPM 5, SHOP 11, XOM 0 on both, compared as sorted
        // JSON on 2026-08-27 — so nothing in a response can tell the two apart. Both are shipped because FMP
        // documents both paths and a caller looks up whichever name they found. This is the guard against one
        // being quietly rewired to the other's path, which no binding test could see.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetEmployeeCountAsync("AAPL");
        await endpoints.GetHistoricalEmployeeCountAsync("AAPL");

        Assert.Equal("/stable/employee-count", handler.Requests[0].AbsolutePath);
        Assert.Equal("/stable/historical-employee-count", handler.Requests[1].AbsolutePath);
    }

    [Fact]
    public async Task Both_employee_count_methods_send_limit_only_when_it_is_supplied()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetEmployeeCountAsync("AAPL");
        await endpoints.GetEmployeeCountAsync("AAPL", 3);
        await endpoints.GetHistoricalEmployeeCountAsync("AAPL");
        await endpoints.GetHistoricalEmployeeCountAsync("AAPL", 3);

        Assert.DoesNotContain("limit=", handler.Requests[0].Query);
        Assert.Contains("limit=3", handler.Requests[1].Query);
        Assert.DoesNotContain("limit=", handler.Requests[2].Query);
        Assert.Contains("limit=3", handler.Requests[3].Query);
    }

    [Fact]
    public async Task A_filer_with_no_employee_history_is_empty_not_an_error()
    {
        // XOM — a major filer — answered zero rows on both paths, 2026-08-27. Empty is normal here.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Empty(await endpoints.GetEmployeeCountAsync("XOM"));
    }

    [Fact]
    public async Task Binds_an_executive_and_leaves_the_nulls_null()
    {
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("key-executives.AAPL.json")));

        var executives = await endpoints.GetKeyExecutivesAsync("AAPL");

        Assert.Equal(4, executives.Count);
        var cfo = executives[0];
        Assert.Equal("Kevan Parekh", cfo.Name);
        Assert.Equal("Senior Vice President & Chief Financial Officer", cfo.Title);
        Assert.Equal(4034174m, cfo.Pay);
        Assert.Equal("USD", cfo.CurrencyPay);
        Assert.Equal("male", cfo.Gender);
        Assert.Equal(1972, cfo.YearBorn);
        Assert.True(cfo.Active);

        // Measured 2026-08-27 over 203 rows across 18 symbols: pay null on 32 of the first 64, yearBorn on 24,
        // gender on 9. Typing any of these non-nullable binds a zero or an empty string over a real absence.
        Assert.Null(executives[1].Pay);
        Assert.Null(executives[1].YearBorn);
        Assert.Null(executives[2].Gender);
    }

    [Fact]
    public async Task Executive_pay_is_not_always_in_dollars()
    {
        // TSM reports in TWD. Comparing Pay across rows without reading CurrencyPay compares 64,496,506 TWD
        // against 4,034,174 USD as though they were the same unit.
        var (endpoints, _) = Build(StubHandler.Json(Binding.Fixture("key-executives.TSM.json")));

        var executives = await endpoints.GetKeyExecutivesAsync("TSM");

        Assert.Equal("TWD", executives[0].CurrencyPay);
        Assert.Equal(64496506m, executives[0].Pay);
    }

    [Fact]
    public async Task Title_since_binds_as_a_string_so_a_future_value_of_any_shape_cannot_throw()
    {
        // Null on all 203 rows measured 2026-08-27, and null in the one documented example an independent
        // client typed from — so no populated value has ever been seen from either source and there is no
        // measured shape to infer a format from. Typed Instant?, LocalDate? or int?, the day FMP starts
        // populating it with anything else is the day this endpoint starts throwing. A string? cannot.
        var (endpoints, _) = Build(StubHandler.Json(
            """[{"name":"A Person","titleSince":"2019-04-01","active":true}]"""));

        var executive = Assert.Single(await endpoints.GetKeyExecutivesAsync("AAPL"));

        Assert.Equal("2019-04-01", executive.TitleSince);
    }

    [Fact]
    public async Task An_etf_has_no_executives_and_that_is_not_an_error()
    {
        // SPY answered [] on 2026-08-27.
        var (endpoints, _) = Build(StubHandler.Json("[]"));

        Assert.Empty(await endpoints.GetKeyExecutivesAsync("SPY"));
    }

    [Fact]
    public async Task Binds_every_field_of_an_executive_compensation_row()
    {
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("governance-executive-compensation.AAPL.json")));

        var rows = await endpoints.GetExecutiveCompensationAsync("AAPL");

        Assert.Equal(2, rows.Count);
        var maestri = rows[0];
        Assert.Equal("0000320193", maestri.Cik);
        Assert.Equal("AAPL", maestri.Symbol);
        Assert.Equal("Apple Inc.", maestri.CompanyName);
        Assert.Equal(new LocalDate(2026, 1, 8), maestri.FilingDate);
        Assert.Equal(Instant.FromUtc(2026, 1, 8, 21, 31, 36), maestri.AcceptedDate);
        Assert.Equal(
            "Luca Maestri Former Senior Vice President, Chief Financial Officer",
            maestri.NameAndPosition);
        Assert.Equal(2025, maestri.Year);
        Assert.Equal(819231m, maestri.Salary);
        Assert.Equal(13003031m, maestri.StockAward);
        Assert.Equal(1638462m, maestri.IncentivePlanCompensation);
        Assert.Equal(22204m, maestri.AllOtherCompensation);
        Assert.Equal(15482928m, maestri.Total);
        Assert.StartsWith("https://www.sec.gov/", maestri.Link);

        // bonus and optionAward are a real 0, not an absence — the model must not treat zero as missing.
        Assert.Equal(0m, maestri.Bonus);
        Assert.Equal(0m, maestri.OptionAward);
    }

    [Fact]
    public async Task Executive_compensation_sends_no_year_because_the_endpoint_ignores_it()
    {
        // symbol=AAPL and symbol=AAPL&year=2025 answered byte-identical bodies of 339 rows spanning 1999-2025
        // on 2026-08-27. This asserts the request, because a signature that accepted `year` would be a lie the
        // compiler cannot catch — and the URL is the only place that lie would show.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetExecutiveCompensationAsync("AAPL");

        var query = Assert.Single(handler.Requests).Query;
        Assert.Contains("symbol=AAPL", query);
        Assert.DoesNotContain("year=", query);
    }

    [Fact]
    public async Task Benchmark_average_compensation_is_fractional()
    {
        // 784407.5555555555 — an average, so integral is the exception rather than the rule. 339 of the 377
        // rows measured on 2026-08-27 were fractional.
        var (endpoints, _) = Build(
            StubHandler.Json(Binding.Fixture("executive-compensation-benchmark.noyear.json")));

        var rows = await endpoints.GetExecutiveCompensationBenchmarkAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal("ABRASIVE, ASBESTOS & MISC NONMETALLIC MINERAL PRODS", rows[0].IndustryTitle);
        Assert.Equal(2024, rows[0].Year);
        Assert.Equal(784407.5555555555m, rows[0].AverageCompensation);
    }

    [Fact]
    public async Task Benchmark_sends_year_only_when_it_is_supplied()
    {
        // Omitted, FMP answers LAST year rather than this one — 377 rows stamped 2024, measured 2026-08-27.
        // The SDK does not substitute a year of its own: doing so would answer a different question than the
        // caller asked, and FMP's own default is the documented behaviour.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetExecutiveCompensationBenchmarkAsync();
        await endpoints.GetExecutiveCompensationBenchmarkAsync(2025);

        Assert.DoesNotContain("year=", handler.Requests[0].Query);
        Assert.Contains("year=2025", handler.Requests[1].Query);
    }
}
