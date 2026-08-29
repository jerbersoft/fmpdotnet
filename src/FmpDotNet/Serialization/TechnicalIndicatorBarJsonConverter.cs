using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Serialization;

/// <summary>Binds a technical-indicator row, whose value column has a different name on each of the nine
/// paths.
///
/// <para><b>Why a converter rather than nine properties.</b> The column is named after the indicator —
/// <c>sma</c>, <c>adx</c>, <c>standardDeviation</c> and so on — so no single
/// <see cref="JsonPropertyNameAttribute"/> binds it. Declaring all nine as properties would leave eight null
/// on every row and make the caller work out which to read. This reads the six known keys by name and treats
/// <b>the single remaining key</b> as the value, resolving
/// <see cref="TechnicalIndicatorBar.Indicator"/> from that key's name.</para>
///
/// <para>Resolving from the wire is the point: the SDK reports the column that arrived rather than the one
/// that was asked for.</para>
///
/// <para>Throws <see cref="JsonException"/> when a row carries no unrecognised key, more than one, or one that
/// is not an indicator column. None of those was observed across 88 captures on 2026-08-29, and each means the
/// row is not what <see cref="TechnicalIndicatorBar"/> models — guessing would be worse than
/// failing.</para></summary>
public sealed class TechnicalIndicatorBarJsonConverter : JsonConverter<TechnicalIndicatorBar>
{
    // Reused rather than reimplemented, so the measured parsing of FMP's space-separated stamp lives in one
    // place.
    private static readonly NullableLocalDateTimeJsonConverter Timestamps = new();

    /// <inheritdoc/>
    public override TechnicalIndicatorBar Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A technical-indicator row must be a JSON object.");

        LocalDateTime? timestamp = null;
        decimal? open = null, high = null, low = null, close = null, volume = null, value = null;
        TechnicalIndicator? indicator = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected a property name in a technical-indicator row.");

            var name = reader.GetString()!;
            reader.Read();

            // Matched case-insensitively, like every other property in the SDK: FmpJsonContext sets
            // PropertyNameCaseInsensitive SDK-wide, and a custom converter such as this one does not inherit
            // that from the source generator — it has to implement it itself.
            if (string.Equals(name, "date", StringComparison.OrdinalIgnoreCase))
                timestamp = Timestamps.Read(ref reader, typeof(LocalDateTime?), options);
            else if (string.Equals(name, "open", StringComparison.OrdinalIgnoreCase))
                open = ReadDecimal(ref reader, options, name);
            else if (string.Equals(name, "high", StringComparison.OrdinalIgnoreCase))
                high = ReadDecimal(ref reader, options, name);
            else if (string.Equals(name, "low", StringComparison.OrdinalIgnoreCase))
                low = ReadDecimal(ref reader, options, name);
            else if (string.Equals(name, "close", StringComparison.OrdinalIgnoreCase))
                close = ReadDecimal(ref reader, options, name);
            else if (string.Equals(name, "volume", StringComparison.OrdinalIgnoreCase))
                volume = ReadDecimal(ref reader, options, name);
            else
            {
                if (!TechnicalIndicatorExtensions.TryFromJsonField(name, out var found))
                    throw new JsonException(
                        $"'{name}' is not a price field or a known indicator column.");
                if (indicator is not null)
                    throw new JsonException(
                        $"A technical-indicator row carried two indicator columns: "
                        + $"'{indicator.Value.ToJsonField()}' and '{name}'.");
                indicator = found;
                value = ReadDecimal(ref reader, options, name);
            }
        }

        if (indicator is null)
            throw new JsonException("A technical-indicator row carried no indicator column.");

        return new TechnicalIndicatorBar
        {
            Timestamp = timestamp,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Indicator = indicator.Value,
            Value = value,
        };
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, TechnicalIndicatorBar value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("date");
        Timestamps.Write(writer, value.Timestamp, options);
        WriteDecimal(writer, "open", value.Open);
        WriteDecimal(writer, "high", value.High);
        WriteDecimal(writer, "low", value.Low);
        WriteDecimal(writer, "close", value.Close);
        WriteDecimal(writer, "volume", value.Volume);
        WriteDecimal(writer, value.Indicator.ToJsonField(), value.Value);
        writer.WriteEndObject();
    }

    private static decimal? ReadDecimal(ref Utf8JsonReader reader, JsonSerializerOptions options, string name)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        // FmpJsonContext sets NumberHandling = AllowReadingFromString SDK-wide, and FMP quoting a number is
        // measured, recurring behaviour this SDK treats as load-bearing — see
        // FmpTransportTests.Reads_numbers_fmp_delivers_as_strings, DirectoryListsTests and
        // FinancialScoresTests. A custom converter does not inherit that option from the source generator, so
        // without this check a single quoted field here would abort the whole response instead of binding,
        // unlike every other model.
        if (reader.TokenType == JsonTokenType.String
            && options.NumberHandling.HasFlag(JsonNumberHandling.AllowReadingFromString))
        {
            // TryParse rather than Parse: System.Text.Json wraps the InvalidOperationException that
            // reader.GetDecimal() throws on a non-numeric token as JsonException, but it does not do that for
            // exceptions this converter's own code throws. A malformed or out-of-range quoted string must
            // still surface as JsonException rather than a raw FormatException or OverflowException escaping
            // uncaught — see FmpTransport.GetListAsync's documented contract, and this SDK's
            // TolerantDecimalJsonConverter and EpochJson.Read (NodaConverters.cs), which use TryParse for the
            // same reason. The value itself is left out of the message: it is arbitrary response content, and
            // naming the field is enough to find it.
            if (!decimal.TryParse(
                    reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                throw new JsonException($"'{name}' carried a quoted value that is not a number.");
            return parsed;
        }

        return reader.GetDecimal();
    }

    private static void WriteDecimal(Utf8JsonWriter writer, string name, decimal? value)
    {
        writer.WritePropertyName(name);
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}
