using System.Text.Json;
using FmpDotNet.Serialization;
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
}
