using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>A security FMP no longer carries as listed, from <c>stable/delisted-companies</c>.
///
/// <para>Measured against the live API on 2026-08-26: the five properties below are the entire response, present on
/// every one of the 9,782 rows the endpoint holds, with none missing and none extra. The archive runs from a
/// delisting dated <c>2026-12-30</c> back to <c>2002-01-31</c>.</para>
///
/// <para><b><see cref="DelistedDate"/> can be in the future, and the newest rows are exactly the ones that
/// are.</b> The list is ordered newest-first, so page 0 is where scheduled delistings sit: the first row on
/// 2026-08-26 was <c>NB2.F</c> dated <c>2026-12-30</c>, four months ahead. Treating this endpoint as "securities
/// that have stopped trading" therefore marks live, currently-trading securities as gone. Compare
/// <see cref="DelistedDate"/> against today before acting on it.</para></summary>
public sealed record DelistedCompany
{
    /// <summary>Ticker as FMP spells it, exchange suffix included — <c>MI-UN.TO</c>, <c>6197.T</c>,
    /// <c>NB2.F</c>. The archive is global, like the directories.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>The company name. Spelled <c>companyName</c> on the wire, as on <c>stock-list</c> and unlike
    /// <c>actively-trading-list</c>'s <c>name</c> — see <see cref="CompanySymbol.Name"/>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The exchange's short code — <c>NYSE</c>, <c>NASDAQ</c>, <c>AMEX</c>, <c>TSX</c>, <c>JPX</c>,
    /// <c>FSX</c>, <c>ASX</c>. This is the short form, matching <c>exchangeShortName</c> on the screener rather
    /// than its long <c>exchange</c>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>When the security first listed. Reaches back further than most of the SDK's history —
    /// <c>1980-01-02</c> was the oldest measured.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>When the security stopped, or is scheduled to stop, being listed. A calendar date with no time of
    /// day, so it is a <see cref="LocalDate"/> rather than an <see cref="Instant"/> — there is no timestamp here to
    /// pick a timezone for.
    ///
    /// <para>This is also the sort key: the endpoint returns rows in descending order of this value, which is what
    /// makes a future date land on page 0. See the note on the type.</para></summary>
    [JsonPropertyName("delistedDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? DelistedDate { get; init; }
}
