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

            switch (name)
            {
                case "date":
                    timestamp = Timestamps.Read(ref reader, typeof(LocalDateTime?), options);
                    break;
                case "open": open = ReadDecimal(ref reader); break;
                case "high": high = ReadDecimal(ref reader); break;
                case "low": low = ReadDecimal(ref reader); break;
                case "close": close = ReadDecimal(ref reader); break;
                case "volume": volume = ReadDecimal(ref reader); break;
                default:
                    if (!TechnicalIndicatorExtensions.TryFromJsonField(name, out var found))
                        throw new JsonException(
                            $"'{name}' is not a price field or a known indicator column.");
                    if (indicator is not null)
                        throw new JsonException(
                            $"A technical-indicator row carried two indicator columns: "
                            + $"'{indicator.Value.ToJsonField()}' and '{name}'.");
                    indicator = found;
                    value = ReadDecimal(ref reader);
                    break;
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

    private static decimal? ReadDecimal(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : reader.GetDecimal();

    private static void WriteDecimal(Utf8JsonWriter writer, string name, decimal? value)
    {
        writer.WritePropertyName(name);
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Value);
    }
}
