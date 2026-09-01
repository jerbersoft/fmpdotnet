// Converters for FMP's scalar oddities: a value whose C# type is ordinary — decimal, string, bool — but whose
// wire form is not, because FMP spells it with a suffix, a sentinel word, or a type that varies per row.
//
// Split out of NodaConverters.cs (#55), where they were filed under a name about time. One converter belongs
// here when what makes it necessary is the SPELLING of a scalar; in ShapeConverters.cs when what makes it
// necessary is the SHAPE of the JSON around it.

using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FmpDotNet.Serialization;

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
