using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One market-capitalisation observation, serving all three of FMP's market-cap paths:
/// <c>stable/market-capitalization</c>, <c>stable/market-capitalization-batch</c> and
/// <c>stable/historical-market-capitalization</c>. Measured 2026-08-27, all three answer the identical three
/// fields, which is why one record serves them rather than three.
///
/// <para><b><see cref="MarketCap"/> is <see langword="decimal"/> and must never be narrowed to
/// <see langword="long"/>.</b> On 2026-08-27 a twenty-symbol batch answered <c>4098415617064.9995</c> for
/// <c>GOOG</c> — one fractional row in twenty, every other row integral. A <c>long?</c> binding throws
/// <c>JsonException</c> on that row and takes the whole response with it, so a caller loses nineteen good rows
/// to one fraction. A single-symbol fixture would never have shown it: <c>AAPL</c> answered the integral
/// <c>4620348450480</c> the same minute. This is the same defect as <see cref="Quote.MarketCap"/>, which was
/// found the same way on a different endpoint.</para></summary>
public sealed record MarketCapitalization
{
    /// <summary>Ticker as FMP spells it.
    ///
    /// <para><b>This is the only safe key for matching a batch response against a batch request.</b>
    /// <see cref="Endpoints.CompanyEndpoints.GetMarketCapBatchAsync"/> answers only for the symbols FMP has a
    /// row for and gives no indication that anything was dropped — see that method.</para></summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The session the capitalisation was observed on — a calendar date with no time of day. On the
    /// single-symbol and batch paths this is the most recent session; on the historical path it is the row's own
    /// session, ordered newest first.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Market capitalisation, in the listing's own currency. Fractional in the wild — see the note on
    /// the type for why this must stay <see langword="decimal"/>.</summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }
}
