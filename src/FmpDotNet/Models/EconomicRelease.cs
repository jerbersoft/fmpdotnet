using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One scheduled or published macroeconomic release, from <c>stable/economic-calendar</c>.
///
/// <para>Measured against the live API on 2026-08-26 — the eleven properties below are the whole response, with
/// none missing and none extra on any of the 713 rows returned for <c>from=2026-08-25&amp;to=2026-09-01</c> or on
/// the 78 rows returned for the single day <c>2026-08-26</c>.</para>
///
/// <para><b>The endpoint is global and unfiltered, and the SDK keeps it that way.</b> That one week carried 713
/// rows across <b>81</b> distinct countries; a single day carried 78 across 19. There is no <c>country</c> or
/// <c>impact</c> parameter on the wire and the SDK adds neither: which of those rows matter is a question about
/// the caller's strategy, not about the API, and a filter baked in here would be an opinion the caller cannot see
/// and cannot lift. Ask for a range, get everything in it, and use LINQ. That is also why <see cref="Impact"/>
/// stays a raw <see cref="string"/> — see the note on that property.</para>
///
/// <para>Rows arrived <b>newest first</b> in both captures — the 713-row week ran from
/// <c>2026-09-01 23:50:00</c> back to <c>2026-08-25 01:30:00</c>, strictly descending, and the single day ran
/// <c>23:50</c> back to <c>01:00</c>. The SDK does not re-sort; a caller who needs chronological order should say
/// so with an <c>OrderBy</c> rather than rely on this, since nothing in the payload promises it. Both ends of the
/// requested range are inclusive: <c>from=2026-08-25</c> returned rows dated 2026-08-25 and <c>to=2026-09-01</c>
/// returned rows dated 2026-09-01.</para>
///
/// <para>Past and future events share one shape. A published release carries <see cref="Previous"/>,
/// <see cref="Estimate"/>, <see cref="Actual"/> and <see cref="Change"/>; an unreported one leaves them null — and
/// then <see cref="ChangePercentage"/> plays by different rules, which is the trap documented on that
/// property.</para></summary>
public sealed record EconomicRelease
{
    /// <summary>When the release is published, in <b>UTC</b>. Named <c>Timestamp</c> rather than <c>Date</c>
    /// because the wire value carries a time of day and is a moment, not a calendar date: the wire spells it
    /// <c>date</c>, but a US 08:30 release and a Tokyo 23:50 release land on different local days from the same
    /// field, so treating it as a date is how a caller ends up off by one.
    ///
    /// <para><b>The UTC reading is the whole point of this endpoint, and it was established by the DST shift
    /// rather than assumed.</b> The field arrives as <c>"yyyy-MM-dd HH:mm:ss"</c> — space-separated, not ISO-T —
    /// and two anchors measured on 2026-08-26 fix the zone between them:</para>
    /// <list type="bullet">
    ///   <item><description><c>{"date":"2026-08-26 12:30:00","country":"US",
    ///     "event":"Core PCE Price Index MoM (Jul)","impact":"High"}</c> — the BEA releases Personal Income and
    ///     Outlays at 08:30 America/New_York, and 26 August is <b>EDT</b>, UTC−4. 12:30 − 4 = 08:30. ✓</description></item>
    ///   <item><description><c>{"date":"2027-01-27 19:00:00","country":"US",
    ///     "event":"Fed Interest Rate Decision","impact":"High"}</c> — the FOMC statement lands at 14:00
    ///     America/New_York, and 27 January is <b>EST</b>, UTC−5. 19:00 − 5 = 14:00. ✓</description></item>
    /// </list>
    /// <para>Two <i>different</i> offsets six months apart is what rules out a fixed one. A hardcoded −5 would put
    /// the August row an hour wrong and a hardcoded −4 would put the January row an hour wrong, so no single
    /// offset can be right for the whole calendar; only "the string is UTC, and Eastern is derived from it through
    /// the tz database" satisfies both. Converting into a local zone is therefore the <b>caller's</b> business and
    /// must go through tzdb — <c>instant.InZone(DateTimeZoneProviders.Tzdb["America/New_York"])</c> — never
    /// arithmetic on −4 or −5.</para>
    ///
    /// <para>Read with <see cref="NullableFmpInstantJsonConverter"/>, the <b>UTC</b> converter, and deliberately
    /// <b>not</b> with <see cref="NullableEasternInstantJsonConverter"/>, which the statement endpoints'
    /// <c>acceptedDate</c> uses. Both parse the identical <c>"uuuu-MM-dd HH:mm:ss"</c> shape, so the string cannot
    /// tell you which is right and the compiler will never object; picking the wrong one shifts every value on
    /// every row by 4 or 5 hours, silently, and turns an 08:30 CPI print into a 04:30 one. If a future reader is
    /// about to "fix" this to Eastern: the two anchors above are why it is not Eastern, and
    /// <c>EconomicsEndpointsTests</c> asserts both of them through tzdb.</para>
    ///
    /// <para>This reading is <b>corroborated, not merely observed once</b>: the consumer application this SDK
    /// replaces derived UTC independently, from these same two anchors, and likewise converts through
    /// <c>DateTimeZoneProviders.Tzdb["America/New_York"]</c> rather than an offset. Two independent derivations
    /// agreeing is worth considerably more than one measurement.</para>
    ///
    /// <para><b>Zone once, then derive both parts from the zoned value.</b> The type is a single
    /// <see cref="Instant"/> rather than a separate date and time-of-day precisely so that the obvious bug cannot
    /// be written: a release near UTC midnight belongs to the <i>previous</i> Eastern day, so computing the day
    /// from one conversion and the clock time from another puts them on different days. Convert once —
    /// <c>var et = release.Timestamp!.Value.InZone(tzdb); var day = et.Date; var clock = et.TimeOfDay;</c> — and
    /// the pair is consistent by construction. Worked example, verified by the consumer's own regression test:
    /// <c>2026-01-02 03:00:00</c> UTC is <c>2026-01-01</c> at <c>22:00</c> in New York.</para>
    ///
    /// <para>Nullable because a converter that threw on one malformed stamp would cost the caller every other
    /// field on the record — and, on a list endpoint, every other row. No null was observed in either
    /// capture.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableFmpInstantJsonConverter))]
    public Instant? Timestamp { get; init; }

    /// <summary>Two-letter country code — <c>US</c>, <c>DE</c>, <c>AU</c>, <c>JP</c>, <c>UK</c>. Every one of the
    /// 713 rows measured on 2026-08-26 was exactly two characters, across 81 distinct codes.
    ///
    /// <para><b>It is not ISO-3166 and must not be parsed as though it were.</b> <c>EU</c> appears as a country —
    /// it is the euro area speaking through the ECB, which is a currency bloc rather than a country and is only
    /// "exceptionally reserved" in ISO-3166. <c>UK</c> is used where ISO-3166 says <c>GB</c>. So this is a lookup
    /// key against FMP's own vocabulary: match it as a string, and treat a code you do not recognise as a code you
    /// do not recognise rather than as bad data.</para></summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>The release's name as FMP spells it, including the period it covers —
    /// <c>"Core PCE Price Index MoM (Jul)"</c>, <c>"GDP Growth Rate QoQ (Q2)"</c>,
    /// <c>"Fed Interest Rate Decision"</c>, <c>"ECB Cipollone Speech"</c>.
    ///
    /// <para>This is the only field that says <i>what</i> the row is, and there is no identifier beside it: no
    /// series code, no event id. So (<see cref="Timestamp"/>, <see cref="Country"/>, <see cref="Event"/>) is the
    /// closest thing to a key, and matching an event across days means matching on this string, whose parenthesised
    /// period changes every release. Non-null on all 713 rows measured.</para></summary>
    [JsonPropertyName("event")] public string? Event { get; init; }

    /// <summary>ISO-4217 currency of the reporting country — <c>USD</c>, <c>EUR</c>, <c>JPY</c>. Non-null on all
    /// 713 rows measured on 2026-08-26. It describes the country, not the figures: it is populated on speeches and
    /// on percentage releases, where no amount of money is involved.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>The prior period's reading, in <see cref="Unit"/>. Null on an event that has no series behind it —
    /// a speech or a press conference — and on some first-time releases.</summary>
    [JsonPropertyName("previous")] public decimal? Previous { get; init; }

    /// <summary>The consensus forecast, in <see cref="Unit"/>. Null where no consensus is published, which is the
    /// common case rather than the exception: only 26 of the 78 rows measured for 2026-08-26 carried one.</summary>
    [JsonPropertyName("estimate")] public decimal? Estimate { get; init; }

    /// <summary>The published reading, in <see cref="Unit"/>, or null while the event is still in the future. This
    /// is the field that says whether a row has happened yet — 66 of the 78 rows for 2026-08-26 had it, the rest
    /// were later that day or carried no figure at all.</summary>
    [JsonPropertyName("actual")] public decimal? Actual { get; init; }

    /// <summary><see cref="Actual"/> minus <see cref="Previous"/>, in <see cref="Unit"/> — FMP's arithmetic, not
    /// the SDK's, and it is a period-over-period change rather than a surprise against
    /// <see cref="Estimate"/>.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>FMP's importance label. <b>A raw string on purpose, not an enum.</b>
    ///
    /// <para>Measured across the 713 rows of 2026-08-25…2026-09-01: <c>"Low"</c> ×556, <c>"Medium"</c> ×133,
    /// <c>"High"</c> ×24 — three values, and no nulls. An enum would fit that sample exactly, which is precisely
    /// the argument against it: the day FMP adds a fourth label the enum either throws and costs the caller the
    /// whole response, or maps to a default and silently reports the wrong importance. Neither failure is visible
    /// in a diff. A string cannot be wrong about a value it has never seen.</para>
    ///
    /// <para>It is also not the SDK's place to rank these. Callers who want High-only compare the string; callers
    /// who disagree with FMP's ranking — and traders do — keep their own table keyed on
    /// <see cref="Event"/>.</para></summary>
    [JsonPropertyName("impact")] public string? Impact { get; init; }

    /// <summary><see cref="Change"/> as a percentage of <see cref="Previous"/>, on 0–100 rather than as a
    /// fraction: a 0.1 → 0.2 move arrives as <c>100</c>, not <c>1.0</c>.
    ///
    /// <para><b>The trap: on this field a real zero and an absent value are indistinguishable.</b> Unreported
    /// events — those with <see cref="Previous"/>, <see cref="Estimate"/>, <see cref="Actual"/> and
    /// <see cref="Change"/> all null — mostly arrive with <c>changePercentage: 0</c> rather than null. Measured
    /// 2026-08-26: of the 15 such rows in the 713-row week, <b>12 carried 0 and only 3 carried null</b>, and 153
    /// of all 713 rows were null here. So the zero is <i>not</i> a reliable "unreported" marker either — both
    /// shapes occur, and a genuine no-change release (Core PCE Price Index YoY (Jul) came in at 3.3 against a
    /// previous 3.3) reports the same <c>0</c> as a speech that has no figures at all.</para>
    ///
    /// <para>The consequence is concrete: anything computing a surprise, a hit rate or an average move must gate
    /// on <see cref="Actual"/> being non-null and must not treat <c>ChangePercentage == 0</c> as "no data". Doing
    /// it the other way round silently averages phantom zeros into the result — and the zeros outnumber the nulls
    /// four to one, so the error is large and points in one direction.</para></summary>
    [JsonPropertyName("changePercentage")] public decimal? ChangePercentage { get; init; }

    /// <summary>Unit of <see cref="Previous"/>, <see cref="Estimate"/>, <see cref="Actual"/> and
    /// <see cref="Change"/>: <c>"%"</c>, <c>"M"</c>, <c>"B"</c>, <c>"K"</c>, <c>"Points"</c>, or null when the row
    /// carries no figures — a speech — or when FMP names no unit. Of the 78 rows measured for 2026-08-26: 56 <c>%</c>,
    /// 7 <c>M</c>, 3 <c>Points</c>, 2 <c>B</c>, 10 null.
    ///
    /// <para><c>M</c> and <c>B</c> are magnitudes, so the figures are <b>already scaled</b>: a Japanese bond-flow
    /// row reads <c>previous: 1135.1</c> with <c>unit: "B"</c>, meaning ¥1,135.1 billion and not ¥1,135.1. Two
    /// rows are comparable only when their units and their <see cref="Currency"/> both match.</para></summary>
    [JsonPropertyName("unit")] public string? Unit { get; init; }
}
