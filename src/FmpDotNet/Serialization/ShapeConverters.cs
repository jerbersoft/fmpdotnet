// Converters for fields whose JSON SHAPE varies rather than whose spelling does — a value that arrives as an
// object on some rows and a string on others, or as a delimited string where a list was meant.
//
// Split out of NodaConverters.cs (#55). Contrast ScalarConverters.cs, which handles a scalar that is always
// a scalar and merely spelled unusually.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace FmpDotNet.Serialization;

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
