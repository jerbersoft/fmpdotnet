using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One SC 13D/G beneficial-ownership disclosure — the filing an investor makes on crossing 5% of a
/// class — from <c>stable/acquisition-of-beneficial-ownership</c>.
///
/// <para><b>FMP files this path under Insider Trades; this SDK files it under institutional ownership.</b> The
/// reporting person is an entity — <c>"The Vanguard Group"</c>, <c>"General Star National Insurance
/// Company"</c> — the subject is a stake rather than a transaction, and the fields are voting and dispositive
/// power. It shares nothing with a Form 4 but the word "ownership". See
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints"/>.</para>
///
/// <para><b>Six of the fifteen fields arrive as JSON strings</b> — <c>"soleVotingPower": "0"</c>,
/// <c>"percentOfClass": "7.48"</c>. Across 422 rows measured 2026-08-28, every non-null value parsed as a
/// number: no <c>"N/A"</c>, no thousands separators. They are read with
/// <see cref="TolerantDecimalJsonConverter"/>, which binds null rather than throwing on anything it cannot
/// parse.</para></summary>
public sealed record BeneficialOwnership
{
    /// <summary>The <b>issuer's</b> Central Index Key, zero-padded — the company whose stock the stake is in,
    /// not the reporting person's.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the disclosure was filed. Bare ISO on this path.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date EDGAR accepted it. <b>A date, not a timestamp, on this path</b> — no time component
    /// arrives, and it was equal to <see cref="FilingDate"/> on every row measured.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The security's CUSIP. Spelled <c>cusip</c> here, not <c>securityCusip</c> as on
    /// <see cref="InstitutionalHolding.SecurityCusip"/> — the attribute is load-bearing.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>The filer — an institution, unnormalised, and the same institution appears under several
    /// spellings across years (<c>"The Vanguard Group"</c>, <c>"Vanguard Group - 23-1945930"</c>). Do not key
    /// on it.</summary>
    [JsonPropertyName("nameOfReportingPerson")] public string? NameOfReportingPerson { get; init; }

    /// <summary>Where the reporting person is organised — <c>"PENNSYLVANIA"</c>, and <c>"Pennsylvania"</c> on
    /// an older row. Case is not normalised.</summary>
    [JsonPropertyName("citizenshipOrPlaceOfOrganization")]
    public string? CitizenshipOrPlaceOfOrganization { get; init; }

    /// <summary>Shares the filer votes alone. <b>Arrives as a JSON string</b>; see the record's
    /// documentation.</summary>
    [JsonPropertyName("soleVotingPower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SoleVotingPower { get; init; }

    /// <summary>Shares the filer votes jointly. <b>Null on 1 of the 99 rows captured for AAPL</b> — the one
    /// place in this record where a quoted numeric is absent rather than <c>"0"</c>, which is why the
    /// converter's null handling is tested rather than assumed.</summary>
    [JsonPropertyName("sharedVotingPower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SharedVotingPower { get; init; }

    /// <summary>Shares the filer can dispose of alone.</summary>
    [JsonPropertyName("soleDispositivePower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SoleDispositivePower { get; init; }

    /// <summary>Shares the filer can dispose of jointly.</summary>
    [JsonPropertyName("sharedDispositivePower")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SharedDispositivePower { get; init; }

    /// <summary>Total shares beneficially owned. <b>Not necessarily the sum of the four powers above</b> — the
    /// captured 2015 row reports 332,239,563 against a sole-dispositive 322,573,028 and a shared-dispositive
    /// 9,666,535, which do sum, while the two 2026 rows report a total beside four zeroes. Nothing is derived
    /// here; all five are reported as sent.</summary>
    [JsonPropertyName("amountBeneficiallyOwned")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? AmountBeneficiallyOwned { get; init; }

    /// <summary>The stake as a percentage of the class — <c>7.48</c>. <c>"0"</c> occurs on rows where the filer
    /// reported the amount without the percentage.</summary>
    [JsonPropertyName("percentOfClass")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? PercentOfClass { get; init; }

    /// <summary>The SEC's reporting-person code — <c>"IA"</c> (investment adviser), <c>"IN"</c> (individual),
    /// <c>"EP"</c> (employee benefit plan). <b>Can carry more than one, comma-joined</b> — <c>"EP, IN"</c> on
    /// the captured 2015 row. Left as the string FMP sent rather than split, because the join is FMP's and
    /// splitting it would be a second unmeasured transform.</summary>
    [JsonPropertyName("typeOfReportingPerson")] public string? TypeOfReportingPerson { get; init; }

    /// <summary>The filing on EDGAR.</summary>
    [JsonPropertyName("url")] public string? Url { get; init; }
}
