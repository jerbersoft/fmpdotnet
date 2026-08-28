using System.Text.Json;
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;

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

    // ---- fmp.Directory -----------------------------------------------------------------------------------------

    private static (DirectoryEndpoints Endpoints, StubHandler Handler) BuildDirectory(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new DirectoryEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task One_page_of_classifications_sends_page_zero_and_the_limit()
    {
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("all-industry-classification.head.json")));

        var rows = await endpoints.GetIndustryClassificationsAsync(limit: 5);

        Assert.Equal(5, rows.Count);
        var uri = handler.Requests.Single();
        Assert.Equal("/stable/all-industry-classification", uri.AbsolutePath);
        Assert.Contains("page=0", uri.Query);
        Assert.Contains("limit=5", uri.Query);
    }

    [Fact]
    public async Task The_whole_universe_is_reached_by_sending_page_one_and_no_limit()
    {
        // The anomaly this method exists for, measured 2026-08-28. page=0 honours `limit` but caps at 1,000 rows,
        // and the dataset is 25,952 — so rows 1,001 onward are reachable only through page>=1, which ignores
        // `limit` entirely and answers the whole universe. page=1, page=2, page=1&limit=10 and page=1 with no
        // limit all returned the same 25,952 rows and the same 7,288,535 bytes, byte-identical.
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("all-industry-classification.head.json")));

        await endpoints.GetAllIndustryClassificationsAsync();

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/all-industry-classification", uri.AbsolutePath);
        Assert.Contains("page=1", uri.Query);
        Assert.DoesNotContain("limit=", uri.Query);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(5000)]
    [InlineData(30000)]
    public async Task A_limit_above_the_measured_cap_is_refused_rather_than_clamped_by_fmp(int limit)
    {
        // Measured 2026-08-28: limit=1000, 5000, 26000 and 30000 all answered exactly 1,000 rows on page 0, with
        // HTTP 200 and nothing in the body to say the request had been trimmed. A caller who asked for 5,000 and
        // believed they had it would be short by four fifths and never told.
        var (endpoints, handler) = BuildDirectory(StubHandler.Json("[]"));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustryClassificationsAsync(limit));

        Assert.Equal("limit", error.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_non_positive_limit_is_refused(int limit)
    {
        var (endpoints, handler) = BuildDirectory(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => endpoints.GetIndustryClassificationsAsync(limit));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void The_classification_cap_is_the_measured_one()
    {
        Assert.Equal(1000, DirectoryEndpoints.MaxIndustryClassificationPageSize);
    }

    [Fact]
    public async Task A_limit_exactly_at_the_measured_cap_succeeds_rather_than_being_refused()
    {
        // Task 2's review found the gap this closes: nothing asserted that the documented maximum itself is
        // accepted. ThrowIfGreaterThan is correct, but ThrowIfGreaterThanOrEqual would pass every other test
        // here while silently rejecting the one value callers are told is safe to send.
        var (endpoints, handler) = BuildDirectory(StubHandler.Json("[]"));

        await endpoints.GetIndustryClassificationsAsync(DirectoryEndpoints.MaxIndustryClassificationPageSize);

        Assert.Contains("limit=1000", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task The_sic_list_takes_no_parameters_at_all()
    {
        // Measured 2026-08-28: the endpoint answered all 444 rows for every combination of page and limit tried,
        // so a `limit` parameter would be a control that controls nothing.
        var (endpoints, handler) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("standard-industrial-classification-list.head.json")));

        var rows = await endpoints.GetSicCodesAsync();

        Assert.Equal(5, rows.Count);
        Assert.Empty(Binding.Unbound(rows[0]));
        Assert.Equal("Office of Life Sciences", rows[0].Office);
        Assert.Equal("100", rows[0].SicCode);
        Assert.Equal("AGRICULTURAL PRODUCTION-CROPS", rows[0].IndustryTitle);

        var uri = handler.Requests.Single();
        Assert.Equal("/stable/standard-industrial-classification-list", uri.AbsolutePath);
        Assert.Equal("?apikey=k", uri.Query);
    }

    [Fact]
    public async Task The_sic_list_strips_a_leading_zero_that_the_classification_paths_keep()
    {
        // The join trap, pinned. SIC 0100 is "AGRICULTURAL PRODUCTION-CROPS"; this endpoint calls it "100" while
        // all-industry-classification carries four-character codes. A caller joining the two on string equality
        // silently matches nothing for every code below 1000, and nothing in either payload says why.
        var (endpoints, _) = BuildDirectory(
            StubHandler.Json(Binding.Fixture("standard-industrial-classification-list.head.json")));

        var rows = await endpoints.GetSicCodesAsync();

        Assert.All(rows, r => Assert.Equal(3, r.SicCode!.Length));
    }

    // ---- fmp.Search --------------------------------------------------------------------------------------------

    private static (SearchEndpoints Endpoints, StubHandler Handler) BuildSearch(
        params HttpResponseMessage[] responses)
    {
        var handler = new StubHandler(responses);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new SearchEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))),
            handler);
    }

    [Fact]
    public async Task Classification_search_sends_only_the_values_it_was_given()
    {
        var (endpoints, handler) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal(5, rows.Count);
        var uri = handler.Requests.Single();
        Assert.Equal("/stable/industry-classification-search", uri.AbsolutePath);
        Assert.Contains("sicCode=3571", uri.Query);
        Assert.DoesNotContain("symbol=", uri.Query);
        Assert.DoesNotContain("cik=", uri.Query);
    }

    [Fact]
    public async Task Classification_search_sends_all_three_when_all_three_are_given()
    {
        // Measured 2026-08-28: symbol=AAPL, cik=320193 and sicCode=3571 together answered 1 row, so the three
        // narrow the result rather than conflicting. That is what makes an all-optional signature safe.
        var (endpoints, handler) = BuildSearch(StubHandler.Json("[]"));

        await endpoints.FindIndustryClassificationAsync("AAPL", "320193", "3571");

        var uri = handler.Requests.Single();
        Assert.Contains("symbol=AAPL", uri.Query);
        Assert.Contains("cik=320193", uri.Query);
        Assert.Contains("sicCode=3571", uri.Query);
    }

    [Fact]
    public async Task Classification_search_refuses_an_empty_query_before_spending_a_call()
    {
        // FMP answers a bare call with HTTP 400 and "Please enter at least one search value: cik, sicCode, or
        // symbol." (measured 2026-08-28). Raising it here costs nothing and says the same thing at the call site.
        var (endpoints, handler) = BuildSearch(StubHandler.Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(() => endpoints.FindIndustryClassificationAsync());
        await Assert.ThrowsAsync<ArgumentException>(
            () => endpoints.FindIndustryClassificationAsync("  ", "", null));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Classification_search_carries_the_literal_None_symbol_through_unchanged()
    {
        // Three of the five captured rows read "None" in `symbol` — a Python None rendered into a string field,
        // the same naive-formatting fault that produces the bracketed address. The SDK does not translate it to
        // null: doing so would assert FMP will never list a security called None, and would hide the fault from
        // the caller who has to decide what to do about it.
        var (endpoints, _) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal("AAPL", rows[0].Symbol);
        Assert.Equal("None", rows[2].Symbol);
        Assert.Equal(3, rows.Count(r => r.Symbol == "None"));
    }

    [Fact]
    public async Task The_search_path_sends_the_bracketed_address_too_and_it_is_normalised()
    {
        // Two of the five IndustryClassification paths bracket, not one: this and all-industry-classification.
        // Measured 2026-08-28 on both ?symbol=AAPL and ?sicCode=3571.
        var (endpoints, _) = BuildSearch(
            StubHandler.Json(Binding.Fixture("industry-classification-search.sic3571.json")));

        var rows = await endpoints.FindIndustryClassificationAsync(sicCode: "3571");

        Assert.Equal("ONE APPLE PARK WAY, CUPERTINO CA 95014", rows[0].BusinessAddress);
    }
}
