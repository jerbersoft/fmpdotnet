namespace FmpDotNet;

/// <summary>The reporting period asked of the period-shaped <c>*-bulk</c> endpoints.
///
/// <para><b>Deliberately separate from <see cref="FiscalPeriod"/>, which the per-symbol endpoints use.</b> They
/// are not the same vocabulary and not the same question: a per-symbol call asks for a cadence and gets a series
/// back, while a bulk call names ONE period and gets every company's row for it. Sharing the enum would offer a
/// caller <c>Quarter</c> here, where it does not mean anything on its own.</para>
///
/// <para><b>Measured 2026-08-26 against <c>cash-flow-statement-bulk</c> for 2025</b>, by response size, which is
/// how the aliases below were found:</para>
/// <list type="bullet">
/// <item><description><c>annual</c> and <c>FY</c> returned byte-identical responses (18,727,772 bytes).</description></item>
/// <item><description><c>Q1</c> returned 12,525,406 bytes and <c>Q4</c> returned 15,516,686 — distinct periods,
/// as expected.</description></item>
/// <item><description><b><c>quarter</c> returned 12,525,406 bytes — exactly <c>Q1</c>.</b> It is an alias for the
/// first quarter, NOT "all quarters". That is the trap this enum exists to close: a caller carrying the
/// per-symbol vocabulary across would write <c>quarter</c>, get a valid 200 with twelve megabytes of real data,
/// and silently be reading Q1 alone.</description></item>
/// <item><description>An unrecognised value answers HTTP 400 with
/// <c>Query Error: Invalid or missing query parameter - period</c>.</description></item>
/// </list></summary>
public enum BulkFiscalPeriod
{
    /// <summary>The full fiscal year. Sent as <c>annual</c>; FMP also accepts <c>FY</c> for the same data.</summary>
    Annual,

    /// <summary>First fiscal quarter. FMP also accepts <c>quarter</c> for this, which is why that spelling is not
    /// offered here.</summary>
    Q1,

    /// <summary>Second fiscal quarter.</summary>
    Q2,

    /// <summary>Third fiscal quarter.</summary>
    Q3,

    /// <summary>Fourth fiscal quarter.</summary>
    Q4,
}

/// <summary>Conversions for <see cref="BulkFiscalPeriod"/>.</summary>
public static class BulkFiscalPeriodExtensions
{
    /// <summary>The value FMP expects in the <c>period=</c> query parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this BulkFiscalPeriod period) => period switch
    {
        BulkFiscalPeriod.Annual => "annual",
        BulkFiscalPeriod.Q1 => "Q1",
        BulkFiscalPeriod.Q2 => "Q2",
        BulkFiscalPeriod.Q3 => "Q3",
        BulkFiscalPeriod.Q4 => "Q4",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Not a known bulk fiscal period."),
    };
}
