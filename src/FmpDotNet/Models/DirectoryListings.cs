using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>One row of <c>stable/financial-statement-symbol-list</c> — the symbols FMP holds statements for,
/// 68,200 measured 2026-08-27.
///
/// <para><b>A strict subset of <c>stable/stock-list</c>'s 91,845</b> — none of the 68,200 fell outside it. So the
/// difference, 23,645 symbols, is exactly the set FMP carries but has no statements for, which is the question
/// this endpoint answers that the stock list cannot.</para></summary>
public sealed record FinancialStatementSymbol
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c> — the <c>stock-list</c> spelling, not the
    /// <c>actively-trading-list</c> one.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The currency the security trades in. Populated on all 68,200 measured rows.</summary>
    [JsonPropertyName("tradingCurrency")] public string? TradingCurrency { get; init; }

    /// <summary>The currency the company reports its statements in, which is <b>not always the one it trades
    /// in</b> — <c>TOELY</c> trades in USD and reports in JPY. Reading either field as "the currency" is wrong for
    /// one of them, and a caller comparing statement figures across symbols must group by this one.
    ///
    /// <para>Null on 149 of 68,200 rows.</para></summary>
    [JsonPropertyName("reportingCurrency")] public string? ReportingCurrency { get; init; }
}

/// <summary>One row of <c>stable/earnings-transcript-list</c> — every symbol FMP holds an earnings-call transcript
/// for, and how many, 11,178 measured 2026-08-27.
///
/// <para>FMP files this under both Directory and Earnings Transcript. It is on
/// <see cref="Endpoints.DirectoryEndpoints"/> because it is a directory: it says what exists, not what any
/// transcript says. <b>The transcripts themselves are not modelled</b> — that is three further paths in the long
/// tail of issue #25.</para></summary>
public sealed record TranscriptSymbol
{
    /// <summary>The ticker as FMP spells it.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The company name, under <c>companyName</c>.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>How many transcripts FMP holds for this symbol.
    ///
    /// <para><b>The wire sends this as a quoted string on all 11,178 rows</b> — <c>"noOfTranscripts": "6"</c>, not
    /// <c>6</c>. It binds to an <see cref="int"/> only because <c>FmpJsonContext</c> sets
    /// <c>NumberHandling = AllowReadingFromString</c>; that option is load-bearing for this property rather than
    /// incidental, and removing it would break this endpoint alone.</para>
    ///
    /// <para>The C# name drops FMP's <c>noOf</c> prefix, which is Hungarian for the type the property already
    /// declares.</para></summary>
    [JsonPropertyName("noOfTranscripts")] public int? TranscriptCount { get; init; }
}
