namespace FmpDotNet.Models;

/// <summary>One entry in a symbol directory: a ticker and the company it belongs to.
///
/// <para>Shared by <see cref="Endpoints.DirectoryEndpoints.GetStockListAsync(CancellationToken)"/> and
/// <see cref="Endpoints.DirectoryEndpoints.GetActivelyTradingAsync(CancellationToken)"/>, which return the same
/// pair of values under <b>different key names</b>: <c>stock-list</c> sends
/// <c>{"symbol":…,"companyName":…}</c> and <c>actively-trading-list</c> sends <c>{"symbol":…,"name":…}</c>. That
/// difference is packaging, not meaning — measured on 2026-08-26, all 68,869 symbols the two endpoints share
/// carried a <b>character-identical</b> company name — so the SDK unwraps both into this one type rather than
/// making the caller learn which endpoint spells it which way. The mapping happens in the endpoint class, which is
/// why the two wire shapes themselves stay <see langword="internal"/>.</para></summary>
public sealed record CompanySymbol
{
    /// <summary>Ticker as FMP spells it, trimmed. Never null and never blank: a row with no symbol is dropped
    /// before it reaches here, because a directory entry with no key is not an entry.
    ///
    /// <para>Roughly 60% of these carry an exchange suffix — <c>PMEH.PA</c>, <c>SNL.BO</c>, <c>473050.KQ</c> —
    /// because both directories are global. 42,601 of <c>actively-trading-list</c>'s 68,869 rows and 53,057 of
    /// <c>stock-list</c>'s 91,844 contained a dot on 2026-08-26. A caller who wants US listings must filter;
    /// neither endpoint takes a country or exchange parameter.</para></summary>
    public required string Symbol { get; init; }

    /// <summary>The company name, trimmed, or <see langword="null"/> when the row carried none.
    ///
    /// <para><b>A blank name does not drop the row, unlike a blank symbol.</b> The asymmetry is deliberate: the
    /// symbol is what the entry is <i>for</i>, so a row without one carries nothing, whereas a row with a symbol
    /// and no name still tells you the symbol exists. Dropping it would silently shrink the universe a caller is
    /// using to decide what is listed — the exact failure this SDK exists to prevent. Nothing measured needed the
    /// tolerance: across all 160,713 rows of both directories, zero names were null, empty or padded.</para></summary>
    public string? Name { get; init; }
}
