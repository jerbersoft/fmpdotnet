using System.Buffers;
using System.Text;
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

/// <summary>Reads <c>SenateNetWorthLine.incomeRange</c>, which FMP sends as an object, as JSON
/// <see langword="null"/>, <b>or as the empty string</b>.
///
/// <para><b>This converter is not a convenience.</b> Measured 2026-08-29 over 250 rows for one filer,
/// <c>incomeRange</c> was an object on 136, <c>null</c> on 100 and <c>""</c> on 14.
/// <see cref="System.Text.Json.JsonSerializer"/> cannot read a string into an object, so a plain
/// <see cref="Models.NetWorthRange"/> property throws on those 14 — and the throw aborts the whole array
/// rather than the row, so on that filer 14 rows cost all 250.</para>
///
/// <para><b>Applied to <c>incomeRange</c> only.</b> Its sibling <c>valueRange</c> was an object on all 214
/// rows where it was present and never a string; putting this converter there too would assert a wire form
/// that was never measured.</para></summary>
public sealed class NetWorthRangeJsonConverter : JsonConverter<Models.NetWorthRange?>
{
    /// <inheritdoc/>
    public override Models.NetWorthRange? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // The empty string is the whole reason this type exists. Any other string is unmeasured and is also
        // read as null rather than thrown on, because one unrecognised value must not cost the response.
        if (reader.TokenType is JsonTokenType.String or JsonTokenType.Null) return null;

        return JsonSerializer.Deserialize(ref reader, FmpJsonContext.Default.NetWorthRange);
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, Models.NetWorthRange? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else JsonSerializer.Serialize(writer, value, FmpJsonContext.Default.NetWorthRange);
    }
}

/// <summary>Reads a JSON scalar of any type as its literal text, for a wire field FMP sends as more than one
/// JSON type and the SDK reports rather than parses.
///
/// <para><b>Written for <see cref="Models.NetWorthDebtDetails.Rate"/> and
/// <see cref="Models.NetWorthDebtDetails.Points"/>, and for a hard binding failure.</b> Measured 2026-08-29
/// over the 100 net-worth rows carrying <c>debtDetails</c>, <c>rate</c> arrived as a JSON number on 23 and as
/// a string on 64, and <c>points</c> as a number on 5 and the string <c>"-"</c> on 82. A JSON number read
/// into a plain <see cref="string"/> property throws under this SDK's context options — and the throw aborts
/// the whole array, so those 23 rows cost all 250.</para>
///
/// <para><b>Why not a numeric type instead.</b> The string forms are not placeholders: they carry a term as
/// well as a rate — <c>"N/A%                        (10 years)"</c> — so a tolerant numeric converter would
/// bind <see langword="null"/> on 64 rows and discard "10 years" with them. FMP has overloaded the field; the
/// SDK hands back what was sent.</para>
///
/// <para>A number surfaces as its <b>literal JSON text</b> rather than a round-trip through
/// <see cref="decimal"/>, so a trailing zero FMP chose to send survives and no value can overflow.</para></summary>
public sealed class ScalarAsStringJsonConverter : JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return "false";
            case JsonTokenType.Number:
                return Encoding.UTF8.GetString(
                    reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
            default:
                // An object or an array here is a shape nobody has measured. Reading it as null costs one
                // field; throwing would cost the whole response, which is the failure this converter exists
                // to prevent. Skip() is required, not optional: a scalar token needs no advancing, but
                // returning from a StartObject without consuming to its EndObject desynchronises the reader
                // for every field after it.
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

/// <summary>Reads a percentage FMP sends as a string with a trailing <c>%</c> — <c>"97.52%"</c> — as a
/// <see cref="decimal"/>.
///
/// <para><b>Written for <c>stable/etf/country-weightings</c>, which is the only path measured to do this.</b>
/// Measured 2026-08-30, all 227 rows returned across 13 ETFs sent <c>weightPercentage</c> as a quoted string
/// with a trailing <c>%</c> and a varying number of decimals — <c>"97.52%"</c>, <c>"0.1%"</c>, <c>"0%"</c>,
/// <c>"100%"</c>. Its sibling <c>stable/etf/sector-weightings</c>, one letter apart in the URL, sends the
/// identically-named field as a <b>bare JSON number</b>. One name, two wire types, two converters.</para>
///
/// <para><b>Why not <see cref="TolerantDecimalJsonConverter"/>.</b> That converter parses quoted numbers with
/// <c>NumberStyles.Float</c>, and <c>decimal.TryParse("97.52%", NumberStyles.Float, …)</c> is
/// <see langword="false"/> — so it would bind <see langword="null"/> on all 227 rows without failing anything.
/// <c>NumberStyles.AllowTrailingSign</c> does not help either; <c>%</c> is not a sign.</para>
///
/// <para>A bare JSON number passes through unchanged, so a future normalisation of the field costs nothing.
/// An unparseable value becomes <see langword="null"/> rather than throwing, following this file's standing
/// convention that one bad value costs one field rather than the whole response.</para></summary>
public sealed class PercentSuffixedDecimalJsonConverter : JsonConverter<decimal?>
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
            case JsonTokenType.String:
                var text = (reader.GetString() ?? "").AsSpan().Trim();
                if (text.Length > 0 && text[^1] == '%') text = text[..^1];
                return decimal.TryParse(
                    text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default:
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        // The wire form, not a bare number: a caller who serialises a row and hands it back to something that
        // expects FMP's own shape gets what FMP sent. Read accepts both, so this cannot round-trip lossily.
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(
            value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
    }
}

/// <summary>Maps FMP's three string spellings of absence — <c>""</c>, <c>"N/A"</c> and <c>"NULL"</c> — to
/// <see langword="null"/>, and passes every other value through verbatim.
///
/// <para><b>Absence is spelled four ways in the ETF and mutual-fund group, and one field uses two of
/// them.</b> Measured 2026-08-30: <c>etf/holdings.asset</c> was <c>""</c> on 17,988 of 35,185 rows (51.1%);
/// <c>funds/disclosure.lei</c> was <c>"N/A"</c> on 495; <c>funds/disclosure-holders-search</c> sent the literal
/// four-character string <c>"NULL"</c> on six fields at once — <c>symbol</c>, <c>entityOrgType</c>,
/// <c>reportingFileNumber</c>, <c>city</c>, <c>zipCode</c>, <c>state</c> — on 26-28% of rows, alongside a real
/// JSON <see langword="null"/> in <c>address</c> on the same rows. On the widest query taken
/// (<c>name=Trust</c>, 66,065 rows) <c>className</c> carried <b>both</b> string spellings: <c>"NULL"</c> ×1,278
/// and <c>"N/A"</c> ×192.</para>
///
/// <para><b>What this costs, stated plainly.</b> A caller can no longer tell "FMP sent nothing" from "FMP sent
/// the word NULL". That is the same trade <see cref="TolerantDecimalJsonConverter"/> already documents, and it
/// is accepted here for a reason that converter cannot claim: the alternative is asking every caller to know
/// four spellings, on more than a quarter of the rows, on the fields they most want. A caller who writes
/// <c>row.State ?? "unknown"</c> without this converter gets the string <c>"NULL"</c> and no warning.</para>
///
/// <para><b>Applied to exactly the properties measured to carry a sentinel, and to no others.</b>
/// <c>etf/holdings.name</c> was populated on all 35,185 rows, so an empty name would be information rather
/// than absence and that property is left alone — as are <c>title</c>, <c>units</c>, <c>assetCat</c>,
/// <c>issuerCat</c>, <c>cik</c>, <c>classId</c>, <c>seriesId</c>, <c>entityName</c>, <c>seriesName</c> and
/// <c>fairValLevel</c>, none of which was ever measured sending one.</para>
///
/// <para>A JSON number reads as its literal text rather than throwing. No measured row sent one into these
/// fields; the branch is there because a number read into a plain <see cref="string"/> property throws under
/// this SDK's context options, and the throw aborts the <b>whole array</b> — the failure measured on
/// <see cref="Models.NetWorthDebtDetails.Rate"/> and documented on
/// <see cref="ScalarAsStringJsonConverter"/>. Two of the fields this converter is applied to are numeric
/// strings (<c>entityOrgType</c> is <c>"30"</c>, <c>"32"</c>, <c>"33"</c>), so it is a shape FMP could
/// plausibly unquote.</para></summary>
public sealed class SentinelStringJsonConverter : JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() switch
                {
                    null or "" or "N/A" or "NULL" => null,
                    var text => text,
                };
            case JsonTokenType.Number:
                return Encoding.UTF8.GetString(
                    reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
            case JsonTokenType.Null:
                return null;
            default:
                // Skip() is required, not optional: returning from a StartObject without consuming to its
                // EndObject desynchronises the reader for every field after it.
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
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

/// <summary>Reads FMP's <c>Y</c>/<c>N</c> string flags as a <see cref="bool"/>.
///
/// <para><b>Written for the four <c>is*</c> fields on <c>stable/funds/disclosure</c></b> —
/// <c>isRestrictedSec</c>, <c>isCashCollateral</c>, <c>isNonCashCollateral</c> and <c>isLoanByFund</c> — which
/// are quoted single letters and not JSON booleans. <c>stable/etf/info.isActivelyTrading</c>, by contrast, is a
/// real JSON boolean and must not take this converter.</para>
///
/// <para><b>A total function over a measured domain, not a two-case parse.</b> Measured 2026-08-30 over a
/// 3,861-row sample, two of the four were <c>N</c> on <b>every</b> row — <c>isRestrictedSec</c> and
/// <c>isNonCashCollateral</c> — so their <c>Y</c> form is inferred from the other two rather than observed.
/// Anything that is neither <c>Y</c> nor <c>N</c>, including <c>""</c> and <c>"N/A"</c>, becomes
/// <see langword="null"/>: an unmeasured third value costs one field rather than the whole row, and this
/// converter never has to be right about a value nobody has seen.</para>
///
/// <para>A real JSON <see langword="true"/> or <see langword="false"/> passes through, so a future
/// normalisation of the field costs nothing. No measured row sent one.</para></summary>
public sealed class YesNoBooleanJsonConverter : JsonConverter<bool?>
{
    /// <inheritdoc/>
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString() switch
            {
                "Y" => true,
                "N" => false,
                _ => null,
            },
            _ => null,
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        // The wire form, not a JSON boolean: a caller who serialises a row gets back what FMP sent. Read
        // accepts both forms, so this cannot round-trip lossily.
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value ? "Y" : "N");
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
