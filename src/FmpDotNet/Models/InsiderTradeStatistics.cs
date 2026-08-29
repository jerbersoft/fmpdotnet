using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One quarter of aggregated insider activity for one issuer, from
/// <c>stable/insider-trading/statistics</c>.
///
/// <para>One row per quarter the issuer has any, newest first — 94 rows for AAPL, measured 2026-08-28, going
/// back to 2003.</para>
///
/// <para><b>Fractional values are the normal case here, not the exception.</b> Over those 94 rows
/// <see cref="AcquiredDisposedRatio"/> was fractional on 87, <see cref="AverageDisposed"/> on 85 and
/// <see cref="AverageAcquired"/> on 76 — while the totals and the transaction counts were fractional on none.
/// That split is the reason four fields here are <see cref="decimal"/> and four are <see cref="int"/>.</para>
///
/// <para><b>Two pairs of fields read alike and count different things.</b>
/// <see cref="DisposedTransactions"/> and <see cref="TotalSales"/> both count filings;
/// <see cref="TotalDisposed"/> counts shares. On the measured 2026 Q2 row those are 40, 14 and 927,380.</para></summary>
public sealed record InsiderTradeStatistics
{
    /// <summary>The issuer's ticker.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The issuer's Central Index Key, zero-padded.</summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The calendar year.</summary>
    [JsonPropertyName("year")] public int? Year { get; init; }

    /// <summary>The calendar quarter, 1 to 4.</summary>
    [JsonPropertyName("quarter")] public int? Quarter { get; init; }

    /// <summary>How many acquiring transactions were filed. A count of filings — never fractional across 94
    /// rows measured.</summary>
    [JsonPropertyName("acquiredTransactions")] public int? AcquiredTransactions { get; init; }

    /// <summary>How many disposing transactions were filed. A count.</summary>
    [JsonPropertyName("disposedTransactions")] public int? DisposedTransactions { get; init; }

    /// <summary><see cref="AcquiredTransactions"/> over <see cref="DisposedTransactions"/>. <b>Fractional on
    /// 87 of 94 rows measured</b> — <c>0.175</c> and <c>1.5</c> on the captured rows — and <c>0</c> in a
    /// quarter with no acquisitions, which is a value rather than an absence.</summary>
    [JsonPropertyName("acquiredDisposedRatio")] public decimal? AcquiredDisposedRatio { get; init; }

    /// <summary>Total <b>shares</b> acquired across the quarter, not a count of filings.</summary>
    [JsonPropertyName("totalAcquired")] public decimal? TotalAcquired { get; init; }

    /// <summary>Total shares disposed across the quarter — 927,380 on the measured 2026 Q2 row, against a
    /// <see cref="TotalSales"/> of 14.</summary>
    [JsonPropertyName("totalDisposed")] public decimal? TotalDisposed { get; init; }

    /// <summary>Mean shares per acquiring transaction. <b>Fractional on 76 of 94 rows.</b></summary>
    [JsonPropertyName("averageAcquired")] public decimal? AverageAcquired { get; init; }

    /// <summary>Mean shares per disposing transaction. <b>Fractional on 85 of 94 rows.</b></summary>
    [JsonPropertyName("averageDisposed")] public decimal? AverageDisposed { get; init; }

    /// <summary>How many open-market purchases were filed — a narrower count than
    /// <see cref="AcquiredTransactions"/>, which includes awards and exercises. <c>0</c> on all three captured
    /// AAPL quarters.</summary>
    [JsonPropertyName("totalPurchases")] public int? TotalPurchases { get; init; }

    /// <summary>How many open-market sales were filed. A count of filings, <b>not shares</b> — 14 on the
    /// measured 2026 Q2 row.</summary>
    [JsonPropertyName("totalSales")] public int? TotalSales { get; init; }
}

/// <summary>One insider FMP knows by name, from <c>stable/insider-trading/reporting-name</c> — a lookup that
/// turns a name into the <c>reportingCik</c>
/// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/> takes.
///
/// <para><b>Matching is on a prefix of a surname-first name.</b> Measured 2026-08-28: <c>name=Cook</c> answered
/// 133 rows all beginning "Cook"; <c>name=Apple</c> answered 20 including <c>"Applebach Richard Jr"</c> and
/// <c>"Applebaum Michelle Galanter"</c>. Searching a given name finds nothing, and this is not a company
/// search.</para></summary>
public sealed record InsiderReportingName
{
    /// <summary>The insider's Central Index Key, zero-padded — the value to pass as
    /// <c>reportingCik</c>.</summary>
    [JsonPropertyName("reportingCik")] public string? ReportingCik { get; init; }

    /// <summary>The name as EDGAR spells it, surname first — <c>"Cook Adam T"</c>.</summary>
    [JsonPropertyName("reportingName")] public string? ReportingName { get; init; }
}

/// <summary>One SEC transaction code, from <c>stable/insider-trading-transaction-type</c> — the eighteen values
/// <see cref="Endpoints.InsiderTradesEndpoints.SearchAsync"/> accepts and
/// <see cref="InsiderTrade.TransactionType"/> carries.
///
/// <para><b>A one-field record rather than an enum, and rather than a bare string.</b></para>
///
/// <para><i>Not an enum:</i> the list is served by an endpoint, so FMP can extend it without an SDK release,
/// and the empty string that appears on 40 of 1,000 measured trade rows would have no member to map to. A
/// closed C# enum over an open server-side list is a breaking change waiting for a Tuesday.</para>
///
/// <para><i>Not <c>IReadOnlyList&lt;string&gt;</c>:</i> the wire shape is
/// <c>[{"transactionType": "A-Award"}, …]</c>, and projecting it to bare strings would need a converter whose
/// only job is to discard a key. If FMP adds a description field, this record absorbs it and the projection
/// would have to be unpicked.</para></summary>
public sealed record InsiderTransactionType
{
    /// <summary>The code — <c>"A-Award"</c> through <c>"Z-Trust"</c>. The letter is the SEC's Table I/II code
    /// and the word is FMP's gloss on it.</summary>
    [JsonPropertyName("transactionType")] public string? TransactionType { get; init; }
}
