namespace FmpDotNet.SmokeTests;

/// <summary>Checks that the sweep can still reach every endpoint — without a key, and without a request.
///
/// <para><b>These are the only tests in this project that are not gated on <c>FMP_API_KEY</c>, and that is
/// deliberate.</b> The live suite runs on a schedule; a defect introduced on a Tuesday would otherwise sit
/// unnoticed until the next scheduled run, and would surface then as an exception inside a sweep rather than as
/// a compile-time-shaped complaint about the thing that actually changed. Both checks below are pure reflection
/// over the SDK's own types, so they run on every push, cost nothing, and fail on the commit that broke
/// them.</para>
///
/// <para>What they protect against is specific: the sweep discovers endpoints by reflection and synthesises
/// arguments by parameter name, so an endpoint added with a parameter named or typed in a way
/// <see cref="Probe.Argument"/> has never seen is an endpoint the live suite would silently never call. A smoke
/// suite that quietly stops covering something is worse than one that fails.</para></summary>
public class SweepCoverageTests
{
    [Fact]
    public void The_sweep_can_supply_arguments_for_every_endpoint_method()
    {
        var unreachable = new List<string>();

        foreach (var group in Probe.Groups())
        foreach (var method in Probe.EndpointMethods(group.PropertyType))
        foreach (var parameter in method.GetParameters())
        {
            try
            {
                Probe.Argument(parameter);
            }
            catch (NotSupportedException ex)
            {
                unreachable.Add(ex.Message);
            }
        }

        Assert.True(unreachable.Count == 0,
            "The live smoke sweep cannot call these endpoints, so they would go unprobed:\n  "
            + string.Join("\n  ", unreachable));
    }

    [Fact]
    public void The_sweep_can_read_rows_out_of_every_endpoint_return_type()
    {
        var unreadable = new List<string>();

        foreach (var group in Probe.Groups())
        foreach (var method in Probe.EndpointMethods(group.PropertyType))
        {
            try
            {
                Probe.ElementType(method.ReturnType);
            }
            catch (NotSupportedException ex)
            {
                unreadable.Add($"{group.Name}.{method.Name}: {ex.Message}");
            }
        }

        Assert.True(unreadable.Count == 0,
            "The live smoke sweep cannot destructure what these endpoints return:\n  "
            + string.Join("\n  ", unreadable));
    }

    [Fact]
    public void Both_tiers_are_populated()
    {
        // Not a tautology: the partition is decided by which transport a group is constructed with, so a
        // refactor that gave BulkEndpoints an FmpTransport would move all twenty bulk endpoints into the
        // scheduled, key-only run — calling FMP's most restricted surface automatically, every week, which is
        // exactly what the second opt-in switch exists to prevent.
        var groups = Probe.Groups().ToList();
        Assert.Contains(groups, g => Probe.IsBulk(g.PropertyType));
        Assert.Contains(groups, g => !Probe.IsBulk(g.PropertyType));
    }

    [Fact]
    public void The_sweep_asks_the_ma_search_for_a_company_name_rather_than_a_ticker()
    {
        // Probe.Argument maps any unrecognised string parameter to LiveApi.Symbol, which would send
        // name=AAPL to mergers-acquisitions-search. That endpoint matches company NAMES: name=Apple answered
        // 3 rows on 2026-08-27 and a ticker matches nothing. The probe would record `rows 0` as the baseline
        // and agree with itself every week thereafter, reporting a healthy endpoint that has never returned
        // anything — the same silent-green failure LiveApi.Exchange and LiveApi.Cik exist to prevent.
        var name = typeof(Endpoints.CompanyEndpoints)
            .GetMethod(nameof(Endpoints.CompanyEndpoints.SearchMergersAcquisitionsAsync))!
            .GetParameters()[0];

        Assert.Equal("Apple", Probe.Argument(name));
    }

    [Fact]
    public void The_sweep_asks_the_filing_searches_for_a_range_wider_than_one_day()
    {
        // Probe.Argument dispatches LocalDate on TYPE alone, so `from` and `to` both became SettledWeekday and
        // the three sec-filings-search paths were probed over a single day. Measured 2026-08-28:
        // sec-filings-search/symbol?symbol=AAPL over 2026-08-21..2026-08-21 answered 0 rows, while the same call
        // over 2026-05-30..2026-08-28 answered 7. A zero-row answer records `outcome empty` with no properties,
        // and every run after it agrees — the endpoint would be probed weekly and never checked.
        var search = typeof(Endpoints.SecFilingsEndpoints)
            .GetMethod(nameof(Endpoints.SecFilingsEndpoints.SearchBySymbolAsync))!;
        var from = (NodaTime.LocalDate)Probe.Argument(search.GetParameters()[1]);
        var to = (NodaTime.LocalDate)Probe.Argument(search.GetParameters()[2]);

        Assert.True(NodaTime.Period.DaysBetween(from, to) >= 60,
            $"The sweep would probe the filing searches over {NodaTime.Period.DaysBetween(from, to)} day(s). "
            + "A short window answers zero rows and records an empty baseline that agrees with itself forever.");
    }

    [Fact]
    public void The_sweep_asks_each_new_search_for_a_value_of_its_own_kind()
    {
        // The string arm of Probe.Argument ends in `_ => LiveApi.Symbol`, so an unrecognised parameter name is
        // NOT an error — it silently becomes "AAPL". company=AAPL, formType=AAPL and sicCode=AAPL each answer
        // HTTP 200 with an empty array rather than an error, so the other coverage test in this file cannot see
        // the problem: the argument IS synthesisable, it is just meaningless. Same failure LiveApi.Exchange and
        // LiveApi.AcquirerNameQuery were written for.
        var filings = typeof(Endpoints.SecFilingsEndpoints);

        Assert.Equal("Apple", Probe.Argument(
            filings.GetMethod(nameof(Endpoints.SecFilingsEndpoints.FindCompanyByNameAsync))!.GetParameters()[0]));
        Assert.Equal("10-K", Probe.Argument(
            filings.GetMethod(nameof(Endpoints.SecFilingsEndpoints.SearchByFormTypeAsync))!.GetParameters()[0]));
        Assert.Equal("3571", Probe.Argument(
            typeof(Endpoints.SearchEndpoints)
                .GetMethod(nameof(Endpoints.SearchEndpoints.FindIndustryClassificationAsync))!
                .GetParameters()[2]));
    }
}
