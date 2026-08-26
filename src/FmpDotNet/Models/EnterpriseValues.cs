using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

// CS1591 (missing XML comment on a public member) is disabled HERE, for this file only, rather than for the
// whole assembly. The 5 properties below are a flat transcription of FMP's wire fields: the property name
// carries the same information a generated one-line summary would, and 5 of those would bury the type-level
// documentation above — which is where this response's actual quirks are recorded.
//
// Scoping it to the file is the point. Suppressing CS1591 project-wide, as this used to, also meant a NEW
// undocumented public member anywhere in the SDK compiled silently. The seven transcription models are the only
// exemptions, and the zero-warning bar holds everywhere else.
#pragma warning disable CS1591

/// <summary>The market-capitalisation-to-enterprise-value bridge for one date. From <c>stable/enterprise-values</c>.
///
/// <para>This endpoint is the odd one out: it carries <b>no</b> <c>fiscalYear</c> and <b>no</b> <c>period</c>, so a row is identified by <see cref="Symbol"/> and <see cref="Date"/> alone. Measured against the live API on 2026-08-26 — the eight fields below are the whole response.</para>
///
/// <para>Every figure is <see langword="decimal"/>, not double. Values measured on the live API reach
/// 4.4e12 and carry up to 17 significant digits — decimal holds that exactly, double rounds it.</para></summary>
public sealed record EnterpriseValues
{
    /// <summary>Ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>Valuation date for this row.</summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    // ---- Enterprise value bridge ----
    [JsonPropertyName("stockPrice")] public decimal? StockPrice { get; init; }

    /// <summary>Share count used for the market capitalisation on this row.</summary>
    [JsonPropertyName("numberOfShares")] public decimal? NumberOfShares { get; init; }

    [JsonPropertyName("marketCapitalization")] public decimal? MarketCapitalization { get; init; }

    [JsonPropertyName("minusCashAndCashEquivalents")] public decimal? MinusCashAndCashEquivalents { get; init; }

    [JsonPropertyName("addTotalDebt")] public decimal? AddTotalDebt { get; init; }

    [JsonPropertyName("enterpriseValue")] public decimal? EnterpriseValue { get; init; }
}
