using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One share split. Serves both <c>stable/splits</c>, which answers one symbol's whole history, and
/// <c>stable/splits-calendar</c>, which answers every symbol in a date range — five fields, measured identical
/// on 2026-08-28.
///
/// <para><b>The ratio is reported as the two integers FMP sent, and nothing is computed from them.</b> A
/// forward split is <c>4/1</c>, a reverse split is <c>1/8</c>, and awkward real-world ratios are ordinary here:
/// <c>51/50</c> for a Taiwanese stock dividend and <c>5629/1000</c> for a Turkish spin-off, both in the same
/// captured response. Dividing them is the caller's business, at the precision the caller wants.</para></summary>
public sealed record StockSplit
{
    /// <summary>Ticker as FMP spells it. The calendar is global — <c>8011.T</c>, <c>SPICEISLIN.BO</c>,
    /// <c>MAZAYA.KW</c> and <c>GOODY.IS</c> all appear in one captured response — so a caller filtering to US
    /// listings must do so explicitly.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The date the split takes effect, and the date the calendar path selects on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Shares held after the split, per <see cref="Denominator"/> shares held before.
    ///
    /// <para><see langword="int"/> rather than a wider or fractional type, and that was measured rather than
    /// assumed: across 961 calendar rows on 2026-08-28 every value was whole, and the largest was
    /// <b>1,011,977</b> against an <see cref="int"/> ceiling of 2,147,483,647. Recording what was measured beats
    /// widening against a fractional value nobody has seen — and the same check went the other way on
    /// <see cref="IpoCalendarEntry.MarketCap"/>, which does not fit.</para></summary>
    [JsonPropertyName("numerator")] public int? Numerator { get; init; }

    /// <summary>Shares held before the split, per <see cref="Numerator"/> shares held after. Largest measured
    /// value 1,000,000.</summary>
    [JsonPropertyName("denominator")] public int? Denominator { get; init; }

    /// <summary>FMP's classification of the event, or <see langword="null"/> where it does not classify one.
    ///
    /// <para><b>Null on 16 of 961 rows measured 2026-08-28</b>, with every other field on those rows fully
    /// populated — so a null here is FMP declining to label the event, not a broken row. The three string values
    /// observed were <c>stock-split</c> ×934, <c>stock-dividend</c> ×10 and <c>spin-off</c> ×1.</para>
    ///
    /// <para>A string and not an enum: four values counting null, drawn from one response, is a sample rather
    /// than a domain, and an unlisted value should reach the caller unchanged rather than fail to
    /// deserialise.</para></summary>
    [JsonPropertyName("splitType")] public string? SplitType { get; init; }
}
