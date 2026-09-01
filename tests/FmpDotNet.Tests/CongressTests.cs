using System.Text.Json;
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
    public async Task Each_path_is_requested_at_the_url_it_lives_at()
    {
        var (endpoints, handler) = Build(
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"),
            StubHandler.Json("[]"), StubHandler.Json("[]"), StubHandler.Json("[]"));

        await endpoints.GetHouseLatestAsync();
        await endpoints.GetSenateLatestAsync();
        await endpoints.GetHouseTradesAsync("AAPL");
        await endpoints.GetHouseTradesByNameAsync("Pelosi");
        await endpoints.GetPositionsAsync();
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
