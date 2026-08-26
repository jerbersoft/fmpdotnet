using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One symbol's end-of-day bar from <c>stable/eod-bulk</c>, which returns every symbol for a given date.
///
/// <para>Prices are <see cref="double"/> rather than <see cref="decimal"/> because this endpoint covers the whole
/// universe including crypto pairs, whose quotes arrive in exponent notation (<c>1.8646e-8</c> is a measured
/// value). Callers working only in equities can narrow at their own boundary.</para></summary>
public sealed record BulkEndOfDayPrice
{
    /// <summary>Ticker as FMP spells it.</summary>
    public required string Symbol { get; init; }

    /// <summary>Session date.</summary>
    public LocalDate? Date { get; init; }

    /// <summary>Opening price.</summary>
    public double? Open { get; init; }

    /// <summary>Session low.</summary>
    public double? Low { get; init; }

    /// <summary>Session high.</summary>
    public double? High { get; init; }

    /// <summary>Closing price.</summary>
    public double? Close { get; init; }

    /// <summary>Close adjusted for splits and dividends.</summary>
    public double? AdjustedClose { get; init; }

    /// <summary>Session volume.</summary>
    public long? Volume { get; init; }

    internal static BulkEndOfDayPrice FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        Open = row.GetDouble("open"),
        Low = row.GetDouble("low"),
        High = row.GetDouble("high"),
        Close = row.GetDouble("close"),
        AdjustedClose = row.GetDouble("adjClose"),
        Volume = row.GetInt64("volume"),
    };
}
