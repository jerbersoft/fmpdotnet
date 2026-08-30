using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace FmpDotNet.Models;

/// <summary>One exchange's trading hours, from <c>stable/all-exchange-market-hours</c> and
/// <c>stable/exchange-market-hours</c>.
///
/// <para><b>One record for both paths, because the wire sends one row.</b> For each of seven exchanges
/// cross-checked 2026-08-30, the single row from <c>exchange-market-hours?exchange=X</c> compared <b>equal,
/// key for key and value for value</b>, to that exchange's row inside the 81-row
/// <c>all-exchange-market-hours</c> response.</para>
///
/// <para><b>The hours arrive as text and are parsed here rather than by a converter, and that is a decision
/// with a reason.</b> A <c>JsonConverter&lt;OffsetTime?&gt;</c> sees one field and can set one property, so
/// nothing could populate <see cref="IsClosedToday"/> — and two properties cannot share one
/// <see cref="JsonPropertyNameAttribute"/>. Binding the text and computing the time is the only shape that
/// gives a caller a real time type <b>and</b> keeps the <c>"CLOSED"</c> sentinel distinguishable from "FMP
/// sent something this SDK could not parse". It also preserves the wire exactly, which is the house
/// rule.</para>
///
/// <para><b>Nothing on this record says whether you can trade right now.</b> <see cref="IsClosedToday"/> is
/// about the exchange's own local calendar day and <see cref="IsMarketOpen"/> is about the instant of the
/// call. They answer different questions and both are surfaced.</para></summary>
public sealed record ExchangeMarketHours
{
    /// <summary>The pattern every hour string on this record is read with.
    ///
    /// <para><c>o&lt;m&gt;</c> and not <c>o&lt;G&gt;</c>: verified against NodaTime 3.2.2 on 2026-08-30, this
    /// pattern formats back <b>byte-identically</b> to what FMP sent — <c>+09:00</c> — while <c>o&lt;G&gt;</c>
    /// emits <c>+09</c> for a whole-hour offset and <c>Z</c> for zero.</para></summary>
    private static readonly OffsetTimePattern HourPattern =
        OffsetTimePattern.CreateWithInvariantCulture("hh:mm tt o<m>");

    /// <summary>FMP's exchange code — <c>"NASDAQ"</c>, <c>"JPX"</c>, <c>"KLS"</c>. 81 distinct values measured
    /// 2026-08-30, of which the 63 that <see cref="FmpDotNet.Endpoints.DirectoryEndpoints.GetExchangesAsync"/>
    /// returns are a subset. The code is case-insensitive on the wire.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The exchange's full name — <c>"Tokyo Stock Exchange"</c>. Populated on all 81 rows measured
    /// 2026-08-30. <b>Not</b> accepted as the <c>exchange</c> argument: measured the same day,
    /// <c>exchange=NASDAQ%20Global%20Market</c> is an HTTP 400.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The opening bell exactly as FMP sent it — <c>"09:00 AM +09:00"</c>, or the literal
    /// <c>"CLOSED"</c>.
    ///
    /// <para>Bound as text so that <see cref="IsClosedToday"/> can exist; the parsed value is
    /// <see cref="OpeningHour"/>. Measured 2026-08-30, <c>"CLOSED"</c> filled <b>124 of 176</b> hour slots
    /// across the 81 rows.</para></summary>
    [JsonPropertyName("openingHour")] public string? OpeningHourText { get; init; }

    /// <summary>The closing bell exactly as FMP sent it, or <c>"CLOSED"</c>. See
    /// <see cref="OpeningHourText"/>.</summary>
    [JsonPropertyName("closingHour")] public string? ClosingHourText { get; init; }

    /// <summary>The <b>afternoon</b> session's opening, exactly as FMP sent it — present on only seven
    /// exchanges.
    ///
    /// <para><b>Absent from 74 of 81 rows measured 2026-08-30, and that is normal rather than missing
    /// data.</b> The seven that carry it all break for lunch: SET (Bangkok), JKT (Jakarta), JPX (Tokyo), SHH
    /// (Shanghai), SHZ (Shenzhen), SES (Singapore) and HOSE (Ho Chi Minh). A record built from the response's
    /// first row — ASX, six keys — reports Tokyo closing at 11:30 AM and loses the larger half of its trading
    /// day.</para></summary>
    [JsonPropertyName("openingAdditional")] public string? OpeningAdditionalText { get; init; }

    /// <summary>The afternoon session's close, exactly as FMP sent it. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonPropertyName("closingAdditional")] public string? ClosingAdditionalText { get; init; }

    /// <summary>The exchange's IANA time zone identifier — <c>"Asia/Tokyo"</c>, <c>"America/New_York"</c>.
    ///
    /// <para>All 81 values measured 2026-08-30 resolved as IANA identifiers (52 distinct), with no
    /// abbreviation and no fixed offset among them, so this can be handed straight to
    /// <c>DateTimeZoneProviders.Tzdb</c>. The SDK does not resolve it: which tzdb version to trust is an
    /// application decision, and resolving it here would bake this SDK's NodaTime version into the
    /// answer.</para></summary>
    [JsonPropertyName("timezone")] public string? Timezone { get; init; }

    /// <summary>Whether the exchange was trading at the instant of the call.
    ///
    /// <para><b>Measured <see langword="false"/> on all 81 rows, on every capture, and the <see langword="true"/>
    /// case is unmeasured.</b> Every capture behind this record was taken on Sunday 2026-08-30. What is
    /// measured is the field's <i>type</i> — a JSON boolean on all 81 rows — and nothing else. This
    /// documentation deliberately describes no behaviour nobody observed.</para>
    ///
    /// <para>Not the same question as <see cref="IsClosedToday"/>, which is about the exchange's local
    /// calendar day rather than this instant.</para></summary>
    [JsonPropertyName("isMarketOpen")] public bool? IsMarketOpen { get; init; }

    /// <summary>The opening bell as a time with its UTC offset, or <see langword="null"/> when the wire sent
    /// <c>"CLOSED"</c> or anything else unparseable. Read <see cref="IsClosedToday"/> to tell those two
    /// apart.</summary>
    [JsonIgnore] public OffsetTime? OpeningHour => ParseHour(OpeningHourText);

    /// <summary>The closing bell as a time with its UTC offset, or <see langword="null"/>. See
    /// <see cref="OpeningHour"/>.</summary>
    [JsonIgnore] public OffsetTime? ClosingHour => ParseHour(ClosingHourText);

    /// <summary>The afternoon session's opening as a time with its UTC offset, or <see langword="null"/> on
    /// the 74 of 81 exchanges that do not break for lunch. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonIgnore] public OffsetTime? OpeningAdditional => ParseHour(OpeningAdditionalText);

    /// <summary>The afternoon session's close as a time with its UTC offset, or <see langword="null"/>. See
    /// <see cref="OpeningAdditionalText"/>.</summary>
    [JsonIgnore] public OffsetTime? ClosingAdditional => ParseHour(ClosingAdditionalText);

    /// <summary>The exchange is not trading on its own local date — the wire sent the literal
    /// <c>"CLOSED"</c> rather than a time.
    ///
    /// <para><b>This is about the exchange's local calendar day, not about this instant.</b> Established
    /// rather than assumed: resolving each row's <see cref="Timezone"/> against the capture's HTTP
    /// <c>Date</c> header on 2026-08-30, 61 of the 62 closures were local <b>weekends</b>, and the four
    /// exchanges showing hours on a local weekend were exactly the Gulf markets EGX, DOH, KUW and SAU, whose
    /// Sunday is a trading day. The single local-weekday closure — KLS on its Monday 2026-08-31 — is
    /// corroborated by <c>holidays-by-exchange</c> naming that date <c>"National Day"</c> with
    /// <c>isClosed: true</c>. Zero unexplained exceptions across all 81 rows.</para>
    ///
    /// <para>A caller must not read this as "the market is not open right now" — that is
    /// <see cref="IsMarketOpen"/>.</para></summary>
    [JsonIgnore] public bool IsClosedToday => OpeningHourText is "CLOSED";

    private static OffsetTime? ParseHour(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var parsed = HourPattern.Parse(text);
        return parsed.Success ? parsed.Value : null;
    }
}
