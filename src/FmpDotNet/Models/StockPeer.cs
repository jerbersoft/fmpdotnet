using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One comparable company, from <c>stable/stock-peers</c>.
///
/// <para>Four fields, measured 2026-08-27: <c>AAPL</c> answered 9 rows, <c>JPM</c> and <c>SPY</c> 10 each,
/// <c>ZZZZNOPE</c> <c>[]</c>. <b>ETFs get peers</b> — <c>SPY</c> answers <c>IVV</c> and friends — so this is not
/// an equity-only endpoint.</para>
///
/// <para>This has its own record rather than reusing <see cref="MarketCapitalization"/> for one reason: the
/// wire name. See <see cref="MarketCap"/>.</para></summary>
public sealed record StockPeer
{
    /// <summary>The peer's ticker, as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The peer's company name.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>Last price, in the listing's own currency.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Market capitalisation.
    ///
    /// <para><b>FMP spells this <c>mktCap</c> here and <c>marketCap</c> on every other endpoint in the Company
    /// group.</b> Measured 2026-08-27. The <c>[JsonPropertyName]</c> below is load-bearing: "correcting" it to
    /// <c>marketCap</c> makes every row bind <see langword="null"/> silently, with no exception and no missing
    /// row to notice. The C# name stays <c>MarketCap</c> so callers see one spelling across the SDK.</para>
    ///
    /// <para><see langword="decimal"/> rather than <see langword="long"/> for the reason recorded on
    /// <see cref="MarketCapitalization.MarketCap"/>. All ten symbols probed on 2026-08-27 answered integral
    /// values here, which is not evidence of integrality — it is the same sample size that hid the fractional
    /// <c>GOOG</c> row on the batch endpoint.</para></summary>
    [JsonPropertyName("mktCap")] public decimal? MarketCap { get; init; }
}
