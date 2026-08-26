using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One position held by one ETF. From <c>stable/etf-holder-bulk</c>, which is paged by <c>part</c>.
///
/// <para><b>This is by a wide margin the largest response the SDK models.</b> Measured 2026-08-26, <c>part=0</c>
/// alone answered <b>298,693,192 bytes across 2,571,137 rows</b> — more than four times <c>ratios-ttm-bulk</c>,
/// which the milestone had called the largest on the API. It covers 4,610 distinct ETFs, with a median of 79
/// holdings each, a 90th percentile of 946, and a maximum of 33,070 for the funds-of-funds
/// (<c>AOA</c>, <c>AOK</c>, <c>AOM</c>, <c>AOR</c>). Stream it; nothing else is viable.</para>
///
/// <para><b><see cref="Symbol"/> is the ETF, <see cref="Asset"/> is what it holds</b> — the opposite way round
/// from every other bulk endpoint, where the symbol is the subject of the row. And <see cref="Asset"/> is blank
/// on roughly three quarters of rows (292,552 of a 400,000-row sample), because cash, bonds and unlisted
/// positions have no ticker; <see cref="Name"/> carries "Other/Cash" and the like for those.</para>
///
/// <para><b>26 rows carry the ETF symbol as the literal string <c>" -- "</c></b>, spaces included. It is a
/// placeholder for an unidentified fund, not a ticker, and it is preserved as sent rather than nulled so that a
/// caller grouping by ETF can see it for what it is.</para></summary>
public sealed record BulkEtfHolding
{
    /// <summary>The ETF holding the position — <b>not</b> the security held. See <see cref="Asset"/>.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>Name of the security held. Populated even where <see cref="Asset"/> is blank, carrying values
    /// such as <c>Other/Cash</c>.</summary>
    public string? Name { get; init; }

    /// <summary>Shares held. Zero for cash-like positions.</summary>
    public decimal? SharesNumber { get; init; }

    /// <summary>Ticker of the security held. Blank on about three quarters of rows.</summary>
    public string? Asset { get; init; }

    /// <summary>The position's weight in the fund, as a <b>percentage</b> rather than a fraction — 11.24 means
    /// 11.24%, measured against rows whose weights sum toward 100.</summary>
    public decimal? WeightPercentage { get; init; }

    /// <summary>CUSIP of the security held, where it has one.</summary>
    public string? Cusip { get; init; }

    /// <summary>ISIN of the security held, where it has one.</summary>
    public string? Isin { get; init; }

    /// <summary>Market value of the position.</summary>
    public decimal? MarketValue { get; init; }

    /// <summary>When FMP last refreshed the holding.</summary>
    public LocalDate? LastUpdated { get; init; }

    internal static BulkEtfHolding FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Name = row.GetString("name"),
        SharesNumber = row.GetDecimal("sharesNumber"),
        Asset = row.GetString("asset"),
        WeightPercentage = row.GetDecimal("weightPercentage"),
        Cusip = row.GetString("cusip"),
        Isin = row.GetString("isin"),
        MarketValue = row.GetDecimal("marketValue"),
        LastUpdated = row.GetDate("lastUpdated"),
    };
}
