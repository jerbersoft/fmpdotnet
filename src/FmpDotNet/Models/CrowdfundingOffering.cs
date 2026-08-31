using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One Regulation Crowdfunding offering — a Form C filing — from <c>stable/crowdfunding-offerings</c>
/// and <c>stable/crowdfunding-offerings-latest</c>.
///
/// <para><b>Forty-eight keys, in the same order on both paths</b>, verified by direct list comparison rather
/// than by eye on 2026-08-31, and confirmed twice more: FMP's own documented sample carries the same 48 keys
/// in the same order, and the independent Python <c>fmpsdk</c> models it as a 48-field type with an
/// identical key set.</para>
///
/// <para><b>Form C filers and Form D filers are disjoint populations.</b> Measured 2026-08-31 in both
/// directions: crowdfunding CIK <c>0002152721</c> answers <b>0 rows</b> on <c>stable/fundraising</c>, and
/// fundraising CIK <c>0001617426</c> answers <b>0 rows</b> here. A CIK from one corpus is not a lookup that
/// failed on the other — it is a query for something that was never there, and it arrives as HTTP 200 with an
/// empty array either way. See <see cref="FundraisingNotice"/>.</para>
///
/// <para><b>Four fields on this record are not what their names suggest.</b>
/// <see cref="Date"/> is not the filing date, <see cref="CompensationAmount"/> is not an amount,
/// <see cref="FinancialInterest"/> is not a flag, and <see cref="OverSubscriptionAccepted"/> arrives as a
/// string. Each carries its measurement below.</para>
///
/// <para><b>Every numeric here is <see cref="decimal"/>, and both halves of that are measured.</b> Fractional:
/// <see cref="OfferingPrice"/> on <b>884</b> of 3,656 rows, <see cref="OfferingAmount"/> on 579,
/// <see cref="MaximumOfferingAmount"/> on 482, and every one of the eighteen fiscal-year fields on 56-339.
/// Negative: <see cref="NetIncomeMostRecentFiscalYear"/> reaches <b>-27,665,487</b> and is negative on 682 of
/// 1,000 rows. An integral type would throw on the first and take the whole response with it — the reasoning
/// is on <see cref="FinancialScores.PiotroskiScore"/>.</para>
///
/// <para>Every property is nullable and the measured null counts are in the docs rather than in the type.
/// "Never null in 1,000 rows" and "cannot be null" are different statements, and only the first was
/// measured.</para></summary>
public sealed record CrowdfundingOffering
{
    /// <summary>The issuer's SEC CIK, zero-padded to ten digits on 1,000 of 1,000 rows measured
    /// 2026-08-31 — <c>"0002010670"</c>. A string, because the padding is part of the identifier as EDGAR
    /// writes it.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's name as EDGAR holds it. 652 distinct values in 1,000 rows measured 2026-08-31.
    /// Usually but not always equal to <see cref="NameOfIssuer"/>, which is the name on the offering.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary><b>Not the filing date.</b> Measured 2026-08-31, this precedes <see cref="FilingDate"/> on
    /// <b>1,000 of 1,000</b> rows with zero exceptions, gaps running 0 to 43 years and a year range of
    /// 1983-2026 — and it is <i>constant across every filing</i> for <b>10 of 18</b> filers sampled,
    /// including one issuer whose <b>48</b> filings all carry <c>12-19-2023</c>.
    ///
    /// <para>That behaviour is a property of the company rather than of the document, which is what a date of
    /// formation looks like. The SDK does not rename it: the wire says <c>date</c> and no reachable FMP
    /// documentation labels it, so inventing a name would be stating a fact nobody measured. What is measured
    /// is stated here, and a test pins <c>Date &lt; FilingDate</c>. Use <see cref="FilingDate"/> when you want
    /// to know when the filing happened.</para>
    ///
    /// <para><b><c>MM-DD-YYYY</c>, and the SDK's ISO converter reads it as null without throwing.</b> See
    /// <see cref="NullableMonthDayYearDateJsonConverter"/>. Never null in 1,000 rows measured
    /// 2026-08-31.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>When the filing was submitted. <b>A date, not a timestamp</b> — its time component was
    /// <c>00:00:00</c> on <b>3,575 of 3,575</b> rows measured 2026-08-31, a dummy midnight bolted on to a
    /// date. Binding it as an instant would leak a meaningless midnight into every comparison a caller
    /// writes. Reaches this type through <see cref="NullableDateAtMidnightJsonConverter"/>; the same field on
    /// <see cref="FundraisingNotice.FilingDate"/> is measured identically and takes the same converter.
    /// Never null in 1,000 rows.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableDateAtMidnightJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing, read as <b>Eastern</b> wall clock.
    ///
    /// <para><b>The typing decision of this record, and the intuitive answer is wrong.</b> The wire sends
    /// <c>"yyyy-MM-dd HH:mm:ss"</c> with no offset and no zone marker, and this SDK carries two converters for
    /// that exact shape. <see cref="NullableFmpInstantJsonConverter"/> reads it as UTC and would put every
    /// acceptance <b>four to five hours early</b>. It compiles, it deserialises, and nothing in the data
    /// reveals it. FMP's documentation does not settle it either: every endpoint page answers HTTP 403 to
    /// automated fetch, and the documented sample carries no offset and no timezone note.</para>
    ///
    /// <para><b>So the wire was measured, over 1,395 distinct values here and 1,779 more on
    /// <see cref="FundraisingSearchHit.Date"/>, spanning 2009-2026.</b> EDT (n=1,060) window
    /// <b>06:00-22:00</b>; EST (n=445) window <b>06:00-21:59</b>. <b>The window does not shift across the DST
    /// boundary</b> — a stored instant would move by an hour, a stripped wall clock does not. And a UTC
    /// reading is refuted arithmetically: 20:00 EDT is 00:00 UTC, so an Eastern-window feed read as UTC must
    /// place rows in hours 22-03, and there are <b>zero</b> in 3,174 values. The only two outside 06:00-21:59
    /// land on EDGAR's 22:00 ET closing minute rather than beyond it, and the drop between hour 17 (114 rows)
    /// and hour 18 (59) sits on EDGAR's 17:30 ET same-day cutoff.</para>
    ///
    /// <para>Never null in 1,000 rows measured 2026-08-31.</para></summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The EDGAR form code — <c>"C"</c>, <c>"C/A"</c>, <c>"C-U"</c> and three others. 6 distinct
    /// values in 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The form code spelled out — <c>"Offering Statement"</c>. 6 distinct values, one per
    /// <see cref="FormType"/>, measured 2026-08-31.</summary>
    [JsonPropertyName("formSignification")] public string? FormSignification { get; init; }

    /// <summary>The issuer's name as it appears on the offering. Never null in 1,000 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("nameOfIssuer")] public string? NameOfIssuer { get; init; }

    /// <summary>The issuer's legal form. Four values measured 2026-08-31: <c>Corporation</c>,
    /// <c>Limited Liability Company</c>, <c>Limited Partnership</c>, <c>Other</c> — the same vocabulary
    /// <see cref="FundraisingNotice.EntityType"/> uses under a different name.</summary>
    [JsonPropertyName("legalStatusForm")] public string? LegalStatusForm { get; init; }

    /// <summary>The two-character jurisdiction the issuer is organised under. 41 distinct values, null on 3
    /// of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("jurisdictionOrganization")] public string? JurisdictionOrganization { get; init; }

    /// <summary>The issuer's street address.</summary>
    [JsonPropertyName("issuerStreet")] public string? IssuerStreet { get; init; }

    /// <summary>The issuer's city.</summary>
    [JsonPropertyName("issuerCity")] public string? IssuerCity { get; init; }

    /// <summary>The issuer's state or country code. Null on 4 of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("issuerStateOrCountry")] public string? IssuerStateOrCountry { get; init; }

    /// <summary>The issuer's postal code, as a <b>string</b>.
    ///
    /// <para>Three forms measured 2026-08-31 over 1,000 rows: <c>99999</c> on 990, <c>9999</c> on 5, and
    /// <c>99999-9999</c> on 5. An integer type loses the leading zero on the four-digit form and throws
    /// outright on the hyphenated one — and a throw here costs the whole response, not one
    /// field.</para></summary>
    [JsonPropertyName("issuerZipCode")] public string? IssuerZipCode { get; init; }

    /// <summary>The issuer's website. Null on 70 of 1,000 rows measured 2026-08-31.</summary>
    [JsonPropertyName("issuerWebsite")] public string? IssuerWebsite { get; init; }

    /// <summary>The funding portal or broker-dealer intermediating the offering. Null on 288 of 1,000 rows
    /// measured 2026-08-31, together with the four other intermediary and security fields — they arrive as
    /// a block or not at all.</summary>
    [JsonPropertyName("intermediaryCompanyName")] public string? IntermediaryCompanyName { get; init; }

    /// <summary>The intermediary's own CIK, zero-padded to ten digits on every non-null row measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("intermediaryCommissionCik")] public string? IntermediaryCommissionCik { get; init; }

    /// <summary>The intermediary's SEC file number, in <c>999-99999</c> form. Null on 288 of 1,000 rows
    /// measured 2026-08-31.</summary>
    [JsonPropertyName("intermediaryCommissionFileNumber")]
    public string? IntermediaryCommissionFileNumber { get; init; }

    /// <summary><b>Free prose, despite the name — never a number.</b> Measured 2026-08-31, a typical value
    /// is <i>"7.9% of the offering amount upon a successful fundraise, and be entitled to reimbursement…"</i>.
    /// Parsing a figure out of it is the caller's decision to make explicitly, not the SDK's to make
    /// silently. Null on 289 of 1,000 rows.</summary>
    [JsonPropertyName("compensationAmount")] public string? CompensationAmount { get; init; }

    /// <summary><b>Free prose, not a flag.</b> 57 distinct values up to 256 characters measured 2026-08-31.
    /// <c>"No"</c> is common, which is exactly what makes a boolean tempting and wrong: the other 56 values
    /// are sentences. Null on 298 of 1,000 rows.</summary>
    [JsonPropertyName("financialInterest")] public string? FinancialInterest { get; init; }

    /// <summary>What is being offered — 4 values measured 2026-08-31. Null on 289 of 1,000 rows.</summary>
    [JsonPropertyName("securityOfferedType")] public string? SecurityOfferedType { get; init; }

    /// <summary>Free text used when <see cref="SecurityOfferedType"/> is "Other". Null on <b>695</b> of 1,000
    /// rows measured 2026-08-31 — the most frequently absent field on this record.</summary>
    [JsonPropertyName("securityOfferedOtherDescription")]
    public string? SecurityOfferedOtherDescription { get; init; }

    /// <summary>How many securities are on offer. 0 to 10,000,000 measured 2026-08-31, never fractional in
    /// 3,656 rows — and <see cref="decimal"/> anyway, for the reason on the type.</summary>
    [JsonPropertyName("numberOfSecurityOffered")] public decimal? NumberOfSecurityOffered { get; init; }

    /// <summary>Price per security. <b>Fractional on 884 of 3,656 rows measured 2026-08-31</b>, 0 to 1,000.
    /// The single clearest reason this record's numerics are not integral.</summary>
    [JsonPropertyName("offeringPrice")] public decimal? OfferingPrice { get; init; }

    /// <summary>The target raise. Fractional on 579 of 3,656 rows measured 2026-08-31, 0 to
    /// 1,000,000.</summary>
    [JsonPropertyName("offeringAmount")] public decimal? OfferingAmount { get; init; }

    /// <summary>Whether the issuer will accept over-subscriptions. <b>The wire sends <c>"Y"</c> or
    /// <c>"N"</c>, not a boolean</b> — never null in 1,000 rows measured 2026-08-31. Reaches this type
    /// through <see cref="YesNoBooleanJsonConverter"/>, which maps any third value to
    /// <see langword="null"/> rather than guessing: <see langword="false"/> and "this SDK has never seen that
    /// value" are different answers.</summary>
    [JsonPropertyName("overSubscriptionAccepted")]
    [JsonConverter(typeof(YesNoBooleanJsonConverter))]
    public bool? OverSubscriptionAccepted { get; init; }

    /// <summary>How over-subscriptions would be allocated. 3 values measured 2026-08-31; null on 297 of
    /// 1,000 rows.</summary>
    [JsonPropertyName("overSubscriptionAllocationType")]
    public string? OverSubscriptionAllocationType { get; init; }

    /// <summary>The ceiling on the raise. Fractional on 482 of 3,656 rows measured 2026-08-31, 0 to
    /// 5,000,000.</summary>
    [JsonPropertyName("maximumOfferingAmount")] public decimal? MaximumOfferingAmount { get; init; }

    /// <summary>When the offering closes. <b><c>MM-DD-YYYY</c>, like <see cref="Date"/></b> and unlike
    /// <see cref="FilingDate"/> beside it — see <see cref="NullableMonthDayYearDateJsonConverter"/>. Null on
    /// 289 of 1,000 rows measured 2026-08-31. Unlike <see cref="Date"/> this one <i>is</i> about the offering:
    /// it is dated in the future relative to the filing.</summary>
    [JsonPropertyName("offeringDeadlineDate")]
    [JsonConverter(typeof(NullableMonthDayYearDateJsonConverter))]
    public LocalDate? OfferingDeadlineDate { get; init; }

    /// <summary>Headcount at filing. 0 to 320 measured 2026-08-31.</summary>
    [JsonPropertyName("currentNumberOfEmployees")] public decimal? CurrentNumberOfEmployees { get; init; }

    // ---- The nine financial pairs. Eighteen fields, not sixteen: the measurements file's census says "16 x"
    // and the wire, FMP's documented sample and the Python fmpsdk all carry nine pairs. Thirty other keys
    // plus eighteen is the 48 all three agree on.
    //
    // Every one of them is decimal? and every one was measured BOTH fractional and negative on 2026-08-31
    // across 3,656 rows. These are unaudited figures self-reported on a Form C by companies that are, in the
    // main, pre-revenue: netIncomeMostRecentFiscalYear is negative on 682 of 1,000 rows. Reading a negative
    // here as a data error would be reading the population wrong.

    /// <summary>Total assets, most recent fiscal year. Fractional on 326 of 3,656 rows measured 2026-08-31;
    /// range -228,414.57 to 220,738,384.</summary>
    [JsonPropertyName("totalAssetMostRecentFiscalYear")]
    public decimal? TotalAssetMostRecentFiscalYear { get; init; }

    /// <summary>Total assets, prior fiscal year. Fractional on 205 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("totalAssetPriorFiscalYear")] public decimal? TotalAssetPriorFiscalYear { get; init; }

    /// <summary>Cash and cash equivalents, most recent fiscal year.
    ///
    /// <para><b>The wire name carries a capital <c>V</c> in "Equivalent"</b> —
    /// <c>cashAndCashEquiValentMostRecentFiscalYear</c> — and it appears that way in FMP's own documented
    /// sample as well as on the wire, so it is stable rather than a transient bug. A
    /// <c>[JsonPropertyName]</c> that "corrects" the spelling binds nothing, silently, on a nullable property
    /// that gives no hint. A test pins both spellings.</para>
    ///
    /// <para>Fractional on 312 of 3,656 rows measured 2026-08-31; range -292,945.30 to
    /// 30,153,080.</para></summary>
    [JsonPropertyName("cashAndCashEquiValentMostRecentFiscalYear")]
    public decimal? CashAndCashEquivalentMostRecentFiscalYear { get; init; }

    /// <summary>Cash and cash equivalents, prior fiscal year. Same capital <c>V</c> on the wire — see
    /// <see cref="CashAndCashEquivalentMostRecentFiscalYear"/>. Fractional on 197 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("cashAndCashEquiValentPriorFiscalYear")]
    public decimal? CashAndCashEquivalentPriorFiscalYear { get; init; }

    /// <summary>Accounts receivable, most recent fiscal year. Fractional on 114 of 3,656 rows measured
    /// 2026-08-31; goes negative to -17,625.45.</summary>
    [JsonPropertyName("accountsReceivableMostRecentFiscalYear")]
    public decimal? AccountsReceivableMostRecentFiscalYear { get; init; }

    /// <summary>Accounts receivable, prior fiscal year. Fractional on 56 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("accountsReceivablePriorFiscalYear")]
    public decimal? AccountsReceivablePriorFiscalYear { get; init; }

    /// <summary>Short-term debt, most recent fiscal year. Fractional on 213 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("shortTermDebtMostRecentFiscalYear")]
    public decimal? ShortTermDebtMostRecentFiscalYear { get; init; }

    /// <summary>Short-term debt, prior fiscal year. Fractional on 139 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("shortTermDebtPriorFiscalYear")]
    public decimal? ShortTermDebtPriorFiscalYear { get; init; }

    /// <summary>Long-term debt, most recent fiscal year. Fractional on 136 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("longTermDebtMostRecentFiscalYear")]
    public decimal? LongTermDebtMostRecentFiscalYear { get; init; }

    /// <summary>Long-term debt, prior fiscal year. Fractional on 61 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("longTermDebtPriorFiscalYear")]
    public decimal? LongTermDebtPriorFiscalYear { get; init; }

    /// <summary>Revenue, most recent fiscal year. Fractional on 198 of 3,656 rows measured 2026-08-31; range
    /// 0 to 128,625,869.</summary>
    [JsonPropertyName("revenueMostRecentFiscalYear")]
    public decimal? RevenueMostRecentFiscalYear { get; init; }

    /// <summary>Revenue, prior fiscal year. Fractional on 147 of 3,656 rows measured 2026-08-31, and
    /// <b>negative</b> on at least one — measured minimum -0.1, which a caller assuming revenue cannot be
    /// negative will not expect.</summary>
    [JsonPropertyName("revenuePriorFiscalYear")] public decimal? RevenuePriorFiscalYear { get; init; }

    /// <summary>Cost of goods sold, most recent fiscal year. Fractional on 207 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("costGoodsSoldMostRecentFiscalYear")]
    public decimal? CostGoodsSoldMostRecentFiscalYear { get; init; }

    /// <summary>Cost of goods sold, prior fiscal year. Fractional on 123 of 3,656 rows measured
    /// 2026-08-31.</summary>
    [JsonPropertyName("costGoodsSoldPriorFiscalYear")]
    public decimal? CostGoodsSoldPriorFiscalYear { get; init; }

    /// <summary>Taxes paid, most recent fiscal year. Fractional on 77 of 3,656 rows measured 2026-08-31;
    /// goes negative to -8,756,000.</summary>
    [JsonPropertyName("taxesPaidMostRecentFiscalYear")]
    public decimal? TaxesPaidMostRecentFiscalYear { get; init; }

    /// <summary>Taxes paid, prior fiscal year. Fractional on 77 of 3,656 rows measured 2026-08-31.</summary>
    [JsonPropertyName("taxesPaidPriorFiscalYear")] public decimal? TaxesPaidPriorFiscalYear { get; init; }

    /// <summary>Net income, most recent fiscal year. <b>Negative on 682 of 1,000 rows measured
    /// 2026-08-31</b>, reaching -27,665,487, and fractional on 339 of 3,656. This is the field that makes the
    /// case for the whole record's typing: an unsigned or integral type would be wrong twice over on the
    /// majority of rows.</summary>
    [JsonPropertyName("netIncomeMostRecentFiscalYear")]
    public decimal? NetIncomeMostRecentFiscalYear { get; init; }

    /// <summary>Net income, prior fiscal year. Fractional on 210 of 3,656 rows measured 2026-08-31; reaches
    /// -28,009,000.</summary>
    [JsonPropertyName("netIncomePriorFiscalYear")] public decimal? NetIncomePriorFiscalYear { get; init; }
}
