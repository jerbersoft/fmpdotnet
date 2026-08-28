using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One line of one 13F filing — a single position a filer reported holding at a quarter end, from
/// <c>stable/institutional-ownership/extract</c>.
///
/// <para><b>This is the raw infotable, one row per security.</b> A large filer's quarter runs to thousands of
/// rows: State Street's 2026 Q2 answered 4,177. The endpoint accepts <c>limit</c> and ignores it — measured
/// 2026-08-28, <c>limit=5</c> returned all 4,177, byte-identical to no limit at all — so
/// <see cref="Endpoints.InstitutionalOwnershipEndpoints.GetHoldingsAsync"/> offers neither <c>limit</c> nor
/// <c>page</c>. Take what comes back.</para>
///
/// <para><b>Three in ten rows have no ticker.</b> See <see cref="Symbol"/>.</para></summary>
public sealed record InstitutionalHolding
{
    /// <summary>The quarter end the filing reports on — <c>2026-06-30</c>.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The date the filing was submitted. Bare ISO on this path — <c>"2026-08-07"</c> — unlike
    /// <c>InstitutionalFiling.FilingDate</c>, which carries a dummy midnight and needs a different
    /// converter.</summary>
    [JsonPropertyName("filingDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? FilingDate { get; init; }

    /// <summary>The date EDGAR accepted the submission. <b>A date, not a timestamp, on this path</b> — measured
    /// 2026-08-28 it carries no time component at all, and was equal to <see cref="FilingDate"/> on every row
    /// sampled. <c>InstitutionalFiling.AcceptedDate</c> is the one place in this group where it is a real
    /// clock.</summary>
    [JsonPropertyName("acceptedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? AcceptedDate { get; init; }

    /// <summary>The filer's SEC Central Index Key, zero-padded to ten characters. An institutional filer, not
    /// an issuer. <see cref="string"/> for the reason on <see cref="IndustryClassification.Cik"/>: the padding
    /// is the value.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The held security's CUSIP. Populated on every row measured, including the rows with no
    /// <see cref="Symbol"/> — which makes it the identifier to key on if you need one that is always
    /// there.</summary>
    [JsonPropertyName("securityCusip")] public string? SecurityCusip { get; init; }

    /// <summary>The ticker, <b>or <see langword="null"/> — which happened on 2,209 of 7,346 rows measured
    /// 2026-08-28, 30.1%.</b>
    ///
    /// <para>A 13F holding need not have a ticker: bonds, warrants and private placements are reportable and do
    /// not have one. A consumer keying holdings by symbol silently drops three rows in ten. Use
    /// <see cref="SecurityCusip"/> or <see cref="NameOfIssuer"/> when you need every row.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's name as the filer typed it — <c>"BOSTON BEER INC"</c>. Upper case, unnormalised,
    /// and populated on every row measured.</summary>
    [JsonPropertyName("nameOfIssuer")] public string? NameOfIssuer { get; init; }

    /// <summary>How many shares were held.
    ///
    /// <para><b><see cref="decimal"/> although every one of the 7,346 rows measured was integral.</b> The whole
    /// family of share and money fields in this group takes <c>decimal?</c> on one piece of evidence:
    /// <c>industryValue</c> on <c>institutional-ownership/industry-summary</c> is the same kind of quantity and
    /// is fractional on 53 of 394 rows. Binding a fractional value to an integer property makes
    /// <c>System.Text.Json</c> throw, and <c>FmpTransport</c> does not wrap the deserialiser — so one such value
    /// costs the caller the entire response, not the field. See <see cref="CompanyProfile.Volume"/> for the
    /// time this SDK learned that the expensive way.</para></summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The class of security — <c>"COM"</c>, <c>"CL A"</c>. The filer's own spelling.</summary>
    [JsonPropertyName("titleOfClass")] public string? TitleOfClass { get; init; }

    /// <summary>What <see cref="Shares"/> counts — <c>"SH"</c> for shares, <c>"PRN"</c> for principal
    /// amount.</summary>
    [JsonPropertyName("sharesType")] public string? SharesType { get; init; }

    /// <summary>Whether the position is a put, a call, or the underlying — <b>and it was blank on all 7,346
    /// rows measured 2026-08-28, across three filers.</b> Never null, never populated.
    ///
    /// <para>Modelled anyway. The same field on
    /// <c>institutional-ownership/extract-analytics/holder</c> <i>is</i> populated (<c>"Share"</c>), so this is
    /// a field FMP sends and could start filling. Omitting it would leave a consumer no way to reach it;
    /// modelling a constant costs one property. The emptiness is recorded here as a measurement rather than
    /// discovered as a bug.</para></summary>
    [JsonPropertyName("putCallShare")] public string? PutCallShare { get; init; }

    /// <summary>The position's reported market value in dollars. <c>decimal?</c> for the reason on
    /// <see cref="Shares"/>.</summary>
    [JsonPropertyName("value")] public decimal? Value { get; init; }

    /// <summary>The EDGAR filing-index page for the accession. Identical across every row of one
    /// filing.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }

    /// <summary>The infotable XML itself, inside the accession.</summary>
    [JsonPropertyName("finalLink")] public string? FinalLink { get; init; }
}
