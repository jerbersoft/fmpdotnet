using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The congressional-disclosure records and the facade that serves them, checked against captures
/// taken live 2026-08-29.</summary>
public class CongressTests
{
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
}
