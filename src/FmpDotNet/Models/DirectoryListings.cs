using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

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
    /// incidental, and removing it alone would break this endpoint.</para>
    ///
    /// <para>The C# name drops FMP's <c>noOf</c> prefix, which is Hungarian for the type the property already
    /// declares.</para></summary>
    [JsonPropertyName("noOfTranscripts")] public int? TranscriptCount { get; init; }
}

/// <summary>One ticker rename from <c>stable/symbol-change</c> — 5,456 measured 2026-08-27, back to the start of
/// FMP's record.
///
/// <para>This is the endpoint that explains a symbol vanishing from
/// <see cref="Endpoints.DirectoryEndpoints.GetActivelyTradingAsync"/> without being delisted. A caller
/// reconciling historical positions against current tickers needs the whole set, which is why
/// <see cref="Endpoints.DirectoryEndpoints.GetSymbolChangesAsync"/> takes no paging arguments and asks for all of
/// it — see that method for what the default would otherwise cost.</para></summary>
public sealed record SymbolChange
{
    /// <summary>The date the change took effect. ISO <c>uuuu-MM-dd</c> on all 5,456 measured rows, none null.
    ///
    /// <para>A <see cref="LocalDate"/> rather than an <see cref="Instant"/>: a rename belongs to a trading day,
    /// and the payload carries no time of day.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>The company's name at the time of the change. Populated on all 5,456 rows.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }

    /// <summary>The ticker before the change — the one to look up in historical data.</summary>
    [JsonPropertyName("oldSymbol")] public string? OldSymbol { get; init; }

    /// <summary>The ticker after the change — the one FMP's current endpoints answer to.</summary>
    [JsonPropertyName("newSymbol")] public string? NewSymbol { get; init; }
}

/// <summary>One SEC registrant from <c>stable/cik-list</c> — about 512,665 measured 2026-08-27.
///
/// <para><b>This is not a symbol directory.</b> Against <c>stable/stock-list</c>'s 91,845 tickers, this endpoint
/// carries every entity with an SEC Central Index Key, most of which have no ticker at all: investment advisers,
/// funds, and <b>individuals</b> — <c>Thompson David Blair</c> is a measured row. A caller expecting a company
/// list will find five and a half times more rows than there are listed securities.</para></summary>
public sealed record CikEntry
{
    /// <summary>The Central Index Key, <b>zero-padded to ten characters</b> — <c>0002150676</c>. All 200 rows
    /// sampled carried exactly ten.
    ///
    /// <para><b>A <see cref="string"/> rather than an integer, deliberately.</b> The padding is part of the
    /// identifier as SEC systems and FMP's own <c>search-cik</c> spell it, and parsing to a number discards it —
    /// after which every consumer has to remember to re-pad, and the one that forgets fails a lookup silently.
    /// <c>SearchEndpoints.FindByCikAsync</c> accepts either form and always echoes this
    /// one.</para></summary>
    [JsonPropertyName("cik")] public string? Cik { get; init; }

    /// <summary>The registrant's name as filed. Populated on every measured row. Not necessarily a company —
    /// see the type's own remarks.</summary>
    [JsonPropertyName("companyName")] public string? CompanyName { get; init; }
}
