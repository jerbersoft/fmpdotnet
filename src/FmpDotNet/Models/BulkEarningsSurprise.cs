using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One reported earnings figure against its estimate, for every symbol that reported in a given year.
/// From <c>stable/earnings-surprises-bulk</c>, which takes a <c>year</c>.
///
/// <para>Measured 2026-08-26 for <c>year=2025</c>: 65,945 rows, 3.1 MB, five columns, no blank field anywhere.
/// Dates spanned 2025-01-01 to 2025-12-31 across 336 distinct days.</para>
///
/// <para><b><see cref="Symbol"/> and <see cref="Date"/> together are NOT a unique key.</b> Measured, 210 pairs
/// appeared more than once in a single year's response. Anything storing these rows under a unique index on
/// (symbol, date) will collide on real data — and a symbol can legitimately carry five rows in one calendar year,
/// because fiscal quarters straddle the year boundary (<c>AMD.NE</c> reported on 2025-02-04, 04-28, 07-28, 10-27
/// and 12-31).</para>
///
/// <para><b>The figures are <see langword="decimal"/>, not double.</b> Unlike <c>eod-bulk</c>, where crypto rows
/// force exponent notation, no value in the measured response used it — and the values run to four decimal places
/// on sub-cent results (<c>-0.0031</c>), which decimal holds exactly.</para></summary>
public sealed record BulkEarningsSurprise
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The date the figure was reported for. Not unique with <see cref="Symbol"/> — see the type's
    /// remarks.</summary>
    public LocalDate? Date { get; init; }

    /// <summary>Earnings per share actually reported. Negative on a loss.</summary>
    public decimal? EpsActual { get; init; }

    /// <summary>The consensus estimate this result is measured against. Subtract to get the surprise; FMP does
    /// not send one.</summary>
    public decimal? EpsEstimated { get; init; }

    /// <summary>When FMP last revised this row.</summary>
    public LocalDate? LastUpdated { get; init; }

    internal static BulkEarningsSurprise FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        EpsActual = row.GetDecimal("epsActual"),
        EpsEstimated = row.GetDecimal("epsEstimated"),
        LastUpdated = row.GetDate("lastUpdated"),
    };
}
