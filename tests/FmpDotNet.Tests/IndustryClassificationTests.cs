using System.Text.Json;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>The seven-field company row FMP serves from five paths across three facades, and the one field on
/// it that arrives in two encodings.
///
/// <para>Measured 2026-08-28: for CIK <c>0000070858</c> (Bank of America), <c>all-industry-classification</c> and
/// <c>sec-filings-company-search/cik</c> returned byte-identical values for <c>symbol</c>, <c>name</c>,
/// <c>cik</c>, <c>sicCode</c>, <c>industryTitle</c> and <c>phoneNumber</c>. Only <c>businessAddress</c> differed,
/// and only in encoding — which is what makes one record right for all five rather than five records that happen
/// to share field names.</para></summary>
public class IndustryClassificationTests
{
    // ---- the address converter ---------------------------------------------------------------------------------

    [Fact]
    public void The_bracketed_encoding_becomes_the_joined_one()
    {
        // FMP publishes the normalisation target itself: measured 2026-08-28 on five randomly sampled CIKs,
        // `", ".join(parts)` of the bracketed value matched the sibling path's plain string exactly, 5 of 5.
        Assert.Equal(
            "BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255",
            BusinessAddressJsonConverter.Normalise(
                "['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']"));
    }

    [Fact]
    public void An_apostrophe_inside_an_element_survives_because_the_transform_is_textual()
    {
        // The row that rules out parsing. Of 1,000 bracketed values sampled 2026-08-28, 999 parse as a Python
        // literal and this one does not: XI'AN carries an unescaped apostrophe inside a single-quoted repr, so
        // the string was built by naive formatting rather than by a serialiser. Every Xi'an, O'Brien and L'Oreal
        // reproduces it, so this is a class of row and not one bad row. Splitting on "', '" is unbothered by it,
        // because the apostrophe is not followed by a comma and a space.
        Assert.Equal(
            "NO. 65, LN, 114, XISHI RD., XI'AN VIL., TAICHUNG CITY  ",
            BusinessAddressJsonConverter.Normalise(
                "['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"));
    }

    [Fact]
    public void A_plain_string_is_returned_untouched()
    {
        // Three of the five paths never send the bracketed form. Measured 2026-08-28:
        // sec-filings-company-search/name answered 0 bracketed values in 976 rows.
        Assert.Equal(
            "ONE APPLE PARK WAY, CUPERTINO CA 95014",
            BusinessAddressJsonConverter.Normalise("ONE APPLE PARK WAY, CUPERTINO CA 95014"));
    }

    [Fact]
    public void A_null_stays_null_and_an_unrecognised_shape_passes_through()
    {
        // The converter never throws and never drops a value. Anything that is not bracketed at both ends is
        // returned as sent, which is what makes it safe on the three paths that never bracket.
        Assert.Null(BusinessAddressJsonConverter.Normalise(null));
        Assert.Equal("", BusinessAddressJsonConverter.Normalise(""));
        Assert.Equal("[]", BusinessAddressJsonConverter.Normalise("[]"));
        Assert.Equal("['unterminated", BusinessAddressJsonConverter.Normalise("['unterminated"));
    }

    [Fact]
    public void A_single_element_address_loses_its_brackets_and_nothing_else()
    {
        // One of the 1,000 sampled values had a single element; 737 had two, 229 three, 27 four and 5 five.
        Assert.Equal("PO BOX 1", BusinessAddressJsonConverter.Normalise("['PO BOX 1']"));
    }

    // ---- binding -----------------------------------------------------------------------------------------------

    [Fact]
    public void A_captured_row_binds_every_one_of_its_seven_fields()
    {
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("0Q16.L", rows[0].Symbol);
        Assert.Equal("BANK OF AMERICA CORP /DE/", rows[0].Name);
        Assert.Equal("0000070858", rows[0].Cik);
        Assert.Equal("6021", rows[0].SicCode);
        Assert.Equal("NATIONAL COMMERCIAL BANKS", rows[0].IndustryTitle);
        Assert.Equal("7043868486", rows[0].PhoneNumber);
    }

    [Fact]
    public void The_converter_is_wired_to_the_property_and_not_merely_written()
    {
        // The failure this guards is silent: without the [JsonConverter] attribute the property still binds, and
        // still carries an address — the bracketed one — so five paths would disagree about what the same field
        // means and nothing would throw.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.Equal("BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255", rows[0].BusinessAddress);
        Assert.Equal("240 GREENWICH STREET, 8TH FLOOR, NEW YORK NY 10286", rows[3].BusinessAddress);
        Assert.DoesNotContain(rows, r => r.BusinessAddress!.StartsWith("['", StringComparison.Ordinal));
    }

    [Fact]
    public void The_cik_keeps_its_leading_zeros()
    {
        // Ten characters, zero-padded. An integer type would destroy the padding that makes the value match
        // EDGAR, and there is no round trip back to it: 320193 could pad to any width.
        var rows = JsonSerializer.Deserialize(
            Binding.Fixture("all-industry-classification.head.json"),
            FmpJsonContext.Default.ListIndustryClassification)!;

        Assert.All(rows, r => Assert.Equal(10, r.Cik!.Length));
        Assert.StartsWith("0000", rows[0].Cik);
    }
}
