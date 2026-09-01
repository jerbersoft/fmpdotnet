using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>A disclosed dollar band — the <c>min</c> and <c>max</c> of a bracket on a Senate financial
/// disclosure.
///
/// <para>Used twice on <see cref="SenateNetWorthLine"/>, for <c>valueRange</c> and <c>incomeRange</c>. Both
/// bounds were integral on every row measured 2026-08-29 — 428 numbers under <c>valueRange</c>, 272 under
/// <c>incomeRange</c>, none carrying a decimal point — and both are <see cref="decimal"/> anyway: they are
/// money, and integral samples say nothing about the next row.</para></summary>
public sealed record NetWorthRange
{
    /// <summary>The bottom of the band.</summary>
    [JsonPropertyName("min")] public decimal? Min { get; init; }

    /// <summary>The top of the band.</summary>
    [JsonPropertyName("max")] public decimal? Max { get; init; }
}

/// <summary>The terms of a disclosed liability, nested on <see cref="SenateNetWorthLine"/>.
///
/// <para><b>A union of two disjoint shapes.</b> Measured 2026-08-29 over the 100 rows where it is present, 87
/// carried <see cref="DateIncurred"/>, <see cref="Points"/> and <see cref="Rate"/>, and 13 carried
/// <see cref="Source"/> alone. Never all four together — an absent key binds
/// <see langword="null"/>.</para></summary>
public sealed record NetWorthDebtDetails
{
    /// <summary>When the debt was incurred.
    ///
    /// <para><b>A year, not a date, and therefore <see cref="string"/>.</b> Measured 2026-08-29, seven
    /// distinct values and every one a bare four-digit year — <c>2003</c>, <c>2021</c>. A
    /// <see cref="LocalDate"/> would fail on all of them.</para></summary>
    [JsonPropertyName("dateIncurred")] public string? DateIncurred { get; init; }

    /// <summary>Points on the loan.
    ///
    /// <para><b><see cref="string"/> because FMP sends two types</b>, and therefore carrying
    /// <see cref="ScalarAsStringJsonConverter"/> — measured 2026-08-29, this was the string <c>"-"</c> on 82
    /// of 100 rows and the number <c>0</c> on 5, and a JSON number read into a bare <see cref="string"/>
    /// throws out of the whole response. Mapping <c>"-"</c> to <see langword="null"/> would collapse it into
    /// the 13 rows that are genuinely null, and those are three states FMP distinguishes.</para></summary>
    [JsonPropertyName("points")]
    [JsonConverter(typeof(ScalarAsStringJsonConverter))]
    public string? Points { get; init; }

    /// <summary>The interest rate.
    ///
    /// <para><b><see cref="string"/>, and this is the one place in the slice where the SDK hands back
    /// something it could have parsed.</b> Measured 2026-08-29, <c>rate</c> arrives as a number on 23 of 100
    /// rows (<c>1.4</c>, <c>2.75</c>, <c>5.25</c>, <c>3</c>) and as a string on 64. The strings are not
    /// placeholders — they carry a term as well as a rate:</para>
    ///
    /// <code>
    /// "N/A%                        (10 years)"
    /// "NA%                        (On Demand)"
    /// </code>
    ///
    /// <para>A tolerant numeric converter would bind <see langword="null"/> on those 64 and discard
    /// "10 years" and "On Demand" with them. FMP has overloaded the field; the SDK reports it rather than
    /// guessing at it. <see cref="ScalarAsStringJsonConverter"/> is what lets the numeric 23 reach a
    /// <see cref="string"/> instead of aborting the response.</para></summary>
    [JsonPropertyName("rate")]
    [JsonConverter(typeof(ScalarAsStringJsonConverter))]
    public string? Rate { get; init; }

    /// <summary>Who the debt is owed to. Present on the 13 rows that carry no rate terms; see the record
    /// summary.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
}

/// <summary>One line of a Senator's financial disclosure, from <c>stable/senate-net-worth</c>.
///
/// <para>One row per disclosed asset, income source or liability, across every report the member has filed.
/// Measured 2026-08-29, <c>H000601</c> answered <b>250 rows</b> and <c>limit</c> was ignored.</para>
///
/// <para><b>Read <see cref="IncomeRange"/> before changing anything here.</b> It is the one property in this
/// slice that needs a converter of its own, and the reason is a hard binding failure rather than a nicety.
/// <see cref="NetWorthDebtDetails.Rate"/> and <see cref="NetWorthDebtDetails.Points"/> carry the other
/// one.</para>
///
/// <para><b><see cref="Value"/> is the midpoint of <see cref="ValueRange"/>; <see cref="Income"/> is NOT the
/// midpoint of <see cref="IncomeRange"/>.</b> The symmetry is a trap — see
/// <see cref="Income"/>.</para></summary>
public sealed record SenateNetWorthLine
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>Which filing — <c>Annual Report</c> or <c>Candidate Report</c>.</summary>
    [JsonPropertyName("formType")] public string? FormType { get; init; }

    /// <summary>The reporting year. A calendar year and whole by its own nature, hence
    /// <see cref="int"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>When the report was filed.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>Which part of the disclosure — <c>Asset</c>, <c>Income</c> or <c>Liabilities</c>.</summary>
    [JsonPropertyName("section")] public string? Section { get; init; }

    /// <summary>FMP's category for the line.</summary>
    [JsonPropertyName("category")] public string? Category { get; init; }

    /// <summary>The asset or counterparty as the disclosure names it. Carries the runs of internal whitespace
    /// the filing does; it is passed through rather than tidied.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The instrument. Free text from the filing, not the vocabulary
    /// <see cref="CongressionalTrade.AssetType"/> uses.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>What kind of income the line produced, where it produces any.</summary>
    [JsonPropertyName("incomeType")] public string? IncomeType { get; init; }

    /// <summary>Whose holding — <c>Self</c>, <c>Joint</c> or <c>Child</c>.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }

    /// <summary>The filer's note.</summary>
    [JsonPropertyName("comment")] public string? Comment { get; init; }

    /// <summary>The liability's terms, on the rows that are liabilities. Null on 150 of 250 rows measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("debtDetails")] public NetWorthDebtDetails? DebtDetails { get; init; }

    /// <summary>The disclosed value band. An object on all 214 rows where it was present, measured
    /// 2026-08-29 — never the empty string its sibling <see cref="IncomeRange"/> sends, which is why this one
    /// carries no converter.</summary>
    [JsonPropertyName("valueRange")] public NetWorthRange? ValueRange { get; init; }

    /// <summary>The midpoint of <see cref="ValueRange"/>, as FMP computes it.
    ///
    /// <para>Verified on <b>214 of 214 rows</b> where both are present, failing on none, measured 2026-08-29.
    /// The SDK passes it through rather than recomputing it. This is where the <c>.5</c> endings across this
    /// group come from.</para></summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }

    /// <summary>The disclosed income band.
    ///
    /// <para><b>Carries <see cref="NetWorthRangeJsonConverter"/>, and must keep it.</b> Measured 2026-08-29
    /// over 250 rows, this arrives as an object on 136, as JSON <see langword="null"/> on 100, and <b>as the
    /// empty string on 14</b>. <c>System.Text.Json</c> cannot read a string into an object, so without the
    /// converter those 14 rows throw — and the throw aborts the entire array, so they cost all 250. The
    /// converter reads <c>""</c> as <see langword="null"/>.</para></summary>
    [JsonPropertyName("incomeRange")]
    [JsonConverter(typeof(NetWorthRangeJsonConverter))]
    public NetWorthRange? IncomeRange { get; init; }

    /// <summary>The income figure FMP reports for the line.
    ///
    /// <para><b>Not the midpoint of <see cref="IncomeRange"/>, and the symmetry with
    /// <see cref="Value"/> is a trap.</b> Measured 2026-08-29 over the 136 rows where the range is an object
    /// and this is present, the midpoint holds on <b>35</b> and fails on <b>101</b> — the first mismatch being
    /// a range of 0 to 201 against an income of 0. Neither figure is derived by the SDK; both are passed
    /// through as sent.</para></summary>
    [JsonPropertyName("income")] public decimal? Income { get; init; }

    /// <summary>The filed disclosure document.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}

/// <summary>One year of a Senator's net worth, totalled by category, from
/// <c>stable/senate-net-worth-aggregated</c>.
///
/// <para>One row per reporting year. Measured 2026-09-01 across <b>every member <c>senate-profile</c>
/// enumerates</b> — 535 asked, 455 answering, 3,425 rows — the years run 2013 through 2024 and a member has
/// between one and twelve rows.</para>
///
/// <para><b>The row shape is per member, and no member shows all of it.</b> The census found <b>27 keys</b>:
/// <c>senateID</c>, <c>year</c>, <c>total</c>, and 24 money categories. Every row of a given member carries
/// the same key set, and that set is the categories the member has ever disclosed — <c>H000601</c> carries 16,
/// <c>G000581</c> carries 21, and nobody carries 27. This type was first modelled from <c>H000601</c>'s six rows
/// and had 16 properties as a result; a 25-member sample (#57) raised that to 25; the census raised it to 27,
/// finding <see cref="SpousalIncome"/> and <see cref="InvestmentAndCapitalGains"/> on members the sample never
/// asked. Three samples, three undercounts, which is why a catch-all for names this type does not know follows
/// the typed fields.</para>
///
/// <para><b><see cref="Total"/> is assets minus liabilities, and the parts reproduce it except inside
/// <see cref="Other"/>.</b> Summing the eleven asset fields, subtracting the six liability fields and ignoring
/// the six income fields gives <c>total</c> exactly on every row where <see cref="Other"/> is zero — 2,907 of
/// 2,907. Where it is not, <see cref="Other"/> reconciles as an asset on 246 rows, as a liability on 228, and as
/// neither on 44. The SDK derives nothing from this; it is recorded so a caller reconstructing net worth knows
/// where the uncertainty lives.</para>
///
/// <para><b>Every one of the 24 money fields is <see cref="decimal"/>, including the seven that never carried a
/// decimal point.</b> Across the census, 18 of the 25 numeric keys flip between bare-integer and decimal-point
/// representation on some row, and the seven that do not include five income fields that are zero on every
/// row — an integral sample of zeros says nothing about the next row, and one fractional value under
/// <see cref="int"/> costs the whole response rather than the field.</para></summary>
public sealed record SenateNetWorthSummary
{
    /// <summary>The member's Bioguide identifier.</summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>The reporting year. Whole by its own nature, hence <see cref="int"/>.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>Net worth for the year.</summary>
    [JsonPropertyName("total")] public decimal? Total { get; init; }

    /// <summary>Revolving credit and lines of credit owed.</summary>
    [JsonPropertyName("revolvingAndCreditLines")] public decimal? RevolvingAndCreditLines { get; init; }

    /// <summary>Salary and wage income. Present on 2,033 rows measured 2026-09-01 and <b>zero on every one of
    /// them</b> — income is disclosed on this path but does not enter <see cref="Total"/>.</summary>
    [JsonPropertyName("salaryAndWages")] public decimal? SalaryAndWages { get; init; }

    /// <summary>Liabilities arising from business interests.</summary>
    [JsonPropertyName("businessLiabilities")] public decimal? BusinessLiabilities { get; init; }

    /// <summary>Mortgages and other property debt.</summary>
    [JsonPropertyName("realEstateLiabilities")] public decimal? RealEstateLiabilities { get; init; }

    /// <summary>Holdings in mutual funds and ETFs.</summary>
    [JsonPropertyName("mutualFundsAndETFs")] public decimal? MutualFundsAndEtfs { get; init; }

    /// <summary>Cash and equivalents.</summary>
    [JsonPropertyName("cashAndCashEquivalents")] public decimal? CashAndCashEquivalents { get; init; }

    /// <summary>Equity in privately held businesses.</summary>
    [JsonPropertyName("ownershipInterest")] public decimal? OwnershipInterest { get; init; }

    /// <summary>Directly held stock.</summary>
    [JsonPropertyName("stock")] public decimal? Stock { get; init; }

    /// <summary>Treasuries and other government paper.</summary>
    [JsonPropertyName("governmentSecurities")] public decimal? GovernmentSecurities { get; init; }

    /// <summary>Everything not covered by another category.</summary>
    [JsonPropertyName("otherAssets")] public decimal? OtherAssets { get; init; }

    /// <summary>Pension and retirement balances.</summary>
    [JsonPropertyName("pensionAndRetirementAssets")] public decimal? PensionAndRetirementAssets { get; init; }

    /// <summary>Real property held.</summary>
    [JsonPropertyName("realEstate")] public decimal? RealEstate { get; init; }

    /// <summary>Assets held in trust.</summary>
    [JsonPropertyName("trusts")] public decimal? Trusts { get; init; }

    // ---- the eleven the first sample never showed (#57) -------------------------------------------------

    /// <summary>FMP's own catch-all category. Capital <c>O</c> on the wire, alone among this path's keys.
    ///
    /// <para><b>Carries either sign, and the row does not say which.</b> Measured 2026-09-01 it is on 2,552
    /// of 3,425 rows and non-zero on 518: on 246 of those <see cref="Total"/> reconciles only if this is added,
    /// on 228 only if it is subtracted, and on 44 neither way. Every row where it is zero reconciles exactly.
    /// Passed through as sent.</para></summary>
    [JsonPropertyName("Other")] public decimal? Other { get; init; }

    /// <summary>Income from business and self-employment. Present on 1,118 rows measured 2026-09-01 and
    /// <b>zero on every one of them</b> — income is disclosed on this path but does not enter
    /// <see cref="Total"/>.</summary>
    [JsonPropertyName("businessAndSelfEmployment")] public decimal? BusinessAndSelfEmployment { get; init; }

    /// <summary>Pension and retirement income. Present on 1,193 rows measured 2026-09-01 and non-zero on
    /// <b>four</b>; income does not enter <see cref="Total"/>.</summary>
    [JsonPropertyName("pensionAndRetirementIncome")] public decimal? PensionAndRetirementIncome { get; init; }

    /// <summary>Income not covered by another income category. Present on 341 rows measured 2026-09-01 and
    /// <b>zero on every one of them</b>.</summary>
    [JsonPropertyName("otherIncome")] public decimal? OtherIncome { get; init; }

    /// <summary>A spouse's income. Present on 153 rows measured 2026-09-01 and <b>zero on every one of
    /// them</b>. One of the two keys the 25-member sample in #57 never saw.</summary>
    [JsonPropertyName("spousalIncome")] public decimal? SpousalIncome { get; init; }

    /// <summary>Investment income and capital gains. Present on 100 rows measured 2026-09-01 and <b>zero on
    /// every one of them</b>. One of the two keys the 25-member sample in #57 never saw.</summary>
    [JsonPropertyName("investmentAndCapitalGains")] public decimal? InvestmentAndCapitalGains { get; init; }

    /// <summary>Stock options held. Present on 66 rows measured 2026-09-01, non-zero on 12.</summary>
    [JsonPropertyName("options")] public decimal? Options { get; init; }

    /// <summary>Asset-backed securities held. Present on 42 rows measured 2026-09-01 — the rarest key on the
    /// path — non-zero on 12.</summary>
    [JsonPropertyName("assetBackedSecurities")] public decimal? AssetBackedSecurities { get; init; }

    /// <summary>Personal loans and other personal debt. Present on 777 rows measured 2026-09-01 and non-zero
    /// on 280 — with <see cref="EducationLiabilities"/>, the liability most often dropped before
    /// #57.</summary>
    [JsonPropertyName("personalLiabilities")] public decimal? PersonalLiabilities { get; init; }

    /// <summary>Student and other education debt. Present on 462 rows measured 2026-09-01 and non-zero on
    /// <b>306</b> — the issue's sample saw it on 5 of 119 rows and ranked it rarest; the census ranks it the
    /// second most consequential of the eleven.</summary>
    [JsonPropertyName("educationLiabilities")] public decimal? EducationLiabilities { get; init; }

    /// <summary>Liabilities not covered by another liability category. Present on 342 rows measured
    /// 2026-09-01, non-zero on 98 — <c>K000389</c>'s 2017 row carries 6,000,000 here against a
    /// <see cref="Total"/> of −73,000.</summary>
    [JsonPropertyName("otherLiabilities")] public decimal? OtherLiabilities { get; init; }
}
