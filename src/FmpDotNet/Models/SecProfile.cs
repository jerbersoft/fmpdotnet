using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>An SEC registrant's profile, from <c>stable/sec-profile</c>.
///
/// <para><b>Not a reuse of <see cref="CompanyProfile"/>, and the difference is the source rather than the
/// spelling.</b> That models <c>stable/profile</c>, which is market data: it carries <c>price</c>,
/// <c>marketCap</c>, <c>beta</c> and <c>volume</c>. This is the EDGAR registration record and carries
/// <c>taxIdentificationNumber</c>, <c>stateOfIncorporation</c> and <c>secFilingsUrl</c>. Sharing one record
/// would mean a caller could not tell which fields their answer actually had.</para>
///
/// <para><b>Thirty-five fields, of which all but four are JSON strings.</b> Measured 2026-08-28 across AAPL,
/// TSM, SHEL, BRK-B, NVO and SPY — every one returned exactly one row. The padded and unpadded forms of the CIK
/// were confirmed equivalent for AAPL only, CIK <c>0000320193</c>. The four boolean exceptions are
/// <see cref="IsActive"/>, <see cref="IsEtf"/>, <see cref="IsAdr"/> and <see cref="IsFund"/>.</para></summary>
public sealed record SecProfile
{
    /// <summary>The ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The SEC Central Index Key, zero-padded to ten characters. <see cref="string"/> for the reason on
    /// <see cref="IndustryClassification.Cik"/>.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The name the registrant files under — <c>"Apple Inc."</c>. Mixed case here, unlike the
    /// upper-cased <see cref="IndustryClassification.Name"/> the classification paths send.</summary>
    [JsonPropertyName("registrantName")] public string? RegistrantName { get; init; }

    /// <summary>The SIC code — <c>"3571"</c>. Blank on one of the six symbols sampled 2026-08-28.</summary>
    [JsonPropertyName("sicCode")] public string? SicCode { get; init; }

    /// <summary>The SIC code's label as this endpoint spells it — <c>"Electronic Computers"</c>, title case.
    /// <see cref="IndustryClassification.IndustryTitle"/> spells the same concept
    /// <c>"ELECTRONIC COMPUTERS"</c>; neither is normalised.</summary>
    [JsonPropertyName("sicDescription")] public string? SicDescription { get; init; }

    /// <summary>FMP's own grouping above the SIC code — <c>"Consumer Electronics"</c>. Not an EDGAR
    /// field.</summary>
    [JsonPropertyName("sicGroup")] public string? SicGroup { get; init; }

    /// <summary>The security's ISIN.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>The business address as one comma-joined line, <b>with the telephone number appended</b> —
    /// <c>"ONE APPLE PARK WAY,CUPERTINO CA 95014,(408) 996-1010"</c>.
    ///
    /// <para><b>Deliberately not put through <see cref="BusinessAddressJsonConverter"/>.</b> That converter
    /// serves the five <see cref="IndustryClassification"/> paths, which join with <c>", "</c> and do not append
    /// the phone. This endpoint joins with a bare <c>","</c> and does. Two different conventions, left as each
    /// was measured.</para></summary>
    [JsonPropertyName("businessAddress")] public string? BusinessAddress { get; init; }

    /// <summary>The mailing address, comma-joined and <b>without</b> the phone number. Frequently identical to
    /// <see cref="BusinessAddress"/> minus that suffix, but not guaranteed to be.</summary>
    [JsonPropertyName("mailingAddress")] public string? MailingAddress { get; init; }

    /// <summary>The registrant's telephone number, unnormalised.</summary>
    [JsonPropertyName("phoneNumber")] public string? PhoneNumber { get; init; }

    /// <summary>Postal code as EDGAR holds it — <c>"95014"</c> for a US filer, <c>"300096"</c> for a Taiwanese
    /// one. <see cref="string"/>, not a number: leading zeros are real in most of the world.</summary>
    [JsonPropertyName("postalCode")] public string? PostalCode { get; init; }

    /// <summary>City.</summary>
    [JsonPropertyName("city")] public string? City { get; init; }

    /// <summary>State or region code — <c>"CA"</c>, <c>"TPE"</c>.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>ISO country code — <c>"US"</c>, <c>"TW"</c>.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>FMP's prose description of the business. Long — the captured Apple value runs to about 2,400
    /// characters.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>The chief executive as EDGAR holds the name. Blank on one of the six symbols sampled
    /// 2026-08-28.</summary>
    [JsonPropertyName("ceo")] public string? Ceo { get; init; }

    /// <summary>The registrant's website.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>The exchange FMP attributes the security to — <c>"NASDAQ"</c>. A raw string rather than an enum,
    /// for the reason <see cref="Quote.Exchange"/> gives.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>Where the registrant is located, as a state or region code. Distinct from
    /// <see cref="StateOfIncorporation"/>: measured 2026-08-28, TSM reads <c>"TPE"</c> here and <c>"F5"</c>
    /// there.</summary>
    [JsonPropertyName("stateLocation")] public string? StateLocation { get; init; }

    /// <summary>Where the registrant is incorporated, in EDGAR's own state-code vocabulary — which includes
    /// non-US codes such as <c>"F5"</c>. Blank on one of the six symbols sampled 2026-08-28.</summary>
    [JsonPropertyName("stateOfIncorporation")] public string? StateOfIncorporation { get; init; }

    /// <summary>The fiscal year end as a <b>month and day with no year</b> — <c>"09-30"</c>.
    ///
    /// <para><see cref="string"/>, and that is the honest type. No date type holds a month and a day without a
    /// year, and choosing one would mean inventing the year — which every caller would then have to know to
    /// ignore. NodaTime's <c>AnnualDate</c> would fit the concept, but the wire value has not been measured
    /// against February 29 or against any malformed form, and a parse that throws would cost the caller the
    /// other 34 fields.</para></summary>
    [JsonPropertyName("fiscalYearEnd")] public string? FiscalYearEnd { get; init; }

    /// <summary>The IPO date — plain ISO, <c>"1980-12-12"</c>, unlike the space-separated stamps on
    /// <see cref="SecFiling"/>. Read with <see cref="NullableLocalDateJsonConverter"/>.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>Headcount.
    ///
    /// <para>The wire sends this <b>quoted</b> — <c>"166000"</c> — and it binds to <see cref="int"/> because
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = JsonNumberHandling.AllowReadingFromString</c> globally. No
    /// converter is needed and none is used.</para></summary>
    [JsonPropertyName("employees")] public int? Employees { get; init; }

    /// <summary>A ready-made EDGAR browse URL for this CIK.</summary>
    [JsonPropertyName("secFilingsUrl")] public string? SecFilingsUrl { get; init; }

    /// <summary>The IRS Employer Identification Number — <c>"94-2404110"</c>. Foreign filers carry the
    /// placeholder <c>"00-0000000"</c>, measured on TSM 2026-08-28, which is a value rather than an
    /// absence.</summary>
    [JsonPropertyName("taxIdentificationNumber")] public string? TaxIdentificationNumber { get; init; }

    /// <summary>The 52-week price range as <b>one formatted string</b> — <c>"225.95 - 344.57"</c>.
    ///
    /// <para><see cref="string"/> rather than two decimals. FMP does not pad: the same field reads
    /// <c>"225.63 - 479"</c> for TSM, measured the same day. Splitting on the separator would be the SDK
    /// asserting a format FMP has never promised, and the failure would be a null price rather than an
    /// error.</para></summary>
    [JsonPropertyName("fiftyTwoWeekRange")] public string? FiftyTwoWeekRange { get; init; }

    /// <summary>Whether FMP considers the registrant active. A real JSON boolean.</summary>
    [JsonPropertyName("isActive")] public bool? IsActive { get; init; }

    /// <summary>FMP's asset classification — <c>"stock"</c>.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>The OpenFIGI composite identifier — <c>"BBG000B9XRY4"</c>.</summary>
    [JsonPropertyName("openFigiComposite")] public string? OpenFigiComposite { get; init; }

    /// <summary>The currency the security is priced in — <c>"USD"</c>.</summary>
    [JsonPropertyName("priceCurrency")] public string? PriceCurrency { get; init; }

    /// <summary>FMP's market sector — <c>"Technology"</c>.</summary>
    [JsonPropertyName("marketSector")] public string? MarketSector { get; init; }

    /// <summary>The security type.
    ///
    /// <para><b>Null on all six symbols sampled 2026-08-28, and modelled anyway.</b> A field that always arrives
    /// empty is recorded and flagged rather than dropped: dropping it would make the day it starts arriving
    /// invisible, and the weekly smoke baseline records it as <c>null</c> today so that day is reported as
    /// drift.</para></summary>
    [JsonPropertyName("securityType")] public string? SecurityType { get; init; }

    /// <summary>Whether the security is an exchange-traded fund. A real JSON boolean.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>Whether the security is an American Depositary Receipt. A real JSON boolean — <c>true</c> for
    /// TSM, measured 2026-08-28.</summary>
    [JsonPropertyName("isAdr")] public bool? IsAdr { get; init; }

    /// <summary>Whether the security is a fund. A real JSON boolean.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }
}
