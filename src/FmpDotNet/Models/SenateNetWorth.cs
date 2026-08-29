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
/// <para>One row per reporting year. Measured 2026-08-29, <c>H000601</c> answered six, 2019 through
/// 2024.</para>
///
/// <para><b>Every one of the fourteen money fields is <see cref="decimal"/>, including the six that looked
/// integral.</b> Measured 2026-08-29 across those six rows, 8 of the 14 changed between bare-integer and
/// decimal-point representation. The other 6 did not, and that is not an exemption: six rows all landing on
/// integers says nothing about the seventh, and one fractional value under <see cref="int"/> costs the whole
/// response rather than the field.</para></summary>
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

    /// <summary>Salary and wage income.</summary>
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
}
