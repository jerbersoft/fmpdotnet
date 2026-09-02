using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The congressional-disclosure records and the facade that serves them, checked against captures
/// taken live 2026-08-29.</summary>
public class CongressTests
{
    private static (CongressEndpoints Endpoints, StubHandler Handler) Build(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CongressEndpoints(
                new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public void A_captured_house_trade_binds_all_sixteen_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-house-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal(3, rows.Count);

        // Row 2 (Morrison) is the fullest: `comment` is the only field FMP left blank on it. Asserting the
        // exact unbound set rather than Assert.Empty is deliberate — `Binding.Unbound` counts a blank string
        // as unbound, and `comment` was empty on 100 of 100 rows measured, so Assert.Empty could never pass.
        Assert.Equal(["Comment"], Binding.Unbound(rows[2]));

        Assert.Equal("SOLS", rows[2].Symbol);
        Assert.Equal("M001234", rows[2].SenateId);
        Assert.Equal(new LocalDate(2026, 8, 26), rows[2].DisclosureDate);
        Assert.Equal(new LocalDate(2026, 8, 11), rows[2].TransactionDate);
        Assert.Equal("Kelly Louise", rows[2].FirstName);
        Assert.Equal("Morrison", rows[2].LastName);
        Assert.Equal("Kelly Louise Morrison", rows[2].Office);
        Assert.Equal("MN03", rows[2].District);
        Assert.Equal("Spouse", rows[2].Owner);
        Assert.Equal("SOLSTICE ADVANCED MTRILS INC", rows[2].AssetDescription);
        Assert.Equal("Stock", rows[2].AssetType);
        Assert.Equal("Sale", rows[2].Type);
        Assert.Equal("$1,001 - $15,000", rows[2].Amount);
        Assert.Equal("False", rows[2].CapitalGainsOver200Usd);
        Assert.Equal("", rows[2].Comment);
        Assert.StartsWith("https://disclosures-clerk.house.gov/", rows[2].Link);
    }

    [Fact]
    public void An_empty_string_is_kept_as_an_empty_string_and_a_null_is_kept_as_null()
    {
        // Both forms occur in this one record and mean different things: measured 2026-08-29, `owner` was ""
        // on 54 of 100 House rows while `senateID` was JSON null on 2. Collapsing either into the other
        // destroys a distinction FMP makes.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-house-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal("", rows[0].Owner);
        Assert.Null(rows[1].SenateId);
        Assert.Equal("", rows[1].District);
    }

    [Fact]
    public void Capital_gains_binds_from_the_string_False_that_FMP_actually_sends()
    {
        // Measured 2026-08-29: the field is the JSON string "False", and `bool?` THROWS on it — the context's
        // AllowReadingFromString covers numbers, not booleans. Only "False" was ever observed, so the
        // affirmative spelling is unknown and no converter can be written for it honestly.
        var rows = JsonSerializer.Deserialize(
            """[{"symbol":"AAPL","capitalGainsOver200USD":"False"}]""",
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal("False", rows[0].CapitalGainsOver200Usd);
    }

    [Fact]
    public void The_senate_feed_binds_with_capital_gains_absent_and_the_other_fifteen_populated()
    {
        // senate-latest is the ONE trade feed that omits capitalGainsOver200USD — 0 of its 100 rows carry it,
        // against 100% on the other seven. One nullable property covers all eight paths.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-latest.json"),
            FmpJsonContext.Default.ListCongressionalTrade)!;

        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].CapitalGainsOver200Usd);
        Assert.Equal(["CapitalGainsOver200Usd", "Comment"], Binding.Unbound(rows[0]).Order());
        Assert.Equal("GS", rows[0].Symbol);
        Assert.Equal("Corporate Bond", rows[0].AssetType);
    }

    [Fact]
    public void A_captured_position_binds_all_eight_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-positions.json"),
            FmpJsonContext.Default.ListCongressMemberPosition)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Z000018", rows[0].SenateId);
        Assert.Equal(118, rows[0].CongressNumber);
        Assert.Equal(new LocalDate(2023, 1, 2), rows[0].StartDate);
        Assert.Equal(new LocalDate(2025, 1, 2), rows[0].EndDate);
        Assert.Equal("Republican", rows[0].Party);
        Assert.Equal("Representative", rows[0].Position);
        Assert.Equal("MT", rows[0].State);
        Assert.Equal(2m, rows[0].YearsInTerm);
    }

    [Fact]
    public void A_fractional_years_in_term_binds_and_does_not_cost_the_rows_around_it()
    {
        // THE trap of this record. Measured 2026-08-29, `yearsInTerm` is a bare integer on 266 of 300 rows
        // and carries a decimal point on 34 — so a smaller sample sees only integers and types it `int`.
        // Under `int?` row 1 does not merely bind wrong: it aborts the whole array and takes rows 0 and 2
        // with it, which is why they are here.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-positions.json"),
            FmpJsonContext.Default.ListCongressMemberPosition)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(2m, rows[0].YearsInTerm);
        Assert.Equal(0.7m, rows[1].YearsInTerm);
        Assert.Equal(0.7m, rows[2].YearsInTerm);
        Assert.Null(rows[1].EndDate);
    }

    [Fact]
    public void A_captured_profile_binds_all_ten_of_its_fields_including_a_fractional_tenure()
    {
        // `yearsActive` is the same trap from the other side: 493 of 500 rows carry a decimal point, so here
        // the integral value is the rare one. Both are asserted.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-profile.json"),
            FmpJsonContext.Default.ListCongressMemberProfile)!;

        Assert.Equal(2, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("L000397", rows[0].SenateId);
        Assert.Equal("Zoe", rows[0].FirstName);
        Assert.Equal("Lofgren", rows[0].LastName);
        Assert.Equal(new LocalDate(1947, 12, 20), rows[0].BirthDate);
        Assert.Equal("Democrat", rows[0].LatestParty);
        Assert.Equal("CA", rows[0].LatestState);
        Assert.Equal("Representative", rows[0].LatestPosition);
        Assert.True(rows[0].Active);
        Assert.Equal(31.7m, rows[0].YearsActive);
        Assert.Equal(8m, rows[1].YearsActive);
    }

    [Fact]
    public void An_income_range_sent_as_the_empty_string_binds_null_and_costs_no_other_row()
    {
        // THE trap of this slice, and the reason NetWorthRangeJsonConverter exists. Measured 2026-08-29,
        // `incomeRange` is an object on 136 of 250 rows, JSON null on 100, and THE EMPTY STRING on 14.
        // System.Text.Json cannot read a string into an object, so without the converter those 14 rows throw
        // — and the throw is not confined to its row: a three-row array where only the middle row sends ""
        // recovered 0 of 3. On this one member that is 14 rows costing all 250.
        //
        // The object-valued rows either side are the point of the test: remove the converter and they are
        // lost too.
        var rows = JsonSerializer.Deserialize(
            """
            [{"senateID":"H000601","incomeRange":{"min":2501,"max":5000},"income":3750.5},
             {"senateID":"H000601","incomeRange":"","income":null,"incomeType":""},
             {"senateID":"H000601","incomeRange":{"min":0,"max":201},"income":0}]
            """,
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal(2501m, rows[0].IncomeRange!.Min);
        Assert.Equal(5000m, rows[0].IncomeRange!.Max);
        Assert.Null(rows[1].IncomeRange);
        Assert.Equal("", rows[1].IncomeType);
        Assert.Equal(0m, rows[2].IncomeRange!.Min);
        Assert.Equal(201m, rows[2].IncomeRange!.Max);

        // Row 2 is the measured mismatch that proves `income` is not derived from `incomeRange`: the midpoint
        // of 0 and 201 is 100.5 and FMP reports 0. Asserted so nobody "fixes" this into a computed property.
        Assert.Equal(0m, rows[2].Income);
        Assert.NotEqual((rows[2].IncomeRange!.Min + rows[2].IncomeRange!.Max) / 2, rows[2].Income);
    }

    [Fact]
    public void Debt_details_binds_in_both_of_the_shapes_FMP_sends()
    {
        // Measured 2026-08-29, `debtDetails` is a union of two DISJOINT shapes — 87 rows carry
        // dateIncurred/points/rate and 13 carry `source` alone. Never all four keys at once. One record with
        // four nullable properties covers both because an absent key binds null.
        var rows = JsonSerializer.Deserialize(
            """
            [{"debtDetails":{"dateIncurred":"2021","points":"-","rate":1.4}},
             {"debtDetails":{"source":"Hall Capital Management Co, LLC         Oklahoma City, OK"}}]
            """,
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("2021", rows[0].DebtDetails!.DateIncurred);
        Assert.Equal("-", rows[0].DebtDetails!.Points);
        Assert.Equal("1.4", rows[0].DebtDetails!.Rate);
        Assert.Null(rows[0].DebtDetails!.Source);

        Assert.StartsWith("Hall Capital", rows[1].DebtDetails!.Source);
        Assert.Null(rows[1].DebtDetails!.DateIncurred);
        Assert.Null(rows[1].DebtDetails!.Rate);
    }

    [Fact]
    public void A_numeric_rate_or_points_reaches_the_string_property_instead_of_aborting_the_array()
    {
        // The second load-bearing converter in this record, and the reason ScalarAsStringJsonConverter
        // exists. Measured 2026-08-29 over the 100 rows where debtDetails is present, `rate` is a JSON
        // number on 23 and `points` on 5 — and a JSON number read into a plain `string?` THROWS under this
        // library's own context options, taking the whole 250-row response with it. Both are typed string
        // because the string forms carry a term ("(10 years)") that a numeric converter would discard.
        //
        // The clean rows either side are the point: without the converter they are lost too.
        var rows = JsonSerializer.Deserialize(
            """
            [{"debtDetails":{"rate":"N/A%                        (10 years)","points":"-"}},
             {"debtDetails":{"rate":1.4,"points":0}},
             {"debtDetails":{"rate":3,"points":"-"}},
             {"debtDetails":{"rate":"NA%                        (On Demand)","points":"-"}}]
            """,
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal(4, rows.Count);
        Assert.Equal("N/A%                        (10 years)", rows[0].DebtDetails!.Rate);
        Assert.Equal("1.4", rows[1].DebtDetails!.Rate);
        Assert.Equal("0", rows[1].DebtDetails!.Points);
        Assert.Equal("3", rows[2].DebtDetails!.Rate);
        Assert.Equal("NA%                        (On Demand)", rows[3].DebtDetails!.Rate);

        // The literal JSON text is what surfaces, not a round-trip through decimal — so trailing zeros FMP
        // chose to send are preserved rather than normalised away.
        var trailing = JsonSerializer.Deserialize(
            """[{"debtDetails":{"rate":1.40}}]""",
            FmpJsonContext.Default.ListSenateNetWorthLine)!;
        Assert.Equal("1.40", trailing[0].DebtDetails!.Rate);
    }

    [Fact]
    public void A_rate_carrying_a_term_survives_intact_beside_a_numeric_one()
    {
        // `rate` arrives as float, int OR string. The strings are not placeholders — they carry a term as
        // well as a rate, so a tolerant numeric converter would bind null and discard "10 years" with it.
        // 64 of the 100 rows where debtDetails is present look like this.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal(3, rows.Count);
        Assert.Equal("N/A%                        (10 years)", rows[0].DebtDetails!.Rate);
        Assert.Equal("1.4", rows[1].DebtDetails!.Rate);
        Assert.Null(rows[2].DebtDetails);
    }

    [Fact]
    public void A_captured_net_worth_line_binds_and_value_is_the_midpoint_of_its_range()
    {
        // `value` is the midpoint of `valueRange` on 214 of 214 rows where both are present, measured
        // 2026-08-29. Neither figure is recomputed by the SDK; this pins that FMP's own arithmetic is what
        // was measured, and it is where the `.5` endings across this group come from.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("H000601", rows[2].SenateId);
        Assert.Equal("Annual Report", rows[2].FormType);
        Assert.Equal(2022, rows[2].Year);
        Assert.Equal(new LocalDate(2023, 8, 14), rows[2].FilingDate);
        Assert.Equal("Asset", rows[2].Section);
        Assert.Equal(100001m, rows[2].ValueRange!.Min);
        Assert.Equal(250000m, rows[2].ValueRange!.Max);
        Assert.Equal(175000.5m, rows[2].Value);
        Assert.Equal((rows[2].ValueRange!.Min + rows[2].ValueRange!.Max) / 2, rows[2].Value);

        // The sibling pair does NOT follow that rule — 35 hold, 101 fail — so nothing here asserts it.
        Assert.Equal(3750.5m, rows[2].Income);
    }

    [Fact]
    public void Date_incurred_is_a_year_string_and_not_a_date()
    {
        // Seven distinct values measured 2026-08-29, every one a bare four-digit year. Typing it LocalDate?
        // would fail on every row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth.json"),
            FmpJsonContext.Default.ListSenateNetWorthLine)!;

        Assert.Equal("2015", rows[0].DebtDetails!.DateIncurred);
    }

    [Fact]
    public void Every_money_field_on_the_aggregate_binds_whether_or_not_it_carries_a_decimal_point()
    {
        // Measured 2026-08-29, 8 of the 14 money fields then modelled changed representation across only six
        // rows; re-measured 2026-09-01 across all 3,425 rows, 18 of the 25 numeric keys do, and the seven that
        // never do include five income fields that are zero everywhere. All 24 are decimal?, and this test
        // asserts one of each kind so typing any of them `int` fails here. The fixture is H000601's two rows
        // and carries none of the eleven keys added by #57; the census fixture in the next test does.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(2, rows.Count);
        Assert.Equal("H000601", rows[0].SenateId);
        Assert.Equal(2024, rows[0].Year);

        Assert.Equal(45074069.5m, rows[0].Total);                    // flipped
        Assert.Equal(97501.5m, rows[0].BusinessLiabilities);          // flipped
        Assert.Equal(12741527.5m, rows[0].Stock);                     // flipped
        Assert.Equal(5559509.5m, rows[0].CashAndCashEquivalents);     // flipped
        Assert.Equal(17006511.5m, rows[0].OwnershipInterest);         // flipped
        Assert.Equal(1075002.5m, rows[0].GovernmentSecurities);       // flipped
        Assert.Equal(14156520m, rows[0].MutualFundsAndEtfs);          // integral here, flips on other rows
        Assert.Equal(557502m, rows[0].RealEstate);                    // integral here, flips on other rows
        Assert.Equal(5250002m, rows[0].RevolvingAndCreditLines);      // integral on all six
        Assert.Equal(0m, rows[0].SalaryAndWages);
        Assert.Equal(1500001m, rows[0].RealEstateLiabilities);
        Assert.Equal(825001m, rows[0].OtherAssets);
        Assert.Equal(0m, rows[0].PensionAndRetirementAssets);
        Assert.Equal(0m, rows[0].Trusts);
    }

    [Fact]
    public void The_eleven_categories_one_member_never_showed_bind_from_the_census_fixture()
    {
        // The shipped record was modelled from H000601's six rows, which carry exactly 16 of the 27 keys FMP
        // sends on this path. Measured 2026-09-01 across all 535 members, the other eleven are on 3,130 of
        // 3,425 rows. This fixture is nine real rows from six members, chosen so every one of the eleven
        // appears — and is non-zero wherever the population ever has it non-zero. A test that asserted 0m
        // alone could pass against a property that binds nothing.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(9, rows.Count);

        // G000581 carries 21 keys — the most of any member — and the six he lacks are exactly these. A
        // property absent from this list is one that bound; a property missing from the record altogether
        // would not appear here either, which is why the value assertions below are not optional.
        Assert.Equal("G000581", rows[0].SenateId);
        Assert.Equal(2024, rows[0].Year);
        Assert.Equal(
            ["AssetBackedSecurities", "BusinessLiabilities", "InvestmentAndCapitalGains", "Options",
             "SalaryAndWages", "SpousalIncome"],
            Binding.Unbound(rows[0]));

        Assert.Equal(32000m, rows[0].Other);                          // asset on this row: total reconciles
        Assert.Equal(0m, rows[0].BusinessAndSelfEmployment);          // income: zero on every row measured
        Assert.Equal(0m, rows[0].PensionAndRetirementIncome);
        Assert.Equal(0m, rows[0].OtherIncome);                        // income: zero on every row measured
        Assert.Equal(0m, rows[0].PersonalLiabilities);
        Assert.Equal(0m, rows[0].EducationLiabilities);
        Assert.Equal(0m, rows[0].OtherLiabilities);

        Assert.Equal(37500000m, rows[1].PersonalLiabilities);         // G000581 2023
        Assert.Equal(175000m, rows[2].OtherLiabilities);              // G000581 2022
        Assert.Equal(75000m, rows[3].EducationLiabilities);           // G000581 2017

        Assert.Equal(15325011m, rows[4].AssetBackedSecurities);       // K000375 2021 — on 42 rows in the census
        Assert.Equal(193523m, rows[4].Other);

        Assert.Equal(48500m, rows[5].Options);                        // M001160 2021 — on 66 rows

        Assert.Equal(0m, rows[6].SpousalIncome);                      // Q000023 2024 — never non-zero, on 153 rows
        Assert.DoesNotContain("SpousalIncome", Binding.Unbound(rows[6]));

        Assert.Equal(0m, rows[7].InvestmentAndCapitalGains);          // C001061 2024 — never non-zero, on 100 rows
        Assert.DoesNotContain("InvestmentAndCapitalGains", Binding.Unbound(rows[7]));

        Assert.Equal(289473.83m, rows[8].PensionAndRetirementIncome); // S001145 2018 — one of four non-zero rows
        Assert.Equal(8000m, rows[8].Other);                           // liability on this row: -169999 needs it subtracted
    }

    // ---- the catch-all (#57) --------------------------------------------------------------------------------

    [Fact]
    public void A_category_the_type_does_not_name_lands_in_UnmappedFields_under_its_wire_spelling()
    {
        // The twenty-eighth key. Three counts of this type — 16, 25, 27 — were each drawn from a sample and
        // each wrong, so the next one must be visible rather than dropped. Deserialised through the context's
        // list type info, not a bare converter call, because that is the path the endpoint uses and the
        // converter only helps if the source generator actually routes through it.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":42,"stock":40,"cryptocurrency":2}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(40m, row.Stock);
        var (name, value) = Assert.Single(row.UnmappedFields);
        Assert.Equal("cryptocurrency", name);
        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        Assert.Equal(2m, value.GetDecimal());
    }

    [Fact]
    public void A_string_the_type_does_not_name_does_not_cost_the_response()
    {
        // The reason the catch-all is JsonElement and not decimal. The likeliest unmodelled key is not a 25th
        // money bucket but an envelope field copied from senate-net-worth, where formType, filingDate and
        // link are strings on all 67,801 rows. A decimal dictionary would throw here and lose every row.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":1,"formType":"Annual Report","filingDate":"2025-05-15"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(1m, row.Total);
        Assert.Equal(2, row.UnmappedFields.Count);
        Assert.Equal("Annual Report", row.UnmappedFields["formType"].GetString());
        Assert.Equal("2025-05-15", row.UnmappedFields["filingDate"].GetString());
    }

    [Fact]
    public void UnmappedFields_is_empty_and_never_null_on_every_census_row()
    {
        // Two claims in one: no named key leaks into the catch-all (if the converter's name table misspelt
        // `stock`, the real `stock` would land here), and an object with nothing unrecognised binds an empty
        // dictionary rather than null. Every row measured 2026-09-01 binds an empty one.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json"),
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        Assert.Equal(9, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.UnmappedFields);
            Assert.Empty(row.UnmappedFields);
        });
    }

    [Fact]
    public void The_converters_name_table_and_the_JsonPropertyName_attributes_agree()
    {
        // The attributes are documentation once the converter owns the binding — the generated binder no
        // longer reads them. So they can drift from the converter's table, and this pins them together from
        // both sides: every key in a fixture that carries all 27 is a [JsonPropertyName] on the type (the
        // attributes cover the wire), and none of those keys reaches UnmappedFields (the converter binds
        // every attributed name — asserted in the test above, and re-asserted here on the same fixture so
        // this test stands on its own).
        var fixture = Binding.Fixture("congress-senate-net-worth-aggregated-all-keys.json");
        using var document = JsonDocument.Parse(fixture);
        var wireKeys = document.RootElement
            .EnumerateArray()
            .SelectMany(row => row.EnumerateObject().Select(p => p.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var attributed = typeof(SenateNetWorthSummary)
            .GetProperties()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(27, wireKeys.Count);
        Assert.Equal(wireKeys, attributed);

        var rows = JsonSerializer.Deserialize(fixture, FmpJsonContext.Default.ListSenateNetWorthSummary)!;
        Assert.All(rows, row => Assert.Empty(row.UnmappedFields));
    }

    [Fact]
    public void A_named_money_field_reads_a_numeric_string_as_the_context_would()
    {
        // FmpJsonContext sets AllowReadingFromString for every model, and a hand-written converter bypasses
        // it. No row measured 2026-09-01 sent a string here, but the context-wide setting exists because FMP
        // flips number representation elsewhere, and the typed members must not be the one place it is off.
        var rows = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":"2024","total":"12.5","stock":"7"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal(2024, row.Year);
        Assert.Equal(12.5m, row.Total);
        Assert.Equal(7m, row.Stock);
        Assert.Empty(row.UnmappedFields);
    }

    [Fact]
    public void A_named_money_field_given_a_non_numeric_string_throws_as_the_context_would()
    {
        // Parity in the other direction. The generated binder throws JsonException on "n/a" in a decimal?
        // slot, and reading it as null or zero here would make the typed members quietly more lenient than
        // every other model in the SDK. A non-numeric `stock` is a defect worth hearing about.
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"stock":"n/a"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary));

        Assert.Contains("stock", ex.Message);
    }

    [Fact]
    public void A_named_money_field_given_a_padded_numeric_string_throws_as_the_context_would()
    {
        // The generated binder's AllowReadingFromString takes JSON number grammar and nothing more: a leading
        // sign and an exponent, no whitespace. NumberStyles.Float would have accepted " 5 " here and made the
        // typed members the one place in the SDK that is quietly more lenient than the context.
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"stock":" 5 "}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary));

        Assert.Contains("stock", ex.Message);
    }

    [Fact]
    public void A_row_that_is_not_an_object_throws()
    {
        // The converter's first guard. Reachable through the public path — FMP has never sent it, but a
        // guard with no test is a guard nobody knows is there.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """[[]]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary));
    }

    [Fact]
    public void A_non_string_senateID_throws_as_the_context_would()
    {
        // ReadText's non-string arm. The generated binder does not coerce a number into a string? slot, and
        // neither does this.
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            """[{"senateID":5,"year":2024}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary));

        Assert.Contains("senateID", ex.Message);
    }

    [Fact]
    public void A_named_key_in_a_different_case_binds_its_property_not_the_catch_all()
    {
        // PropertyNameCaseInsensitive = true on the context, re-implemented by the converter. `Other` is the
        // one key on this path that is not camelCase; if FMP ever re-cases it, every other model in the SDK
        // would still bind it and this one must too. A null binds null rather than throwing, for the same
        // parity reason.
        var rows = JsonSerializer.Deserialize(
            """[{"SENATEID":"X000001","Year":2024,"other":5,"STOCK":9,"trusts":null}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(rows);
        Assert.Equal("X000001", row.SenateId);
        Assert.Equal(2024, row.Year);
        Assert.Equal(5m, row.Other);
        Assert.Equal(9m, row.Stock);
        Assert.Null(row.Trusts);
        Assert.Empty(row.UnmappedFields);
    }

    [Fact]
    public void A_row_survives_a_round_trip_with_its_typed_values_and_its_unmapped_keys()
    {
        // The write path exists for symmetry with FinancialReportJsonConverter and must not lose a member.
        // Null members are skipped on write because absence and null bind identically on read, so the
        // comparison is on values rather than on bytes.
        var original = JsonSerializer.Deserialize(
            """[{"senateID":"X000001","year":2024,"total":42,"stock":40,"mutualFundsAndETFs":3,"Other":2,"cryptocurrency":2,"formType":"Annual Report"}]""",
            FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var json = JsonSerializer.Serialize(original, FmpJsonContext.Default.ListSenateNetWorthSummary);
        var again = JsonSerializer.Deserialize(json, FmpJsonContext.Default.ListSenateNetWorthSummary)!;

        var row = Assert.Single(again);
        Assert.Equal("X000001", row.SenateId);
        Assert.Equal(2024, row.Year);
        Assert.Equal(42m, row.Total);
        Assert.Equal(40m, row.Stock);
        Assert.Equal(3m, row.MutualFundsAndEtfs);
        Assert.Equal(2m, row.Other);
        Assert.Null(row.Trusts);
        Assert.Equal(2, row.UnmappedFields.Count);
        Assert.Equal(2m, row.UnmappedFields["cryptocurrency"].GetDecimal());
        Assert.Equal("Annual Report", row.UnmappedFields["formType"].GetString());
        Assert.DoesNotContain("trusts", json, StringComparison.Ordinal);
        // The read side is case-insensitive, so a mis-cased entry in the WRITE table would round-trip green and
        // ship wrong casing to anyone re-serialising. This is the one wire name whose casing is not the
        // camelCase of its property name.
        Assert.Contains("\"mutualFundsAndETFs\":", json, StringComparison.Ordinal);
    }

    // ---- the request surface -----------------------------------------------------------------------------

    [Fact]
    public async Task By_member_sends_senateID_and_never_id()
    {
        // THE trap of this slice's request surface. Measured 2026-08-29, `stable/house-trades-by-id` is named
        // for a parameter it does not accept: `?id=M001217` came back BYTE-IDENTICAL to the bare call — 100
        // rows spanning 21 different members, HTTP 200, no error. The wire parameter is `senateID`.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetHouseTradesByMemberAsync("M001217");

        var query = handler.Requests[0].Query;
        Assert.Contains("senateID=M001217", query);
        Assert.DoesNotContain("id=M001217", query.Replace("senateID=M001217", ""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task By_member_refuses_a_missing_id_before_it_reaches_the_wire(string? senateId)
    {
        // The endpoint ANSWERS without the parameter, with someone else's data. That is exactly why the SDK
        // must not pass a blank through: FMP's willingness to reply is the hazard.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => endpoints.GetSenateTradesByMemberAsync(senateId!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_latest_feeds_refuse_a_limit_above_the_measured_cap()
    {
        // Measured 2026-08-29: limit=1000 and limit=5000 each answered exactly 250 with HTTP 200 and nothing
        // in the body saying the request was trimmed. A caller who asks for 1000 and pages by 1000 reads a
        // quarter of the feed and is never told.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        var thrown = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHouseLatestAsync(limit: CongressEndpoints.MaxCongressionalTradePageSize + 1));

        Assert.Equal("limit", thrown.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task The_four_filtered_trade_paths_page_like_the_latest_feeds()
    {
        // Measured 2026-09-02: house-trades?symbol=AAPL holds 513 rows and a bare call answers 100 of them with
        // nothing in the body saying so; senate-trades-by-id answers 100 of M001243's 145. `limit` is honoured
        // up to 250 — 251 and 1000 both answered 250 on house-trades and senate-trades, the cap the latest feeds
        // measured — and `page` is a page index over it. Sent explicitly, as the latest feeds send theirs.
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetHouseTradesAsync("AAPL");
        await endpoints.GetSenateTradesAsync("AAPL", page: 2, limit: CongressEndpoints.MaxCongressionalTradePageSize);
        await endpoints.GetHouseTradesByMemberAsync("P000197", page: 1);
        await endpoints.GetSenateTradesByMemberAsync("M001243", limit: 5);

        Assert.Equal("?symbol=AAPL&page=0&limit=100", handler.Requests[0].Query);
        Assert.Equal("?symbol=AAPL&page=2&limit=250", handler.Requests[1].Query);
        Assert.Equal("?senateID=P000197&page=1&limit=100", handler.Requests[2].Query);
        Assert.Equal("?senateID=M001243&page=0&limit=5", handler.Requests[3].Query);
    }

    [Fact]
    public async Task The_filtered_trade_paths_share_the_latest_feeds_paging_guard()
    {
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHouseTradesAsync("AAPL", page: -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetSenateTradesAsync("AAPL", limit: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetHouseTradesByMemberAsync(
                "P000197", limit: CongressEndpoints.MaxCongressionalTradePageSize + 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetSenateTradesByMemberAsync("M001243", page: -1));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_empty_criteria_sends_nothing_and_gets_fmp_s_default_page()
    {
        // Nothing on the wire is the request the 2026-08-29 fixtures were captured with. What that default IS
        // differs by path: positions answers 300 of 8,227 rows, and profile answers 500 of the 535 ACTIVE members
        // — measured 2026-09-02 the bare answer is byte-identical to active=true.
        var (endpoints, handler) = Build(StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetPositionsAsync(new CongressPositionCriteria());
        await endpoints.GetProfilesAsync(new CongressProfileCriteria());

        Assert.Equal("", handler.Requests[0].Query);
        Assert.Equal("", handler.Requests[1].Query);
    }

    [Fact]
    public async Task Every_position_filter_reaches_the_wire_under_fmp_s_spelling()
    {
        // `senateID`, capital I-D, on this path as on the four trade paths. Measured 2026-09-02 the filters are
        // exact and case-sensitive — `republican` answers zero rows at HTTP 200 — so the spelling of the VALUE is
        // the caller's; the spelling of the NAME is pinned here.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetPositionsAsync(new CongressPositionCriteria
        {
            Party = "Republican", Position = "Senator", SenateId = "M001243", Page = 1, Limit = 5,
        });

        Assert.Equal("?party=Republican&position=Senator&senateID=M001243&page=1&limit=5",
            handler.Requests[0].Query);
    }

    [Fact]
    public async Task Every_profile_filter_reaches_the_wire_and_a_false_active_is_sent_rather_than_dropped()
    {
        // Measured 2026-09-02: the bare answer IS active=true, and active=false is the only road to the 720
        // former members. A false that fell out with the unset properties would answer the active 535 and look
        // like a filter that worked.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await endpoints.GetProfilesAsync(new CongressProfileCriteria
        {
            Active = false, LatestParty = "Independent", LatestPosition = "Vice President",
            SenateId = "M001243", Page = 1, Limit = 5,
        });

        Assert.Equal(
            "?active=false&latestParty=Independent&latestPosition=Vice%20President&senateID=M001243&page=1&limit=5",
            handler.Requests[0].Query);
    }

    [Fact]
    public async Task The_criteria_refuse_paging_fmp_would_clamp_or_answer()
    {
        // Measured 2026-09-02: limit=5000 answers 300 on positions and 500 on profile, silently — the same
        // "trimmed and not told" the latest feeds guard against at 250. A negative page is one more value FMP
        // answers rather than rejects.
        var (endpoints, handler) = Build(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetPositionsAsync(new CongressPositionCriteria { Page = -1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetPositionsAsync(new CongressPositionCriteria { Limit = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetPositionsAsync(new CongressPositionCriteria
                { Limit = CongressEndpoints.MaxCongressMemberPositionPageSize + 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetProfilesAsync(new CongressProfileCriteria
                { Limit = CongressEndpoints.MaxCongressMemberProfilePageSize + 1 }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => endpoints.GetProfilesAsync(null!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"),
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetHouseLatestAsync();
        await endpoints.GetSenateLatestAsync();
        await endpoints.GetHouseTradesAsync("AAPL");
        await endpoints.GetHouseTradesByNameAsync("Pelosi");
        await endpoints.GetPositionsAsync(new CongressPositionCriteria());
        await endpoints.GetNetWorthSummaryAsync("H000601");

        Assert.Equal("/stable/house-latest", handler.Requests[0].AbsolutePath);
        Assert.Equal("/stable/senate-latest", handler.Requests[1].AbsolutePath);
        Assert.Equal("/stable/house-trades", handler.Requests[2].AbsolutePath);
        Assert.Equal("/stable/house-trades-by-name", handler.Requests[3].AbsolutePath);
        Assert.Equal("/stable/senate-positions", handler.Requests[4].AbsolutePath);
        Assert.Equal("/stable/senate-net-worth-aggregated", handler.Requests[5].AbsolutePath);
        Assert.Contains("symbol=AAPL", handler.Requests[2].Query);
        Assert.Contains("name=Pelosi", handler.Requests[3].Query);
        Assert.Contains("senateID=H000601", handler.Requests[5].Query);
    }
}
