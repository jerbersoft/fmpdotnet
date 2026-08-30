using System.Text.Json;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The nine ETF and mutual-fund paths, checked against captures taken live 2026-08-30.</summary>
public class EtfAndFundsTests
{
    [Fact]
    public void A_country_weighting_binds_both_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-country-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfCountryWeighting)!;

        Assert.Equal(9, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("United States", rows[0].Country);
        Assert.Equal(97.52m, rows[0].WeightPercentage);
    }

    [Theory]
    [InlineData("97.52%", "97.52")]
    [InlineData("0.1%", "0.1")]
    [InlineData("0.02%", "0.02")]
    [InlineData("0%", "0")]
    [InlineData("100%", "100")]
    [InlineData("0.01%", "0.01")]
    public void A_country_weight_parses_the_percent_suffix(string wire, string expected)
    {
        // Measured 2026-08-30: 227 of 227 rows on this path sent the weight as a STRING with a trailing `%`,
        // with a varying number of decimals. TolerantDecimalJsonConverter cannot read it —
        // decimal.TryParse("97.52%", NumberStyles.Float, ...) is false — so reaching for the existing converter
        // here would silently null every row on the path. This test fails if that swap is ever made.
        var row = JsonSerializer.Deserialize(
            $$"""[{"country":"X","weightPercentage":"{{wire}}"}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            row.WeightPercentage);
    }

    [Fact]
    public void A_country_weight_is_null_when_it_cannot_be_parsed_and_the_country_survives()
    {
        // The file's standing convention: one bad value costs one field, never the whole response.
        var row = JsonSerializer.Deserialize(
            """[{"country":"Narnia","weightPercentage":"about a third%"}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Null(row.WeightPercentage);
        Assert.Equal("Narnia", row.Country);
    }

    [Fact]
    public void A_country_weight_sent_as_a_bare_number_still_binds()
    {
        // No measured row did this, so it is not a claim about the wire — it is the converter refusing to lose
        // a value it can plainly read if FMP ever normalises the field to match its sibling path.
        var row = JsonSerializer.Deserialize(
            """[{"country":"X","weightPercentage":1.18}]""",
            FmpJsonContext.Default.ListEtfCountryWeighting)![0];

        Assert.Equal(1.18m, row.WeightPercentage);
    }

    [Fact]
    public void A_sector_weighting_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(12, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("Basic Materials", rows[0].Sector);
        Assert.Equal(1.62m, rows[0].WeightPercentage);
    }

    [Fact]
    public void The_sector_weight_is_a_bare_number_and_takes_no_percent_converter()
    {
        // The trap this pins: `weightPercentage` is a NUMBER on stable/etf/sector-weightings and a
        // "97.52%" STRING on stable/etf/country-weightings, measured 2026-08-30. The two records therefore
        // carry different converters on identically-named properties. Giving this one the percent converter
        // would still pass — it reads bare numbers — but giving the country one no converter nulls 227 rows.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"SPY","sector":"Technology","weightPercentage":37.4}]""",
            FmpJsonContext.Default.ListEtfSectorWeighting)![0];

        Assert.Equal(37.4m, row.WeightPercentage);
    }

    [Fact]
    public void The_sectors_are_alphabetical_and_not_ordered_by_weight()
    {
        // Measured 2026-08-30, and it is the surprise in the group: `etf/country-weightings` sorts by weight
        // descending while its sibling `etf/sector-weightings` sorts alphabetically. Nothing re-sorts these
        // client-side, so the <returns> doc reports the measured order and this test holds the report honest.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(
            rows.Select(r => r.Sector).OrderBy(s => s, StringComparer.Ordinal),
            rows.Select(r => r.Sector));
        Assert.NotEqual(
            rows.Select(r => r.WeightPercentage).OrderByDescending(w => w),
            rows.Select(r => r.WeightPercentage));
    }

    [Fact]
    public void The_thirty_place_sector_weight_rounds_and_does_not_throw()
    {
        // 1.4210854715202004e-14 is SPY's `Cash & Others` weight — 2^-46, the residue of a floating-point
        // subtraction. It needs 30 decimal places and decimal has 28. Checked on .NET 10 rather than assumed:
        // System.Text.Json ROUNDS it and does not throw. Recorded here so that nobody later "fixes" this by
        // switching the slice to double, which would round every large figure in the group far more
        // damagingly — `etf/asset-exposure.marketValue` reaches 7,434,183,997,921.512 with 17 significant
        // digits, which double cannot hold.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        var cash = rows.Single(r => r.Sector == "Cash & Others");

        Assert.Equal(0.0000000000000142108547152020m, cash.WeightPercentage);
    }
}
