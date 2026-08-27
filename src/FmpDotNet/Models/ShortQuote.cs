using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>The four-field quote — symbol, price, change and volume — that nine of FMP's sixteen Quote endpoints
/// return.
///
/// <para>Measured against the live API on 2026-08-27. These four properties are the whole response from
/// <c>stable/quote-short</c>, <c>stable/batch-quote-short</c>, <c>stable/batch-exchange-quote</c>, and the six
/// asset-class batches (<c>batch-etf-quotes</c>, <c>batch-mutualfund-quotes</c>, <c>batch-commodity-quotes</c>,
/// <c>batch-crypto-quotes</c>, <c>batch-forex-quotes</c>, <c>batch-index-quotes</c>) — byte-identical in shape
/// across all nine.</para>
///
/// <para><b>This is the shape to reach for on the whole-universe endpoints.</b> The same eight batch paths answer
/// <see cref="Quote"/> instead when called with <c>short=false</c>, at roughly five times the payload: measured
/// 2026-08-27, <c>batch-etf-quotes</c> returned 1,345,381 bytes in this shape and 6,629,855 bytes in the full one,
/// for the same 14,537 rows. If symbol and price are what you need, this is 80% cheaper for exactly the same
/// coverage.</para>
///
/// <para><b><see cref="Change"/> is a price move, not a percentage</b> — the percentage field of
/// <see cref="Quote"/> is one of the thirteen this shape drops. A caller who needs a percentage has to divide by
/// the previous close, which this shape also does not carry, so in practice needing a percentage means calling the
/// full form.</para></summary>
public sealed record ShortQuote
{
    /// <summary>The symbol as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The last price.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The day's move in price terms, matching <see cref="Quote.Change"/>. Not a percentage — see the
    /// note on the type.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>Shares traded so far in the session. <see cref="decimal"/> rather than <see cref="long"/> —
    /// fractional volumes are common on the whole-universe batches this shape is built for. See
    /// <see cref="Quote.Volume"/> for the measurements.</summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }
}
