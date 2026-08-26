using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>FMP's own letter rating for one company, with the six component scores it is built from. From
/// <c>stable/rating-bulk</c> — 45,008 rows and 1.8 MB measured 2026-08-26.
///
/// <para><b><see cref="Rating"/> is left as the upstream string, and the scale is not the one you would guess.</b>
/// Measured across all 45,008 rows the values were, in order of frequency: C, B+, C+, B, A-, B-, C-, D+, A, A+,
/// <b>S-</b> (363) and <b>S</b> (26). Two grades sit ABOVE A+, which no A-to-F enum would have had a member for —
/// and D- and F never appeared at all, so a scale inferred from this snapshot would be wrong at both ends.</para>
///
/// <para>The six component scores are small integers. They are FMP's own scoring of each factor, not the
/// underlying ratios.</para></summary>
public sealed record BulkCompanyRating
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The date FMP computed the rating.</summary>
    public LocalDate? Date { get; init; }

    /// <summary>The letter grade. See the type's remarks: the observed scale runs to <c>S</c>, above <c>A+</c>.</summary>
    public string? Rating { get; init; }

    /// <summary>Score for the discounted-cash-flow factor.</summary>
    public int? DiscountedCashFlowScore { get; init; }

    /// <summary>Score for the return-on-equity factor.</summary>
    public int? ReturnOnEquityScore { get; init; }

    /// <summary>Score for the return-on-assets factor.</summary>
    public int? ReturnOnAssetsScore { get; init; }

    /// <summary>Score for the debt-to-equity factor.</summary>
    public int? DebtToEquityScore { get; init; }

    /// <summary>Score for the price-to-earnings factor.</summary>
    public int? PriceToEarningsScore { get; init; }

    /// <summary>Score for the price-to-book factor.</summary>
    public int? PriceToBookScore { get; init; }

    internal static BulkCompanyRating FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Date = row.GetDate("date"),
        Rating = row.GetString("rating"),
        DiscountedCashFlowScore = row.GetInt32("discountedCashFlowScore"),
        ReturnOnEquityScore = row.GetInt32("returnOnEquityScore"),
        ReturnOnAssetsScore = row.GetInt32("returnOnAssetsScore"),
        DebtToEquityScore = row.GetInt32("debtToEquityScore"),
        PriceToEarningsScore = row.GetInt32("priceToEarningsScore"),
        PriceToBookScore = row.GetInt32("priceToBookScore"),
    };
}
