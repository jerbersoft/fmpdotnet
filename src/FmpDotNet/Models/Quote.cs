using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>A full quote, from <c>stable/quote</c>, <c>stable/batch-quote</c>, or any of the batch endpoints
/// called with <c>short=false</c>.
///
/// <para>Measured against the live API on 2026-08-27: the seventeen properties below are the whole response, with
/// none missing and none extra. AAPL answered
/// <c>{"symbol":"AAPL","name":"Apple Inc.","price":313.45,"changePercentage":1.14553,"change":3.55,
/// "volume":33571543,"dayLow":308.8001,"dayHigh":315.43,"yearHigh":344.57,"yearLow":225.95,
/// "marketCap":4603751738200,"priceAvg50":311.2182,"priceAvg200":282.12024,"exchange":"NASDAQ","open":310.245,
/// "previousClose":309.9,"timestamp":1787774400}</c>.</para>
///
/// <para><b>One shape covers every asset class.</b> <c>BTCUSD</c>, <c>EURUSD</c>, <c>^GSPC</c> and <c>GCUSD</c>
/// were each measured returning exactly these fields — which is why FMP's Indexes, Commodity, Forex and Crypto
/// sections re-document <c>stable/quote</c> rather than adding endpoints, and why this SDK models it once. Fields
/// that make no sense for the asset class simply carry whatever FMP puts there; <see cref="MarketCap"/> on a
/// currency pair is not meaningful.</para>
///
/// <para><b>This is the expensive shape.</b> On the whole-universe batch endpoints it is roughly five times the
/// payload of <see cref="ShortQuote"/> — measured 2026-08-27, <c>batch-etf-quotes</c> answered 1,345,381 bytes
/// short against 6,629,855 bytes full, for the same 14,537 rows. The SDK exposes the two as separate methods so
/// that cost is visible at the call site.</para></summary>
public sealed record Quote
{
    /// <summary>The symbol as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The security's display name — <c>"Apple Inc."</c>. Absent from <see cref="ShortQuote"/>, and one
    /// of the thirteen fields that make the full shape worth its extra bytes.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The last price.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>The day's move as a percentage, on 0–100 rather than as a fraction: AAPL read <c>1.14553</c> for
    /// a 1.15% move.
    ///
    /// <para><b>The wire spells this <c>changePercentage</c> here and <c>changePercent</c> on
    /// <see cref="EndOfDayBar.ChangePercent"/></b> — one concept, two spellings, in two groups a caller will use
    /// together. Both are mapped explicitly; neither name is the SDK's choice.</para></summary>
    [JsonPropertyName("changePercentage")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? ChangePercentage { get; init; }

    /// <summary>The day's move in price terms — <see cref="Price"/> minus <see cref="PreviousClose"/>, FMP's
    /// arithmetic rather than the SDK's.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>Shares traded so far in the session.
    ///
    /// <para><b><see cref="decimal"/> rather than <see cref="long"/>, established by the universe rather than by a
    /// single symbol.</b> AAPL and every other liquid name answer a whole number here, which is exactly why the
    /// field was first typed <c>long</c> — and the whole-market batches then refused to deserialise. Measured
    /// 2026-08-27: 496 of 4,778 <c>batch-crypto-quotes</c> rows are fractional (<c>0X1USD</c> at
    /// <c>10.492659228892249</c>), along with 17 of 14,537 ETFs, 6 of 14,352 NASDAQ listings, and one index —
    /// <c>^STOXX50E</c> at <c>479570.1</c>. A single-symbol probe cannot find this; only sweeping the universe
    /// can.</para>
    ///
    /// <para>The daily endpoints are different and stay <see cref="long"/>: <see cref="EndOfDayBar.Volume"/> was
    /// checked over 87 sessions of <c>BTCUSD</c> — the asset most likely to trade fractionally — and every one was
    /// integral.</para></summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }

    /// <summary>The session's low so far.</summary>
    [JsonPropertyName("dayLow")] public decimal? DayLow { get; init; }

    /// <summary>The session's high so far.</summary>
    [JsonPropertyName("dayHigh")] public decimal? DayHigh { get; init; }

    /// <summary>The highest price of the trailing 52 weeks.</summary>
    [JsonPropertyName("yearHigh")] public decimal? YearHigh { get; init; }

    /// <summary>The lowest price of the trailing 52 weeks.</summary>
    [JsonPropertyName("yearLow")] public decimal? YearLow { get; init; }

    /// <summary>Market capitalisation.
    ///
    /// <para><b><see cref="decimal"/> rather than <see cref="long"/>, and the reason is a floating-point artefact
    /// in FMP's own serialisation rather than real precision.</b> AAPL reads a clean <c>4603751738200</c>, but
    /// measured 2026-08-27 <c>GOOG</c> reads <c>4115284521472.9995</c> and a mutual fund reads
    /// <c>3658640886852.9995</c> — the same <c>.9995</c> tail, which is a double that could not represent the
    /// integer it came from. Nothing observed exceeded <see cref="long.MaxValue"/>; the problem is purely the
    /// fraction.</para>
    ///
    /// <para>This differs from <see cref="CompanyProfile.MarketCap"/>, which is still <see cref="long"/> because
    /// that endpoint has never been observed sending a fraction. If it ever does, it will fail the same way this
    /// did — loudly, on the whole response.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }

    /// <summary>The 50-day simple moving average of the close, as FMP computes it.</summary>
    [JsonPropertyName("priceAvg50")] public decimal? PriceAvg50 { get; init; }

    /// <summary>The 200-day simple moving average of the close, as FMP computes it.</summary>
    [JsonPropertyName("priceAvg200")] public decimal? PriceAvg200 { get; init; }

    /// <summary>The exchange FMP attributes the quote to — <c>"NASDAQ"</c>. A raw string rather than an enum, for
    /// the reason <see cref="EconomicRelease.Impact"/> gives: a value the SDK has never seen must not cost the
    /// caller the response.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>The session's opening price.</summary>
    [JsonPropertyName("open")] public decimal? Open { get; init; }

    /// <summary>The previous session's close.</summary>
    [JsonPropertyName("previousClose")] public decimal? PreviousClose { get; init; }

    /// <summary>When the quote was struck, read from a Unix epoch in <b>seconds</b>.
    ///
    /// <para><b>Seconds here, milliseconds on the aftermarket endpoints — under the same field name, in the same
    /// endpoint group.</b> Measured 2026-08-27: this field read <c>1787774400</c>, which as seconds is
    /// 2026-08-26 20:00:00 UTC — 16:00 ET, the closing print. <see cref="AftermarketTrade.Timestamp"/> read
    /// <c>1787819647000</c>, which as milliseconds is 04:34 ET, pre-market. Each is correct for its endpoint and
    /// neither unit can be inferred from the field name.</para>
    ///
    /// <para>Read with <see cref="NullableEpochSecondsInstantJsonConverter"/>, deliberately not the millisecond
    /// twin — which would report every quote as dated 1970-01-21, silently, since that value is perfectly
    /// representable. See the converter for why the two are separate types rather than one that guesses.</para></summary>
    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(NullableEpochSecondsInstantJsonConverter))]
    public Instant? Timestamp { get; init; }
}
