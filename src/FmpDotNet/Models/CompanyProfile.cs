using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>A company profile from <c>stable/profile</c>.
///
/// <para>Numeric-looking fields FMP delivers as strings keep their string type here rather than being coerced:
/// <see cref="Cik"/> is zero-padded (<c>"0000320193"</c>) and would lose its leading zeros as a number, and
/// <see cref="FullTimeEmployees"/> arrives quoted (<c>"166000"</c>) but is an estimate that some issuers report as
/// a range. Callers that want numbers can parse; callers that want to display the value cannot un-parse it.</para></summary>
public sealed record CompanyProfile
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>Registered company name.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>Latest trade price in <see cref="Currency"/>.</summary>
    [JsonPropertyName("price")] public decimal? Price { get; init; }

    /// <summary>Market capitalisation, in <see cref="Currency"/>.
    ///
    /// <para><b><see langword="decimal"/> and not <see langword="long"/>, because FMP serves this fractional.</b>
    /// Measured live 2026-08-27: <c>symbol=GOOG</c> answered <c>4098415617064.9995</c> while <c>GOOGL</c>
    /// answered the integral <c>4122584209576</c> the same minute. Read as <c>long?</c> — which this was until
    /// that measurement — <c>System.Text.Json</c> throws <c>JsonException</c> on the fractional row and aborts
    /// the whole response, so all 36 fields of the profile are lost to one fraction on one of them. The same
    /// defect, found the same way, as <see cref="Quote.MarketCap"/> and
    /// <see cref="MarketCapitalization.MarketCap"/>.</para></summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }

    /// <summary>Beta against the broad market.</summary>
    [JsonPropertyName("beta")] public decimal? Beta { get; init; }

    /// <summary>Most recent dividend per share.</summary>
    [JsonPropertyName("lastDividend")] public decimal? LastDividend { get; init; }

    /// <summary>52-week range as FMP renders it, e.g. <c>224.69-344.57</c>.</summary>
    [JsonPropertyName("range")] public string? Range { get; init; }

    /// <summary>Absolute price change on the session.</summary>
    [JsonPropertyName("change")] public decimal? Change { get; init; }

    /// <summary>Fractional price change on the session — <c>-0.14178</c> means -0.14178%, not -14%.</summary>
    [JsonPropertyName("changePercentage")] public decimal? ChangePercentage { get; init; }

    /// <summary>Session volume.
    ///
    /// <para><b><see langword="decimal"/> and not <see langword="long"/>, because FMP serves this fractional on
    /// some symbols and not on others, with no way to tell which in advance.</b> Measured live 2026-08-28 across
    /// fifteen symbols: four came back fractional — <c>PRTA</c> <c>269557.44003</c>, <c>PRDO</c>
    /// <c>394291.08578</c>, <c>BAC</c> <c>19757648.08675</c>, <c>RIVN</c> <c>31548435.12699</c> — and the rest
    /// (<c>AAPL</c>, <c>NVDA</c>, <c>TSLA</c>, <c>F</c>, <c>PLTR</c>, <c>SOFI</c>, <c>NIO</c>, <c>GME</c>,
    /// <c>AMC</c>, <c>BBAI</c>, <c>MRV.TO</c>) were integral. It is not even a fixed property of a symbol:
    /// <c>AAPL</c> itself answered <c>24580543.97571</c> at 15:03 ET and an integral volume twelve minutes later
    /// the same session. Read as <see langword="long"/> — which this was until that measurement — a fractional
    /// row does not become <see langword="null"/>: <c>System.Text.Json</c> throws <c>JsonException</c>, and
    /// <c>FmpTransport</c> does not wrap <c>DeserializeAsync</c>, so the one fractional row costs the whole
    /// 36-field response. Not the same figure as <see cref="BulkCompanyProfile.Volume"/>, a separate measurement
    /// on a separate endpoint.</para></summary>
    [JsonPropertyName("volume")] public decimal? Volume { get; init; }

    /// <summary>Average daily volume.
    ///
    /// <para><see langword="decimal"/> rather than <see langword="long"/>. Integral on all fifteen symbols
    /// sampled 2026-08-28 alongside <see cref="Volume"/> — no fraction was observed here — but widened for the
    /// same reason <see cref="BulkCompanyProfile.AverageVolume"/> already is: an averaged figure has no promise
    /// of being whole, and the type is cheaper to widen now than after a caller has stored it.</para></summary>
    [JsonPropertyName("averageVolume")] public decimal? AverageVolume { get; init; }

    /// <summary>Reporting currency, ISO 4217.</summary>
    [JsonPropertyName("currency")] public string? Currency { get; init; }

    /// <summary>SEC Central Index Key, zero-padded to ten digits.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>ISIN.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>CUSIP.</summary>
    [JsonPropertyName("cusip")] public string? Cusip { get; init; }

    /// <summary>Listing exchange, short form, e.g. <c>NASDAQ</c>.</summary>
    [JsonPropertyName("exchange")] public string? Exchange { get; init; }

    /// <summary>Listing exchange, long form, e.g. <c>NASDAQ Global Select</c>.</summary>
    [JsonPropertyName("exchangeFullName")] public string? ExchangeFullName { get; init; }

    /// <summary>FMP's sector label. The permitted values are the ones <c>stable/available-sectors</c> lists.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>FMP's industry label, from <c>stable/available-industries</c>.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>Country of domicile, ISO 3166-1 alpha-2.</summary>
    [JsonPropertyName("country")] public string? Country { get; init; }

    /// <summary>Corporate website.</summary>
    [JsonPropertyName("website")] public string? Website { get; init; }

    /// <summary>Business description.</summary>
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>Chief executive.</summary>
    [JsonPropertyName("ceo")] public string? Ceo { get; init; }

    /// <summary>Headcount as reported. Quoted in the payload; kept as text — see the type remarks.</summary>
    [JsonPropertyName("fullTimeEmployees")] public string? FullTimeEmployees { get; init; }

    /// <summary>Switchboard number.</summary>
    [JsonPropertyName("phone")] public string? Phone { get; init; }

    /// <summary>Street address.</summary>
    [JsonPropertyName("address")] public string? Address { get; init; }

    /// <summary>City.</summary>
    [JsonPropertyName("city")] public string? City { get; init; }

    /// <summary>State or province.</summary>
    [JsonPropertyName("state")] public string? State { get; init; }

    /// <summary>Postal code.</summary>
    [JsonPropertyName("zip")] public string? Zip { get; init; }

    /// <summary>Logo URL.</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }

    /// <summary>IPO date.</summary>
    [JsonPropertyName("ipoDate")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? IpoDate { get; init; }

    /// <summary>True when <see cref="Image"/> is FMP's placeholder rather than a real logo.</summary>
    [JsonPropertyName("defaultImage")] public bool? DefaultImage { get; init; }

    /// <summary>True when the security is an ETF.</summary>
    [JsonPropertyName("isEtf")] public bool? IsEtf { get; init; }

    /// <summary>True while the security still trades. Going false is how a delisting first shows up here.</summary>
    [JsonPropertyName("isActivelyTrading")] public bool? IsActivelyTrading { get; init; }

    /// <summary>True when the security is an ADR.</summary>
    [JsonPropertyName("isAdr")] public bool? IsAdr { get; init; }

    /// <summary>True when the security is a fund.</summary>
    [JsonPropertyName("isFund")] public bool? IsFund { get; init; }
}
