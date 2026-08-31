using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The six Fundraisers paths, checked against captures taken live 2026-08-31.</summary>
public class FundraisersTests
{
    [Fact]
    public void A_crowdfunding_row_binds_every_one_of_its_forty_eight_keys()
    {
        // Binding.Unbound names every [JsonPropertyName] property that came back null, blank or empty, so
        // this is the WHOLE record binding rather than a spot check. Five models in this repo were measured
        // 2026-08-27 with most of their [JsonPropertyName] attributes doing nothing, which a two-field
        // assertion cannot see. Task 1 verified the -latest fixture's three rows carry no null at all.
        var latest = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings-latest.head.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Equal(3, latest.Count);
        Assert.All(latest, r => Assert.Empty(Binding.Unbound(r)));

        // The by-CIK fixture is Finlete Funding, Inc., and its one absent field is named rather than waved
        // at: securityOfferedOtherDescription was null on 695 of 1,000 rows measured 2026-08-31, so a fixture
        // without it would be the unusual case, not this one.
        var byCik = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings.0002010670.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Equal(3, byCik.Count);
        Assert.All(byCik, r => Assert.Equal(["SecurityOfferedOtherDescription"], Binding.Unbound(r)));
        Assert.All(byCik, r => Assert.Equal("0002010670", r.Cik));
    }

    [Fact]
    public void The_offering_date_is_month_day_year_and_the_ISO_converter_reads_it_as_null()
    {
        // THE test this slice exists to protect, and the failure it guards is silent in both directions.
        // NullableLocalDateJsonConverter parses with LocalDatePattern.Iso and returns null on failure rather
        // than throwing (NodaConverters.cs:43-44), so binding crowdfunding's `date` with it yields null on
        // 100% of rows at HTTP 200 with no exception and no warning. Measured 2026-08-31 by deserialising
        // through it: "08-28-2026" -> null, "04-30-2027" -> null, "2026-08-31" -> 2026-08-31.
        //
        // The component order is measured, not assumed: over 1,000 crowdfunding rows and 6,542 dated search
        // rows the first component never exceeded 12 while the second reached 31, so DD-MM-YYYY is ruled out
        // by 7,542 rows.
        var row = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011","offeringDeadlineDate":"10-31-2026"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(new LocalDate(2011, 11, 22), row.Date);
        Assert.Equal(new LocalDate(2026, 10, 31), row.OfferingDeadlineDate);

        // The same two strings through the ISO converter, which is what a naive binding would have used.
        // FundraisingNotice.Date carries it, and reading a crowdfunding value with it gives NOTHING back.
        var throughIso = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Null(throughIso.Date);

        // And absence has one spelling on this converter, whichever way it arrives.
        var absent = JsonSerializer.Deserialize(
            """[{"date":null},{"date":""},{"date":"not a date"},{"companyName":"no date key at all"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.All(absent, r => Assert.Null(r.Date));
    }

    [Fact]
    public void The_offering_date_precedes_the_filing_date_on_every_row()
    {
        // The most easily-missed semantic trap in the slice, caught from FMP's own documented sample, which
        // shows "date": "11-22-2011" beside "filingDate": "2026-07-30 00:00:00" — fifteen years apart.
        // Measured 2026-08-31, `date` precedes `filingDate` on 1,000 of 1,000 rows with zero exceptions,
        // gaps of 0 to 43 years and a year range of 1983-2026; and it is constant across every filing for
        // 10 of 18 filers, including Finlete Funding, whose 48 filings all carry 12-19-2023. It is a property
        // of the company, not of the filing. This test fails if anyone renames Date to FilingDate or swaps
        // the two converters.
        foreach (var fixture in new[]
                 {
                     "crowdfunding-offerings.0002010670.json",
                     "crowdfunding-offerings-latest.head.json",
                 })
        {
            var rows = JsonSerializer.Deserialize(
                Binding.Fixture(fixture), FmpJsonContext.Default.ListCrowdfundingOffering)!;

            Assert.All(rows, r =>
            {
                Assert.NotNull(r.Date);
                Assert.NotNull(r.FilingDate);
                Assert.True(r.Date < r.FilingDate, $"{r.Cik}: {r.Date} is not before {r.FilingDate}");
            });
        }

        // And the by-CIK fixture pins the constancy: three filings by one issuer, one formation date.
        var finlete = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings.0002010670.json"),
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.Single(finlete.Select(r => r.Date).Distinct());
    }

    [Fact]
    public void The_filing_date_is_a_date_and_the_accepted_date_is_an_Eastern_instant()
    {
        // Two fields, one wire format, two different converters — and swapping either is silent.
        //
        // filingDate: its time component was 00:00:00 on 3,575 of 3,575 rows measured 2026-08-31, exactly
        // what NullableDateAtMidnightJsonConverter was written for in the SEC Filings slice (2,115 of 2,115
        // there). Binding it as a timestamp leaks a meaningless midnight into every comparison a caller
        // writes, so the property type itself is asserted, not just the value.
        Assert.Equal(
            typeof(LocalDate?),
            typeof(CrowdfundingOffering).GetProperty(nameof(CrowdfundingOffering.FilingDate))!.PropertyType);

        var row = JsonSerializer.Deserialize(
            """[{"filingDate":"2026-07-30 00:00:00","acceptedDate":"2026-08-28 21:52:44"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(new LocalDate(2026, 7, 30), row.FilingDate);

        // acceptedDate: the SDK carries two converters for the identical "yyyy-MM-dd HH:mm:ss" shape and they
        // are four to five hours apart. NullableFmpInstantJsonConverter (UTC) compiles here, deserialises
        // here, and is wrong. The measurement that chose Eastern: over 1,395 acceptedDate values and 1,779
        // fundraising-search timestamps spanning 2009-2026, the window is 06:00-22:00 in EDT (n=1,060) and
        // 06:00-21:59 in EST (n=445) — it does NOT shift across the DST boundary, which a stored instant
        // would — and ZERO of 3,174 values fall in hours 22-05, which a UTC reading of an Eastern-window feed
        // would arithmetically require.
        Assert.Equal(Instant.FromUtc(2026, 8, 29, 1, 52, 44), row.AcceptedDate);   // EDT, UTC-4

        var winter = JsonSerializer.Deserialize(
            """[{"acceptedDate":"2026-01-14 16:05:00"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(Instant.FromUtc(2026, 1, 14, 21, 5, 0), winter.AcceptedDate);  // EST, UTC-5

        // The two offsets differ, which rules out every FIXED-offset reading as well as UTC: a converter
        // hard-coding -4 or -5 would pass one of the assertions above and fail this one.
        var summer = JsonSerializer.Deserialize(
            """[{"acceptedDate":"2026-08-27 16:05:00"}]""",
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.NotEqual(
            summer.AcceptedDate!.Value - Instant.FromUtc(2026, 8, 27, 16, 5, 0),
            winter.AcceptedDate!.Value - Instant.FromUtc(2026, 1, 14, 16, 5, 0));
    }

    [Fact]
    public void Over_subscription_is_a_Y_or_an_N_and_anything_else_is_null_rather_than_false()
    {
        // The wire sends "Y"/"N" strings, not booleans. YesNoBooleanJsonConverter maps any unmeasured third
        // value to null rather than guessing — which matters because `false` and "we have never seen this
        // value" are different answers, and only one of them is true.
        var rows = JsonSerializer.Deserialize(
            """
            [{"overSubscriptionAccepted":"Y"},{"overSubscriptionAccepted":"N"},
             {"overSubscriptionAccepted":"MAYBE"},{"overSubscriptionAccepted":null},{"cik":"0000000000"}]
            """,
            FmpJsonContext.Default.ListCrowdfundingOffering)!;

        Assert.True(rows[0].OverSubscriptionAccepted);
        Assert.False(rows[1].OverSubscriptionAccepted);
        Assert.Null(rows[2].OverSubscriptionAccepted);
        Assert.Null(rows[3].OverSubscriptionAccepted);
        Assert.Null(rows[4].OverSubscriptionAccepted);
    }

    [Fact]
    public void The_two_misspelled_wire_names_and_the_string_zip_code_are_reproduced_exactly()
    {
        // cashAndCashEquiValent* carries a capital V in "Equivalent". It is in FMP's own documented sample
        // AND on the wire, so it is stable rather than a transient bug — and a [JsonPropertyName] that
        // "corrects" it binds nothing, silently, on a property whose type gives no hint.
        //
        // issuerZipCode is a STRING: three forms measured 2026-08-31 over 1,000 rows — 99999 on 990, 9999 on
        // 5, and 99999-9999 on 5. An integer type loses the leading zero on the four-digit form and throws
        // outright on the hyphenated one, taking the whole response with it.
        var row = JsonSerializer.Deserialize(
            """
            [{"cashAndCashEquiValentMostRecentFiscalYear":1.5,
              "cashAndCashEquiValentPriorFiscalYear":-2.5,
              "issuerZipCode":"01234-5678",
              "compensationAmount":"7.9% of the offering amount upon a successful fundraise",
              "financialInterest":"No",
              "totalAssetMostRecentFiscalYear":220738384.75,
              "netIncomeMostRecentFiscalYear":-27665487}]
            """,
            FmpJsonContext.Default.ListCrowdfundingOffering)![0];

        Assert.Equal(1.5m, row.CashAndCashEquivalentMostRecentFiscalYear);
        Assert.Equal(-2.5m, row.CashAndCashEquivalentPriorFiscalYear);
        Assert.Equal("01234-5678", row.IssuerZipCode);

        // compensationAmount and financialInterest are free prose despite their names — 57 distinct values up
        // to 256 characters on the second, and "No" is common but it is not a boolean.
        Assert.StartsWith("7.9%", row.CompensationAmount);
        Assert.Equal("No", row.FinancialInterest);

        // Fractional AND negative: offeringPrice was fractional on 884 of 3,656 rows measured 2026-08-31 and
        // netIncomeMostRecentFiscalYear reached -27,665,487. Every numeric here is decimal? for that reason.
        Assert.Equal(220738384.75m, row.TotalAssetMostRecentFiscalYear);
        Assert.Equal(-27665487m, row.NetIncomeMostRecentFiscalYear);
    }

    [Fact]
    public void A_crowdfunding_search_hit_carries_three_keys_and_a_date_that_is_often_absent()
    {
        // 461 of 7,003 measured search rows carry a null date — 6.6% — and FMP's own documented sample shows
        // one. The date is the SAME MM-DD-YYYY encoding as the offering record's, which is why this record
        // exists separately from FundraisingSearchHit: those three keys are identical and the date is not.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("crowdfunding-offerings-search.Wellness.json"),
            FmpJsonContext.Default.ListCrowdfundingSearchHit)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.NotNull(r.Cik));
        Assert.All(rows, r => Assert.NotNull(r.Name));
        Assert.Single(rows, r => r.Date is null);
        Assert.Equal(2, rows.Count(r => r.Date is not null));
        Assert.All(rows.Where(r => r.Date is not null), r => Assert.InRange(r.Date!.Value.Year, 1983, 2026));

        // Three keys and no more. This record is deliberately tiny; a field added here would be a field FMP
        // does not send.
        Assert.Equal(3, typeof(CrowdfundingSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [Fact]
    public void The_crowdfunding_offering_binds_all_forty_eight_wire_names_and_no_others()
    {
        // The count is the point. 48 keys were confirmed three ways on 2026-08-31: against the live captures,
        // against FMP's documented sample (same 48 keys in the same ORDER), and against the independent
        // Python fmpsdk, whose TypedDict carries 48 fields with an identical key set. The measurements file's
        // census says "16 x *MostRecentFiscalYear / *PriorFiscalYear"; the wire has NINE PAIRS, eighteen
        // fields, and 30 + 18 = 48. This test is what makes that arithmetic checkable.
        var names = typeof(CrowdfundingOffering)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .ToList();

        Assert.Equal(48, names.Count);
        Assert.All(names, n => Assert.NotNull(n));
        Assert.Equal(48, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, names.Count(n => n is not null
                                          && (n.EndsWith("MostRecentFiscalYear", StringComparison.Ordinal)
                                              || n.EndsWith("PriorFiscalYear", StringComparison.Ordinal))));
        Assert.Contains("cashAndCashEquiValentMostRecentFiscalYear", names);
        Assert.Contains("cashAndCashEquiValentPriorFiscalYear", names);
    }

    [Fact]
    public void A_fundraising_row_binds_every_one_of_its_forty_three_keys()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising.0001617426.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("0001617426", r.Cik));

        // The four absent fields are NAMED rather than waved at, and every one of them is a measured
        // structural absence rather than a binding failure: incorporatedWithinFiveYears was null on 30 of 100
        // rows measured 2026-08-31, securitiesOfferedAreOfEquityType on 64, revenueRange on 29, and
        // yearOfIncorporation is the empty string on 30 — which SentinelStringJsonConverter collapses to null
        // so that absence has one spelling. If this list grows, a [JsonPropertyName] stopped binding.
        Assert.All(rows, r => Assert.Equal(
            ["IncorporatedWithinFiveYears", "RevenueRange", "SecuritiesOfferedAreOfEquityType",
             "YearOfIncorporation"],
            Binding.Unbound(r)));

        // Zero is a value, not an absence: findersFees was 0 on all 100 rows measured 2026-08-31 and
        // Binding.Unbound does not flag it. A caller reading 0 there is reading what FMP sent.
        Assert.All(rows, r => Assert.NotNull(r.FindersFees));
    }

    [Fact]
    public void The_empty_string_reads_as_null_and_the_other_forty_one_fields_survive()
    {
        // The trap that made yearOfIncorporation a string. Measured 2026-08-31 over 100 rows it is NEVER
        // null, is "" on 30, and is a four-digit year on the other 70 — a JSON string in both cases. It is
        // NOT int?: FmpJsonContext sets NumberHandling = AllowReadingFromString globally, so "1998" would
        // bind — but "" THROWS, and System.Text.Json aborts the entire list deserialisation rather than the
        // one field. Thirty percent of rows would cost the caller the whole response.
        //
        // dateOfFirstSale ("" on 7 of 100) needs no special handling: NullableLocalDateJsonConverter already
        // reads "" as null. This test pins both, and pins that the row around them survives.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-latest.head.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        var emptyYear = Assert.Single(rows, r => r.YearOfIncorporation is null);
        Assert.NotNull(emptyYear.Cik);
        Assert.NotNull(emptyYear.EntityType);
        Assert.NotNull(emptyYear.FilingDate);
        Assert.NotNull(emptyYear.TotalAmountSold);

        var emptyFirstSale = Assert.Single(rows, r => r.DateOfFirstSale is null);
        Assert.NotNull(emptyFirstSale.Cik);
        Assert.NotNull(emptyFirstSale.YearOfIncorporation);

        // And the same two shapes through a literal, so the test states the wire form rather than depending
        // on which rows the fixture happened to catch.
        var literal = JsonSerializer.Deserialize(
            """[{"yearOfIncorporation":"","dateOfFirstSale":"","cik":"0000000000"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Null(literal.YearOfIncorporation);
        Assert.Null(literal.DateOfFirstSale);
        Assert.Equal("0000000000", literal.Cik);

        // A real year stays a string. This is the user's settled decision: the wire sends a string, so the
        // SDK surfaces a string.
        var present = JsonSerializer.Deserialize(
            """[{"yearOfIncorporation":"1998","dateOfFirstSale":"2014-10-03"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Equal("1998", present.YearOfIncorporation);
        Assert.Equal(new LocalDate(2014, 10, 3), present.DateOfFirstSale);
    }

    [Fact]
    public void A_field_called_date_is_encoded_four_different_ways_across_this_group()
    {
        // The single fact that shapes six of this slice's ten records, pinned in one place. Four records
        // carry a field literally named `date`, and no two of the four agree on what it is:
        //
        //   crowdfunding-offerings        MM-DD-YYYY               -> LocalDate?  (issuer formation date)
        //   crowdfunding-offerings-search MM-DD-YYYY               -> LocalDate?  (same, null on 6.6%)
        //   fundraising / -latest         yyyy-MM-dd               -> LocalDate?
        //   fundraising-search            yyyy-MM-dd HH:mm:ss      -> Instant?    (Eastern acceptance)
        //
        // Each wrong pairing fails differently and NONE of them throws: the ISO converter nulls a
        // MM-DD-YYYY value, the MM-DD-YYYY converter nulls an ISO one, and the UTC instant converter binds
        // an Eastern timestamp four to five hours early.
        var crowdfunding = JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""", FmpJsonContext.Default.ListCrowdfundingOffering)![0];
        var crowdfundingHit = JsonSerializer.Deserialize(
            """[{"date":"12-19-2022"}]""", FmpJsonContext.Default.ListCrowdfundingSearchHit)![0];
        var fundraising = JsonSerializer.Deserialize(
            """[{"date":"2026-08-28"}]""", FmpJsonContext.Default.ListFundraisingNotice)![0];
        var fundraisingHit = JsonSerializer.Deserialize(
            """[{"date":"2026-08-31 11:34:51"}]""", FmpJsonContext.Default.ListFundraisingSearchHit)![0];

        Assert.Equal(new LocalDate(2011, 11, 22), crowdfunding.Date);
        Assert.Equal(new LocalDate(2022, 12, 19), crowdfundingHit.Date);
        Assert.Equal(new LocalDate(2026, 8, 28), fundraising.Date);
        Assert.Equal(Instant.FromUtc(2026, 8, 31, 15, 34, 51), fundraisingHit.Date);   // EDT, UTC-4

        // Cross-fed, each converter answers null rather than throwing — which is the whole reason a wrong
        // pairing is silent and needs a test rather than an exception to catch it.
        Assert.Null(JsonSerializer.Deserialize(
            """[{"date":"2026-08-28"}]""", FmpJsonContext.Default.ListCrowdfundingOffering)![0].Date);
        Assert.Null(JsonSerializer.Deserialize(
            """[{"date":"11-22-2011"}]""", FmpJsonContext.Default.ListFundraisingNotice)![0].Date);

        // And the two `date` properties on the two three-key search records are different CLR types. This is
        // the assertion that fails if anyone merges CrowdfundingSearchHit and FundraisingSearchHit on the
        // grounds that they carry the same three key names — which they do.
        Assert.Equal(typeof(LocalDate?),
            typeof(CrowdfundingSearchHit).GetProperty(nameof(CrowdfundingSearchHit.Date))!.PropertyType);
        Assert.Equal(typeof(Instant?),
            typeof(FundraisingSearchHit).GetProperty(nameof(FundraisingSearchHit.Date))!.PropertyType);
    }

    [Fact]
    public void An_amount_above_Int32_binds_rather_than_overflowing_the_response()
    {
        // Measured 2026-08-31 over 406 rows, totalAmountSold reaches 13,475,150,514 — 6.3x Int32.MaxValue.
        // An int? property does not lose the value: System.Text.Json THROWS on the overflow and aborts the
        // whole list, so one large raise costs the caller every other row in the response.
        //
        // decimal? rather than long? for the reason recorded on FinancialScores.PiotroskiScore: all eight
        // amount fields were whole on 406 of 406 rows, but "not seen fractional yet" is not "cannot be
        // fractional", and long? inherits the same abort-the-response failure the day one arrives with cents.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-latest.head.json"),
            FmpJsonContext.Default.ListFundraisingNotice)!;

        var big = Assert.Single(rows, r => r.TotalAmountSold > int.MaxValue);
        Assert.NotNull(big.Cik);
        Assert.NotNull(big.TotalOfferingAmount);

        var literal = JsonSerializer.Deserialize(
            """[{"totalAmountSold":13475150514,"totalOfferingAmount":1000000000.5,"cik":"0000000000"}]""",
            FmpJsonContext.Default.ListFundraisingNotice)![0];

        Assert.Equal(13475150514m, literal.TotalAmountSold);
        Assert.Equal(1000000000.5m, literal.TotalOfferingAmount);
        Assert.Equal("0000000000", literal.Cik);
    }

    [Fact]
    public void The_fundraising_search_date_is_the_acceptance_timestamp_of_the_filing()
    {
        // Not an assumption. Measured 2026-08-31 for CIK 0001617426, all 14 fundraising-search timestamps
        // equal the 14 acceptedDate values returned by fundraising?cik=... EXACTLY. The field is named
        // `date` and it is not a date; a LocalDate? here would silently discard the time of day, and the
        // UTC converter would move it four to five hours.
        var hits = JsonSerializer.Deserialize(
            Binding.Fixture("fundraising-search.Schutt.json"),
            FmpJsonContext.Default.ListFundraisingSearchHit)!;

        Assert.Equal(3, hits.Count);
        Assert.All(hits, h => Assert.Equal("0001617426", h.Cik));
        Assert.All(hits, h => Assert.NotNull(h.Date));

        // Every measured value falls in the Eastern 06:00-22:00 window, which is the finding that chose the
        // converter: zero of 3,174 values landed in hours 22-05, which a UTC reading would require.
        var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
        Assert.All(hits, h =>
            Assert.InRange(h.Date!.Value.InZone(eastern).Hour, 6, 22));

        // Three keys and no more, same as the crowdfunding hit and a different type from it.
        Assert.Equal(3, typeof(FundraisingSearchHit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [Fact]
    public void The_fundraising_notice_binds_all_forty_three_wire_names_and_no_others()
    {
        // 43 keys, confirmed on 2026-08-31 against the live captures and against the independent Python
        // fmpsdk, whose TypedDict carries 43 fields with an identical key set.
        var names = typeof(FundraisingNotice)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .ToList();

        Assert.Equal(43, names.Count);
        Assert.All(names, n => Assert.NotNull(n));
        Assert.Equal(43, names.Distinct(StringComparer.Ordinal).Count());

        // The two corpora are disjoint and this record must not grow the other one's fields. Measured
        // 2026-08-31: a crowdfunding CIK answers 0 rows on stable/fundraising and vice versa.
        Assert.DoesNotContain("overSubscriptionAccepted", names);
        Assert.DoesNotContain("intermediaryCompanyName", names);
    }
}
