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

/// <summary>Reads a JSON number as <see cref="decimal"/>, answering <see langword="null"/> for a value too large to
/// be one instead of aborting the response.
///
/// <para><b>For FMP's computed percentages, which are divisions and can therefore be divisions by zero.</b>
/// Measured 2026-08-27, <c>stable/batch-etf-quotes</c> answered
/// <c>{"symbol":"BMJJF","price":177.34,"changePercentage":6.3878959205932735e+35,"change":177.34,
/// "previousClose":0, …}</c> — a change of 6.4×10³⁵ percent, which is what a 177.34 move against a zero previous
/// close produces. It is nonsense, but it is well-formed JSON and FMP sends it.</para>
///
/// <para><b>Without this converter that single row costs the caller all 14,537.</b>
/// <see cref="decimal"/> tops out near 7.9×10²⁸, so the value overflows, <see cref="System.Text.Json"/> throws,
/// and the exception surfaces as "the ETF quotes endpoint is broken" rather than "one ETF has a silly percentage".
/// That is the same trade this file already makes for unparseable dates, for the same reason: a single bad field
/// must not destroy every good one beside it.</para>
///
/// <para><b>Why not <see cref="double"/> instead.</b> It would represent the value, and it would also quietly
/// lower the precision of every ordinary percentage in the SDK — which is 561 <see cref="decimal"/> properties
/// against 5 <see cref="double"/> ones, a convention worth keeping for money. Reading the outlier as null keeps
/// the exact type for the 14,536 rows that deserve it and loses one field on the row that does not.</para>
///
/// <para><b>Null here means "FMP sent something unrepresentable", which is not the same as "FMP sent nothing".</b>
/// Both arrive as null and a caller cannot tell them apart from the value alone. That is a real limitation, and it
/// is accepted only because the alternative is losing the whole response. It is applied to the computed
/// percentages and <b>not</b> to prices, volumes or market caps: those are not divisions, none has been observed
/// out of range, and silently nulling a price is a much worse failure than silently nulling a ratio.</para></summary>
public sealed class TolerantDecimalJsonConverter : JsonConverter<decimal?>
{
    /// <inheritdoc/>
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.TryGetDecimal(out var value) ? value : null;
            // Quoted numbers, for consistency with the context's AllowReadingFromString.
            case JsonTokenType.String:
                return decimal.TryParse(
                    reader.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default:
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}

/// <summary>Normalises <c>businessAddress</c> so that one property means the same thing on all five paths that
/// send it.
///
/// <para><b>Two encodings for one field, measured 2026-08-28.</b> <c>all-industry-classification</c> sends a
/// stringified Python list — <c>"['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']"</c>, 1,000 of
/// 1,000 rows sampled — and <c>industry-classification-search</c> was confirmed to send the same bracketed form
/// on two queries. <c>sec-filings-company-search/name?company=Bank</c> sends the same address for the same CIK
/// as <c>"BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255"</c>, 0 of 976 rows bracketed — the other two
/// <c>sec-filings-company-search/*</c> paths were not separately sampled at that volume. The joined form is
/// FMP's own: <c>", ".join(parts)</c> of the bracketed value reproduced the sibling path's string exactly on
/// five of five randomly sampled CIKs, so this converter adopts a target FMP publishes rather than inventing
/// one.</para>
///
/// <para><b>The transform is textual, not a parse, and the difference is load-bearing.</b> Of those 1,000
/// values, 999 parse as a Python literal and one does not:
/// <c>"['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"</c> (AGCC, CIK 0002060016), where
/// <c>XI'AN</c> carries an unescaped apostrophe inside a single-quoted repr. The string was built by naive
/// formatting rather than by a serialiser, so every apostrophe in an address — Xi'an, O'Brien, L'Oreal —
/// reproduces the fault. Stripping the brackets and replacing <c>', '</c> handles that row correctly, because
/// the apostrophe is not followed by a comma and a space. A real parse fails on it.</para>
///
/// <para><b>One direction only.</b> Splitting the joined form back into parts would be lossy: nineteen of the
/// 1,000 sampled values carry a comma or a quote inside an element.</para>
///
/// <para>Anything that is not bracketed at both ends is returned exactly as sent. The converter never throws and
/// never drops a value, which is also what makes it safe on the three paths that never bracket. Whitespace is
/// not trimmed — <c>'TAICHUNG CITY  '</c> keeps its trailing spaces, because FMP sent them and trimming would be
/// a second unmeasured transform riding on this one.</para></summary>
public sealed class BusinessAddressJsonConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Every other converter in this file guards TokenType before reading a string; this one didn't, and
        // Utf8JsonReader.GetString() throws on anything but String, PropertyName or Null. The realistic trigger
        // is FMP fixing the naive-string-formatting bug this converter exists to undo: if businessAddress ever
        // arrives as a real JSON array instead of a stringified one, an unguarded read costs the WHOLE response
        // (FmpTransport.GetListAsync does not wrap DeserializeAsync) rather than the one field, which is exactly
        // the house rule this class's own doc claims to follow.
        if (reader.TokenType != JsonTokenType.String)
        {
            // Skip() rather than an early return alone: for StartArray/StartObject the reader is only
            // positioned at the OPENING token, and System.Text.Json's VerifyRead demands the converter leave
            // the reader past the matching close token — otherwise it throws its own JsonException ("read too
            // much or not enough") in place of the one this guard exists to avoid. Skip() is a correct no-op
            // on the scalar tokens (Number, True, False, Null) reaching this branch too.
            reader.Skip();
            return null;
        }
        return Normalise(reader.GetString());
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);

    /// <summary>The transform itself, exposed so it can be tested without a serialiser around it.</summary>
    internal static string? Normalise(string? raw)
    {
        if (raw is null) return null;
        if (!raw.StartsWith("['", StringComparison.Ordinal) || !raw.EndsWith("']", StringComparison.Ordinal))
            return raw;

        return raw[2..^2].Replace("', '", ", ", StringComparison.Ordinal);
    }
}

/// <summary>Reads <c>price-target-summary</c>'s <c>publishers</c> field, which is a <b>string containing a JSON
/// array</b>, into the list it describes.
///
/// <para>Measured 2026-08-28, AAPL answered:</para>
///
/// <code>
/// "publishers": "[\"StreetInsider\",\"Benzinga\",\"Pulse 2.0\",\"MarketWatch\",\"Investing\",\"Barrons\",\"Investor's Business Daily\"]"
/// </code>
///
/// <para><b>A real parse is safe here, and that is not true of every double-encoded field in this SDK.</b>
/// <see cref="BusinessAddressJsonConverter"/> deals with a stringified <i>Python</i> list built by naive
/// formatting, where an apostrophe inside an element breaks the encoding and a parse fails on it. This one is
/// genuine JSON: the apostrophe in <c>Investor's Business Daily</c> sits inside a double-quoted JSON string and
/// is correctly escaped, so <c>JsonSerializer</c> reads it back exactly.</para>
///
/// <para>It binds to <see cref="IReadOnlyList{T}"/> of <see cref="string"/> so that the ordinary path and
/// <see cref="Models.BulkPriceTargetSummary.Publishers"/> agree about the type of this field; before this
/// converter they would not have.</para>
///
/// <para><b>Empty and null mean different things, deliberately.</b> An empty list is FMP saying there are no
/// publishers — 874 of 5,277 rows on the bulk path measured 2026-08-26 — and <see langword="null"/> is this SDK
/// saying the field could not be read. Collapsing the two would turn a format change upstream into a silent,
/// universal "no publishers".</para>
///
/// <para>Deserialisation goes through <see cref="FmpJsonContext"/> rather than a reflection-based overload,
/// because this assembly declares <c>IsAotCompatible</c> and a reflecting <c>Deserialize</c> would fail the
/// build on IL2026/IL3050.</para></summary>
public sealed class PublisherListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<string>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Guarded from the start, unlike BusinessAddressJsonConverter, which shipped without this and had it
        // added by review. Utf8JsonReader.GetString() throws on anything but String, PropertyName or Null, and
        // the realistic trigger is FMP fixing the double-encoding: if `publishers` ever arrives as a real JSON
        // array, an unguarded read costs the WHOLE response, since FmpTransport does not wrap DeserializeAsync.
        if (reader.TokenType != JsonTokenType.String)
        {
            // Skip() rather than an early return alone: for StartArray/StartObject the reader is positioned at
            // the OPENING token only, and System.Text.Json's VerifyRead demands the converter leave it past the
            // matching close token -- otherwise it throws its own JsonException ("read too much or not enough")
            // in place of the one this guard exists to avoid. Skip() is a correct no-op on the scalar tokens
            // (Number, True, False, Null) that also reach this branch.
            reader.Skip();
            return null;
        }

        var raw = reader.GetString();
        if (raw is null) return null;
        if (raw.Length == 0) return [];

        try
        {
            return JsonSerializer.Deserialize(raw, FmpJsonContext.Default.ListString);
        }
        catch (JsonException)
        {
            return null;   // unreadable, which is not the same as empty
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
        => writer.WriteStringValue(
            JsonSerializer.Serialize(new List<string>(value), FmpJsonContext.Default.ListString));
}
