using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One officer's reported compensation for one fiscal year, from
/// <c>stable/governance-executive-compensation</c>.
///
/// <para><b>The endpoint returns the filer's whole history in a single call.</b> Measured 2026-08-27:
/// <c>AAPL</c> answered 339 rows spanning 1999 → 2025, <c>JPM</c> 160. There is no server-side year filter —
/// <c>year</c> is documented and ignored — so a caller holding one of these lists is holding decades, not a
/// year. See
/// <see cref="Endpoints.CompanyEndpoints.GetExecutiveCompensationAsync(string, CancellationToken)"/>.</para>
///
/// <para>All fifteen fields below were populated on every row of both filers measured.</para>
///
/// <para><b>Plan gating, measured by a third party rather than here.</b> Every path in this slice answered 200
/// on the Ultimate key this SDK was measured with, so this repo's own measurements say nothing about lower
/// tiers. An independent client recorded this endpoint as available on its plans on 2026-08-23; the two
/// endpoints in this group that it recorded as 402 are noted on their own methods.</para></summary>
public sealed record ExecutiveCompensation
{
    /// <summary>The filer's SEC Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The filer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The filer's name.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>When the proxy statement was filed.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>When EDGAR accepted the filing. EDGAR's <b>Eastern</b> wall clock, matching
    /// <see cref="IncomeStatement.AcceptedDate"/> — not UTC.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableEasternInstantJsonConverter))]
    public Instant? AcceptedDate { get; init; }

    /// <summary>The officer's name and title, run together in one string with no separator —
    /// <c>"Luca Maestri Former Senior Vice President, Chief Financial Officer"</c>.
    ///
    /// <para><b>Treat it as opaque.</b> There is no delimiter to split on and no reliable rule for where the
    /// name ends: titles begin with words that also appear in names, and the obvious split on the first comma
    /// cuts inside the title rather than between the two. If you need the name alone, match it against
    /// <see cref="Endpoints.CompanyEndpoints.GetKeyExecutivesAsync(string, CancellationToken)"/>, which reports
    /// name and title as separate fields.</para></summary>
    [JsonPropertyName("nameAndPosition")] public string? NameAndPosition { get; init; }

    /// <summary>The fiscal year the compensation was reported for.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>Base salary. Zero is a real reported zero here, not an absence.</summary>
    [JsonPropertyName("salary")] public decimal? Salary { get; init; }

    /// <summary>Cash bonus. Reported as <c>0</c> on both filers measured — a real zero, not a missing
    /// field.</summary>
    [JsonPropertyName("bonus")] public decimal? Bonus { get; init; }

    /// <summary>Value of stock awards.</summary>
    [JsonPropertyName("stockAward")] public decimal? StockAward { get; init; }

    /// <summary>Value of option awards. Reported as <c>0</c> on both filers measured.</summary>
    [JsonPropertyName("optionAward")] public decimal? OptionAward { get; init; }

    /// <summary>Non-equity incentive plan compensation.</summary>
    [JsonPropertyName("incentivePlanCompensation")]
    public decimal? IncentivePlanCompensation { get; init; }

    /// <summary>Everything the other components do not cover.</summary>
    [JsonPropertyName("allOtherCompensation")] public decimal? AllOtherCompensation { get; init; }

    /// <summary>Total reported compensation for the year.</summary>
    [JsonPropertyName("total")] public decimal? Total { get; init; }

    /// <summary>URL of the EDGAR filing index the row was taken from.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}

/// <summary>Average executive compensation for one SEC industry classification in one year, from
/// <c>stable/executive-compensation-benchmark</c>.
///
/// <para><b>Omitting the year answers LAST year, not this one.</b> Measured on 2026-08-27, the bare call
/// answered 377 rows every one of them stamped <c>2024</c>; <c>year=2025</c> answered 365 and <c>year=2010</c>
/// answered 386. A caller who omits the year and reads the rows as current is two years out. <c>year=1990</c>
/// answers a single row whose average is <c>0</c>, so a year outside the data is not an error either.</para>
///
/// <para><b>Cold, this endpoint is slow enough to trip a default HTTP timeout.</b> The first call to
/// <c>year=2025</c> took <b>37.18 s</b>; the identical call later took <b>0.53 s</b>. The SDK's own
/// <c>FmpOptions.RequestTimeout</c> default accommodates it, but a caller layering a shorter timeout of their
/// own will see the first call fail and every subsequent one succeed.</para>
///
/// <para><b>Plan gating, measured by a third party rather than here.</b> An independent client recorded this
/// endpoint answering <b>402 on free and on Starter</b>, and working on Premium, on 2026-08-23. Every path in
/// this slice answered 200 on the Ultimate key this SDK was measured with, so that restriction is not something
/// this repo's own measurements could have found.</para></summary>
public sealed record ExecutiveCompensationBenchmark
{
    /// <summary>The SEC industry classification, upper-cased as FMP sends it — <c>"ADHESIVES &amp;
    /// SEALANTS"</c>. Not the same vocabulary as <c>CompanyProfile.Industry</c>.</summary>
    [JsonPropertyName("industryTitle")] public string? IndustryTitle { get; init; }

    /// <summary>The year the average covers. See the type note: this is not necessarily the year you asked
    /// for, because omitting the parameter gets last year rather than this one.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>Mean total compensation across the industry's filers.
    ///
    /// <para><b>Fractional</b> — <c>784407.5555555555</c>, and 339 of the 377 rows measured on 2026-08-27 were.
    /// It is a mean, so a whole number is the exception. A <c>long?</c> binding would throw on almost every
    /// row.</para></summary>
    [JsonPropertyName("averageCompensation")] public decimal? AverageCompensation { get; init; }
}
