using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The filing row and the two date fields on it, checked against captures taken live 2026-08-28.
///
/// <para><b>The two dates arrive in the same format and mean different things.</b> Across 2,115 rows sampled
/// from three paths, <c>filingDate</c>'s time component was <c>00:00:00</c> in 2,115 of 2,115 cases — it is a
/// date wearing a dummy time. <c>acceptedDate</c> was 19 characters in all 2,115 and is a real EDGAR wall clock
/// in US Eastern. Reading either with the other's converter compiles, binds, and is wrong by hours or by a
/// meaningless midnight.</para></summary>
public class SecFilingsTests
{
    // ---- the filingDate converter ------------------------------------------------------------------------------

    [Fact]
    public void A_filing_date_loses_its_dummy_midnight()
    {
        var row = JsonSerializer.Deserialize(
            """[{"filingDate":"2025-03-06 00:00:00"}]""", FmpJsonContext.Default.ListSecFiling)![0];

        Assert.Equal(new LocalDate(2025, 3, 6), row.FilingDate);
    }

    [Fact]
    public void A_filing_date_that_is_null_or_unreadable_costs_one_field_not_the_row()
    {
        // House rule for every date converter in this file: a single bad stamp must not abort the response and
        // take the other seven fields with it. The bare-ISO case is NOT a measured wire form — 2,115 of 2,115
        // rows carried the time — it is here to pin that an unexpected shape reads as null rather than throwing.
        var rows = JsonSerializer.Deserialize(
            """
            [{"symbol":"A","filingDate":null},
             {"symbol":"B","filingDate":""},
             {"symbol":"C","filingDate":"2025-03-06"}]
            """, FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Null(r.FilingDate));
        Assert.Equal("C", rows[2].Symbol);
    }

    // ---- binding -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_captured_eight_k_row_binds_seven_of_its_eight_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(5, rows.Count);
        // hasFinancials is explicitly null on all five: measured 2026-08-28 over 1,000 sec-filings-8k rows it was
        // null 107 times, false 725 and true 168, so a null here is the field FMP sent, not a field it omitted.
        Assert.Equal(["HasFinancials"], Binding.Unbound(rows[0]));
        Assert.Equal("SUNE", rows[0].Symbol);
        Assert.Equal("0000022701", rows[0].Cik);
        Assert.Equal("8-K", rows[0].FormType);
        Assert.Null(rows[0].HasFinancials);
        Assert.EndsWith("0000897101-24-000091-index.htm", rows[0].Link);
        Assert.EndsWith("pegy240248_8k.htm", rows[0].FinalLink);
    }

    [Fact]
    public void The_accepted_date_is_read_as_eastern_wall_clock_not_as_utc()
    {
        // The silent one. 2024-03-01 falls before that year's DST switch, so Eastern is UTC-5 and
        // "2024-03-01 22:47:48" is 2024-03-02T03:47:48Z. Read with NullableFmpInstantJsonConverter — the UTC twin,
        // one identifier away and the same wire format — every value would land five hours early, still sort
        // correctly, and still look entirely plausible.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 47, 48), rows[0].AcceptedDate);
        Assert.Equal(Instant.FromUtc(2024, 3, 2, 3, 27, 32), rows[2].AcceptedDate);
    }

    [Fact]
    public void Filing_date_cannot_be_derived_from_accepted_date()
    {
        // The trap, in one response. Rows 1 and 2 were accepted at 22:47 and 22:45 on 2024-03-01 and carry a
        // filingDate of 2024-03-04. Rows 3 to 5 were accepted at 22:27 and 22:22 the same evening and carry a
        // filingDate of 2024-03-01. Same endpoint, same page, same acceptance hour, two different answers — so
        // neither field is computable from the other, and a caller filtering on the wrong one is not told.
        //
        // It matters because `from` and `to` filter acceptedDate, NOT filingDate: measured 2026-08-28,
        // sec-filings-financials over 2025-03-01..2025-03-05 answered 722 rows, of which 16 carried a filingDate
        // past the requested `to` — and all 16 of those carried an acceptedDate inside it, with zero rows in the
        // whole response falling outside. 722 is comfortably under the 1,000 cap, so truncation cannot explain it.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("sec-filings-8k.head.json"), FmpJsonContext.Default.ListSecFiling)!;

        var acceptedOn = new LocalDate(2024, 3, 1);
        Assert.All(rows, r => Assert.Equal(acceptedOn, r.AcceptedDate!.Value.InZone(
            DateTimeZoneProviders.Tzdb["America/New_York"]).Date));

        Assert.Equal(new LocalDate(2024, 3, 4), rows[0].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 4), rows[1].FilingDate);
        Assert.Equal(new LocalDate(2024, 3, 1), rows[2].FilingDate);
    }
}
