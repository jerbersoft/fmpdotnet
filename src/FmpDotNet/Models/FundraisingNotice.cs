using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One Regulation D exempt-offering notice — a Form D filing — from <c>stable/fundraising</c> and
/// <c>stable/fundraising-latest</c>.
///
/// <para><b>Forty-three keys, in the same order on both paths</b>, verified by direct list comparison on
/// 2026-08-31 and matched field-for-field by the independent Python <c>fmpsdk</c>.</para>
///
/// <para><b>Form D filers and Form C filers are disjoint populations</b>, measured in both directions on
/// 2026-08-31: fundraising CIK <c>0001617426</c> answers <b>0 rows</b> on <c>stable/crowdfunding-offerings</c>
/// and crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> here. Both answers arrive as HTTP 200 with an
/// empty array, so a CIK sent to the wrong corpus reads exactly like a company with no filings. See
/// <see cref="CrowdfundingOffering"/>.</para>
///
/// <para><b><see cref="Date"/> here is ISO; the same field on <see cref="CrowdfundingOffering"/> is
/// <c>MM-DD-YYYY</c>.</b> Four records in this group carry a field named <c>date</c> and no two of the four
/// encodings agree. Swapping the converters is silent in both directions — each answers
/// <see langword="null"/> rather than throwing.</para>
///
/// <para><b>Two fields say "absent" with an empty string rather than with null</b> —
/// <see cref="YearOfIncorporation"/> on 30 of 100 rows and <see cref="DateOfFirstSale"/> on 7, measured
/// 2026-08-31. Both collapse to <see langword="null"/> here so that absence has one spelling.</para>
///
/// <para>Every property is nullable and the measured null counts are in the docs rather than in the
/// type.</para></summary>
public sealed record FundraisingNotice
{
    /// <summary>The issuer's SEC CIK, zero-padded to ten digits on every row measured 2026-08-31.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name as EDGAR holds it. Usually equal to <see cref="EntityName"/>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The notice's own date, <b>ISO <c>yyyy-MM-dd</c></b>.
    ///
    /// <para><b>Not the same encoding as <see cref="CrowdfundingOffering.Date"/>, which is
    /// <c>MM-DD-YYYY</c></b> — and unlike that field, this one tracks the filing rather than the company:
    /// measured 2026-08-31 it sits within days of <see cref="FilingDate"/> rather than years before it.
    /// Reaches this type through <see cref="NullableLocalDateJsonConverter"/>, which reads <c>""</c> and
    /// <c>"0000-00-00"</c> as null as well.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When the filing was submitted. <b>A date, not a timestamp</b> — <c>00:00:00</c> on 3,575 of
    /// 3,575 rows measured 2026-08-31 across both filing corpora. See
    /// <see cref="CrowdfundingOffering.FilingDate"/>.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, read as <b>Eastern</b> wall clock — the full account of the
    /// measurement is on <see cref="CrowdfundingOffering.AcceptedDate"/>. <b>This is also exactly what
    /// <see cref="FundraisingSearchHit.Date"/> carries</b>: measured 2026-08-31 for CIK <c>0001617426</c>, all
    /// 14 search timestamps matched these 14 values exactly.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form code. Two values measured 2026-08-31: <c>"D"</c> and <c>"D/A"</c>.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The form code spelled out — <c>"Notice of Exempt Offering of Securities"</c>, or the
    /// amendment wording for <c>D/A</c>.</summary>
    [JsonPropertyName("formSignification")] public string? FormSignification { get; init; }

    /// <summary>The issuing entity's name as it appears on the notice.</summary>
    [JsonPropertyName("entityName")] public string? EntityName { get; init; }

    /// <summary>The issuer's street address.</summary>
    [JsonPropertyName("issuerStreet")] public string? IssuerStreet { get; init; }

    /// <summary>The issuer's city.</summary>
    [JsonPropertyName("issuerCity")] public string? IssuerCity { get; init; }

    /// <summary>The issuer's state or country code — <c>"CA"</c>.</summary>
    [JsonPropertyName("issuerStateOrCountry")] public string? IssuerStateOrCountry { get; init; }

    /// <summary>The same jurisdiction spelled out — <c>"CALIFORNIA"</c>. Redundant with
    /// <see cref="IssuerStateOrCountry"/> and surfaced anyway, because the wire sends both and the SDK does
    /// not decide which one a caller wanted.</summary>
    [JsonPropertyName("issuerStateOrCountryDescription")]
    public string? IssuerStateOrCountryDescription { get; init; }

    /// <summary>The issuer's postal code, as a <b>string</b> — four- and five-digit forms both measured
    /// 2026-08-31. See <see cref="CrowdfundingOffering.IssuerZipCode"/>.</summary>
    [JsonPropertyName("issuerZipCode")] public string? IssuerZipCode { get; init; }

    /// <summary>The issuer's telephone number, <b>in three different formats</b>. Measured 2026-08-31 over
    /// 100 rows: <c>999-999-9999</c> on 33, <c>9999999999</c> on 18, and <c>999 999 9999</c> on 8. A caller
    /// comparing two of these strings is comparing formats, not numbers.</summary>
    [JsonPropertyName("issuerPhoneNumber")] public string? IssuerPhoneNumber { get; init; }

    /// <summary>Where the entity is incorporated — <c>"DELAWARE"</c>.</summary>
    [JsonPropertyName("jurisdictionOfIncorporation")] public string? JurisdictionOfIncorporation { get; init; }

    /// <summary>The entity's legal form. Four values measured 2026-08-31 — the same vocabulary
    /// <see cref="CrowdfundingOffering.LegalStatusForm"/> carries under a different name.</summary>
    [JsonPropertyName("entityType")] public string? EntityType { get; init; }

    /// <summary>Whether the entity was incorporated within the last five years. <b>Null on 30 of 100 rows
    /// measured 2026-08-31</b>, and the null is not a defect: a Form D filer that does not answer the
    /// question leaves it blank, and <see langword="false"/> would be a different claim.</summary>
    [JsonPropertyName("incorporatedWithinFiveYears")] public bool? IncorporatedWithinFiveYears { get; init; }

    /// <summary>The year the entity was incorporated — <b>a string, and deliberately so</b>.
    ///
    /// <para>Measured 2026-08-31 over 100 rows: <b>never null</b>, <c>""</c> on <b>30</b>, and a four-digit
    /// year on the other 70 — a JSON string in both cases. It is <b>not</b> <see cref="int"/>.
    /// <c>FmpJsonContext</c> sets <c>NumberHandling = AllowReadingFromString</c> globally, so <c>"1998"</c>
    /// would bind — but <c>""</c> throws, and <c>System.Text.Json</c> aborts the <i>entire list</i>
    /// deserialisation rather than the one field. Thirty percent of rows would cost the caller the whole
    /// response.</para>
    ///
    /// <para>Reaches this type through <see cref="SentinelStringJsonConverter"/>, which collapses <c>""</c>
    /// (and <c>"N/A"</c> and <c>"NULL"</c>) to <see langword="null"/> so absence has one spelling. A caller
    /// who wants a number writes <c>int.Parse</c> and decides for themselves what an absent year
    /// means.</para></summary>
    [JsonPropertyName("yearOfIncorporation")]
    [JsonConverter(typeof(SentinelStringJsonConverter))]
    public string? YearOfIncorporation { get; init; }

    /// <summary>The related person's first name. <b>Carries placeholders rather than nulls</b> — <c>"-"</c>,
    /// <c>"--"</c> and <c>"N/A"</c> all measured 2026-08-31, on a field that is never null. Left unconverted
    /// on purpose: unlike <see cref="YearOfIncorporation"/>, no measurement establishes that these three
    /// spellings all mean the same thing, and collapsing them would be a guess presented as a
    /// fact.</summary>
    [JsonPropertyName("relatedPersonFirstName")] public string? RelatedPersonFirstName { get; init; }

    /// <summary>The related person's last name — which for an entity holds the whole entity name.</summary>
    [JsonPropertyName("relatedPersonLastName")] public string? RelatedPersonLastName { get; init; }

    /// <summary>The related person's street address.</summary>
    [JsonPropertyName("relatedPersonStreet")] public string? RelatedPersonStreet { get; init; }

    /// <summary>The related person's city.</summary>
    [JsonPropertyName("relatedPersonCity")] public string? RelatedPersonCity { get; init; }

    /// <summary>The related person's state or country code.</summary>
    [JsonPropertyName("relatedPersonStateOrCountry")] public string? RelatedPersonStateOrCountry { get; init; }

    /// <summary>The same jurisdiction spelled out.</summary>
    [JsonPropertyName("relatedPersonStateOrCountryDescription")]
    public string? RelatedPersonStateOrCountryDescription { get; init; }

    /// <summary>The related person's postal code, as a string.</summary>
    [JsonPropertyName("relatedPersonZipCode")] public string? RelatedPersonZipCode { get; init; }

    /// <summary>How the related person relates to the issuer — <c>"Director"</c>,
    /// <c>"Executive Officer"</c>.</summary>
    [JsonPropertyName("relatedPersonRelationship")] public string? RelatedPersonRelationship { get; init; }

    /// <summary>The issuer's industry as Form D classifies it — <c>"Pooled Investment Fund"</c>.</summary>
    [JsonPropertyName("industryGroupType")] public string? IndustryGroupType { get; init; }

    /// <summary>The issuer's revenue band as a phrase, not a number. 5 distinct values measured 2026-08-31;
    /// null on 29 of 100 rows.</summary>
    [JsonPropertyName("revenueRange")] public string? RevenueRange { get; init; }

    /// <summary>The Securities Act exemptions claimed, as a <b>comma-joined list in one string</b> —
    /// <c>"06b, 3C, 3C.7"</c>, measured 2026-08-31. Splitting it is the caller's decision; the SDK surfaces
    /// what the wire sent.</summary>
    [JsonPropertyName("federalExemptionsExclusions")] public string? FederalExemptionsExclusions { get; init; }

    /// <summary>Whether this notice amends an earlier one. Agrees with <see cref="FormType"/> being
    /// <c>"D/A"</c>. Never null in 100 rows measured 2026-08-31.</summary>
    [JsonPropertyName("isAmendment")] public bool? IsAmendment { get; init; }

    /// <summary>When the first sale under the offering occurred. <b><c>""</c> on 7 of 100 rows measured
    /// 2026-08-31</b> and never JSON null — <see cref="NullableLocalDateJsonConverter"/> already reads the
    /// empty string as null, so unlike <see cref="YearOfIncorporation"/> this one needs no sentinel
    /// converter.</summary>
    [JsonPropertyName("dateOfFirstSale")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DateOfFirstSale { get; init; }

    /// <summary>Whether the offering is expected to last more than a year. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("durationOfOfferingIsMoreThanYear")]
    public bool? DurationOfOfferingIsMoreThanYear { get; init; }

    /// <summary>Whether equity is among the securities offered. <b>Null on 64 of 100 rows measured
    /// 2026-08-31</b> — the most frequently absent field on this record, and absent rather than
    /// <see langword="false"/>.</summary>
    [JsonPropertyName("securitiesOfferedAreOfEquityType")]
    public bool? SecuritiesOfferedAreOfEquityType { get; init; }

    /// <summary>Whether the offering is part of a business combination. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("isBusinessCombinationTransaction")]
    public bool? IsBusinessCombinationTransaction { get; init; }

    /// <summary>The smallest accepted investment. 0 to 5,000,000 measured 2026-08-31 over 406 rows.</summary>
    [JsonPropertyName("minimumInvestmentAccepted")] public decimal? MinimumInvestmentAccepted { get; init; }

    /// <summary>The total size of the offering. 0 to 1,000,000,000 measured 2026-08-31 over 406
    /// rows — <b>within Int32 by 0.5 orders of magnitude and typed the same as
    /// <see cref="TotalAmountSold"/> anyway</b>, because "has not overflowed yet" is not a
    /// type.</summary>
    [JsonPropertyName("totalOfferingAmount")] public decimal? TotalOfferingAmount { get; init; }

    /// <summary>How much has actually been sold.
    ///
    /// <para><b>Measured maximum 13,475,150,514 on 2026-08-31 — 6.3 times <see cref="int.MaxValue"/>.</b>
    /// An <see cref="int"/> property does not truncate that: <c>System.Text.Json</c> throws on the overflow
    /// and aborts the whole list, so one large raise costs the caller every other row in the
    /// response.</para>
    ///
    /// <para><see cref="decimal"/> rather than <see cref="long"/> for the reason recorded on
    /// <see cref="FinancialScores.PiotroskiScore"/>: all eight amount fields on this record were whole on
    /// 406 of 406 rows, and "not seen fractional yet" is not "cannot be fractional" — FMP is known to
    /// serialise counts through a float elsewhere, and <see cref="long"/> inherits the same
    /// abort-the-response failure the day one arrives with cents.</para></summary>
    [JsonPropertyName("totalAmountSold")] public decimal? TotalAmountSold { get; init; }

    /// <summary>The unsold balance. 0 to 881,533,305 measured 2026-08-31.</summary>
    [JsonPropertyName("totalAmountRemaining")] public decimal? TotalAmountRemaining { get; init; }

    /// <summary>Whether any non-accredited investor participated. Never null in 100 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("hasNonAccreditedInvestors")] public bool? HasNonAccreditedInvestors { get; init; }

    /// <summary>How many investors have already subscribed. 0 to 10,000 measured 2026-08-31.</summary>
    [JsonPropertyName("totalNumberAlreadyInvested")] public decimal? TotalNumberAlreadyInvested { get; init; }

    /// <summary>Commissions paid on the sale. 0 to 8,000,000 measured 2026-08-31.</summary>
    [JsonPropertyName("salesCommissions")] public decimal? SalesCommissions { get; init; }

    /// <summary>Finders' fees paid. <b>Zero on all 100 rows measured 2026-08-31</b>, and surfaced anyway —
    /// zero is what the wire said, and the sweep records it as populated rather than as an absence.</summary>
    [JsonPropertyName("findersFees")] public decimal? FindersFees { get; init; }

    /// <summary>Gross proceeds applied to the uses disclosed on the notice. 0 to 8,715,408 measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("grossProceedsUsed")] public decimal? GrossProceedsUsed { get; init; }
}
