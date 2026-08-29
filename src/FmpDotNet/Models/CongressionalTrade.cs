using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One disclosed trade by a member of Congress or their immediate family, from any of the eight
/// congressional trade paths.
///
/// <para><b>One record for eight paths.</b> <c>house-latest</c>, <c>house-trades</c>,
/// <c>house-trades-by-id</c>, <c>house-trades-by-name</c> and their four Senate counterparts all answer these
/// keys. Measured 2026-08-29, seven of the eight carry all sixteen; see
/// <see cref="CapitalGainsOver200Usd"/> for the one that does not.</para>
///
/// <para><b>Nothing here is an enum.</b> <see cref="Type"/>, <see cref="AssetType"/> and <see cref="Owner"/>
/// read like closed vocabularies and are not: measured 2026-08-29, the House and Senate feeds already
/// disagree, with <c>Cryptocurrency</c> appearing only on the House side and <c>Mutual Fund</c> only on the
/// Senate side. The union of seven <see cref="AssetType"/> values is a floor, not a vocabulary, and a closed
/// C# enum over an open server-side list is a breaking change waiting for a Tuesday.</para>
///
/// <para><b>Empty strings are kept as empty strings.</b> <see cref="Comment"/> was blank on every one of the
/// 200 rows measured across both latest feeds, and <see cref="SenateId"/> is the only field here that arrives
/// as a JSON <see langword="null"/>. Both forms occur and they mean different things.</para></summary>
public sealed record CongressionalTrade
{
    /// <summary>The ticker traded, as FMP spells it. Blank on 3 of 100 House rows measured
    /// 2026-08-29 — a disclosed asset with no ticker, not a missing value.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The member's Bioguide identifier — <c>M001217</c>, <c>P000197</c>.
    ///
    /// <para><b>Named <c>senateID</c> on the wire even for Representatives</b>, which is FMP's naming rather
    /// than a fault in the capture. This is the value
    /// <see cref="Endpoints.CongressEndpoints.GetHouseTradesByMemberAsync"/> filters on.</para>
    ///
    /// <para>The only field on this record measured to arrive as JSON <see langword="null"/> — 2 of 100 House
    /// rows on 2026-08-29.</para></summary>
    [JsonPropertyName("senateID")] public string? SenateId { get; init; }

    /// <summary>The date the disclosure was filed. Always later than <see cref="TransactionDate"/>.</summary>
    [JsonPropertyName("disclosureDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DisclosureDate { get; init; }

    /// <summary>The date the trade was executed.</summary>
    [JsonPropertyName("transactionDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? TransactionDate { get; init; }

    /// <summary>The member's given name.</summary>
    [JsonPropertyName("firstName")] public string? FirstName { get; init; }

    /// <summary>The member's surname. This is what
    /// <see cref="Endpoints.CongressEndpoints.GetHouseTradesByNameAsync"/> matches on.</summary>
    [JsonPropertyName("lastName")] public string? LastName { get; init; }

    /// <summary>The member's full name as the disclosure spells it.</summary>
    [JsonPropertyName("office")] public string? Office { get; init; }

    /// <summary>The district for a Representative (<c>FL23</c>) or the state for a Senator (<c>PA</c>). Blank
    /// on 28 of 100 House rows measured 2026-08-29.</summary>
    [JsonPropertyName("district")] public string? District { get; init; }

    /// <summary>Who holds the position — <c>Self</c>, <c>Spouse</c>, <c>Joint</c>, or blank. Blank on 54 of
    /// 100 House rows and 2 of 100 Senate rows measured 2026-08-29, and kept blank rather than
    /// nulled.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }

    /// <summary>The asset as the disclosure describes it, which is prose rather than a normalised
    /// name.</summary>
    [JsonPropertyName("assetDescription")] public string? AssetDescription { get; init; }

    /// <summary>What kind of asset. Seven values measured 2026-08-29 across both feeds — <c>Stock</c>,
    /// <c>Stock Option</c>, <c>ETF</c>, <c>REIT</c>, <c>Corporate Bond</c>, <c>Mutual Fund</c>,
    /// <c>Cryptocurrency</c> — and the two feeds do not agree on that list, so it is a floor rather than a
    /// vocabulary. See the record summary.</summary>
    [JsonPropertyName("assetType")] public string? AssetType { get; init; }

    /// <summary>The transaction — <c>Purchase</c>, <c>Sale</c> or <c>Exchange</c> on every row measured
    /// 2026-08-29.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>The disclosed value, as a bracketed band rather than a figure — <c>$1,001 - $15,000</c>
    /// through <c>$1,000,001 - $5,000,000</c>, seven distinct values measured 2026-08-29.
    ///
    /// <para><b>A string, and deliberately not parsed.</b> Congressional disclosure reports a range, so there
    /// is no exact amount to model and none is invented. FMP publishes structured bounds only on the net-worth
    /// path — see <c>NetWorthRange</c>.</para></summary>
    [JsonPropertyName("amount")] public string? Amount { get; init; }

    /// <summary>Whether the sale realised more than $200 in capital gains.
    ///
    /// <para><b>A string, not a <see cref="bool"/>, and both halves of that are measured.</b> It arrives as
    /// the JSON string <c>"False"</c>, and measured 2026-08-29 against this library's own
    /// <c>FmpJsonContext</c> options a <c>bool?</c> property <b>throws</b> on it — the context's
    /// <c>NumberHandling = AllowReadingFromString</c> rescues numbers, not booleans. Only <c>"False"</c> was
    /// ever observed, so the spelling of the affirmative is unknown and a converter would be guessing at the
    /// one value it exists to handle.</para>
    ///
    /// <para><b>Always <see langword="null"/> from <c>senate-latest</c>.</b> That path is the only one of the
    /// eight that omits the key — 0 of its 100 rows carried it on 2026-08-29, against 100% on the other
    /// seven.</para></summary>
    [JsonPropertyName("capitalGainsOver200USD")] public string? CapitalGainsOver200Usd { get; init; }

    /// <summary>The filer's note. Blank on all 200 rows measured across both latest feeds on
    /// 2026-08-29.</summary>
    [JsonPropertyName("comment")] public string? Comment { get; init; }

    /// <summary>The disclosure document — a House clerk PDF or a Senate EFD record.</summary>
    [JsonPropertyName("link")] public string? Link { get; init; }
}
