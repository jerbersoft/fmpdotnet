using System.Globalization;
using System.Text.Json;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The six Indexes paths, checked against captures taken live 2026-08-30.</summary>
public class IndexesTests
{
    [Fact]
    public void A_change_row_binds_all_seven_of_its_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(3, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal(new LocalDate(2026, 6, 29), rows[0].DateAdded);
        Assert.Equal("Alphabet Inc.", rows[0].AddedSecurity);
        Assert.Equal("VZ", rows[0].RemovedTicker);
        Assert.Equal("Verizon Communications Inc.", rows[0].RemovedSecurity);
        Assert.Equal(new LocalDate(2026, 6, 29), rows[0].Date);
        Assert.Equal("GOOGL", rows[0].Symbol);
        Assert.StartsWith("To better reflect", rows[0].Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void A_long_form_date_binds_under_any_culture(string culture)
    {
        // `dateAdded` is US long form with ENGLISH month names — "June 29, 2026" — on all 2,055 rows measured
        // 2026-08-30. A pattern built from the ambient culture parses none of them on a German or French
        // host: "June" is "Juni" there, and NodaTime answers a parse failure, which this file's converters
        // turn into null. The whole column would arrive empty in production and green in CI.
        //
        // WHAT THIS TEST CATCHES, stated exactly: an implementation that builds its pattern from
        // CultureInfo.CurrentCulture PER CALL fails here every time. One that builds a static pattern from
        // the current culture fails here only if this test runs before anything else touches the converter,
        // because a static pattern captures the culture at type-initialisation time. The invariant pattern
        // the converter actually uses is immune to both, which is the point.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal(culture, CultureInfo.CurrentCulture.Name);   // the setter must actually have taken

            var rows = JsonSerializer.Deserialize(
                Binding.Fixture("historical-dowjones-constituent.head.json"),
                FmpJsonContext.Default.ListIndexConstituentChange)!;

            Assert.Equal(new LocalDate(2026, 6, 29), rows[0].DateAdded);
            Assert.Equal(new LocalDate(2024, 11, 8), rows[1].DateAdded);
            Assert.Equal(new LocalDate(2020, 8, 31), rows[2].DateAdded);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Both_day_paddings_parse_because_the_wire_sends_both()
    {
        // Measured 2026-08-30 over historical-sp500-constituent alone: 213 rows carry a zero-padded
        // single-digit day and 407 carry an unpadded one. A pattern of "MMMM dd, yyyy" parses only the first
        // group and a pattern of "MMMM d, yyyy" parses BOTH, which is why the converter uses the latter.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(new LocalDate(2026, 8, 5), rows[0].DateAdded);   // "August 05, 2026" — padded
        Assert.Equal(new LocalDate(2025, 7, 9), rows[1].DateAdded);   // "July 9, 2025"    — unpadded
    }

    [Fact]
    public void dateAdded_and_date_are_read_separately()
    {
        // They disagree on 205 of 2,055 rows measured 2026-08-30 — 202 by exactly one day with `date` the
        // earlier — so deriving either from the other is wrong 205 times. The 1957 pair is the proof that
        // they are two facts and not one value rendered twice: identical `dateAdded`, different `date`.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal(new LocalDate(2025, 7, 9), rows[1].DateAdded);
        Assert.Equal(new LocalDate(2025, 7, 8), rows[1].Date);
        Assert.NotEqual(rows[1].DateAdded, rows[1].Date);

        Assert.Equal(rows[3].DateAdded, rows[4].DateAdded);           // both "March 04, 1957"
        Assert.Equal(new LocalDate(1957, 3, 3), rows[3].Date);
        Assert.Equal(new LocalDate(1957, 3, 4), rows[4].Date);
    }

    [Fact]
    public void The_dow_jones_feed_spells_absence_with_an_empty_string()
    {
        // 136 empty strings and ZERO JSON nulls across all 86 Dow Jones rows, measured 2026-08-30. An
        // implementer who tests only against this path never meets the other spelling, which is why the
        // sentinel converter is applied to all four text fields rather than to the ones that looked null.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Null(rows[2].AddedSecurity);                            // wire sent ""
        Assert.Equal("PFE", rows[2].RemovedTicker);
    }

    [Fact]
    public void The_sp500_feed_spells_absence_with_a_json_null_instead()
    {
        // 823 empty strings AND 20 JSON nulls across the same four fields on historical-sp500-constituent,
        // measured 2026-08-30; historical-nasdaq-constituent adds 83 and 8. Two spellings of one fact, and
        // which one arrives depends on the path. Both must land on null or a caller needs to know both.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-sp500-constituent.dates.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Null(rows[2].RemovedTicker);                            // wire sent JSON null
        Assert.Null(rows[2].RemovedSecurity);
        Assert.Null(rows[3].RemovedTicker);                            // wire sent ""
        Assert.Null(rows[3].Reason);
        Assert.Equal("Prologis", rows[2].AddedSecurity);
    }

    [Fact]
    public void A_row_is_an_addition_or_a_removal_and_symbol_names_whichever_it_is()
    {
        // Measured across 2,055 rows: never both, never neither. `symbol` follows the populated side, so a
        // caller reading `symbol` as "the security that joined" is wrong on every removal row.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("historical-dowjones-constituent.head.json"),
            FmpJsonContext.Default.ListIndexConstituentChange)!;

        Assert.Equal("GOOGL", rows[0].Symbol);                         // an addition: symbol is the joiner
        Assert.Equal("Alphabet Inc.", rows[0].AddedSecurity);

        Assert.Equal("PFE", rows[2].Symbol);                           // a removal: symbol is the leaver
        Assert.Null(rows[2].AddedSecurity);
        Assert.Equal("PFE", rows[2].RemovedTicker);
    }

    [Fact]
    public void The_long_form_converter_does_not_round_trip_a_zero_padded_day()
    {
        // Not a defect and not a TODO — a measured impossibility, pinned so nobody "fixes" it into a
        // pattern that stops parsing half the corpus. The wire sends BOTH paddings and no single NodaTime
        // pattern emits both, so Write normalises to the unpadded form. Read accepts either, so nothing is
        // lost on a round trip through this SDK; only the exact bytes differ.
        var row = JsonSerializer.Deserialize(
            """[{"dateAdded":"August 05, 2026"}]""",
            FmpJsonContext.Default.ListIndexConstituentChange)![0];

        Assert.Equal(new LocalDate(2026, 8, 5), row.DateAdded);
        Assert.Contains(
            "\"dateAdded\":\"August 5, 2026\"",
            JsonSerializer.Serialize(new List<Models.IndexConstituentChange> { row },
                FmpJsonContext.Default.ListIndexConstituentChange),
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_iso_date_in_dateAdded_would_not_parse_and_that_is_why_it_needs_its_own_converter()
    {
        // NullableLocalDateJsonConverter uses LocalDatePattern.Iso, which rejects "June 29, 2026" outright
        // and returns null rather than throwing — so reusing it here would have emptied the column with no
        // error anywhere. The inverse is true too, which this asserts: the long-form pattern does not accept
        // ISO. Neither converter can cover the other's path, and this record uses both, one per field.
        var row = JsonSerializer.Deserialize(
            """[{"dateAdded":"2026-06-29","date":"2026-06-29"}]""",
            FmpJsonContext.Default.ListIndexConstituentChange)![0];

        Assert.Null(row.DateAdded);
        Assert.Equal(new LocalDate(2026, 6, 29), row.Date);
    }
}
