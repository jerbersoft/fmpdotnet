using System.Text.Json.Serialization;
using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One symbol's price change over eleven trailing windows, from <c>stable/stock-price-change</c>.
///
/// <para>Measured against the live API on 2026-08-27: the twelve properties below are the whole response. AAPL
/// answered <c>{"symbol":"AAPL","1D":1.14553,"5D":0.44864605,"1M":-6.96328,"3M":0.83641628,"6M":18.65016,
/// "ytd":15.29832,"1Y":35.99288,"3Y":73.95527,"5Y":110.9354,"10Y":1073.53051,"max":244115.03701}</c>.</para>
///
/// <para><b>Every wire name here needs an explicit mapping, and that is not a stylistic choice.</b> Ten of the
/// eleven windows are spelled as things C# cannot name — <c>1D</c>, <c>5D</c>, <c>1M</c> and the rest all begin
/// with a digit — so there is no casing convention that would reach them and no property name that could match by
/// default. FMP is also not self-consistent about the ones that could have been: the windows are uppercase but
/// <c>ytd</c> and <c>max</c> are lowercase.</para>
///
/// <para><b>The values are percentages on 0–100, not fractions</b>, and they are already relative — no base price
/// is carried, so nothing here can be turned back into a price without a separate <see cref="Quote"/> call.
/// <see cref="OneDay"/> matches <see cref="Quote.ChangePercentage"/> for the same symbol, measured at
/// <c>1.14553</c> on both.</para>
///
/// <para><b><see cref="Max"/> is since inception and gets very large</b> — AAPL read 244,115%, which is a factor
/// of about 2,442. It is also the field most likely to be split-sensitive in a way the others are not; treat it
/// as a headline figure rather than an input to anything.</para></summary>
public sealed record PriceChange
{
    /// <summary>The symbol as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The change over one day, as a percentage on 0–100. Matches
    /// <see cref="Quote.ChangePercentage"/>.</summary>
    [JsonPropertyName("1D")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? OneDay { get; init; }

    /// <summary>The change over five days, as a percentage on 0–100.</summary>
    [JsonPropertyName("5D")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? FiveDay { get; init; }

    /// <summary>The change over one month, as a percentage on 0–100.</summary>
    [JsonPropertyName("1M")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? OneMonth { get; init; }

    /// <summary>The change over three months, as a percentage on 0–100.</summary>
    [JsonPropertyName("3M")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? ThreeMonth { get; init; }

    /// <summary>The change over six months, as a percentage on 0–100.</summary>
    [JsonPropertyName("6M")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? SixMonth { get; init; }

    /// <summary>The change since the start of the calendar year, as a percentage on 0–100. Lowercase on the wire,
    /// where the fixed windows are uppercase.</summary>
    [JsonPropertyName("ytd")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? YearToDate { get; init; }

    /// <summary>The change over one year, as a percentage on 0–100.</summary>
    [JsonPropertyName("1Y")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? OneYear { get; init; }

    /// <summary>The change over three years, as a percentage on 0–100.</summary>
    [JsonPropertyName("3Y")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? ThreeYear { get; init; }

    /// <summary>The change over five years, as a percentage on 0–100.</summary>
    [JsonPropertyName("5Y")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? FiveYear { get; init; }

    /// <summary>The change over ten years, as a percentage on 0–100. Already well past 100 for a long-lived
    /// winner — AAPL read 1073.53.</summary>
    [JsonPropertyName("10Y")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? TenYear { get; init; }

    /// <summary>The change since inception, as a percentage on 0–100. Lowercase on the wire, and very large — see
    /// the note on the type.</summary>
    [JsonPropertyName("max")]
    [JsonConverter(typeof(TolerantDecimalJsonConverter))]
    public decimal? Max { get; init; }
}
