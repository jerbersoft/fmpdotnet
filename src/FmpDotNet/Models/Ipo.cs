using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One scheduled or priced offering from <c>stable/ipos-calendar</c>.
///
/// <para><b>This is mostly a scheduling feed, not a pricing one.</b> Measured across 450 rows on 2026-08-28,
/// <see cref="Shares"/> was null on 349, <see cref="PriceRange"/> on 441 and <see cref="MarketCap"/> on 354 —
/// and the three are absent independently, so a row can carry a share count and a market cap with no price
/// range beside them. <see cref="Actions"/> was <c>Expected</c> on 359 rows and <c>Priced</c> on 91, and even
/// among the 102 rows with any numeric populated, 11 were still <c>Expected</c>. Gate on the field you are
/// about to read, not on the label.</para></summary>
public sealed record IpoCalendarEntry
{
    /// <summary>The ticker the offering will trade under. Warrants and units appear as their own rows with
    /// their own tickers — <c>XLABW</c> beside <c>XLAB</c>, <c>IPHXU</c> for a unit — so one company can occupy
    /// several rows on one date.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The offering date, and the date this path selects on.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary><b>The same value as <see cref="Date"/>, in a different format, and it carries nothing.</b>
    ///
    /// <para>Measured across all 450 rows on 2026-08-28: the date part of <c>daa</c> equalled <c>date</c> in
    /// <b>450 of 450</b>, and the time part took exactly <b>one</b> distinct value across the whole response —
    /// <c>T04:00:00.000Z</c>, which is midnight Eastern. So this is <see cref="Date"/> at midnight in EDT,
    /// expressed as UTC, under a name that explains neither.</para>
    ///
    /// <para>Kept as the raw string rather than parsed to a date or an instant, deliberately. Parsing it would
    /// manufacture a second temporal property that cannot disagree with <see cref="Date"/> and would invite a
    /// caller to think it might mean something else. <b>Use <see cref="Date"/>.</b></para></summary>
    [JsonPropertyName("daa")] public string? Daa { get; init; }

    /// <summary>The issuer's name as FMP writes it, including the instrument — <c>"… Warrant"</c>,
    /// <c>"… Class A Common Stock"</c>, <c>"… Unit"</c>.</summary>
    [JsonPropertyName("company")] public string? Company { get; init; }

    /// <summary>Where it lists. Two values across 450 rows measured 2026-08-28, <c>NASDAQ</c> and <c>NYSE</c> —
    /// a string rather than an enum, because two values from one response is a sample, not a domain.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>FMP's status label: <c>Expected</c> on 359 of 450 rows and <c>Priced</c> on 91, measured
    /// 2026-08-28. Note it does not partition the numeric fields — 11 of the 102 rows carrying a populated
    /// number were still labelled <c>Expected</c>.</summary>
    [JsonPropertyName("actions")] public string? Actions { get; init; }

    /// <summary>Shares offered, or <see langword="null"/> — which is the common case, 349 of 450.
    ///
    /// <para><see langword="decimal"/> rather than an integer type, matching
    /// <see cref="SharesFloat.OutstandingShares"/>. The measured maximum was 555,555,555, which does fit an
    /// <see cref="int"/>; the type follows the SDK's existing convention for share counts rather than the
    /// narrowest thing today's sample allows.</para></summary>
    [JsonPropertyName("shares")] public decimal? Shares { get; init; }

    /// <summary>The offering price or price band, <b>as a formatted string</b>, or <see langword="null"/> —
    /// which is overwhelmingly the common case, 441 of 450.
    ///
    /// <para><b>Not a number, and this was measured rather than assumed.</b> The nine populated values on
    /// 2026-08-28 were all strings, in two shapes: six ranges (<c>"5.00 - 7.00"</c>, <c>"15 - 17"</c>,
    /// <c>"11.25 - 13.25"</c>) and three single prices (<c>"10.00"</c>). Typed <see langword="decimal"/> this
    /// property would read <b>null on all 450 rows</b> — null where FMP sent null, and null where FMP sent a
    /// price — with nothing in the data to tell the two apart. It is the same kind of field as
    /// <see cref="SecProfile.FiftyTwoWeekRange"/>.</para>
    ///
    /// <para>The SDK does not split or parse it: both shapes are real, the separator is not guaranteed, and a
    /// caller who wants numbers can see which shape they have.</para></summary>
    [JsonPropertyName("priceRange")] public string? PriceRange { get; init; }

    /// <summary>Expected market capitalisation at the offering, or <see langword="null"/> — 354 of 450.
    ///
    /// <para><b><see langword="decimal"/> and never a narrower type.</b> Measured 2026-08-28, values ran to
    /// <b>74,999,999,925</b> — about thirty-five times <see cref="int"/>'s ceiling of 2,147,483,647. An
    /// <see cref="int"/> property does not read an out-of-range value as null: <c>System.Text.Json</c> throws,
    /// and <c>FmpTransport</c> does not wrap <c>DeserializeAsync</c>, so a single such row would cost the caller
    /// the whole response. Same rule and same reason as
    /// <see cref="MarketCapitalization.MarketCap"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}
