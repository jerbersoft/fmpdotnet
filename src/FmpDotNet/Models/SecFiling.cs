using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One EDGAR filing, from any of five paths: <c>stable/sec-filings-8k</c>,
/// <c>stable/sec-filings-financials</c>, and all three of
/// <c>stable/sec-filings-search/{symbol,cik,form-type}</c>.
///
/// <para><b>One record with a nullable rather than two records.</b> The two feeds send eight fields; the three
/// search paths send the same seven minus <c>hasFinancials</c>. A second record would duplicate seven properties
/// to express one absence — see <see cref="HasFinancials"/>, where the absence is documented instead.</para>
///
/// <para><b>The two feeds differ by filter, not by shape.</b> Measured 2026-08-28 over 1,000 rows each:
/// <c>sec-filings-8k</c> returned <c>formType</c> <c>8-K</c> 1,000 times, while
/// <c>sec-filings-financials</c> returned <c>8-K</c> 861 times, <c>6-K</c> 137 and <c>10-K</c> twice. So one
/// filters by form and the other by whether financial data is attached.</para></summary>
public sealed record SecFiling
{
    /// <summary>The ticker FMP attributes the filing to. Two rows can share one filing — <c>SBC</c> and
    /// <c>SBCWW</c> in the captured page are the same accession number under two tickers.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters. <see cref="string"/> for the
    /// reason on <see cref="IndustryClassification.Cik"/>: the padding is the value.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The date EDGAR stamps the filing with.
    ///
    /// <para><b>A date, not a timestamp, and not derivable from <see cref="AcceptedDate"/>.</b> The wire sends
    /// <c>"2024-03-04 00:00:00"</c>; the time was <c>00:00:00</c> on 2,115 of 2,115 rows measured 2026-08-28, so
    /// it is discarded — see <see cref="NullableDateAtMidnightJsonConverter"/>. A filing accepted late in the
    /// evening may be stamped a later business day, and may not: in the five captured rows of one page,
    /// <c>SUNE</c> and <c>CGBDL</c> were accepted at 22:47 and 22:45 on 2024-03-01 and stamped 2024-03-04, while
    /// <c>SLE</c>, <c>SBCWW</c> and <c>SBC</c> were accepted at 22:27 and 22:22 the same evening and stamped
    /// 2024-03-01.</para>
    ///
    /// <para><b>This is not the field <c>from</c> and <c>to</c> filter on.</b> They filter
    /// <see cref="AcceptedDate"/>, so a response legitimately contains rows whose <c>FilingDate</c> falls outside
    /// the range you asked for — measured 2026-08-28, 16 of 722 rows over a five-day window. Those rows are not
    /// errors and are not dropped here.</para></summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The moment EDGAR accepted the submission.
    ///
    /// <para><b>Read as US Eastern wall clock, not UTC</b> — see
    /// <see cref="NullableEasternInstantJsonConverter"/>, which establishes the zone from a measured DST shift
    /// rather than assuming it. The UTC twin reads the identical wire format and would land every value four or
    /// five hours early, sorting correctly and looking plausible.</para>
    ///
    /// <para><b>This is the field <c>from</c> and <c>to</c> actually filter on</b>, which is why a response can
    /// carry rows whose <see cref="FilingDate"/> sits outside the requested range. Corroborated 2026-08-28 by the
    /// acceptance-hour distribution over 1,000 8-K rows: a spike of 434 at 16:00 — the post-close surge — and 63
    /// rows from 21:00 onward, which is exactly the population that can spill into a later
    /// <see cref="FilingDate"/>.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form type — <c>"8-K"</c>, <c>"6-K"</c>, <c>"10-K"</c>, <c>"4"</c>, <c>"25-NSE"</c>.
    ///
    /// <para>A raw <see cref="string"/> rather than an enum, for the reason
    /// <see cref="EconomicRelease.Impact"/> gives: a form type this SDK has never seen must not cost the caller
    /// the response. Three distinct values appeared in 1,000 rows of one endpoint alone, and EDGAR defines
    /// hundreds.</para></summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>Whether FMP has financial data attached to this filing.
    ///
    /// <para><b>Null means two different things, and which one depends on the path you called.</b> On the three
    /// <c>sec-filings-search/*</c> paths the field is <b>absent from the payload entirely</b> — measured
    /// 2026-08-28 — so null there means "this endpoint does not say". On <c>sec-filings-8k</c> the field is
    /// present and explicitly <c>null</c> on some rows (107 of 1,000), alongside <c>false</c> (725) and
    /// <c>true</c> (168), so null there is FMP's own answer.</para>
    ///
    /// <para>On <c>sec-filings-financials</c> it was <c>true</c> on 1,000 of 1,000 rows, which is what that
    /// endpoint selects on — so the field carries no information there.</para></summary>
    [JsonPropertyName("hasFinancials")] public bool? HasFinancials { get; init; }

    /// <summary>The EDGAR filing-index page for the accession.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The primary document itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
