using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>Reads FMP's report-period label — <c>FY</c>, <c>Q1</c>–<c>Q4</c> — as a <see cref="FiscalPeriod"/>.
///
/// <para>Applied only to <see cref="FinancialReportLink.Period"/>, and the narrowness is the point. Everywhere
/// else in this SDK a <c>period</c> field is a LABEL on a row of data and stays a string. On a report link it is
/// an ARGUMENT: the caller passes it straight back to
/// <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>, and that list-then-fetch round trip is the
/// only reason <c>financial-reports-dates</c> exists.</para>
///
/// <para>An unrecognised label reads as null rather than throwing, following the date converters: one unreadable
/// row out of 65 must not cost the caller the other 64.</para></summary>
public sealed class ReportPeriodJsonConverter : JsonConverter<FiscalPeriod?>
{
    /// <inheritdoc/>
    public override FiscalPeriod? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String
            ? reader.GetString() switch
            {
                "FY" or "annual" => FiscalPeriod.Annual,
                "Q1" => FiscalPeriod.Q1,
                "Q2" => FiscalPeriod.Q2,
                "Q3" => FiscalPeriod.Q3,
                "Q4" => FiscalPeriod.Q4,
                _ => null,
            }
            : null;

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FiscalPeriod? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value == FiscalPeriod.Annual ? "FY" : value.Value.ToQueryValue());
    }
}

/// <summary>One filing FMP holds a rendered report for, and the two links it publishes for it. From
/// <c>stable/financial-reports-dates</c> — 65 rows for AAPL measured 2026-08-27, FY and Q1–Q4 back to 2013.
///
/// <para><b>The two links are not usable as they arrive.</b> Both carry the literal string
/// <c>apikey=YOUR_API_KEY</c> rather than a key, so fetching one as-is fails. They are documentation of the URL
/// shape, not credentials — call <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/> or
/// <see cref="Endpoints.StatementEndpoints.GetFinancialReportWorkbookAsync"/> with
/// <see cref="Symbol"/>, <see cref="FiscalYear"/> and <see cref="Period"/> instead, which is what
/// <see cref="Period"/> is typed as an enum for.</para></summary>
public sealed record FinancialReportLink
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON integer on this path.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Which report — <see cref="FiscalPeriod.Annual"/> for the <c>FY</c> filing, or the named quarter.
    ///
    /// <para><b>Typed as the request enum rather than as FMP's label string</b>, because this value's job is to
    /// be handed back to <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>. Null when FMP sent a
    /// label this SDK does not recognise, which no measured row did.</para></summary>
    [JsonPropertyName("period")]
    [JsonConverter(typeof(ReportPeriodJsonConverter))]
    public FiscalPeriod? Period { get; init; }

    /// <summary>FMP's URL for the rendered JSON report. <b>Carries <c>YOUR_API_KEY</c>, not a key.</b></summary>
    [JsonPropertyName("linkJson")] public string? LinkJson { get; init; }

    /// <summary>FMP's URL for the XLSX workbook. <b>Carries <c>YOUR_API_KEY</c>, not a key.</b></summary>
    [JsonPropertyName("linkXlsx")] public string? LinkXlsx { get; init; }
}

/// <summary>One filing rendered as report sections. From <c>stable/financial-reports-json</c>.
///
/// <para><b>This is a rendered document, not a record, and the type does not pretend otherwise.</b> The response
/// is a flat object of 73 keys measured 2026-08-27 for AAPL FY2025: <c>symbol</c>, <c>period</c>, <c>year</c>,
/// and 70 report SECTION NAMES. The section names are truncated to about 30 characters
/// (<c>"CONSOLIDATED STATEMENTS OF OPER"</c>), carry spaces, parentheses and commas, and differ per filing —
/// <c>period=Q1</c> answered 45 keys against <c>FY</c>'s 73. Anything typed over them would be a guess dressed as
/// an API, so <see cref="Sections"/> stays open.</para>
///
/// <para>Each section is a JSON array of single-key objects, the key being a full column header and the value a
/// list of cell strings:</para>
///
/// <code>
/// {"CONSOLIDATED BALANCE SHEETS - USD ($) shares in Thousands, $ in Millions": ["Sep. 27, 2025", "Sep. 28, 2024"]}
/// </code>
///
/// <para>For figures you want to compute with, use the statement endpoints. This is for showing a filing the way
/// it was laid out.</para></summary>
[JsonConverter(typeof(FinancialReportJsonConverter))]
public sealed record FinancialReport
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The period FMP says it answered, in its own label vocabulary — <c>FY</c> or <c>Q1</c>–<c>Q4</c>.
    ///
    /// <para><b>Worth reading rather than assuming.</b> FMP normalises the request: asking for <c>annual</c> gets
    /// <c>FY</c> back, and asking for <c>quarter</c> got <c>Q1</c> back — which is why the SDK refuses to send
    /// that. See <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/>.</para></summary>
    public string? Period { get; init; }

    /// <summary>Fiscal year. Arrives as a JSON <b>string</b> on this path.</summary>
    public int? Year { get; init; }

    /// <summary>The report's sections, keyed by FMP's truncated section name. Never <see langword="null"/>;
    /// empty when the response carried nothing but the three scalars.</summary>
    public IReadOnlyDictionary<string, JsonElement> Sections { get; init; } =
        ReadOnlyDictionary<string, JsonElement>.Empty;
}

/// <summary>Splits <c>stable/financial-reports-json</c>'s flat object into three scalars and everything else.
///
/// <para><b>Hand-written rather than <c>[JsonExtensionData]</c>, which is the obvious tool and the wrong one
/// here.</b> That attribute requires the property to be a mutable <c>Dictionary&lt;string, JsonElement&gt;</c>
/// and public, so using it would put a mutable dictionary on the public surface of a record whose other
/// collection properties are read-only. Twenty lines buys consistency.</para></summary>
public sealed class FinancialReportJsonConverter : JsonConverter<FinancialReport>
{
    /// <inheritdoc/>
    public override FinancialReport Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A financial report must be a JSON object.");

        var symbol = "";
        string? period = null;
        int? year = null;
        var sections = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!;
            reader.Read();
            switch (name)
            {
                case "symbol": symbol = reader.GetString() ?? ""; break;
                case "period": period = reader.GetString(); break;
                // The wire sends "2025", a string, but an int would be just as legal and this costs nothing.
                case "year":
                    year = reader.TokenType switch
                    {
                        JsonTokenType.Number => reader.GetInt32(),
                        JsonTokenType.String when int.TryParse(reader.GetString(), out var parsed) => parsed,
                        _ => null,
                    };
                    break;
                default: sections[name] = JsonElement.ParseValue(ref reader); break;
            }
        }

        return new FinancialReport { Symbol = symbol, Period = period, Year = year, Sections = sections };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FinancialReport value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("symbol", value.Symbol);
        writer.WriteString("period", value.Period);
        if (value.Year is { } year) writer.WriteNumber("year", year); else writer.WriteNull("year");
        foreach (var (name, section) in value.Sections)
        {
            writer.WritePropertyName(name);
            section.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
