using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace FinancialModelingPrep.Serialization;

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
