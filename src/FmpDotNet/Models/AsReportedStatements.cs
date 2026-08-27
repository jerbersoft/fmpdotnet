using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One filing's figures exactly as the issuer tagged them, from the four <c>*-as-reported</c> paths.
///
/// <para><b>This is not a statement model with a few extra fields — it is a different kind of object.</b> The
/// modelled statements (<see cref="IncomeStatement"/> and friends) are FMP's normalisation: a fixed field set
/// that means the same thing for every filer. This is the XBRL as filed, so the keys are the issuer's own tags
/// and the set changes per company and per year. Measured 2026-08-27, <c>income-statement-as-reported</c>
/// answered 24 keys for AAPL and 39 for JPM; <c>financial-statement-full-as-reported</c> answered 300 for AAPL
/// and 923 for JPM. Nothing is missing from the smaller one — the filers tagged different things.</para>
///
/// <para><b>Which is why <see cref="Data"/> is an open dictionary of <see cref="JsonElement"/> and not a record,
/// and not a dictionary of <see cref="decimal"/>.</b> No record can express a field set that varies by filer. And
/// the values are not all numbers: AAPL's FY2025 full statement held 234 integers, 47 strings and 19 floats in
/// one object. The strings are filing metadata — <c>documenttype: "10-K"</c>,
/// <c>currentfiscalyearenddate: "--09-27"</c> — and a <c>Dictionary&lt;string, decimal&gt;</c> throws on every
/// one of them, losing the whole response. Some of the integers are not money either:
/// <c>entityaddresspostalzipcode</c> is a postal code. <see cref="JsonElement"/> is honest about what arrived and
/// costs the caller one <c>GetDecimal()</c>.</para>
///
/// <para>Keys are lowercased, concatenated XBRL tags —
/// <c>revenuefromcontractwithcustomerexcludingassessedtax</c>, <c>costofgoodsandservicessold</c>. Measured over
/// AAPL, JPM, BRK-B and TSM: no null values, no keys colliding under case-insensitive comparison, no non-ASCII
/// keys, and the largest magnitude anywhere was 7.1e12, comfortably inside <see cref="decimal"/>.</para></summary>
public sealed record AsReportedStatement
{
    /// <summary>Ticker as FMP spells it, read from the response rather than echoed back from the argument.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON <b>integer</b> on these four paths and as a <b>string</b> on seven
    /// others in the same section; one <c>int?</c> reads both only because <c>FmpJsonContext</c> sets
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c> for annual, <c>Q1</c>–<c>Q4</c> for quarterly.
    /// FMP's RESPONSE vocabulary, which is not the request vocabulary <see cref="FiscalPeriod"/> sends for
    /// <see cref="FiscalPeriod.Annual"/>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the figures are reported in — not necessarily USD.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Period end — the last day of the fiscal period this row reports.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The filing's tagged facts, keyed by XBRL tag. Never <see langword="null"/> — an absent or empty
    /// <c>data</c> object binds to an empty dictionary, so a caller does not null-check it.
    ///
    /// <para>Read a number with <c>Data["revenuefromcontractwithcustomerexcludingassessedtax"].GetDecimal()</c>,
    /// and check <see cref="JsonElement.ValueKind"/> first on any key you have not measured: see the type's
    /// summary for why a third of some payloads is not numeric.</para></summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, JsonElement> Data
    {
        // Backed by a nullable field rather than an auto-property with a `= Empty` initialiser: measured
        // 2026-08-27, System.Text.Json's source-generated deserialiser sets every `init` member through one
        // object-initialiser expression, so a member absent from the payload binds through the initialiser to
        // `default` rather than to the property's own field initialiser. An auto-property here would silently
        // let an absent `data` key answer null despite the doc comment promising otherwise.
        get => _data ?? ReadOnlyDictionary<string, JsonElement>.Empty;
        init => _data = value;
    }

    private readonly IReadOnlyDictionary<string, JsonElement>? _data;
}

/// <summary>One period's revenue split by product line or by geography, from the two
/// <c>revenue-*-segmentation</c> paths.
///
/// <para><b>The same five-field envelope as <see cref="AsReportedStatement"/>, and deliberately a different
/// type</b> — because <see cref="Data"/> here is <see cref="decimal"/> rather than <see cref="JsonElement"/>.
/// That is measured, not assumed: across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B and KO, both endpoints, both
/// cadences, <b>every row rather than a sample</b>, the values were 3,201 integers and 36 floats and not one
/// string. Segmentation is genuinely segment-name-to-number where as-reported is not, and sharing a field layout
/// is not a reason to share a type when one of the two has a proven value domain. If FMP ever sends a string
/// here the binding throws, which is the correct outcome — a non-numeric segment revenue is a defect worth
/// hearing about rather than silently reading as zero.</para>
///
/// <para><b>Keys are the company's own segment names</b>, so they carry spaces, ampersands and commas —
/// <c>"Wearables, Home and Accessories"</c>, <c>"Consumer &amp; Community Banking"</c> — and they change when the
/// company reorganises. They are labels, not identifiers, and nothing guarantees the same name across
/// years.</para>
///
/// <para>Measured 2026-08-27, the segment count ranges from 1 (O) to 6 (XOM) per period.</para></summary>
public sealed record RevenueSegmentation
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Fiscal year. Arrives as a JSON integer on both segmentation paths.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Fiscal period as FMP labels the row: <c>FY</c>, or <c>Q1</c>–<c>Q4</c>.</summary>
    [JsonPropertyName("period")] public string? Period { get; init; }

    /// <summary>ISO currency the figures are reported in.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Period end.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Revenue by segment name. Never <see langword="null"/>; empty when FMP sent no split.</summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, decimal> Data
    {
        // See AsReportedStatement.Data for why this is backed by a nullable field rather than an auto-property:
        // an `init` auto-property's own initialiser is not honoured when the JSON key is absent.
        get => _data ?? ReadOnlyDictionary<string, decimal>.Empty;
        init => _data = value;
    }

    private readonly IReadOnlyDictionary<string, decimal>? _data;
}
