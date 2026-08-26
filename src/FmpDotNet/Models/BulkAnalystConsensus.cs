using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One symbol's analyst rating distribution and the consensus label FMP derives from it. From
/// <c>stable/upgrades-downgrades-consensus-bulk</c>.
///
/// <para>Measured 2026-08-26: 13,363 rows, 326 kB, seven columns, no blank field anywhere in the response.</para>
///
/// <para><b>This is a global list, not a US one.</b> It is ordered by symbol and the first rows are
/// <c>000550.SZ</c> and <c>0005.HK</c> — two and a half times the row count of
/// <c>price-target-summary-bulk</c> over the same universe. A caller expecting US tickers will find most of what
/// it reads is not.</para>
///
/// <para><b>The five counts are a distribution, and they sum to the analyst count</b> — there is no separate
/// total column, so add them if you need one.</para></summary>
public sealed record BulkAnalystConsensus
{
    /// <summary>Ticker as FMP spells it, including foreign exchange suffixes such as <c>.SZ</c> and <c>.HK</c>.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>Analysts rating the symbol a strong buy.</summary>
    public int? StrongBuy { get; init; }

    /// <summary>Analysts rating the symbol a buy.</summary>
    public int? Buy { get; init; }

    /// <summary>Analysts rating the symbol a hold.</summary>
    public int? Hold { get; init; }

    /// <summary>Analysts rating the symbol a sell.</summary>
    public int? Sell { get; init; }

    /// <summary>Analysts rating the symbol a strong sell.</summary>
    public int? StrongSell { get; init; }

    /// <summary>FMP's own summary label for the distribution.
    ///
    /// <para><b>Left as the upstream string rather than mapped to an enum</b>, because the observed vocabulary
    /// does not match the column names it is derived from. Measured 2026-08-26 across all 13,363 rows the only
    /// values were <c>Buy</c> (8,402), <c>Hold</c> (4,622), <c>Sell</c> (332) and <c>Strong Buy</c> (7) — note
    /// the space, and note that <c>Strong Sell</c> never appeared at all despite having its own count column. An
    /// enum built on that snapshot would reject a value the moment one of the missing labels turns up.</para></summary>
    public string? Consensus { get; init; }

    internal static BulkAnalystConsensus FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        StrongBuy = row.GetInt32("strongBuy"),
        Buy = row.GetInt32("buy"),
        Hold = row.GetInt32("hold"),
        Sell = row.GetInt32("sell"),
        StrongSell = row.GetInt32("strongSell"),
        Consensus = row.GetString("consensus"),
    };
}
