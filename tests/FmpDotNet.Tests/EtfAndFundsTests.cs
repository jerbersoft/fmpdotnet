using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
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
        // carry different converters on identically-named properties, and giving the country one no
        // converter nulls 227 rows.
        //
        // The write assertion is the guard. PercentSuffixedDecimalJsonConverter READS a bare number fine, so
        // a read-only assertion would pass whether or not it were attached here — it could not fail if
        // someone added it "for consistency". Its Write appends "%", so attaching it would silently change
        // what this SDK serialises on this path, and that is what the second assertion catches.
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"SPY","sector":"Technology","weightPercentage":37.4}]""",
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(37.4m, rows[0].WeightPercentage);
        Assert.Contains(
            "\"weightPercentage\":37.4",
            JsonSerializer.Serialize(rows, FmpJsonContext.Default.ListEtfSectorWeighting));
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
        // (SCHD) to 284 hours (IJH, IJR) on one sweep. The XML doc says so; this test holds the shape.
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

    [Fact]
    public void An_etf_info_row_binds_all_nineteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("etf-info.SPY.json"), FmpJsonContext.Default.ListEtfInfo)!;

        Assert.Single(rows);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("SPY", rows[0].Symbol);
        Assert.Equal("State Street SPDR S&P 500 ETF", rows[0].Name);
        Assert.StartsWith("SPY is the best-recognized", rows[0].Description);
        Assert.Equal("US78462F1030", rows[0].Isin);
        Assert.Equal("Equity", rows[0].AssetClass);
        Assert.Equal("78462F103", rows[0].SecurityCusip);
        Assert.Equal("US", rows[0].Domicile);
        Assert.StartsWith("https://www.ssga.com/", rows[0].Website);
        Assert.Equal("SPDR", rows[0].EtfCompany);
        Assert.Equal(0.09m, rows[0].ExpenseRatio);
        Assert.Equal(816147480000m, rows[0].AssetsUnderManagement);
        Assert.Equal(49440271m, rows[0].AvgVolume);
        Assert.Equal(new LocalDate(1993, 1, 22), rows[0].InceptionDate);
        Assert.Equal(771.27m, rows[0].Nav);
        Assert.Equal("USD", rows[0].NavCurrency);
        Assert.Equal(504, rows[0].HoldingsCount);
        Assert.True(rows[0].IsActivelyTrading);
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 23, 12, 50) + Duration.FromMilliseconds(6),
            rows[0].UpdatedAt);
        Assert.Equal(12, rows[0].SectorsList!.Count);
    }

    [Fact]
    public void The_info_timestamp_reads_the_iso_form_and_keeps_its_milliseconds()
    {
        // The SECOND `updatedAt` format in this group. `etf/holdings` sends `2026-08-30 06:51:13` — space
        // separated, no zone marker, and measured UTC by falsification. `etf/info` sends
        // `2026-08-29T23:12:50.006Z`, 33 of 33 rows measured 2026-08-30: ISO-8601 with milliseconds and an
        // explicit Z, so it needs no zone measurement — it is UTC because it says so.
        //
        // NullableFmpInstantJsonConverter cannot read this shape: its pattern expects a space separator and
        // no Z, so it would bind null on every row. This test fails if it is ever substituted here.
        var row = JsonSerializer.Deserialize(
            """[{"updatedAt":"2026-08-29T23:12:50.006Z"}]""", FmpJsonContext.Default.ListEtfInfo)![0];

        Assert.Equal(Instant.FromUtc(2026, 8, 29, 23, 12, 50) + Duration.FromMilliseconds(6), row.UpdatedAt);
    }

    [Fact]
    public void A_nested_sector_binds_industry_and_exposure_and_not_the_sibling_paths_key_names()
    {
        // The nested objects spell the same two facts with DIFFERENT keys from stable/etf/sector-weightings:
        // `industry` where the path says `sector`, `exposure` where it says `weightPercentage`. And the
        // `industry` key holds SECTOR names — "Basic Materials", "Cash & Others" — not industries.
        //
        // The property is Sector and the attribute is [JsonPropertyName("industry")], under the same rule that
        // binds `senateID` to SenateId. DO NOT "fix" the attribute: the property would then bind nothing,
        // silently, and this test is the only thing that would notice.
        var row = JsonSerializer.Deserialize(
            """[{"sectorsList":[{"industry":"Technology","exposure":37.4}]}]""",
            FmpJsonContext.Default.ListEtfInfo)![0];

        Assert.Equal("Technology", row.SectorsList![0].Sector);
        Assert.Equal(37.4m, row.SectorsList[0].Exposure);
    }

    [Fact]
    public void The_nested_sectors_are_the_sector_weightings_path_value_for_value()
    {
        // Measured 2026-08-30: all 13 ETFs cross-checked agreed on the key set AND on every value, with no
        // rounding difference. One of the nine paths is fully contained in another. That is why the SDK ships
        // two records for one fact and says so in both docs — a maintainer who finds the duplication should
        // find this test before deleting either one.
        var info = JsonSerializer.Deserialize(
            Binding.Fixture("etf-info.SPY.json"), FmpJsonContext.Default.ListEtfInfo)![0];
        var weightings = JsonSerializer.Deserialize(
            Binding.Fixture("etf-sector-weightings.SPY.json"),
            FmpJsonContext.Default.ListEtfSectorWeighting)!;

        Assert.Equal(
            weightings.Select(w => (w.Sector, w.WeightPercentage)),
            info.SectorsList!.Select(s => (s.Sector, s.Exposure)));
    }

    [Fact]
    public void The_holdings_count_binds_as_a_count_and_zero_is_a_value_not_an_absence()
    {
        // `holdingsCount` is NOT the number of holdings. Cross-checked on 33 ETFs against the row count
        // stable/etf/holdings returned for the same symbol on the same day, they agreed on ONE: BND reports
        // 346 and returns 17,252; ARKK reports 10 and returns 47; GLD and SLV report 0 and return 1. It
        // cannot pre-size a buffer, cannot page (there is none), and cannot decide whether calling the
        // holdings path is worthwhile.
        //
        // Zero is therefore a real measured value on this field, not a missing one, which is what this test
        // pins: it fails if the property is ever narrowed to a non-nullable int with 0 as its "absent".
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"GLD","holdingsCount":0},{"symbol":"BND","holdingsCount":346}]""",
            FmpJsonContext.Default.ListEtfInfo)!;

        Assert.Equal(0, rows[0].HoldingsCount);
        Assert.Equal(346, rows[1].HoldingsCount);
    }

    [Fact]
    public void Is_actively_trading_is_a_real_json_boolean_and_takes_no_converter()
    {
        // The only genuine JSON boolean in the whole slice — true on all 33 rows measured 2026-08-30. The
        // four `is*` fields on funds/disclosure are `Y`/`N` STRINGS and need YesNoBooleanJsonConverter; this
        // one does not.
        //
        // The write assertion is the guard. YesNoBooleanJsonConverter READS a real JSON boolean correctly —
        // its Read maps JsonTokenType.True and False straight through — so a read-only assertion would pass
        // whether or not it were attached, and could not fail if someone added it. Its Write emits "Y"/"N",
        // so attaching it would silently change what this SDK serialises, and that is what the second
        // assertion catches.
        var rows = JsonSerializer.Deserialize(
            """[{"isActivelyTrading":false}]""", FmpJsonContext.Default.ListEtfInfo)!;

        Assert.False(rows[0].IsActivelyTrading);
        Assert.Contains(
            "\"isActivelyTrading\":false",
            JsonSerializer.Serialize(rows, FmpJsonContext.Default.ListEtfInfo));
    }

    [Fact]
    public void A_fund_disclosure_row_binds_all_twenty_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure.SPY.2026q1.head.json"),
            FmpJsonContext.Default.ListFundDisclosure)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0000884394", rows[0].Cik);
        Assert.Equal(new LocalDate(2026, 3, 31), rows[0].Date);
        Assert.Equal("PM", rows[0].Symbol);
        Assert.Equal("Philip Morris International Inc", rows[0].Name);
        Assert.Equal("HL3H1H2BGXWVG3BSWR90", rows[0].Lei);
        Assert.Equal("Philip Morris International Inc", rows[0].Title);
        Assert.Equal("718172109", rows[0].Cusip);
        Assert.Equal("US7181721090", rows[0].Isin);
        Assert.Equal(18128850m, rows[0].Balance);
        Assert.Equal("NS", rows[0].Units);
        Assert.Equal("USD", rows[0].CurrencyCode);
        Assert.Equal(2997424059m, rows[0].ValueUsd);
        Assert.Equal(0.4602323652851295m, rows[0].PercentValue);
        Assert.Equal("Long", rows[0].PayoffProfile);
        Assert.Equal("EC", rows[0].AssetCategory);
        Assert.Equal("CORP", rows[0].IssuerCategory);
        Assert.Equal("US", rows[0].InvestmentCountry);
        Assert.False(rows[0].IsRestrictedSecurity);
        Assert.Equal("1", rows[0].FairValueLevel);
        Assert.False(rows[0].IsCashCollateral);
        Assert.False(rows[0].IsNonCashCollateral);
        Assert.False(rows[0].IsLoanByFund);
    }

    [Fact]
    public void The_accepted_date_reads_as_eastern_on_both_sides_of_the_dst_boundary()
    {
        // The zone was established by identity against a field this SDK already measured against EDGAR.
        // Twenty NPORT-P filings across two CIKs and ten quarters were looked up a second time through
        // stable/sec-filings-search/cik, whose acceptedDate was measured Eastern against EDGAR on 2026-08-26.
        // Twelve of nineteen matched TO THE SECOND (10 of 10 for the SPY trust); the largest residual across
        // all nineteen was 90 SECONDS, against 3,600 for an hour. Nothing in that distribution is an offset.
        //
        // The two rows below are the heads of two different measured responses, chosen so that a FIXED offset
        // fails one of them: 15:11:03 on 2026-05-28 is EDT (UTC-4) and 16:49:39 on 2026-02-26 is EST (UTC-5).
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure.dst-pair.json"),
            FmpJsonContext.Default.ListFundDisclosure)!;

        Assert.Equal(Instant.FromUtc(2026, 5, 28, 19, 11, 3), rows[0].AcceptedDate);   // EDT, -4
        Assert.Equal(Instant.FromUtc(2026, 2, 26, 21, 49, 39), rows[1].AcceptedDate);  // EST, -5

        // And it is NOT the UTC reading that EtfHolding.UpdatedAt takes on the identical wire shape.
        Assert.NotEqual(Instant.FromUtc(2026, 5, 28, 15, 11, 3), rows[0].AcceptedDate);
    }

    [Theory]
    [InlineData("\"Y\"", true)]
    [InlineData("\"N\"", false)]
    [InlineData("\"X\"", null)]
    [InlineData("\"\"", null)]
    [InlineData("\"N/A\"", null)]
    [InlineData("null", null)]
    public void Yes_and_no_become_true_and_false_and_everything_else_becomes_null(string wire, bool? expected)
    {
        // The four `is*` fields are Y/N STRINGS, not JSON booleans — unlike EtfInfo.IsActivelyTrading, which
        // is a real one. Written as a total function over a measured domain rather than a two-case parse:
        // isRestrictedSec and isNonCashCollateral were `N` on all 3,861 rows sampled 2026-08-30, so their `Y`
        // form is unmeasured, and an unexpected third value must cost one field rather than the whole row.
        // The hole is not last in the object — see the note in Every_spelling_of_absence_reads_as_null.
        var row = JsonSerializer.Deserialize(
            $$"""[{"isLoanByFund":{{wire}},"cik":"0000884394"}]""",
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Equal(expected, row.IsLoanByFund);
    }

    [Fact]
    public void The_disclosure_sentinels_become_null_and_the_row_survives()
    {
        // A verbatim measured row: ARKK's 2026 Q1 BRERA HOLDINGS PLC WTS line, which carries THREE spellings
        // of absence at once — a real JSON null in `symbol`, "N/A" in `lei`, and "" in `isin`.
        var row = JsonSerializer.Deserialize(
            """
            [{"cik":"0001579982","date":"2026-01-30","acceptedDate":"2026-03-31 14:42:43","symbol":null,
              "name":"BRERA HOLDINGS PLC WTS","lei":"N/A","title":"BRERA HOLDINGS PLC WTS",
              "cusip":"000000000","isin":"","balance":4316257,"units":"NS","cur_cd":"USD",
              "valUsd":4359419.57,"pctVal":0.06529031951794871,"payoffProfile":"Long","assetCat":"EC",
              "issuerCat":"CORP","invCountry":"US","isRestrictedSec":"N","fairValLevel":"1",
              "isCashCollateral":"N","isNonCashCollateral":"N","isLoanByFund":"N"}]
            """,
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Null(row.Symbol);   // a real JSON null — 176 of 11,522 rows measured 2026-08-30
        Assert.Null(row.Lei);      // "N/A" — 495 rows
        Assert.Null(row.Isin);     // ""    — 149 rows
        Assert.Equal("BRERA HOLDINGS PLC WTS", row.Name);
        Assert.Equal("000000000", row.Cusip);
        Assert.Equal(4316257m, row.Balance);
    }

    [Fact]
    public void The_futures_row_nulls_its_name_its_cusip_and_its_country()
    {
        // QQQ's own 2026 Q1 CME E-Mini futures line, verbatim: SIX sentinels at once — "N/A" in `name`,
        // `lei`, `cusip`, `payoffProfile` and `invCountry`, and "" in `isin`. Measured 2026-08-30, `name` and
        // `invCountry` each carried "N/A" on 120 of 11,522 rows, 1.0%.
        //
        // This test exists because Name and InvestmentCountry were the only two converter-bearing properties
        // on this record whose converter could be deleted with the whole suite still green: every other
        // sentinel assertion here was fed a value SentinelStringJsonConverter leaves alone, so it passed
        // whether or not the converter was attached.
        var row = JsonSerializer.Deserialize(
            """
            [{"cik":"0001067839","date":"2026-03-31","acceptedDate":"2026-05-28 06:53:06","symbol":"NQM6",
              "name":"N/A","lei":"N/A","title":"CME E-Mini NASDAQ 100 Index Future","cusip":"N/A","isin":"",
              "balance":700,"units":"NC","cur_cd":"USDUSD","valUsd":-12012436.8,
              "pctVal":-0.0032285713047007715,"payoffProfile":"N/A","assetCat":"DE","issuerCat":"OTHER",
              "invCountry":"N/A","isRestrictedSec":"N","fairValLevel":"1","isCashCollateral":"N",
              "isNonCashCollateral":"N","isLoanByFund":"N"}]
            """,
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Null(row.Name);
        Assert.Null(row.InvestmentCountry);
        Assert.Null(row.Cusip);
        Assert.Null(row.Lei);
        Assert.Null(row.Isin);
        Assert.Null(row.PayoffProfile);

        // The row survives its own absences: everything beside the sentinels still binds.
        Assert.Equal("NQM6", row.Symbol);
        Assert.Equal(700m, row.Balance);
        Assert.Equal(-12012436.8m, row.ValueUsd);
        Assert.Equal("1", row.FairValueLevel);
    }

    [Fact]
    public void The_currency_code_can_be_usdusd_and_binds_verbatim()
    {
        // A verbatim measured row: FXAIX's 2026 Q1 S&P 500 E-mini futures line. `cur_cd` was USDUSD on 29 of
        // 3,861 rows measured 2026-08-30 — all of them equity-futures lines (units NC, assetCat DE,
        // payoffProfile N/A). A doubled currency code, not a typo in this test. It is recorded so that a
        // strict three-letter currency type is never chosen for this field: this row would not fit it.
        var row = JsonSerializer.Deserialize(
            """
            [{"symbol":"ESH6","name":"CHICAGO MERCANTILE EXCH INC","cusip":"N/A","isin":"",
              "title":"S and P500 EMINI FUT MAR26 ESH6","balance":2288,"units":"NC","cur_cd":"USDUSD",
              "valUsd":5282494.16,"pctVal":0.0007040306952703573,"payoffProfile":"N/A","assetCat":"DE"}]
            """,
            FmpJsonContext.Default.ListFundDisclosure)![0];

        Assert.Equal("USDUSD", row.CurrencyCode);
        Assert.Equal("NC", row.Units);
        Assert.Null(row.Cusip);           // "N/A"
        Assert.Null(row.PayoffProfile);   // "N/A" — 123 of the 11,522 rows measured 2026-08-30, 1.1%
        Assert.Equal("DE", row.AssetCategory);
    }

    [Fact]
    public void The_fair_value_level_stays_a_string_and_takes_no_sentinel_converter()
    {
        // fairValLevel is a quoted integer — "1" x3,829, "2" x28, "3" x4 over the 3,861-row sample measured
        // 2026-08-30 — and it is a CODE, not a quantity: an ASC 820 fair-value level. Parsing it to int?
        // would invent a numeric identity the source does not have. It carries NO sentinel converter, because
        // no measured row ever sent a sentinel here — see the ruling recorded at the top of this plan.
        //
        // The second row is the guard, and it is deliberately a value FMP was never measured sending. A test
        // that fed only "3" would pass whether or not the converter is attached, because "3" is not one of
        // the four sentinels — so it could not fail if someone later added the converter and reverted the
        // ruling. Feeding a sentinel is the only assertion that can.
        var rows = JsonSerializer.Deserialize(
            """[{"fairValLevel":"3"},{"fairValLevel":"N/A"}]""",
            FmpJsonContext.Default.ListFundDisclosure)!;

        Assert.Equal("3", rows[0].FairValueLevel);
        Assert.Equal("N/A", rows[1].FairValueLevel);
    }

    [Fact]
    public void A_fund_disclosure_date_binds_all_three_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-dates.SPY.json"),
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        Assert.Equal(8, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].Date);
        Assert.Equal(2026, rows[0].Year);
        Assert.Equal(2, rows[0].Quarter);
    }

    [Fact]
    public void The_disclosure_dates_come_back_newest_first()
    {
        // Measured 2026-08-30 over 127 rows: `date` descending on every response. Nothing re-sorts this
        // client-side, so the <returns> doc reports the measured order and this test holds it honest.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-dates.SPY.json"),
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        Assert.Equal(rows.Select(r => r.Date).OrderByDescending(d => d), rows.Select(r => r.Date));
    }

    [Fact]
    public void The_year_and_quarter_are_calendar_quarters_of_a_fiscal_period_end()
    {
        // The two fields do not describe the same calendar the `date` does. `date` is the fund's FISCAL
        // period-end — FXAIX reports on 2026-05-31 and 2025-11-30, ARKK on 2026-01-30 — while `year` and
        // `quarter` count CALENDAR quarters, so FXAIX's May date reads as Q2. Verified over 80 rows across
        // three funds 2026-08-30: year == date.Year and quarter == (date.Month - 1) / 3 + 1, with ZERO
        // mismatches. That relation is what makes the two fields usable as arguments to
        // GetFundDisclosureAsync, which is the only reason a caller reads them.
        //
        // The rows below are verbatim measured captures from FXAIX and ARKK.
        var rows = JsonSerializer.Deserialize(
            """
            [{"date":"2026-05-31","year":2026,"quarter":2},
             {"date":"2026-02-28","year":2026,"quarter":1},
             {"date":"2025-11-30","year":2025,"quarter":4},
             {"date":"2026-01-30","year":2026,"quarter":1}]
            """,
            FmpJsonContext.Default.ListFundDisclosureDate)!;

        foreach (var row in rows)
        {
            Assert.Equal(row.Date!.Value.Year, row.Year);
            Assert.Equal((row.Date.Value.Month - 1) / 3 + 1, row.Quarter);
        }
    }

    [Fact]
    public void A_fund_holder_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(5, rows.Count);
        Assert.Equal("0001181848", rows[0].Cik);
        Assert.Equal("SKYBRIDGE MULTI-ADVISER HEDGE FUND PORTFOLIOS LLC", rows[0].Holder);
        Assert.Equal("78462F103", rows[0].SecurityCusip);
        Assert.Equal(122518791.23m, rows[0].Shares);
        Assert.Equal(new LocalDate(2026, 6, 30), rows[0].DateReported);
        Assert.Equal(0m, rows[0].Change);
        Assert.Equal(11.79723956m, rows[0].WeightPercent);

        // Change is 0 on the head row, which Binding.Unbound does NOT count as unbound (only null, blank and
        // empty collections count), so the whole-record check goes on a row where every field is non-zero.
        Assert.Empty(Binding.Unbound(rows[3]));
    }

    [Fact]
    public void One_holders_response_mixes_reporting_dates_across_years()
    {
        // "Latest" is each HOLDER's own most recent filing, not a single as-of date for the response.
        // Measured 2026-08-30, SPY's 220 rows carried 19 distinct dates spanning 2019-09-30 to 2026-06-30,
        // and AAPL's 3,209 rows carried 66 spanning 2019-09-30 to 2026-07-31. Four recent dates dominate, but
        // 18 of SPY's rows and 292 of AAPL's report a date before 2026 at all — a holder that stopped filing
        // in 2019 is still in the response, with its 2019 position. Rows in one response are therefore NOT
        // comparable as of one date, and DateReported must be read per row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(3, rows.Select(r => r.DateReported).Distinct().Count());
        Assert.Equal(new LocalDate(2019, 9, 30), rows.Min(r => r.DateReported));
        Assert.Equal(new LocalDate(2026, 6, 30), rows.Max(r => r.DateReported));
    }

    [Fact]
    public void A_holders_change_is_signed_and_shares_are_fractional()
    {
        // Measured 2026-08-30: `change` was 0 on 2,532 of AAPL's 3,209 rows, positive on 291 and negative on
        // 386; `shares` ranged -990 to 1,016,998,069 and is fractional (122518791.23, 3049046.052).
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-latest.SPY.json"),
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(-894335m, rows[3].Change);
        Assert.Equal(3049046.052m, rows[2].Shares);
    }

    [Fact]
    public void An_empty_holder_or_na_cusip_becomes_null()
    {
        // Both rows are verbatim measured captures from the AAPL response. `holder` was "" on 16 rows and
        // `securityCusip` was "N/A" on 3 — two different spellings on one path, and the reason the sentinel
        // converter is applied to both properties.
        var rows = JsonSerializer.Deserialize(
            """
            [{"cik":"0002042316","holder":"","securityCusip":"037833100","shares":3264563,
              "dateReported":"2026-06-30","change":-150796,"weightPercent":0.00216968},
             {"cik":"0002042513","holder":"Somebody","securityCusip":"N/A","shares":46772,
              "dateReported":"2026-06-30","change":3469,"weightPercent":0.04495317}]
            """,
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Null(rows[0].Holder);
        Assert.Equal("037833100", rows[0].SecurityCusip);
        Assert.Null(rows[1].SecurityCusip);
        Assert.Equal("Somebody", rows[1].Holder);
        Assert.Equal(-150796m, rows[0].Change);
    }

    [Fact]
    public void A_holder_weight_can_exceed_one_hundred()
    {
        // Measured range 2026-08-30: 1.2e-07 to 264.39824722. Not range-checked, and must not be — the third
        // percentage field in this group that exceeds 100.
        var rows = JsonSerializer.Deserialize(
            """[{"weightPercent":264.39824722},{"weightPercent":1.2e-07}]""",
            FmpJsonContext.Default.ListFundHolder)!;

        Assert.Equal(264.39824722m, rows[0].WeightPercent);
        Assert.Equal(0.00000012m, rows[1].WeightPercent);
    }

    [Fact]
    public void A_fund_share_class_binds_all_thirteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Equal(4, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("BRACX", rows[0].Symbol);
        Assert.Equal("0001221845", rows[0].Cik);
        Assert.Equal("C000003891", rows[0].ClassId);
        Assert.Equal("S000001469", rows[0].SeriesId);
        Assert.Equal("BLACKROCK ALLOCATION TARGET SHARES", rows[0].EntityName);
        Assert.Equal("30", rows[0].EntityOrgType);
        Assert.Equal("BATS SERIES C", rows[0].SeriesName);
        Assert.Equal("BATS SERIES C", rows[0].ClassName);
        Assert.Equal("811-21457", rows[0].ReportingFileNumber);
        Assert.Equal("100 BELLEVUE PARKWAY", rows[0].Address);
        Assert.Equal("WILMINGTON", rows[0].City);
        Assert.Equal("19809", rows[0].ZipCode);
        Assert.Equal("DE", rows[0].State);
    }

    [Fact]
    public void The_null_row_nulls_its_whole_address_block_and_keeps_everything_else()
    {
        // The sharpest case in the slice. Measured 2026-08-30, `entityOrgType`, `reportingFileNumber`,
        // `city`, `zipCode` and `state` were the literal string "NULL" on exactly the same 1,540 rows on
        // which `address` was a real JSON null — one missing address block, encoded two different ways inside
        // one object. `symbol` was "NULL" on 82 more rows than that, so it is not purely the same population.
        //
        // What survives is the point: cik, classId, seriesId, entityName, seriesName and className are all
        // real on this row. The sentinel converter must not cost them.
        var row = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)![1];

        Assert.Null(row.Symbol);
        Assert.Null(row.EntityOrgType);
        Assert.Null(row.ReportingFileNumber);
        Assert.Null(row.Address);
        Assert.Null(row.City);
        Assert.Null(row.ZipCode);
        Assert.Null(row.State);

        Assert.Equal("0000110055", row.Cik);
        Assert.Equal("C000005579", row.ClassId);
        Assert.Equal("S000002175", row.SeriesId);
        Assert.Equal("BLACKROCK SUSTAINABLE BALANCED FUND, INC.", row.EntityName);
        Assert.Equal("BLACKROCK SUSTAINABLE BALANCED FUND, INC.", row.SeriesName);
        Assert.Equal("Investor B", row.ClassName);
    }

    [Fact]
    public void The_address_block_carries_both_a_json_null_and_an_empty_string()
    {
        // Two rows, two encodings, one meaning. The BlackRock row sends address:null with "NULL" siblings;
        // the Pioneer row sends "" on all four. Both were measured 2026-08-30 — which is why Address takes
        // the converter even though its headline absence is a real JSON null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("funds-disclosure-holders-search.nulls.json"),
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Null(rows[1].Address);   // JSON null   — 1,540 of 5,869 rows
        Assert.Null(rows[2].Address);   // ""          — 8 rows in the same corpus
        Assert.Null(rows[2].City);
        Assert.Equal("PPPAX", rows[2].Symbol);
        Assert.Equal("0001175959", rows[2].Cik);
    }

    [Theory]
    [InlineData("\"NULL\"")]
    [InlineData("\"N/A\"")]
    public void The_class_name_carries_two_spellings_of_absence(string wire)
    {
        // One field, two sentinels, in one corpus. On the widest query taken 2026-08-30 (`name=Trust`, 66,065
        // rows) `className` was "NULL" x1,278 AND "N/A" x192. A caller checking for one of the two would miss
        // the other, which is the argument for a converter over documentation here.
        // The hole is not last in the object — see the note in Every_spelling_of_absence_reads_as_null.
        var row = JsonSerializer.Deserialize(
            $$"""[{"className":{{wire}},"cik":"0001350487"}]""",
            FmpJsonContext.Default.ListFundShareClass)![0];

        Assert.Null(row.ClassName);
    }

    [Fact]
    public void The_entity_org_type_stays_a_string_and_its_sentinel_becomes_null()
    {
        // A numeric string with a non-numeric sentinel in the same field: "30" x3,635, "32" x17, "33" x5 and
        // "NULL" x1,540, measured 2026-08-30. Any caller reaching for int.Parse gets an outright failure on a
        // quarter of the rows. It stays a string because it is an SEC entity ORGANISATION TYPE — a code, not
        // a quantity — and nothing a caller does with it is arithmetic.
        var rows = JsonSerializer.Deserialize(
            """[{"entityOrgType":"30"},{"entityOrgType":"NULL"}]""",
            FmpJsonContext.Default.ListFundShareClass)!;

        Assert.Equal("30", rows[0].EntityOrgType);
        Assert.Null(rows[1].EntityOrgType);
    }

    private static (EtfAndFundsEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")
    {
        var handler = new StubHandler(StubHandler.Json(body));
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new EtfAndFundsEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Theory]
    [InlineData("asset-exposure", "/stable/etf/asset-exposure")]
    [InlineData("country-weightings", "/stable/etf/country-weightings")]
    [InlineData("holdings", "/stable/etf/holdings")]
    [InlineData("info", "/stable/etf/info")]
    [InlineData("sector-weightings", "/stable/etf/sector-weightings")]
    public async Task Each_etf_method_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        switch (which)
        {
            case "asset-exposure": await endpoints.GetEtfAssetExposureAsync("QQQ"); break;
            case "country-weightings": await endpoints.GetEtfCountryWeightingsAsync("QQQ"); break;
            case "holdings": await endpoints.GetEtfHoldingsAsync("QQQ"); break;
            case "info": await endpoints.GetEtfInfoAsync("QQQ"); break;
            default: await endpoints.GetEtfSectorWeightingsAsync("QQQ"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task An_etf_method_sends_the_symbol_and_nothing_beside_it()
    {
        // Measured 2026-08-30: `limit` and `page` are ignored on all nine paths — byte-identical responses
        // with and without them, including a 17,252-row, 4.9 MB etf/holdings?symbol=BND. Offering either
        // would let a caller believe a page happened. Asserted against the WHOLE query string, not just those
        // two, so any future parameter this method starts sending is caught as well.
        var (endpoints, handler) = Build();

        await endpoints.GetEtfHoldingsAsync("QQQ");

        Assert.Equal("?symbol=QQQ", handler.Requests[0].Query);
    }

    [Fact]
    public async Task Get_etf_info_returns_the_single_row_rather_than_a_list()
    {
        // All 33 responses measured 2026-08-30 were single-element arrays, which is why this one method on
        // the facade returns a record instead of a list — the CompanyEndpoints.GetProfileAsync precedent.
        var (endpoints, _) = Build(Binding.Fixture("etf-info.SPY.json"));

        var info = await endpoints.GetEtfInfoAsync("SPY");

        Assert.NotNull(info);
        Assert.Equal("SPY", info.Symbol);
        Assert.Equal(12, info.SectorsList!.Count);
    }

    [Fact]
    public async Task Get_etf_info_returns_null_when_the_array_is_empty()
    {
        // An unknown symbol answers `[]` at HTTP 200, not an error — measured 2026-08-30, and so does a
        // perfectly valid stock ticker: AAPL returned `[]` on all four ETF-only paths.
        var (endpoints, _) = Build();

        Assert.Null(await endpoints.GetEtfInfoAsync("AAPL"));
    }

    [Theory]
    [InlineData("asset-exposure")]
    [InlineData("country-weightings")]
    [InlineData("holdings")]
    [InlineData("info")]
    [InlineData("sector-weightings")]
    public async Task A_comma_in_the_symbol_is_rejected_before_the_request_goes_out(string which)
    {
        // Measured 2026-08-30: `symbol=SPY,QQQ` returns `[]` with HTTP 200 on etf/info and
        // etf/sector-weightings, while the plural `symbols=` is a 400. So the comma-joined form that works on
        // QuoteEndpoints.Batch is not merely unsupported here — it is a SILENT WRONG ANSWER, indistinguishable
        // from "this ETF has no data". This is the one place in the slice where a signature can prevent one.
        //
        // Deliberately narrow: it rejects the COMMA, not "not a known ETF". An unknown symbol legitimately
        // answers [] and so does a stock; those are honest empties and stay documented rather than guarded.
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "asset-exposure" => endpoints.GetEtfAssetExposureAsync("SPY,QQQ"),
            "country-weightings" => endpoints.GetEtfCountryWeightingsAsync("SPY,QQQ"),
            "holdings" => endpoints.GetEtfHoldingsAsync("SPY,QQQ"),
            "info" => endpoints.GetEtfInfoAsync("SPY,QQQ"),
            _ => endpoints.GetEtfSectorWeightingsAsync("SPY,QQQ"),
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Equal("symbol", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("asset-exposure")]
    [InlineData("country-weightings")]
    [InlineData("holdings")]
    [InlineData("info")]
    [InlineData("sector-weightings")]
    public async Task A_blank_symbol_is_rejected_before_the_request_goes_out(string which)
    {
        // Measured 2026-08-30: a bare `symbol=` is an HTTP 400 from FMP on every one of these paths.
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "asset-exposure" => endpoints.GetEtfAssetExposureAsync("  "),
            "country-weightings" => endpoints.GetEtfCountryWeightingsAsync("  "),
            "holdings" => endpoints.GetEtfHoldingsAsync("  "),
            "info" => endpoints.GetEtfInfoAsync("  "),
            _ => endpoints.GetEtfSectorWeightingsAsync("  "),
        };

        await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("disclosure", "/stable/funds/disclosure")]
    [InlineData("dates", "/stable/funds/disclosure-dates")]
    [InlineData("holders", "/stable/funds/disclosure-holders-latest")]
    [InlineData("search", "/stable/funds/disclosure-holders-search")]
    public async Task Each_fund_method_asks_its_own_path(string which, string expected)
    {
        var (endpoints, handler) = Build();

        switch (which)
        {
            case "disclosure": await endpoints.GetFundDisclosureAsync("SPY", 2026, 1); break;
            case "dates": await endpoints.GetFundDisclosureDatesAsync("SPY"); break;
            case "holders": await endpoints.GetFundHoldersAsync("AAPL"); break;
            default: await endpoints.SearchFundsByNameAsync("Schwab"); break;
        }

        Assert.Equal(expected, handler.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task The_disclosure_call_sends_the_symbol_the_year_and_the_quarter()
    {
        // Asserted against the whole query string: `limit` and `page` are ignored by FMP on this path
        // (measured 2026-08-30, `funds/disclosure?symbol=SPY&year=2026&quarter=1&limit=10` returned all 503
        // rows), so offering either would let a caller believe a page happened.
        var (endpoints, handler) = Build();

        await endpoints.GetFundDisclosureAsync("SPY", 2026, 1);

        Assert.Equal("?symbol=SPY&year=2026&quarter=1", handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_search_call_sends_the_name_under_its_own_parameter()
    {
        var (endpoints, handler) = Build();

        await endpoints.SearchFundsByNameAsync("Schwab");

        Assert.Equal("?name=Schwab", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task A_quarter_outside_one_to_four_is_rejected_before_the_request_goes_out(int quarter)
    {
        // Measured 2026-08-30: quarter=0 and quarter=5 both return HTTP 200 with `[]`, while quarter=Q1 is a
        // 400. So a caller who sends 0 is told "no holdings", not "bad request" — the same silent-empty
        // failure the comma guard exists for. Four quarters is not a measurement; it is what a quarter is.
        var (endpoints, handler) = Build();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetFundDisclosureAsync("SPY", 2026, quarter));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(1990)]
    [InlineData(2030)]
    public async Task The_year_is_deliberately_not_bounded(int year)
    {
        // Measured 2026-08-30: year=1990 and year=2030 both return HTTP 200 with `[]`, and year=abc is a 400.
        // No bound is imposed here, and this test is what stops one being added: a lower bound would have to
        // come from measured coverage extents, which differ per fund (2019-09-30 SPY, 2019-11-30 FXAIX,
        // 2020-04-30 ARKK) and will move. Encoding one of them would be inventing a fact.
        var (endpoints, handler) = Build();

        var rows = await endpoints.GetFundDisclosureAsync("SPY", year, 1);

        Assert.Empty(rows);
        Assert.Single(handler.Requests);
        Assert.Contains($"year={year}", handler.Requests[0].Query);
    }

    [Theory]
    [InlineData("disclosure")]
    [InlineData("dates")]
    [InlineData("holders")]
    public async Task A_comma_in_the_symbol_is_rejected_on_the_fund_paths_too(string which)
    {
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "disclosure" => endpoints.GetFundDisclosureAsync("SPY,QQQ", 2026, 1),
            "dates" => endpoints.GetFundDisclosureDatesAsync("SPY,QQQ"),
            _ => endpoints.GetFundHoldersAsync("SPY,QQQ"),
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Equal("symbol", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("disclosure")]
    [InlineData("dates")]
    [InlineData("holders")]
    public async Task A_blank_symbol_is_rejected_on_the_fund_paths_too(string which)
    {
        var (endpoints, handler) = Build();

        Task Call() => which switch
        {
            "disclosure" => endpoints.GetFundDisclosureAsync("  ", 2026, 1),
            "dates" => endpoints.GetFundDisclosureDatesAsync("  "),
            _ => endpoints.GetFundHoldersAsync("  "),
        };

        await Assert.ThrowsAsync<ArgumentException>(Call);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_blank_name_is_rejected_before_the_request_goes_out()
    {
        // Measured 2026-08-30: a bare `name=` is an HTTP 400 on this path.
        var (endpoints, handler) = Build();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.SearchFundsByNameAsync("  "));

        Assert.Equal("name", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task All_nine_paths_are_reachable_and_each_asks_a_different_one()
    {
        // The whole surface in one assertion. Nine methods, nine distinct paths, no duplicates and no typos —
        // measured 2026-08-30, no two of the nine share a key tuple either, so a copy-paste that pointed two
        // methods at one path would bind the wrong shape without failing anything else here.
        var (endpoints, handler) = Build();

        await endpoints.GetEtfAssetExposureAsync("QQQ");
        await endpoints.GetEtfCountryWeightingsAsync("QQQ");
        await endpoints.GetEtfHoldingsAsync("QQQ");
        await endpoints.GetEtfInfoAsync("QQQ");
        await endpoints.GetEtfSectorWeightingsAsync("QQQ");
        await endpoints.GetFundDisclosureAsync("QQQ", 2025, 3);
        await endpoints.GetFundDisclosureDatesAsync("QQQ");
        await endpoints.GetFundHoldersAsync("QQQ");
        await endpoints.SearchFundsByNameAsync("Schwab");

        Assert.Equal(
            [
                "/stable/etf/asset-exposure",
                "/stable/etf/country-weightings",
                "/stable/etf/holdings",
                "/stable/etf/info",
                "/stable/etf/sector-weightings",
                "/stable/funds/disclosure",
                "/stable/funds/disclosure-dates",
                "/stable/funds/disclosure-holders-latest",
                "/stable/funds/disclosure-holders-search",
            ],
            handler.Requests.Select(u => u.AbsolutePath).ToArray());
    }
}
