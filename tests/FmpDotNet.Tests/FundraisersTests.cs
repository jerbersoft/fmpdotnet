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
}
