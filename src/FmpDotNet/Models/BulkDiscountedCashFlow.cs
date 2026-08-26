using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's discounted-cash-flow valuation for one company beside its market price. From
/// <c>stable/dcf-bulk</c> — 33,583 rows and 1.6 MB measured 2026-08-26.
///
/// <para><b>The price column is literally named <c>Stock Price</c></b> — capitalised, with a space, unlike every
/// other column on every other endpoint the SDK models. It is mapped to <see cref="StockPrice"/>, but anything
/// reading this CSV by hand has to spell it FMP's way.</para>
///
/// <para><b><see cref="Dcf"/> is regularly negative and regularly missing.</b> Measured: 1,664 of the 33,583 rows
/// carried no value at all, and a negative valuation is ordinary rather than exceptional (<c>000008.SZ</c> came
/// back at -6.21 against a price of 2.50). <see cref="StockPrice"/> was populated on every row. Any ratio of the
/// two therefore needs a null check on the numerator and tolerates a negative result.</para></summary>
public sealed record BulkDiscountedCashFlow
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The date FMP computed the valuation.</summary>
    public LocalDate? Date { get; init; }

    /// <summary>The discounted-cash-flow value per share. Often negative, and absent on about 5% of rows.</summary>
    public decimal? Dcf { get; init; }

    /// <summary>The market price the valuation is to be compared against.</summary>
    /// <remarks>FMP spells the column <c>Stock Price</c> — capitalised, with a space.</remarks>
    public decimal? StockPrice { get; init; }

    internal static BulkDiscountedCashFlow FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        Dcf = row.GetDecimal("dcf"),
        StockPrice = row.GetDecimal("Stock Price"),
    };
}
