// Converters for FMP's date, time and timestamp fields — everything whose output is a NodaTime type.
//
// Split out of a single 906-line file (#55), which had accumulated eight converters that read decimals,
// strings and booleans and had nothing to do with time. See ScalarConverters.cs and ShapeConverters.cs.

using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace FmpDotNet.Serialization;

/// <summary>Reads FMP's ISO date fields (<c>"1980-12-12"</c>) as <see cref="LocalDate"/>.
///
/// <para>Written here rather than taken from <c>NodaTime.Serialization.SystemTextJson</c> because the SDK's models
/// are source-generated: a property-level <see cref="JsonConverterAttribute"/> is what the generator understands,
/// and it keeps the package graph to NodaTime itself.</para></summary>
public sealed class LocalDateJsonConverter : JsonConverter<LocalDate>
{
    /// <inheritdoc/>
    public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        var parsed = LocalDatePattern.Iso.Parse(raw ?? "");
        return parsed.Success
            ? parsed.Value
            : throw new JsonException($"Could not read '{raw}' as an ISO date.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options)
        => writer.WriteStringValue(LocalDatePattern.Iso.Format(value));
}

/// <summary>As <see cref="LocalDateJsonConverter"/>, but tolerant of the ways FMP says "no date".
///
/// <para>An absent date arrives as JSON null, as <c>""</c>, or occasionally as <c>"0000-00-00"</c>. All three mean
/// the same thing and none of them should abort the surrounding response, so all three read as null rather than
/// throwing — a single unparseable date must not cost the caller every other field on the record.</para></summary>
public sealed class NullableLocalDateJsonConverter : JsonConverter<LocalDate?>
{
    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = LocalDatePattern.Iso.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(LocalDatePattern.Iso.Format(value.Value));
    }
}

/// <summary>Reads FMP's <c>"yyyy-MM-dd HH:mm:ss"</c> timestamps as an <see cref="Instant"/>.
///
/// <para>The form is space-separated, NOT ISO-T, and the reading is <b>UTC</b> — established by the DST shift
/// rather than assumed: a measured August 2026 Core PCE row reads <c>12:30:00</c> (08:30 ET, EDT is UTC-4) and a
/// measured January 2027 FOMC row reads <c>19:00:00</c> (14:00 ET, EST is UTC-5). Two different offsets six months
/// apart match the DST rule exactly, so a fixed offset would be right for only half the calendar. Converting to a
/// local zone is the caller's business and must go through the tz database, never a hardcoded -4/-5.</para></summary>
public sealed class NullableFmpInstantJsonConverter : JsonConverter<Instant?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value.InUtc().ToInstant() : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value.InUtc().LocalDateTime));
    }
}

/// <summary>Reads the <c>acceptedDate</c> on the statement endpoints, which is <b>Eastern</b> wall clock.
///
/// <para>Same <c>"yyyy-MM-dd HH:mm:ss"</c> shape as <see cref="NullableFmpInstantJsonConverter"/> and a different
/// timezone, so the two are not interchangeable. FMP passes EDGAR's acceptance time through in Eastern local time
/// with the offset stripped; reading it as UTC — which is what the economic calendar needs — puts every filing
/// timestamp 4 or 5 hours early.</para>
///
/// <para>Measured against SEC EDGAR on 2026-08-26, not assumed. EDGAR's submissions API publishes the same
/// acceptances in true UTC:</para>
/// <list type="bullet">
///   <item><description>AAPL 10-K (0000320193-25-000079): FMP <c>2025-10-31 06:01:26</c>, EDGAR
///     <c>2025-10-31T10:01:26Z</c> — 4 hours, and 31 October is EDT (UTC-4).</description></item>
///   <item><description>JPM 10-K (0001628280-26-008131): FMP <c>2026-02-13 16:20:00</c>, EDGAR
///     <c>2026-02-13T21:20:00Z</c> — 5 hours, and 13 February is EST (UTC-5).</description></item>
///   <item><description>XOM 10-K (0000034088-26-000045): FMP <c>2026-02-18 16:06:52</c>, EDGAR
///     <c>2026-02-18T21:06:52Z</c> — 5 hours, EST again.</description></item>
/// </list>
///
/// <para>Two different offsets six months apart is the point: a fixed -5 would be wrong for half the year, so the
/// conversion goes through the tz database rather than arithmetic. Resolution is lenient — EDGAR accepts filings
/// between 06:00 and 22:00 Eastern, so neither the spring gap nor the autumn overlap should ever be hit, but a
/// malformed hour must not cost the caller the other 38 fields on the record.</para></summary>
public sealed class NullableEasternInstantJsonConverter : JsonConverter<Instant?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    private static readonly DateTimeZone Eastern =
        DateTimeZoneProviders.Tzdb["America/New_York"];

    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value.InZoneLeniently(Eastern).ToInstant() : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value.InZone(Eastern).LocalDateTime));
    }
}

/// <summary>Reads FMP's <c>"yyyy-MM-dd HH:mm:ss"</c> timestamps as a <see cref="LocalDateTime"/> — a wall clock
/// with <b>no timezone attached</b>, which is exactly what is known about them.
///
/// <para><b>The third converter for this wire shape, and the only honest one where the zone was never
/// measured.</b> <see cref="NullableFmpInstantJsonConverter"/> reads it as UTC and
/// <see cref="NullableEasternInstantJsonConverter"/> reads it as Eastern; each of those readings was established
/// by measuring a DST shift on its own endpoint, and the two are four or five hours apart. Applying either to a
/// field whose zone nobody checked would not be a small risk — it would be a fabricated fact, wrong by hours,
/// with nothing in the data to reveal it.</para>
///
/// <para>So this converter declines to guess. A <see cref="LocalDateTime"/> still sorts, still compares and still
/// formats; what it will not do is claim to be a moment in time. If the zone is ever measured, this becomes an
/// <see cref="Instant"/> and the caller's code gets more correct rather than differently wrong.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad stamp costs one field rather than
/// the whole response.</para></summary>
public sealed class NullableLocalDateTimeJsonConverter : JsonConverter<LocalDateTime?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public override LocalDateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}

/// <summary>Reads FMP's <c>"uuuu-MM-dd HH:mm:ss"</c> form as a <see cref="LocalDate"/>, discarding a time
/// component that carries no information.
///
/// <para><b>The fourth converter for this one wire format, and the measurement is what earns it.</b> Across
/// 2,115 rows sampled 2026-08-28 from <c>sec-filings-8k</c>, <c>sec-filings-financials</c> and
/// <c>sec-filings-search/form-type</c>, the time component of <c>filingDate</c> was <c>00:00:00</c> in
/// <b>2,115 of 2,115</b> cases. It is a date with a dummy midnight bolted on, not a timestamp.</para>
///
/// <para><b>Neither existing converter fits.</b> <see cref="NullableLocalDateJsonConverter"/> uses
/// <c>LocalDatePattern.Iso</c>, which rejects the trailing time outright and would null every value.
/// <see cref="NullableLocalDateTimeJsonConverter"/> binds it and then leaks a meaningless midnight into every
/// comparison a caller writes.</para>
///
/// <para><b>One pattern, no fallback, deliberately.</b> If FMP ever drops the dummy time, this reads null rather
/// than quietly accepting a second format — and the weekly smoke baseline reports that as
/// <c>FilingDate: now always null, was populated</c>, on the run after it happens. A silent fallback would make
/// the change invisible, which is the opposite of what a measured SDK is for.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad stamp costs one field rather
/// than the whole response.</para></summary>
public sealed class NullableDateAtMidnightJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDateTimePattern Pattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value.Date : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value.AtMidnight()));
    }
}

/// <summary>Reads a Unix epoch timestamp in <b>seconds</b> as an <see cref="Instant"/> — the form
/// <c>stable/quote</c> uses.
///
/// <para><b>This converter and <see cref="NullableEpochMillisecondsInstantJsonConverter"/> both exist because FMP
/// spells the same field name two ways in the same endpoint group, and only the magnitude tells them
/// apart.</b> Measured 2026-08-27:</para>
/// <list type="bullet">
///   <item><description><c>stable/quote</c> answered <c>"timestamp": 1787774400</c> — seconds, which is
///     2026-08-26 20:00:00 UTC, or 16:00 ET: the closing print, exactly where a daily quote's stamp
///     belongs.</description></item>
///   <item><description><c>stable/aftermarket-trade</c> answered <c>"timestamp": 1787819647000</c> —
///     milliseconds, which is 2026-08-27 08:34:07 UTC, or 04:34 ET: pre-market, exactly where an
///     aftermarket feed's stamp belongs.</description></item>
/// </list>
///
/// <para>Read the millisecond value as seconds and you land in the year 58623; read the second value as
/// milliseconds and you land on 1970-01-21. The first throws, the second does not — it silently reports every
/// quote as fifty-six years old. That asymmetry is why the two are separate types rather than one converter that
/// guesses from the magnitude: a guess would work on today's data and change meaning the day FMP alters a unit,
/// with nothing in a diff to show for it.</para>
///
/// <para>Nullable, and lenient about the token type, for the reason the rest of this file is: a single unreadable
/// stamp must not cost the caller every other field on the record. A value out of
/// <see cref="Instant.FromUnixTimeSeconds"/>'s range reads as null rather than throwing.</para></summary>
public sealed class NullableEpochSecondsInstantJsonConverter : JsonConverter<Instant?>
{
    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => EpochJson.Read(ref reader, Instant.FromUnixTimeSeconds);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value.ToUnixTimeSeconds());
    }
}

/// <summary>Reads a Unix epoch timestamp in <b>milliseconds</b> as an <see cref="Instant"/> — the form the
/// aftermarket endpoints use.
///
/// <para>The twin of <see cref="NullableEpochSecondsInstantJsonConverter"/>; that type carries the measurements
/// and the reason the two are kept apart. Applied to <c>stable/aftermarket-trade</c> and
/// <c>stable/aftermarket-quote</c>, and to their <c>batch-</c> forms.</para></summary>
public sealed class NullableEpochMillisecondsInstantJsonConverter : JsonConverter<Instant?>
{
    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => EpochJson.Read(ref reader, Instant.FromUnixTimeMilliseconds);

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value.ToUnixTimeMilliseconds());
    }
}

/// <summary>The token handling shared by the two epoch converters.</summary>
internal static class EpochJson
{
    /// <summary>Reads a JSON number — or a quoted one — and turns it into an <see cref="Instant"/> through
    /// <paramref name="toInstant"/>.
    ///
    /// <para>Quoted numbers are accepted because the context sets
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/> for every other numeric field, and a converter that
    /// refused <c>"1787774400"</c> where <c>marketCap</c> accepts <c>"4603751738200"</c> would be an inconsistency
    /// with no reason behind it.</para></summary>
    internal static Instant? Read(ref Utf8JsonReader reader, Func<long, Instant> toInstant)
    {
        long epoch;
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number when reader.TryGetInt64(out epoch):
                break;
            case JsonTokenType.String when long.TryParse(
                reader.GetString(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out epoch):
                break;
            default:
                return null;
        }

        try
        {
            return toInstant(epoch);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Out of NodaTime's representable range — which is precisely what milliseconds read as seconds looks
            // like. Null rather than a throw, so one bad stamp costs one field instead of the whole response.
            return null;
        }
    }
}

/// <summary>Reads an ISO-8601 timestamp that carries its own <c>Z</c> — <c>"2026-08-29T23:12:50.006Z"</c> — as
/// an <see cref="Instant"/>.
///
/// <para><b>The fourth converter in this file for a wall-clock timestamp string, and the only one that
/// needs no zone measurement.</b> <see cref="NullableFmpInstantJsonConverter"/> and
/// <see cref="NullableEasternInstantJsonConverter"/> both read <c>"uuuu-MM-dd HH:mm:ss"</c>, which carries no
/// offset, and each had to establish its zone by measuring a DST shift.
/// <see cref="NullableLocalDateTimeJsonConverter"/> declines to guess where nobody measured. This form states
/// its offset, so there is nothing to establish.</para>
///
/// <para><b>Written for <c>stable/etf/info.updatedAt</c></b>, which sent
/// <c>uuuu-MM-dd'T'HH:mm:ss.fff'Z'</c> on 33 of 33 rows measured 2026-08-30 — while its sibling
/// <c>stable/etf/holdings</c> sends the space-separated form for the same concept. One name, two formats, on
/// two paths one word apart in the URL. Substituting
/// <see cref="NullableFmpInstantJsonConverter"/> here binds <see langword="null"/> on every row: its pattern
/// expects a space separator and no <c>Z</c>.</para>
///
/// <para>Uses NodaTime's <see cref="InstantPattern.ExtendedIso"/>, which reads the fractional seconds and the
/// <c>Z</c> and tolerates a value with no fractional part. Null on an unparseable value, like the rest of this
/// file.</para></summary>
public sealed class NullableIsoInstantJsonConverter : JsonConverter<Instant?>
{
    private static readonly InstantPattern Pattern = InstantPattern.ExtendedIso;

    /// <inheritdoc/>
    public override Instant? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Instant? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}

/// <summary>Reads FMP's US long-form dates — <c>"June 29, 2026"</c> — as a <see cref="LocalDate"/>.
///
/// <para><b>Written for the three <c>historical-*-constituent</c> paths</b>, whose <c>dateAdded</c> is the
/// only long-form date in this SDK. Every one of the <b>2,055</b> values measured 2026-08-30 parsed with
/// <c>MMMM d, uuuu</c>. Its sibling field <c>date</c> on the same row is ISO and takes
/// <see cref="NullableLocalDateJsonConverter"/> — two date formats in one object, which is why this record
/// carries two date converters rather than one.</para>
///
/// <para><b>Invariant culture is load-bearing, not boilerplate.</b> The month names are English. A pattern
/// built from the ambient culture parses <b>nothing</b> on a German or French host — and because this file's
/// converters answer an unparseable value with <see langword="null"/> rather than throwing, the column would
/// arrive empty in production and green in CI. That is the failure this converter is shaped to prevent.</para>
///
/// <para><b><see cref="Write"/> cannot round-trip the wire byte for byte, and that is measured rather than
/// sloppy.</b> The wire uses <b>both</b> day paddings — measured 2026-08-30 on
/// <c>historical-sp500-constituent</c> alone, 213 values carry a zero-padded single-digit day
/// (<c>"August 05, 2026"</c>) and 407 carry an unpadded one (<c>"November 8, 2024"</c>). No single NodaTime
/// pattern emits both, so <c>d</c> is chosen because it <b>parses</b> both; a zero-padded input therefore
/// comes back unpadded. Nothing is lost — <see cref="Read"/> accepts either form — but a test that asserts a
/// byte-identical round trip on this converter is asserting something untrue, and the guard test asserts the
/// parsed value instead.</para>
///
/// <para>Null on an unparseable value, following the rest of this file: one bad date costs one field rather
/// than the whole response.</para></summary>
public sealed class LongFormLocalDateJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDatePattern Pattern =
        LocalDatePattern.CreateWithInvariantCulture("MMMM d, uuuu");

    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}

/// <summary>Reads FMP's <c>MM-DD-YYYY</c> dates — <c>"11-22-2011"</c> — as a <see cref="LocalDate"/>.
///
/// <para><b>The fifth converter for a date in this SDK, and the trap it closes is the reason it exists.</b>
/// <see cref="NullableLocalDateJsonConverter"/> parses with <c>LocalDatePattern.Iso</c> and answers
/// <see langword="null"/> on failure rather than throwing, so binding a <c>MM-DD-YYYY</c> field with it
/// yields <b>null on 100% of rows, at HTTP 200, with no exception and no warning</b>. Measured 2026-08-31 by
/// deserialising through it: <c>"08-28-2026"</c> and <c>"04-30-2027"</c> both read as null, while
/// <c>"2026-08-31"</c> reads correctly.</para>
///
/// <para><b>The component order is measured, not assumed.</b> Over 1,000 crowdfunding offering rows and
/// 6,542 dated search rows captured 2026-08-31, the first component never exceeded <b>12</b> while the
/// second reached <b>31</b> — so <c>DD-MM-YYYY</c> is ruled out by 7,542 rows. FMP's own documented sample
/// corroborates it independently with <c>"11-22-2011"</c> and <c>"10-31-2026"</c>: a 22 and a 31 in second
/// position can only be days.</para>
///
/// <para><b>Invariant culture is load-bearing, not boilerplate</b> — for the reason
/// <see cref="LongFormLocalDateJsonConverter"/> records. The separator and field order are fixed here rather
/// than taken from the host, so a French or German runtime reads the same value this one does.</para>
///
/// <para><b>One pattern, no fallback, deliberately.</b> If FMP ever switches this field to ISO, this reads
/// null rather than quietly accepting a second format, and the weekly smoke baseline reports it as
/// <c>Date: now always null, was populated</c> on the run after it happens. A silent fallback would make the
/// change invisible, which is the opposite of what a measured SDK is for.</para>
///
/// <para>Applied to <c>CrowdfundingOffering.Date</c>, <c>CrowdfundingOffering.OfferingDeadlineDate</c> and
/// <c>CrowdfundingSearchHit.Date</c>. Its sibling <c>FundraisingNotice.Date</c> is ISO on the same-named
/// field of a different path and keeps <see cref="NullableLocalDateJsonConverter"/> — the two are one
/// substitution apart and neither substitution throws.</para>
///
/// <para>Null on JSON null, on <c>""</c> and on any unparseable value, following the rest of this file: one
/// bad date costs one field rather than the whole response.</para></summary>
public sealed class NullableMonthDayYearDateJsonConverter : JsonConverter<LocalDate?>
{
    private static readonly LocalDatePattern Pattern =
        LocalDatePattern.CreateWithInvariantCulture("MM-dd-uuuu");

    /// <inheritdoc/>
    public override LocalDate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalDate? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}

/// <summary>Reads a bare wall-clock time — <c>"13:00"</c> — as a <see cref="LocalTime"/>.
///
/// <para><b>Written for <c>stable/holidays-by-exchange</c>'s <c>adjOpenTime</c> and <c>adjCloseTime</c></b>,
/// which are the only <see cref="LocalTime"/> fields in this SDK. All 50 non-null values measured 2026-08-30
/// matched <c>HH:mm</c> — 49 of them <c>"13:00"</c> and one <c>"13:30"</c> on 2015-11-27.</para>
///
/// <para><b>The value carries no offset and the response carries no zone.</b> <c>holidays-by-exchange</c> has
/// no <c>timezone</c> key at all — verified absent on all 446 rows — so a caller who needs an instant must
/// take the zone from the matching <see cref="Models.ExchangeMarketHours.Timezone"/>, fetched from
/// <c>stable/exchange-market-hours</c>. This converter does not guess one, and could not: the same wire
/// format on <c>all-exchange-market-hours</c> is spelled <c>"09:30 AM +09:00"</c> instead, which is the
/// sharper half of this group's two-spellings-of-a-time problem.</para>
///
/// <para>This pattern round-trips exactly, unlike <see cref="LongFormLocalDateJsonConverter"/>, so a guard
/// test for this converter may assert the serialised form. Null on an unparseable value, following the rest
/// of this file.</para></summary>
public sealed class LocalTimeJsonConverter : JsonConverter<LocalTime?>
{
    private static readonly LocalTimePattern Pattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    /// <inheritdoc/>
    public override LocalTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        var parsed = Pattern.Parse(reader.GetString() ?? "");
        return parsed.Success ? parsed.Value : null;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LocalTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(Pattern.Format(value.Value));
    }
}
