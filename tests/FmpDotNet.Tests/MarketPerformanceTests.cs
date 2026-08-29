using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The eleven Market Performance paths, checked against captures taken live 2026-08-29.</summary>
public class MarketPerformanceTests
{
    [Fact]
    public void A_mover_binds_all_six_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-biggest-gainers.head.json"),
            FmpJsonContext.Default.ListMarketMover)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("FNGR", rows[0].Symbol);
        Assert.Equal("FingerMotion, Inc.", rows[0].Name);
        Assert.Equal(0.398m, rows[0].Price);
        Assert.Equal(0.2246m, rows[0].Change);
        Assert.Equal(129.5271m, rows[0].ChangePercentage);
        Assert.Equal("NASDAQ", rows[0].Exchange);
    }

    [Fact]
    public void The_movers_third_spelling_of_change_percentage_binds_to_the_house_name()
    {
        // FMP spells this fact three ways: `changePercentage` on quote, `changePercent` on end-of-day, and
        // `changesPercentage` — with the S — here. EndOfDayBar already documents its divergence and normalises
        // the C# name; this follows the same rule. Do NOT "fix" the attribute: the property would then bind
        // nothing, silently, and Binding.Unbound above is the only other thing that would notice.
        var row = JsonSerializer.Deserialize(
            """[{"changesPercentage":129.5271}]""", FmpJsonContext.Default.ListMarketMover)![0];

        Assert.Equal(129.5271m, row.ChangePercentage);
    }

    [Fact]
    public void A_mover_carries_no_date_of_its_own()
    {
        // Measured 2026-08-29: the movers shape is exactly six keys and none of them is a date or a timestamp.
        // The lists describe a session and never name it — cross-checked against `stable/quote?symbol=FNGR`,
        // which returned the identical price, change and percentage with `timestamp 1787947201`
        // (2026-08-28 20:00:01Z). This test fails if a future capture grows a date field, which would mean the
        // model can now answer a question its own doc says it cannot.
        using var wire = JsonDocument.Parse(
            Binding.Fixture("market-performance-biggest-gainers.head.json"));

        var keys = wire.RootElement[0].EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            ["symbol", "price", "name", "change", "changesPercentage", "exchange"], keys);
    }

    [Fact]
    public void A_sector_performance_row_binds_all_four_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-performance-snapshot.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(11, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 8, 28), rows[0].Date);
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal("NASDAQ", rows[0].Exchange);
        Assert.Equal(0.17296837188471859m, rows[0].AverageChange);
    }

    [Fact]
    public void An_industry_performance_row_binds_the_industry_key()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-industry-performance-snapshot.head.json"),
            FmpJsonContext.Default.ListIndustryPerformance)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Advertising Agencies", rows[0].Industry);
        // An ampersand survives the round trip; it is URL-encoded on the way out, not on the way back.
        Assert.Equal("Aerospace & Defense", rows[1].Industry);
        Assert.Equal(0.5507225355896539m, rows[0].AverageChange);
    }

    [Fact]
    public void A_sector_pe_row_binds_the_pe_key()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-pe-snapshot.head.json"),
            FmpJsonContext.Default.ListSectorPe)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal(25.792527521262276m, rows[0].Pe);
    }

    [Fact]
    public void A_pe_of_zero_stays_zero_and_is_not_turned_into_null()
    {
        // Measured 2026-08-29: 12 of 254 industry-PE rows read exactly 0, emitted as JSON `0` rather than
        // `0.0` — eight on NASDAQ and four on NYSE. Across 359 measured values `pe` was never negative and
        // never null, so zero is carrying "no meaningful aggregate PE" in band. Biotechnology on the NYSE is
        // not a zero-multiple industry. The SDK does not have the evidence to say which zeros are real, so it
        // reports what FMP sent; translating them would invent information.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-industry-pe-snapshot.head.json"),
            FmpJsonContext.Default.ListIndustryPe)!;

        Assert.Equal("Agricultural Inputs", rows[2].Industry);
        Assert.Equal(0m, rows[2].Pe);
        Assert.NotNull(rows[2].Pe);
    }

    [Fact]
    public void The_deep_history_number_formats_both_bind_to_the_same_decimal()
    {
        // Two things at once, and both are load-bearing for the decision to ship no custom converter here.
        //
        // 1. FMP writes values below 1e-6 in EXPONENT form. Measured 2026-08-29, exactly ten values in the
        //    corpus do so, all of them in a deep-history request and all below that threshold — every value at
        //    or above it, including the 22-digit one below, is written out in full.
        // 2. The metrics reach 22 fractional digits and 17 significant digits, which is why they are `decimal`.
        //    This test stops compiling if anyone retypes these properties as `double`.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-historical-sector-performance.head.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(0.0000005735079118365113m, rows[0].AverageChange);
        Assert.Equal(-0.0000026524148173594842m, rows[1].AverageChange);
        Assert.Equal(-1.171486877582397m, rows[2].AverageChange);
    }

    [Fact]
    public void A_snapshot_past_the_end_of_the_data_returns_rows_that_do_not_share_a_date()
    {
        // The trap this SDK documents rather than guards. Measured 2026-08-29, `date=2026-09-01` returned 11
        // rows bearing THREE dates — and it is not "each sector's latest row": asked for 2026-08-28 directly,
        // Industrials and Real Estate both return rows dated 2026-08-28. `date=2027-01-04` produced the same
        // split sector for sector, and sector-pe-snapshot produced it too.
        //
        // This test pins the DOCUMENTED behaviour: the SDK hands back all eleven rows unmodified, with their
        // dates intact, so a caller can compare. A future change to filter or clamp has to break this
        // deliberately.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("market-performance-sector-performance-ragged.json"),
            FmpJsonContext.Default.ListSectorPerformance)!;

        Assert.Equal(11, rows.Count);
        Assert.Equal(3, rows.Select(r => r.Date).Distinct().Count());
        Assert.Equal(new LocalDate(2026, 8, 25), rows.Single(r => r.Sector == "Industrials").Date);
        Assert.Equal(new LocalDate(2026, 8, 27), rows.Single(r => r.Sector == "Consumer Cyclical").Date);
        Assert.Equal(new LocalDate(2026, 8, 28), rows.Single(r => r.Sector == "Technology").Date);
    }

    private static (MarketPerformanceEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new MarketPerformanceEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Theory]
    [InlineData("gainers", "/stable/biggest-gainers")]
    [InlineData("losers", "/stable/biggest-losers")]
    [InlineData("actives", "/stable/most-actives")]
    public async Task Each_movers_list_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        _ = which switch
        {
            "gainers" => await endpoints.GetBiggestGainersAsync(),
            "losers" => await endpoints.GetBiggestLosersAsync(),
            _ => await endpoints.GetMostActivesAsync(),
        };

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task The_movers_send_nothing_but_the_key()
    {
        // Measured 2026-08-29: `limit=10`, `exchange=NYSE` and `page=1` each returned a response BYTE-IDENTICAL
        // to the bare request. The three lists are fixed at 50 rows and span every exchange at once. Offering
        // any of those parameters would let a caller believe a filter happened, so the methods take only a
        // cancellation token — and this test fails if one is ever added.
        var (endpoints, handler) = Build();

        await endpoints.GetBiggestGainersAsync();

        var query = handler.Requests[0].Query;
        Assert.DoesNotContain("limit=", query);
        Assert.DoesNotContain("exchange=", query);
        Assert.DoesNotContain("page=", query);
    }

    [Fact]
    public async Task A_movers_list_binds_through_the_facade()
    {
        var (endpoints, _) = Build(Binding.Fixture("market-performance-biggest-gainers.head.json"));

        var rows = await endpoints.GetBiggestGainersAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal("FNGR", rows[0].Symbol);
        Assert.Equal(129.5271m, rows[0].ChangePercentage);
    }

    [Fact]
    public async Task The_sector_performance_snapshot_sends_the_date_the_exchange_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), "NASDAQ");

        Assert.Equal("/stable/sector-performance-snapshot", handler.Requests[0].AbsolutePath);
        var query = handler.Requests[0].Query;
        Assert.Contains("date=2026-08-28", query);
        Assert.Contains("exchange=NASDAQ", query);
        // The optional filter is omitted entirely when null rather than sent empty — an empty `sector=`
        // is not a request that was ever measured.
        Assert.DoesNotContain("sector=", query);
    }

    [Fact]
    public async Task The_sector_filter_goes_out_as_FMPs_own_label()
    {
        // Measured 2026-08-29, `date=2026-08-28&sector=Technology` returned exactly one row — real server-side
        // filtering, which is why it is offered. The enum member is FinancialServices; the wire wants
        // "Financial Services", with the space.
        var (endpoints, handler) = Build();

        await endpoints.GetSectorPerformanceSnapshotAsync(
            new LocalDate(2026, 8, 28), "NASDAQ", Sector.FinancialServices);

        Assert.Contains("sector=Financial%20Services", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_industry_performance_snapshot_sends_the_date_the_exchange_and_nothing_else()
    {
        var (endpoints, handler) = Build();

        await endpoints.GetIndustryPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), "NASDAQ");

        Assert.Equal("/stable/industry-performance-snapshot", handler.Requests[0].AbsolutePath);
        var query = handler.Requests[0].Query;
        Assert.Contains("date=2026-08-28", query);
        Assert.Contains("exchange=NASDAQ", query);
        // The optional filter is omitted entirely when null rather than sent empty — an empty `industry=`
        // is not a request that was ever measured.
        Assert.DoesNotContain("industry=", query);
    }

    [Fact]
    public async Task The_industry_filter_url_encodes_an_ampersand()
    {
        // Measured 2026-08-29: `industry=Aerospace & Defense` returns rows when encoded. An unencoded
        // ampersand would split the query string and silently drop everything after it, including the key.
        var (endpoints, handler) = Build();

        await endpoints.GetIndustryPerformanceSnapshotAsync(
            new LocalDate(2026, 8, 28), "NASDAQ", "Aerospace & Defense");

        Assert.Contains("industry=Aerospace%20%26%20Defense", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData("sector-pe", "/stable/sector-pe-snapshot")]
    [InlineData("industry-performance", "/stable/industry-performance-snapshot")]
    [InlineData("industry-pe", "/stable/industry-pe-snapshot")]
    public async Task Each_remaining_snapshot_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();
        var date = new LocalDate(2026, 8, 28);

        switch (which)
        {
            case "sector-pe": await endpoints.GetSectorPeSnapshotAsync(date, "NASDAQ"); break;
            case "industry-performance":
                await endpoints.GetIndustryPerformanceSnapshotAsync(date, "NASDAQ"); break;
            default: await endpoints.GetIndustryPeSnapshotAsync(date, "NASDAQ"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_exchange_is_rejected_before_the_request_goes_out(string exchange)
    {
        // A blank exchange reaches FMP as an OMITTED one, which silently selects NASDAQ alone. Rejecting here
        // is the only place the two can be told apart.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), exchange));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_supplied_but_blank_industry_filter_is_rejected()
    {
        // Omitting `industry` is valid and means "every industry". Supplying "   " is a mistake, and unguarded
        // it would reach FMP meaning exactly the same thing — the caller would believe a filter happened.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetIndustryPerformanceSnapshotAsync(new LocalDate(2026, 8, 28), "NASDAQ", "   "));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_snapshot_returns_the_ragged_rows_through_the_facade_unmodified()
    {
        // The end-to-end half of the trap test in Task 3: the facade must not filter, clamp or reorder.
        var (endpoints, _) = Build(Binding.Fixture("market-performance-sector-performance-ragged.json"));

        var rows = await endpoints.GetSectorPerformanceSnapshotAsync(new LocalDate(2026, 9, 1), "NASDAQ");

        Assert.Equal(11, rows.Count);
        Assert.Equal(3, rows.Select(r => r.Date).Distinct().Count());
    }

    [Fact]
    public async Task The_historical_sector_path_always_sends_a_window()
    {
        // The point of requiring `from` and `to`: omitting them upstream returns 2024-02-01..2024-03-01,
        // measured 2026-08-29 — thirty months stale, at HTTP 200, with nothing in the body saying so.
        // `from` defaults to 2024-02-01 and `to` to 2024-03-01, both hard-coded, and `limit=100` does not move
        // them. Non-nullable parameters are how that default becomes unreachable.
        var (endpoints, handler) = Build();

        await endpoints.GetHistoricalSectorPerformanceAsync(
            Sector.Technology, "NASDAQ", new LocalDate(2026, 8, 1), new LocalDate(2026, 8, 28));

        Assert.Equal("/stable/historical-sector-performance", handler.Requests[0].AbsolutePath);
        var query = handler.Requests[0].Query;
        Assert.Contains("sector=Technology", query);
        Assert.Contains("exchange=NASDAQ", query);
        Assert.Contains("from=2026-08-01", query);
        Assert.Contains("to=2026-08-28", query);
    }

    [Theory]
    [InlineData("sector-pe", "/stable/historical-sector-pe")]
    [InlineData("industry-performance", "/stable/historical-industry-performance")]
    [InlineData("industry-pe", "/stable/historical-industry-pe")]
    public async Task Each_remaining_historical_path_is_asked_by_name(string which, string expected)
    {
        var (endpoints, handler) = Build();
        var from = new LocalDate(2026, 8, 1);
        var to = new LocalDate(2026, 8, 28);

        switch (which)
        {
            case "sector-pe":
                await endpoints.GetHistoricalSectorPeAsync(Sector.Technology, "NASDAQ", from, to); break;
            case "industry-performance":
                await endpoints.GetHistoricalIndustryPerformanceAsync("Steel", "NASDAQ", from, to); break;
            default:
                await endpoints.GetHistoricalIndustryPeAsync("Steel", "NASDAQ", from, to); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task A_backwards_range_is_rejected_before_the_request_goes_out()
    {
        // Measured 2026-08-29: `from=2026-08-28&to=2026-08-01` answers `[]` with HTTP 200 — a spent call that
        // says nothing happened. Rejecting here is the only place that reads as an error.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHistoricalSectorPerformanceAsync(
                Sector.Technology, "NASDAQ", new LocalDate(2026, 8, 28), new LocalDate(2026, 8, 1)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_exchange_is_rejected_on_the_historical_path()
    {
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.GetHistoricalSectorPerformanceAsync(
                Sector.Technology, "  ", new LocalDate(2026, 8, 1), new LocalDate(2026, 8, 28)));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("industry-pe")]
    [InlineData("industry-performance")]
    public async Task A_blank_industry_is_rejected_on_the_historical_path(string which)
    {
        var (endpoints, handler) = Build();
        var from = new LocalDate(2026, 8, 1);
        var to = new LocalDate(2026, 8, 28);

        Task Call() => which == "industry-pe"
            ? endpoints.GetHistoricalIndustryPeAsync("  ", "NASDAQ", from, to)
            : endpoints.GetHistoricalIndustryPerformanceAsync("  ", "NASDAQ", from, to);

        await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_historical_path_binds_the_deep_history_number_formats()
    {
        var (endpoints, _) = Build(
            Binding.Fixture("market-performance-historical-sector-performance.head.json"));

        var rows = await endpoints.GetHistoricalSectorPerformanceAsync(
            Sector.Technology, "NASDAQ", new LocalDate(2000, 1, 1), new LocalDate(2016, 1, 1));

        Assert.Equal(3, rows.Count);
        Assert.Equal(0.0000005735079118365113m, rows[0].AverageChange);
        Assert.Equal(-0.0000026524148173594842m, rows[1].AverageChange);
    }
}
