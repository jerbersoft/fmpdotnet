using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

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
        // damagingly — `etf/asset-exposure.marketValue` reaches 7,434,183,997,921.512, which binary64
        // cannot represent: the nearest double is 7,434,183,997,921.51171875.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        var cash = rows.Single(r => r.Sector == "Cash & Others");

        Assert.Equal(0.0000000000000142108547152020m, cash.WeightPercentage);
    }

    [Fact]
    public void A_holding_binds_all_nine_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.SPY.head.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("NVDA", rows[0].Asset);
        Assert.Equal("NVIDIA CORP", rows[0].Name);
        Assert.Equal("US67066G1040", rows[0].Isin);
        Assert.Equal("67066G104", rows[0].SecurityCusip);
        Assert.Equal(296861422m, rows[0].SharesNumber);
        Assert.Equal(8.29427804m, rows[0].WeightPercentage);
        Assert.Equal(67656626530m, rows[0].MarketValue);
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 13, 47, 36), rows[0].UpdatedAt);
    }

    [Fact]
    public void An_empty_asset_isin_or_cusip_becomes_null_and_the_rest_of_the_row_survives()
    {
        // Measured 2026-08-30 over 35,185 rows: `asset` was "" on 51.1%, `isin` on 51.0% and `securityCusip`
        // on 22.8%. That is not an anomaly to route around — it is what a bond fund looks like. BND's 17,252
        // holdings are mostly unlisted debt with no ticker. Without the converter a caller writing
        // `row.Asset ?? "unlisted"` gets "" on half the rows and no warning.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.BND.sentinels.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Null(rows[0].Asset);
        Assert.Null(rows[0].Isin);
        Assert.Equal("CMT001142", rows[0].SecurityCusip);   // one field absent, its neighbour present
        Assert.Null(rows[1].SecurityCusip);

        // Everything else on both rows still bound.
        Assert.Equal("MKTLIQ 12/31/2049", rows[0].Name);
        Assert.Equal(54112647.476m, rows[0].SharesNumber);
        Assert.Equal(5410723621.13m, rows[0].MarketValue);
        Assert.Equal("US Dollar", rows[1].Name);
        Assert.Equal(1093207268.48m, rows[1].SharesNumber);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"N/A\"")]
    [InlineData("\"NULL\"")]
    [InlineData("null")]
    public void Every_spelling_of_absence_reads_as_null(string wire)
    {
        // Four spellings, one meaning. `etf/holdings` was only measured sending "" — the other two string
        // forms were measured on `funds/disclosure` and `funds/disclosure-holders-search` — but this is one
        // converter and this is where its whole domain is pinned.
        // The interpolation hole is NOT last in the object on purpose: `{{wire}}}]` would put three closing
        // braces together, which is ambiguous inside a $$-interpolated raw string. Do not reorder these keys.
        var row = JsonSerializer.Deserialize(
            $$"""[{"asset":{{wire}},"symbol":"BND"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Null(row.Asset);
        Assert.Equal("BND", row.Symbol);
    }

    [Fact]
    public void A_real_value_survives_the_sentinel_converter()
    {
        var row = JsonSerializer.Deserialize(
            """[{"asset":"NVDA","isin":"US67066G1040","securityCusip":"67066G104"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("NVDA", row.Asset);
        Assert.Equal("US67066G1040", row.Isin);
        Assert.Equal("67066G104", row.SecurityCusip);
    }

    [Fact]
    public void A_number_sent_into_a_sentinel_field_binds_as_its_literal_text()
    {
        // No measured row did this. The branch exists because a JSON number read into a plain string property
        // THROWS under this SDK's context options, and the throw aborts the whole array — the failure measured
        // on NetWorthDebtDetails.Rate, where 23 numeric rows would have cost all 250. Two of the fields this
        // converter is applied to are numeric strings, so it is a shape FMP could plausibly unquote.
        var row = JsonSerializer.Deserialize(
            """[{"asset":30}]""", FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("30", row.Asset);
    }

    [Fact]
    public void The_holding_name_is_not_sentinel_converted()
    {
        // `name` was populated on all 35,185 rows measured 2026-08-30, so an empty name would be information,
        // not absence — and this SDK does not convert a field whose sentinel it has never seen. This test
        // fails if the converter is ever added to Name "for consistency".
        var row = JsonSerializer.Deserialize(
            """[{"name":""}]""", FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal("", row.Name);
    }

    [Fact]
    public void The_holdings_timestamp_reads_as_utc_and_not_as_eastern()
    {
        // THE falsification, reproduced. Measured 2026-08-30, `etf/holdings?symbol=SCHD` returned
        // `updatedAt 2026-08-30 06:51:13` in a response whose own Date header read
        // `Sun, 30 Aug 2026 10:05:35 GMT`. Read as Eastern, 06:51:13 EDT is 10:51:13Z — 46 minutes AFTER FMP
        // generated the response carrying it, and a cache stamp cannot postdate its own response. Read as UTC
        // it is 3h14m old, which is ordinary. Reproduced 18 seconds later against a fresh response.
        //
        // So this field takes NullableFmpInstantJsonConverter (UTC) while FundDisclosure.AcceptedDate takes
        // NullableEasternInstantJsonConverter, on the identical `uuuu-MM-dd HH:mm:ss` wire shape. Swapping
        // them costs four or five hours and nothing throws.
        var row = JsonSerializer.Deserialize(
            """[{"symbol":"SCHD","updatedAt":"2026-08-30 06:51:13"}]""",
            FmpJsonContext.Default.ListEtfHolding)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 30, 6, 51, 13), row.UpdatedAt);
        Assert.NotEqual(Instant.FromUtc(2026, 8, 30, 10, 51, 13), row.UpdatedAt);
    }

    [Fact]
    public void The_holdings_timestamp_is_one_value_for_the_whole_response()
    {
        // Measured 2026-08-30: 33 of 33 responses carried exactly ONE distinct `updatedAt` across every row.
        // It is a per-symbol cache stamp, not a per-holding as-of date, and staleness ranged from 3.2 hours
        // (ARKK) to 284 hours (IJH, IJR) on one sweep. The XML doc says so; this test holds the shape.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-holdings.SPY.head.json"),
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Single(rows.Select(r => r.UpdatedAt).Distinct());
    }

    [Fact]
    public void Negative_and_fractional_holdings_survive()
    {
        // Both rows carry measured extremes, not invented ones: across the 35,185 rows measured 2026-08-30
        // `sharesNumber` reached -2,920,694,176 and 0.0001383508577753182, `weightPercentage` -0.34898692 and
        // 100, and `marketValue` -560,343,250 and 155,526,370,000. An integer type is wrong for `sharesNumber`
        // twice over — it is signed AND fractional.
        var rows = JsonSerializer.Deserialize(
            """
            [{"sharesNumber":-2920694176,"weightPercentage":-0.34898692,"marketValue":-560343250},
             {"sharesNumber":0.0001383508577753182,"weightPercentage":100,"marketValue":155526370000}]
            """,
            FmpJsonContext.Default.ListEtfHolding)!;

        Assert.Equal(-2920694176m, rows[0].SharesNumber);
        Assert.Equal(-0.34898692m, rows[0].WeightPercentage);
        Assert.Equal(-560343250m, rows[0].MarketValue);
        Assert.Equal(0.0001383508577753182m, rows[1].SharesNumber);
        Assert.Equal(155526370000m, rows[1].MarketValue);
    }

    [Fact]
    public void An_asset_exposure_row_binds_all_five_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-asset-exposure.SPY.head.json"),
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("XCHG", rows[0].Symbol);
        Assert.Equal("SPY", rows[0].Asset);
        Assert.Equal(3189m, rows[0].SharesNumber);
        Assert.Equal(0.34179638m, rows[0].WeightPercentage);
        Assert.Equal(2459037.9m, rows[0].MarketValue);
    }

    [Fact]
    public void The_asset_is_the_constant_on_asset_exposure_and_the_symbol_is_not()
    {
        // This path runs the other way from the four other `etf/*` paths: given an asset it answers which
        // ETFs hold it. Measured 2026-08-30, `asset` was identical across every row of all 8 responses while
        // `symbol` named a different fund on each. A caller who reads `symbol` as "the fund I asked about"
        // is reading the wrong field, which is why both properties say so in their docs.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-asset-exposure.SPY.head.json"),
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Single(rows.Select(r => r.Asset).Distinct());
        Assert.Equal(3, rows.Select(r => r.Symbol).Distinct().Count());
    }

    [Fact]
    public void An_asset_exposure_weight_is_bounded_by_neither_zero_nor_one_hundred()
    {
        // Both rows are verbatim measured captures. NVD is an inverse NVDA product; HEMI's MSFT line reported
        // a 50,506% weight against a zero market value. Measured 2026-08-30, this field's range on
        // `etf/asset-exposure` was -199.9869 to 50,506 — so it cannot be range-checked, cannot be documented
        // as a 0-100 percentage, and cannot take an unsigned type. This test fails if a guard is ever added.
        var rows = JsonSerializer.Deserialize(
            """
            [{"symbol":"NVD","asset":"NVDA","sharesNumber":-457235,"weightPercentage":-199.9869,
              "marketValue":-103015045.5},
             {"symbol":"HEMI","asset":"MSFT","sharesNumber":0,"weightPercentage":50506,"marketValue":0}]
            """,
            FmpJsonContext.Default.ListEtfAssetExposure)!;

        Assert.Equal(-199.9869m, rows[0].WeightPercentage);
        Assert.Equal(-457235m, rows[0].SharesNumber);
        Assert.Equal(-103015045.5m, rows[0].MarketValue);
        Assert.Equal(50506m, rows[1].WeightPercentage);
    }

    [Fact]
    public void An_asset_exposure_market_value_binds_exactly_rather_than_to_the_nearest_double()
    {
        // 7,434,183,997,921.512 is the measured maximum on this field, 2026-08-30 — 16 significant digits,
        // and not exactly representable in binary64: the nearest double is 7,434,183,997,921.51171875. A
        // double-typed property would bind that instead and this assertion would fail. This is the other
        // half of the argument in The_thirty_place_sector_weight_rounds_and_does_not_throw.
        var row = JsonSerializer.Deserialize(
            """[{"marketValue":7434183997921.512}]""",
            FmpJsonContext.Default.ListEtfAssetExposure)![0];

        Assert.Equal(7434183997921.512m, row.MarketValue);
    }
}
