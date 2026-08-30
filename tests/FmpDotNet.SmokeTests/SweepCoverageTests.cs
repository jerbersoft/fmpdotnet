namespace FmpDotNet.SmokeTests;

/// <summary>Checks that the sweep can still reach every endpoint — without a key, and without a request.
///
/// <para><b>Not one of these is gated on <c>FMP_API_KEY</c>, and that is deliberate.</b> The live suite runs on
/// a schedule; a defect introduced on a Tuesday would otherwise sit unnoticed until the next scheduled run, and
/// would surface then as an exception inside a sweep rather than as a compile-time-shaped complaint about the
/// thing that actually changed. (<see cref="BaselineRecordingTests"/> is keyless too, and for the same reason;
/// what is specific to this class is <i>what</i> it guards — that the sweep can still reach every endpoint and
/// still ask it something worth answering.) All fifteen checks below are pure
/// reflection over the SDK's own types and literal assertions about what <see cref="Probe"/> would do with them,
/// so they run on every push, cost nothing, and fail on the commit that broke them.</para>
///
/// <para>Two are general: can the sweep supply an argument for every parameter on every endpoint method, and can
/// it read rows out of every endpoint's return type. One confirms the ordinary/bulk partition itself is
/// non-empty. The remaining twelve pin the literal argument <see cref="Probe.Argument"/> would synthesise for
/// specific endpoints where synthesis succeeds but produces a value the endpoint cannot answer meaningfully
/// — a ticker where the endpoint wants a company name, a single day where a filing search needs a wide date
/// range, a bare symbol where a search wants a form type or a SIC code, a wide range where the earnings and
/// economic calendars need a narrow one, a single day where five calendars need a week, an issuer's CIK where
/// four 13F paths need an institutional filer's, a window computed from today where the indicator series
/// stopped in late 2025, a narrow calendar window inherited by the treasury path that wants a wide one, a
/// ticker and a recent range where the COT paths want a futures contract code and a range the frozen data
/// covers, a ticker where five of nine ETF and mutual-fund paths want a fund and a sixth wants a fund
/// company's name, and a ninety-day trailing window where the holiday calendar holds three days and a quiet
/// quarter none — so a probe that runs without error but never asks a meaningful question doesn't slip back
/// in unnoticed.</para>
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
    public void The_sweep_gives_the_filing_search_a_wide_from_and_the_earnings_calendar_a_narrow_one()
    {
        // Probe.Argument used to map every `from` parameter to LiveApi.RangeStart regardless of which endpoint
        // it belonged to. That is right for SecFilingsEndpoints — see the test above — and wrong for
        // CalendarEndpoints.GetEarningsCalendarAsync and EconomicsEndpoints.GetEconomicCalendarAsync: their own
        // doc comments measure a 91-day window as truncated (earnings calendar, hard cap at 4000) or
        // non-monotonic (economic calendar, a 6-month window returning fewer rows than the 3-month window it
        // contains). A regression back to one global `from` would widen those two again without any live test
        // noticing until the next scheduled run.
        var filingFrom = typeof(Endpoints.SecFilingsEndpoints)
            .GetMethod(nameof(Endpoints.SecFilingsEndpoints.SearchBySymbolAsync))!.GetParameters()[1];
        var earningsFrom = typeof(Endpoints.CalendarEndpoints)
            .GetMethod(nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync))!.GetParameters()[0];
        var economicFrom = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync))!.GetParameters()[0];

        Assert.Equal(LiveApi.RangeStart, Probe.Argument(filingFrom));
        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(earningsFrom));
        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(economicFrom));
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

    [Fact]
    public void The_sweep_gives_the_five_new_calendars_a_week_and_the_earnings_calendar_a_day()
    {
        // Probe.Argument dispatches `from` on the declaring type, and every date-ranged CalendarEndpoints method
        // used to get LiveApi.SettledWeekday for both ends -- a one-day window. Measured 2026-08-28 with
        // to=2026-08-21, one day answers: dividends 331, splits 12, ipos-calendar 5, ipos-disclosure 116, and
        // ipos-prospectus ONE. A single quiet week takes that last one to zero, which records `outcome empty`
        // as its baseline and then agrees with itself for ever.
        //
        // Seven days answers 1652 / 40 / 34 / 764 / 8 -- all comfortably non-zero, and the dividend calendar at
        // 41% of its 4000-row cap rather than the 81% a fortnight would give it.
        //
        // GetEarningsCalendarAsync is the deliberate exception and stays at one day: its own doc measures a
        // 7-day peak-season window at 3676 rows, 92% of the same cap. Narrowing it was the previous slice's
        // fix and widening it here would undo that.
        var calendar = typeof(Endpoints.CalendarEndpoints);
        var weekly = new[]
        {
            nameof(Endpoints.CalendarEndpoints.GetDividendsCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetSplitsCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoCalendarAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoDisclosuresAsync),
            nameof(Endpoints.CalendarEndpoints.GetIpoProspectusesAsync),
        };

        foreach (var name in weekly)
        {
            var method = calendar.GetMethod(name)!;
            var from = (NodaTime.LocalDate)Probe.Argument(method.GetParameters()[0]);
            var to = (NodaTime.LocalDate)Probe.Argument(method.GetParameters()[1]);

            Assert.Equal(6, NodaTime.Period.DaysBetween(from, to));
        }

        var earnings = calendar.GetMethod(nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync))!;
        Assert.Equal(
            LiveApi.SettledWeekday,
            (NodaTime.LocalDate)Probe.Argument(earnings.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_never_widens_a_window_that_was_narrowed_because_it_truncates()
    {
        // The regression guard for the two endpoints whose own documentation measures a wide window as unsafe.
        // A future change that collapsed the `from` arm back to one rule per declaring type would widen both of
        // these, and nothing else in the suite would notice until the next scheduled live run.
        var earnings = typeof(Endpoints.CalendarEndpoints)
            .GetMethod(nameof(Endpoints.CalendarEndpoints.GetEarningsCalendarAsync))!.GetParameters()[0];
        var economic = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync))!.GetParameters()[0];

        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(earnings));
        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(economic));
    }

    [Fact]
    public void The_sweep_asks_the_thirteen_f_paths_for_a_filer_cik_rather_than_an_issuer_cik()
    {
        // The synthesiser produces a well-formed CIK for every one of these, so the generic argument check
        // above passes either way — this is the check that the CIK means the right thing. Measured 2026-08-28,
        // Apple's issuer CIK (LiveApi.Cik) answers ZERO rows on all four of these paths with HTTP 200, so the
        // sweep would record `rows 0` as their baseline and match it every week after. Berkshire's filer CIK
        // answers 53, 41, 33 and 53.
        // Probe.EndpointMethods rather than raw BindingFlags, so this walks exactly the methods the sweep
        // walks — and so the file needs no `using System.Reflection`.
        var filerKeyed = Probe.EndpointMethods(typeof(Endpoints.InstitutionalOwnershipEndpoints))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.Name == "cik")
            .ToList();

        // Four of them, and if that number changes this test should be revisited rather than adjusted.
        Assert.Equal(4, filerKeyed.Count);
        Assert.All(filerKeyed, p => Assert.Equal(LiveApi.FilerCik, Probe.Argument(p)));
        Assert.NotEqual(LiveApi.Cik, LiveApi.FilerCik);

        // And the issuer meaning survives elsewhere: SecFilings still gets an issuer's CIK.
        var issuerKeyed = Probe.EndpointMethods(typeof(Endpoints.SecFilingsEndpoints))
            .SelectMany(m => m.GetParameters())
            .First(p => p.Name == "cik");
        Assert.Equal(LiveApi.Cik, Probe.Argument(issuerKeyed));
    }

    [Fact]
    public void The_sweep_asks_the_etf_and_fund_paths_for_a_fund_rather_than_for_apple()
    {
        // The synthesiser produces a well-formed symbol for every one of these, so the generic argument check
        // above passes either way — this is the check that the symbol means the right thing. Measured
        // 2026-08-30, LiveApi.Symbol (AAPL) answers ZERO rows on all four ETF-only paths AND on
        // funds/disclosure-dates: five of the nine endpoints would record `outcome empty` as their baseline
        // and match it every week after.
        //
        // QQQ was chosen by measurement rather than by taste: of the ETFs probed it is the smallest that
        // answers non-empty on all eight symbol paths — 30 / 8 / 107 / 1 / 11 / 28 / 87 rows across the seven
        // symbol-only paths, plus 101 rows for funds/disclosure at SettledYear/SettledQuarter (2025 Q3) —
        // for roughly 124 KB in total, against SPY's ~500 KB.
        var symbolKeyed = Probe.EndpointMethods(typeof(Endpoints.EtfAndFundsEndpoints))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.Name == "symbol")
            .ToList();

        // Eight of the nine methods take a symbol; SearchFundsByNameAsync takes a name. If that number
        // changes this test should be revisited rather than adjusted.
        Assert.Equal(8, symbolKeyed.Count);
        Assert.All(symbolKeyed, p => Assert.Equal(LiveApi.EtfSymbol, Probe.Argument(p)));
        Assert.NotEqual(LiveApi.Symbol, LiveApi.EtfSymbol);

        // And the ninth gets a fund-company word, not a ticker and not the M&A acquirer name. Measured
        // 2026-08-30, `name` on this path is a whole-word match against the REGISTRANT name: "Schwab"
        // answered 211 rows, while a prefix and a two-word phrase both answer zero.
        var nameKeyed = Probe.EndpointMethods(typeof(Endpoints.EtfAndFundsEndpoints))
            .SelectMany(m => m.GetParameters())
            .Single(p => p.Name == "name");

        Assert.Equal(LiveApi.FundNameQuery, Probe.Argument(nameKeyed));
        Assert.NotEqual(LiveApi.AcquirerNameQuery, LiveApi.FundNameQuery);

        // The AAPL default survives everywhere else: the quote path still gets a ticker.
        var quoteSymbol = Probe.EndpointMethods(typeof(Endpoints.QuoteEndpoints))
            .SelectMany(m => m.GetParameters())
            .First(p => p.Name == "symbol");
        Assert.Equal(LiveApi.Symbol, Probe.Argument(quoteSymbol));
    }

    [Fact]
    public void The_sweep_asks_the_indicator_path_for_a_window_the_data_actually_covers()
    {
        // Probe.Argument dispatched `from` on the DECLARING TYPE, so every EconomicsEndpoints method got
        // LiveApi.SettledWeekday — a one-day window that is right for the economic calendar and useless for
        // the two paths #40 added beside it. Worse than useless on this one: measured 2026-08-29, every
        // economic-indicators series stops between 2025-10-01 and 2025-11-26, so
        // name=GDP&from=2026-05-23&to=2026-08-21 — the window RangeStart and SettledWeekday produce — answers
        // a well-formed EMPTY ARRAY at HTTP 200. The probe would record `outcome empty` on the day it was
        // written and match that baseline green for ever.
        //
        // This is one of two FIXED date ranges in the sweep — the other is the COT probes' (see
        // LiveApi.CotRangeStart) — and the inversion is the point of both: everywhere else a hard-coded date
        // is a suite with an expiry, and here the DATA is what is frozen.
        var indicator = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetIndicatorAsync))!;

        Assert.Equal(LiveApi.IndicatorRangeStart, Probe.Argument(indicator.GetParameters()[1]));
        Assert.Equal(LiveApi.IndicatorRangeEnd, Probe.Argument(indicator.GetParameters()[2]));
        Assert.NotEqual(LiveApi.SettledWeekday, Probe.Argument(indicator.GetParameters()[1]));

        // And the indicator itself must be one that carries rows. EconomicIndicator.Inflation and
        // ThreeMonthCertificateOfDepositRate are valid names that answer an empty array, measured 2026-08-29.
        Assert.Equal(EconomicIndicator.Gdp, Probe.Argument(indicator.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_still_asks_the_economic_calendar_for_a_single_day()
    {
        // The narrowing in #40 must not have cost the calendar its own window. Its doc records a 6-month
        // range returning FEWER rows than the 3-month range inside it, and "the widest range verified intact
        // here is one week" — so a day, with no margin spent.
        //
        // GetTreasuryRatesAsync deliberately does NOT keep the day: it falls through to RangeStart, and 90
        // days answered 62 complete rows on 2026-08-29 where one day answers one.
        var calendar = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetEconomicCalendarAsync))!;
        var treasury = typeof(Endpoints.EconomicsEndpoints)
            .GetMethod(nameof(Endpoints.EconomicsEndpoints.GetTreasuryRatesAsync))!;

        Assert.Equal(LiveApi.SettledWeekday, Probe.Argument(calendar.GetParameters()[0]));
        Assert.Equal(LiveApi.RangeStart, Probe.Argument(treasury.GetParameters()[0]));
    }

    [Fact]
    public void The_sweep_asks_the_COT_paths_for_a_contract_code_and_a_range_the_data_covers()
    {
        // Two silent-green traps on one facade. The string arm of Probe.Argument ends in `_ => AAPL`, and the
        // COT paths take a futures contract code — measured 2026-08-29, symbol=AAPL answers `[]` at HTTP 200.
        // And the COT data stops at 2024-02-27, so any relative range answers `[]` too.
        //
        // One quarter and not more: 13 rows is commitment-of-traders-analysis's hard cap, and a wider window
        // records two sibling probes disagreeing for a reason that is not drift.
        var report = typeof(Endpoints.CotEndpoints)
            .GetMethod(nameof(Endpoints.CotEndpoints.GetReportAsync))!;

        Assert.Equal(LiveApi.CotContract, Probe.Argument(report.GetParameters()[0]));
        Assert.NotEqual(LiveApi.Symbol, Probe.Argument(report.GetParameters()[0]));
        Assert.Equal(LiveApi.CotRangeStart, Probe.Argument(report.GetParameters()[1]));
        Assert.Equal(LiveApi.CotRangeEnd, Probe.Argument(report.GetParameters()[2]));
        Assert.True(NodaTime.Period.DaysBetween(LiveApi.CotRangeStart, LiveApi.CotRangeEnd) <= 92,
            "A COT window wider than a quarter makes `analysis` and `report` disagree at `analysis`'s 13-row "
            + "cap, which reads as drift and is not.");
    }

    [Fact]
    public void The_sweep_asks_the_holiday_path_for_a_window_with_holidays_in_it()
    {
        // The generic LocalDate arm gives `from` LiveApi.RangeStart and `to` LiveApi.SettledWeekday — a
        // ninety-day trailing window. Measured 2026-08-30 against the 446-row NASDAQ corpus, that window
        // (2026-05-23 .. 2026-08-21) holds THREE holidays and a quiet quarter takes it to zero, which
        // records `outcome empty` as this endpoint's healthy baseline and matches itself green for ever.
        //
        // This is the THIRD fixed range in the sweep, after LiveApi.IndicatorRangeStart and
        // LiveApi.CotRangeStart, and it is fixed for its own reason: not that the data stops, but that the
        // holiday calendar is SPARSE — about 13 rows a year for NASDAQ — so a window has to be years wide
        // before it is safely non-empty. 2024-01-01 .. 2026-12-31 returned 38 rows on 2026-08-30.
        var holidays = typeof(Endpoints.MarketHoursEndpoints)
            .GetMethod(nameof(Endpoints.MarketHoursEndpoints.GetHolidaysAsync))!;

        Assert.Equal(LiveApi.Exchange, Probe.Argument(holidays.GetParameters()[0]));
        Assert.Equal(LiveApi.HolidayRangeStart, Probe.Argument(holidays.GetParameters()[1]));
        Assert.Equal(LiveApi.HolidayRangeEnd, Probe.Argument(holidays.GetParameters()[2]));
        Assert.NotEqual(LiveApi.RangeStart, Probe.Argument(holidays.GetParameters()[1]));

        // Wide enough to be safe, and the SDK's own documented boundary rule means a one-day range would
        // answer empty no matter what falls on that day.
        Assert.True(
            NodaTime.Period.Between(LiveApi.HolidayRangeStart, LiveApi.HolidayRangeEnd).Years >= 2,
            "The holiday calendar is sparse; a window narrower than two years is one quiet stretch away "
            + "from an empty baseline.");

        // And the single-exchange path keeps the existing arm — no new string constant was needed, because
        // NASDAQ answered 200 on both market-hours paths on 2026-08-30.
        var single = typeof(Endpoints.MarketHoursEndpoints)
            .GetMethod(nameof(Endpoints.MarketHoursEndpoints.GetExchangeAsync))!;

        Assert.Equal(LiveApi.Exchange, Probe.Argument(single.GetParameters()[0]));
    }
}
