namespace FmpDotNet;

/// <summary>The reporting cadence asked of the period-shaped endpoints.
///
/// <para>Deliberately not a string. FMP uses two different vocabularies for the same concept — the request takes
/// <c>annual</c>/<c>quarter</c> while the response labels rows <c>FY</c>/<c>Q1</c>-<c>Q4</c> — and an enum keeps a
/// caller from posting a response value back as a request value.</para></summary>
public enum FiscalPeriod
{
    /// <summary>Full fiscal years. Rows come back labelled <c>FY</c>.</summary>
    Annual,

    /// <summary>Fiscal quarters. Rows come back labelled <c>Q1</c> through <c>Q4</c>.</summary>
    Quarter,
}

/// <summary>Conversions for <see cref="FiscalPeriod"/>.</summary>
public static class FiscalPeriodExtensions
{
    /// <summary>The value FMP expects in the <c>period=</c> query parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this FiscalPeriod period) => period switch
    {
        FiscalPeriod.Annual => "annual",
        FiscalPeriod.Quarter => "quarter",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Not a known fiscal period."),
    };
}
