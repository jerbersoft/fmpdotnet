using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One day an exchange is closed, or closes early, from <c>stable/holidays-by-exchange</c>.
///
/// <para><b>Two shapes, and the difference is the whole point of this record.</b> Measured across 446 rows on
/// 2026-08-30, every row is one of exactly two states and they are exact complements:</para>
///
/// <list type="bullet">
///   <item><description><b>396 rows</b> — <c>isClosed: true</c>, <c>isFullyClosed</c> <b>absent</b>, no
///     adjusted time. The exchange did not trade.</description></item>
///   <item><description><b>50 rows</b> — <c>isClosed: null</c>, <c>isFullyClosed: false</c>,
///     <c>adjCloseTime</c> set. The exchange traded a shortened session.</description></item>
///   <item><description><b>0 rows</b> — <c>isClosed: false</c>. The wire has never been observed sending
///     it.</description></item>
/// </list>
///
/// <para><b>So <see cref="IsClosed"/> alone cannot answer "is the exchange closed that day?"</b> Its
/// <see langword="null"/> means <i>an early close</i>, not <i>unknown</i>, and a caller who reads it as
/// unknown treats 50 measured rows as unanswerable. <see cref="ClosesEarly"/> is the derived predicate that
/// says which state a row is in; the wire pair is kept verbatim beside it, because
/// <c>isClosed: false</c> has never been observed and an enum collapsing the two states would have nowhere
/// to put it if it appeared.</para>
///
/// <para><b>The response carries no time zone.</b> Verified absent on all 446 rows. A caller who needs an
/// instant from <see cref="AdjustedCloseTime"/> must take the zone from
/// <see cref="ExchangeMarketHours.Timezone"/> on the matching exchange.</para></summary>
public sealed record ExchangeHoliday
{
    /// <summary>The exchange code the row belongs to, echoed from the request. Populated on all 446 rows
    /// measured 2026-08-30.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The date of the holiday, ISO on the wire. Populated on all 446 rows measured
    /// 2026-08-30.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The holiday's name — <c>"Independence Day"</c>, <c>"Christmas"</c>. Not unique within an
    /// exchange: the name repeats once a year, and NASDAQ's 446 rows reach 2032-12-31. Populated on all 446
    /// rows measured 2026-08-30.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Whether the exchange was fully shut — <b><see langword="true"/> or
    /// <see langword="null"/>, never <see langword="false"/></b>.
    ///
    /// <para><b>Read the record's summary before using this field.</b> Measured across 446 rows on
    /// 2026-08-30 it was <see langword="true"/> on 396 and <see langword="null"/> on 50, and
    /// <see langword="false"/> on none. The <see langword="null"/> rows are early closes, not unknowns —
    /// <see cref="ClosesEarly"/> is the predicate that says so.</para></summary>
    [JsonPropertyName("isClosed")] public bool? IsClosed { get; init; }

    /// <summary>The shortened session's opening time, with no zone attached.
    ///
    /// <para><b>Never observed carrying a value.</b> It was <see langword="null"/> on all 446 rows measured
    /// 2026-08-30. It is modelled because the key is present on every row, and this doc records the absence
    /// rather than claiming the field is always null — those are different statements and only the first is
    /// measured.</para></summary>
    [JsonPropertyName("adjOpenTime")]
    [JsonConverter(typeof(LocalTimeJsonConverter))]
    public LocalTime? AdjustedOpenTime { get; init; }

    /// <summary>The shortened session's closing time, with no zone attached — <c>13:00</c> on 49 of the 50
    /// early closes measured 2026-08-30, and <c>13:30</c> on the fiftieth (2015-11-27).
    ///
    /// <para><b>No offset and no zone.</b> The response has no <c>timezone</c> key, verified absent on all
    /// 446 rows, so an instant needs <see cref="ExchangeMarketHours.Timezone"/> from
    /// <c>stable/exchange-market-hours</c> for the same exchange.</para></summary>
    [JsonPropertyName("adjCloseTime")]
    [JsonConverter(typeof(LocalTimeJsonConverter))]
    public LocalTime? AdjustedCloseTime { get; init; }

    /// <summary>FMP's own flag for the early-close shape — <see langword="false"/> on the 50 early closes
    /// and <b>absent</b> on the other 396, measured 2026-08-30.
    ///
    /// <para>Kept verbatim rather than folded into <see cref="ClosesEarly"/>, because it is what the wire
    /// sent and because a future <see langword="true"/> would carry information this SDK has no measurement
    /// for.</para></summary>
    [JsonPropertyName("isFullyClosed")] public bool? IsFullyClosed { get; init; }

    /// <summary>The exchange traded a shortened session that day rather than closing.
    ///
    /// <para><b>Derived from <see cref="AdjustedCloseTime"/> and not from <see cref="IsFullyClosed"/>,
    /// deliberately.</b> Both candidate signals selected the <b>identical</b> 50 rows across all 446 measured
    /// 2026-08-30, so the choice is not about accuracy. <see cref="AdjustedCloseTime"/> wins because it does
    /// not depend on a key that is absent from 89% of rows: a response that stopped sending
    /// <c>isFullyClosed</c> would silently turn every early close into a non-event under the other
    /// reading.</para></summary>
    [JsonIgnore] public bool ClosesEarly => AdjustedCloseTime is not null;
}
