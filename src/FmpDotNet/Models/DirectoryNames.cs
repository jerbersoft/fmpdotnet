using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/available-sectors</c>: a sector label wrapped in a single-property object.
///
/// <para>The endpoint answers <c>[{"sector":"Basic Materials"}, …]</c> — 11 rows, one key each, measured against
/// the live API on 2026-08-26. Nothing hangs off the label, so an object per sector is packaging rather than
/// structure. This type exists only to unwrap it once, inside
/// <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync(CancellationToken)"/>, which is why it is
/// <see langword="internal"/>: were it public, every caller would have to reach through <c>.Sector</c> and handle
/// a null that the SDK has already dealt with. The public shape is
/// <see cref="IReadOnlyList{T}"/> of <see cref="string"/>.</para></summary>
internal sealed record SectorName
{
    /// <summary>The sector label. Nullable because the deserialiser cannot promise a key is present, not because
    /// any measured row omitted it — all 11 carried a non-empty value.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }
}

/// <summary>One row of <c>stable/available-industries</c>: an industry label wrapped in a single-property object.
///
/// <para>Same packaging as <see cref="SectorName"/> under a different key — <c>[{"industry":"Steel"}, …]</c>, 159
/// rows measured on 2026-08-26 — and unwrapped in the same place and for the same reason, so it is
/// <see langword="internal"/> too.</para></summary>
internal sealed record IndustryName
{
    /// <summary>The industry label. See <see cref="SectorName.Sector"/> for why it is nullable.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }
}

/// <summary>One row of <c>stable/stock-list</c>: a ticker and its company name under the key
/// <c>companyName</c>.
///
/// <para><see langword="internal"/> for the same reason as <see cref="SectorName"/>, and for one more: this shape
/// and <see cref="ActivelyTradingRow"/> differ only in that key name, and publishing both would push an upstream
/// spelling inconsistency onto every caller. They are unwrapped into the single public
/// <see cref="CompanySymbol"/> inside
/// <see cref="Endpoints.DirectoryEndpoints.GetStockListAsync(CancellationToken)"/>.</para></summary>
internal sealed record StockListRow
{
    /// <summary>The ticker. Nullable because the deserialiser cannot promise a key is present — no measured row
    /// omitted it, and all 91,844 were unique.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
}

/// <summary>One row of <c>stable/actively-trading-list</c>: the same ticker-and-name pair as
/// <see cref="StockListRow"/>, except that the name arrives under <c>name</c> rather than <c>companyName</c>.
///
/// <para>The two spellings are the whole difference between the endpoints' row shapes, and the values behind them
/// agree character for character on every shared symbol. That is why this type exists rather than a second
/// <c>[JsonPropertyName]</c> on one model: System.Text.Json binds one wire name per property, so two names need
/// two shapes — mapped to one public <see cref="CompanySymbol"/> at the endpoint.</para></summary>
internal sealed record ActivelyTradingRow
{
    /// <summary>The ticker. See <see cref="StockListRow.Symbol"/>.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>name</c> — the key that differs from
    /// <see cref="StockListRow.CompanyName"/>.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}

/// <summary>One row of <c>stable/available-countries</c>: an ISO 3166-1 alpha-2 country code wrapped in a
/// single-property object.
///
/// <para>The same packaging as <see cref="SectorName"/> and <see cref="IndustryName"/> under a third key —
/// <c>[{"country":"FK"}, …]</c>, 117 rows measured 2026-08-27 — and unwrapped in the same place and for the same
/// reason, so it is <see langword="internal"/> too.</para>
///
/// <para><b>These are codes, not names.</b> The key is spelled <c>country</c>, which reads like a name, and every
/// measured value is a two-letter code. <c>available-exchanges</c> carries both spellings for the same fact —
/// <see cref="ExchangeInfo.CountryCode"/> and <see cref="ExchangeInfo.CountryName"/> — so a caller who needs
/// display text can join against that rather than shipping its own table.</para></summary>
internal sealed record CountryName
{
    /// <summary>The ISO alpha-2 code. See <see cref="SectorName.Sector"/> for why it is nullable.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }
}
