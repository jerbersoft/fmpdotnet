using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>A registrant and its SIC classification, from any of five paths across three facades:
/// <c>stable/all-industry-classification</c>, <c>stable/industry-classification-search</c>, and all three of
/// <c>stable/sec-filings-company-search/{symbol,cik,name}</c>.
///
/// <para><b>One record rather than five, because the five are the same data and not merely the same field
/// names.</b> Measured 2026-08-28: for CIK <c>0000070858</c>, <c>all-industry-classification</c> and
/// <c>sec-filings-company-search/cik</c> returned byte-identical values for all six non-address fields. Only
/// <see cref="BusinessAddress"/> differed, and only in encoding — see
/// <see cref="BusinessAddressJsonConverter"/>, which makes that difference invisible here.</para></summary>
public sealed record IndustryClassification
{
    /// <summary>The ticker, where the registrant has one.
    ///
    /// <para><b>The literal four-character string <c>"None"</c> stands in for "no ticker" on some rows</b>, rather
    /// than a JSON null — measured 2026-08-28 on <c>industry-classification-search?sicCode=3571</c>, where three
    /// of five rows read <c>"None"</c>, and on <c>sec-filings-company-search/name?company=Apple</c>, where four of
    /// five do. It is the same naive-formatting fault that produces the bracketed address: a Python <c>None</c>
    /// rendered into a string field. The SDK passes it through rather than translating it, because translating it
    /// would be asserting that FMP will never send a real security called <c>None</c>, and because a caller who
    /// filters on it can see what they are filtering.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The registrant's name as EDGAR spells it — <c>"BANK OF AMERICA CORP /DE/"</c>. Upper-cased on
    /// most rows and mixed-case on others; FMP passes EDGAR through and so does this.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters — <c>"0000320193"</c>.
    ///
    /// <para><see cref="string"/> rather than an integer type: the padding is what makes the value match EDGAR,
    /// and there is no round trip back to it once it is gone.</para></summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The Standard Industrial Classification code — <c>"6021"</c>.
    ///
    /// <para><b><see cref="string"/>, and the two endpoints that serve SIC codes disagree about their width.</b>
    /// Measured 2026-08-28: this path sends four characters, while
    /// <c>stable/standard-industrial-classification-list</c> sends <c>"100"</c> for SIC 0100
    /// ("AGRICULTURAL PRODUCTION-CROPS") — the same code space with the leading zero stripped. The SDK preserves
    /// what each endpoint sent and normalises neither, because normalising would mean choosing one of two
    /// spellings FMP itself does not reconcile. Blank on rows FMP has not classified — measured on four of five
    /// <c>sec-filings-company-search/name</c> rows.</para></summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The SIC code's label — <c>"NATIONAL COMMERCIAL BANKS"</c>. Blank wherever
    /// <see cref="SicCode"/> is.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The registrant's business address as one line — <c>"ONE APPLE PARK WAY, CUPERTINO CA 95014"</c>.
    ///
    /// <para><b>Normalised on the way in, because FMP sends this field in two encodings.</b> See
    /// <see cref="BusinessAddressJsonConverter"/> for the measurement, the target, and why the transform is
    /// textual rather than a parse. Not split into parts: nineteen of 1,000 sampled values carry a comma inside
    /// an element, so a structured address type would have to guess.</para></summary>
    [JsonPropertyName("businessAddress")]
    [JsonConverter(typeof(BusinessAddressJsonConverter))]
    public string? BusinessAddress { get; init; }

    /// <summary>The registrant's telephone number, in whatever form EDGAR holds it — <c>"7043868486"</c> and
    /// <c>"(408) 345-8886"</c> both appear in the first five rows. Unnormalised on purpose.</summary>
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }
}

/// <summary>One row of <c>stable/standard-industrial-classification-list</c> — the SIC vocabulary itself, and
/// the SEC review office that owns each code.
///
/// <para>Named for the <see cref="CikEntry"/> precedent: a reference-list row that is an entry in a vocabulary
/// rather than a thing in the market. Measured 2026-08-28, the endpoint answers all <b>444</b> rows for every
/// combination of <c>page</c> and <c>limit</c> tried — see
/// <see cref="Endpoints.DirectoryEndpoints.GetSicCodesAsync(CancellationToken)"/>.</para></summary>
public sealed record SicCodeEntry
{
    /// <summary>The SEC review office that handles filings under this code — <c>"Office of Life Sciences"</c>.
    /// Present on every one of the 444 rows measured 2026-08-28.</summary>
    [JsonPropertyName("office")] public string? Office { get; init; }

    /// <summary>The SIC code, <b>with any leading zero stripped</b> — <c>"100"</c> is SIC 0100.
    ///
    /// <para>That is not this SDK's doing and is not corrected here: <see cref="IndustryClassification.SicCode"/>
    /// carries the same code space four characters wide on a different endpoint, measured the same day. A caller
    /// joining the two must pad, and this documentation is where they find that out rather than in a lookup that
    /// silently matches nothing.</para></summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The code's label — <c>"AGRICULTURAL PRODUCTION-CROPS"</c>.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }
}
