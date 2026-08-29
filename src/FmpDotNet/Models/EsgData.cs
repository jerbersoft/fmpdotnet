using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One filing's environmental, social and governance scores. From
/// <c>stable/esg-disclosures</c>.
///
/// <para>One row per SEC filing rather than per period: measured 2026-08-29 on AAPL's full history the
/// rows are 10-Q, 10-K and the obsolete 10-K405 filings (see <see cref="FormType"/>), each carrying the
/// four scores as of that filing. <see cref="Date"/> is the period end and <see cref="AcceptedDate"/> is
/// when EDGAR accepted it, which is why the two differ by about a month.</para></summary>
public sealed record EsgDisclosure
{
    /// <summary>The period end the filing reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date EDGAR accepted the filing — later than <see cref="Date"/> on every row measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, <b>zero-padded to ten characters</b> — <c>0000320193</c>. A
    /// string, not a number: the leading zeros are significant and every other <c>cik</c> in this SDK is a
    /// string for the same reason.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as EDGAR carries it.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The EDGAR form type the scores were taken from — <c>10-Q</c>, <c>10-K</c>, or
    /// <c>10-K405</c>. Not a two-value set: measured 2026-08-29 on AAPL's full 130-row history (1993-12-31
    /// to 2026-06-27) the breakdown was 98 <c>10-Q</c>, 30 <c>10-K</c> and 2 <c>10-K405</c> — an obsolete
    /// EDGAR annual-report variant discontinued in 2003 that survives only in the older rows. A caller
    /// filtering on <c>formType is "10-Q" or "10-K"</c> silently drops those two rows.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The environmental score, 0 to 100.</summary>
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }

    /// <summary>The social score, 0 to 100.</summary>
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }

    /// <summary>The governance score, 0 to 100.</summary>
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }

    /// <summary>The composite score, 0 to 100. <b>Bound from the wire name <c>ESGScore</c></b>; the property
    /// is house-cased, as <c>cik</c> binds to <c>Cik</c>.</summary>
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }

    /// <summary>The EDGAR index page for the filing.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}

/// <summary>One company's ESG risk rating for one fiscal year. From <c>stable/esg-ratings</c>.
///
/// <para><b>Not returned in year order.</b> Measured 2026-08-29 on AAPL the first three rows were 1998, 2025
/// and 1994. Sort before presenting.</para></summary>
public sealed record EsgRating
{
    /// <summary>The ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters. See
    /// <see cref="EsgDisclosure.Cik"/>.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as EDGAR carries it.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The industry the rank below is against — <c>CONSUMER ELECTRONICS</c>. FMP's own
    /// vocabulary, uppercased, and <b>not</b> the list
    /// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync"/> serves — that one is title-cased
    /// (<c>Consumer Electronics</c>), so the two do not join without normalising.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>The fiscal year the rating is for.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The letter rating — <c>B</c>. Bound from the wire name <c>ESGRiskRating</c>. A string and
    /// not an enum: the full set of grades was not enumerated, and a closed C# enum over an open server-side
    /// vocabulary is a breaking change waiting for a Tuesday.</summary>
    [JsonPropertyName("ESGRiskRating")] public string? EsgRiskRating { get; init; }

    /// <summary><b>A sentence, not a number</b> — <c>"3 out of 9"</c>, <c>"19 out of 21"</c>, measured
    /// 2026-08-29. Typing this <see langword="int"/> is the obvious guess and it throws on every row. A
    /// caller who wants the two numbers parses them and owns the result; FMP does not send them
    /// separately.</summary>
    [JsonPropertyName("industryRank")] public string? IndustryRank { get; init; }
}

/// <summary>One sector's average ESG scores for one fiscal period. From <c>stable/esg-benchmark</c>.
///
/// <para><b><see cref="Sector"/> is on this record and not on the method that fetches it</b>, and the
/// asymmetry is deliberate: FMP <i>returns</i> the field and <i>ignores</i> the query parameter of the same
/// name. Measured 2026-08-29, <c>?sector=APPAREL RETAIL</c> was byte-identical to the bare call — 1003 rows
/// across 291 sectors. See <see cref="Endpoints.EsgEndpoints.GetBenchmarkAsync"/>.</para></summary>
public sealed record EsgBenchmark
{
    /// <summary>The fiscal year.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>The fiscal period — <c>Q1</c> through <c>Q4</c> or <c>FY</c>, both measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>The sector, in FMP's own uppercase vocabulary — <c>APPAREL RETAIL</c>,
    /// <c>MEDICAL - CARE FACILITIES</c>. 291 distinct values measured 2026-08-29. <b>Filter on this
    /// client-side</b>; the endpoint's <c>sector</c> parameter does nothing.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>The sector's average environmental score, 0 to 100.</summary>
    [JsonPropertyName("environmentalScore")] public decimal? EnvironmentalScore { get; init; }

    /// <summary>The sector's average social score, 0 to 100.</summary>
    [JsonPropertyName("socialScore")] public decimal? SocialScore { get; init; }

    /// <summary>The sector's average governance score, 0 to 100.</summary>
    [JsonPropertyName("governanceScore")] public decimal? GovernanceScore { get; init; }

    /// <summary>The sector's average composite score, 0 to 100. Bound from the wire name
    /// <c>ESGScore</c>.</summary>
    [JsonPropertyName("ESGScore")] public decimal? EsgScore { get; init; }
}
