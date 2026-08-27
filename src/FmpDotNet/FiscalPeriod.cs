namespace FmpDotNet;

/// <summary>The reporting cadence asked of the period-shaped endpoints.
///
/// <para><b>Six values, not two.</b> Beyond <c>annual</c> and <c>quarter</c>, FMP accepts each fiscal quarter as
/// a filter ACROSS years, which is a different question from "give me quarters". Measured on AAPL 2026-08-27:</para>
///
/// <code>
/// period=annual   ->  FY2025, FY2024, FY2023, FY2022 …
/// period=quarter  ->  Q32026, Q22026, Q12026, Q42025 …
/// period=Q1       ->  Q12026, Q12025, Q12024, Q12023 …
/// </code>
///
/// <para><b>Deliberately not a string, and the reason has changed.</b> An earlier version of this type said the
/// enum stopped a caller posting a response value back as a request value — FMP labels rows <c>FY</c>/<c>Q1</c>
/// while the request took <c>annual</c>/<c>quarter</c>. That is no longer true: <c>Q1</c> is legal in both
/// directions and <c>FY</c> is accepted as a synonym for <c>annual</c>. What the enum earns now is different and
/// still worth having — it makes all six legal values discoverable, and it rejects everything else. Measured
/// 2026-08-27, <b>an unrecognised period silently falls back to annual</b> on the statement paths:
/// <c>period=bogus</c> answers FY rows at HTTP 200 with no warning, so a typo costs you the wrong series and
/// nothing says so. On the two report-document paths the same typo is an HTTP 400 instead. One query parameter,
/// two behaviours, neither documented.</para>
///
/// <para><b>The order of these members is load-bearing.</b> <see cref="Annual"/> and <see cref="Quarter"/> keep
/// ordinals 0 and 1; the quarters were appended. A caller who persisted the underlying integer keeps reading the
/// value they stored.</para></summary>
public enum FiscalPeriod
{
    /// <summary>Full fiscal years. Rows come back labelled <c>FY</c>.</summary>
    Annual,

    /// <summary>Fiscal quarters, most recent first, across every quarter. Rows come back labelled <c>Q1</c>
    /// through <c>Q4</c>.
    ///
    /// <para><b>Not accepted on the two report-document paths</b> —
    /// <see cref="Endpoints.StatementEndpoints.GetFinancialReportAsync"/> and
    /// <see cref="Endpoints.StatementEndpoints.GetFinancialReportWorkbookAsync"/> reject it. A filed report is one
    /// fiscal period, and "the 2025 quarterly report" is not a document that exists. See those methods for what
    /// FMP does instead when you ask.</para></summary>
    Quarter,

    /// <summary>First fiscal quarter of each year, across years — Q1 2026, Q1 2025, Q1 2024 …</summary>
    Q1,

    /// <summary>Second fiscal quarter of each year, across years.</summary>
    Q2,

    /// <summary>Third fiscal quarter of each year, across years.</summary>
    Q3,

    /// <summary>Fourth fiscal quarter of each year, across years. <b>Not the same series as
    /// <see cref="Annual"/></b> even where the period ends on the same day: measured on AAPL 2026-08-27, the Q4
    /// end and the fiscal year end are both 2025-09-27, and the two series carry different figures.</summary>
    Q4,
}

/// <summary>Conversions for <see cref="FiscalPeriod"/>.</summary>
public static class FiscalPeriodExtensions
{
    /// <summary>The value FMP expects in the <c>period=</c> query parameter.
    ///
    /// <para>Throws on an undeclared member rather than emitting something plausible. That is not defensive
    /// tidiness: an unrecognised <c>period</c> is silently reinterpreted as annual by the statement paths, so a
    /// value that escaped this method would return a well-formed answer to a question nobody asked.</para></summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
    public static string ToQueryValue(this FiscalPeriod period) => period switch
    {
        FiscalPeriod.Annual => "annual",
        FiscalPeriod.Quarter => "quarter",
        FiscalPeriod.Q1 => "Q1",
        FiscalPeriod.Q2 => "Q2",
        FiscalPeriod.Q3 => "Q3",
        FiscalPeriod.Q4 => "Q4",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Not a known fiscal period."),
    };
}
