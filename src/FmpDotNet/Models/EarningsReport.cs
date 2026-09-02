using System.Text.Json.Serialization;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Models;

/// <summary>One earnings date for one symbol — reported or still scheduled. From <c>stable/earnings</c>.
///
/// <para><b>The head of the list is normally an event that has not happened yet.</b> Measured against the live API
/// on 2026-08-26: AAPL's newest row was <c>2026-10-29</c> with <see cref="EpsActual"/> and
/// <see cref="RevenueActual"/> both <see langword="null"/> while <see cref="EpsEstimated"/> (<c>1.98</c>) and
/// <see cref="RevenueEstimated"/> (<c>113205200000</c>) were populated. Rows come back newest first, so a caller
/// asking for "the last N earnings" is handed an unreported one at position 0 and, if it averages the actuals
/// without checking, quietly averages N-1 of them. Test <see cref="EpsActual"/> for null rather than comparing
/// <see cref="Date"/> against today: a date in the past with null actuals means FMP has not ingested the report
/// yet, which is a different thing from "no report was due".</para>
///
/// <para>Exactly seven fields on every row, none missing and none extra — measured across all 165 rows AAPL
/// returns. The same seven are the whole of an unflagged <c>stable/earnings-calendar</c> row, which is why
/// <see cref="EarningsCalendarEntry"/> is this record plus five nullable extras rather than a different shape.</para>
///
/// <para><b>Now this record carries the same five extras, and the two shapes coincide (#51).</b> Measured
/// 2026-09-02, <c>includeReportTimes=true</c> on <c>stable/earnings</c> adds exactly <c>time</c>,
/// <c>periodEnding</c>, <c>fiscalPeriod</c>, <c>fiscalYear</c> and <c>confirmed</c> to every one of AAPL's 165
/// rows, with the seven shared fields byte-equal per row and — unlike the calendar path — <b>no row re-dated and
/// none reordered</b>. They are still two types. The calendar's rows shift a day under the flag and this path's
/// do not; its measurements come from a 48-row global sample and these from one issuer's forty years; and a
/// merge would rename the calendar's public type to buy nothing a caller can observe. The five properties below
/// are <see langword="null"/> unless the flag was sent, which means "not asked for" and not "not known" — the
/// same reading <see cref="EarningsCalendarEntry"/> asks for.</para>
///
/// <para>Money and per-share figures are <see langword="decimal"/> and never <see langword="double"/>: revenue
/// reaches 2.3e12 in the measured captures and EPS is signed — <c>-0.17</c> on <c>0AAW.L</c> and <c>-0.15</c> on
/// <c>SCCB</c> — so nothing here may be assumed positive.</para></summary>
public sealed record EarningsReport
{
    /// <summary>Ticker as FMP spells it. Class-share tickers use FMP's hyphenated form (<c>BRK-B</c>, not
    /// <c>BRK.B</c>), and the dotted spelling answers an empty array rather than an error.</summary>
    [JsonPropertyName("symbol")] public string? Symbol { get; init; }

    /// <summary>The earnings date, as a plain calendar date — there is no time of day on this endpoint. The
    /// session marker is <see cref="ReportTime"/>, and only with the flag.
    ///
    /// <para>Nullable because the converter reads an unparseable date as null rather than throwing away the rest of
    /// the response, but it is never null on a row the SDK hands back: rows with no usable date are dropped, since
    /// the date is half of this row's identity. See
    /// <see cref="Endpoints.CalendarEndpoints.GetEarningsAsync(string, int?, bool, CancellationToken)"/>.</para></summary>
    [JsonPropertyName("date")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? Date { get; init; }

    /// <summary>Reported EPS, or <see langword="null"/> when the report is still in the future or not yet ingested.
    /// Negative values are ordinary — <c>-0.17</c> measured — so this must not be treated as unsigned.</summary>
    [JsonPropertyName("epsActual")] public decimal? EpsActual { get; init; }

    /// <summary>Consensus EPS estimate for this date. Populated on future rows, which is what makes a null
    /// <see cref="EpsActual"/> alongside a populated estimate the signal that a row is a forecast.</summary>
    [JsonPropertyName("epsEstimated")] public decimal? EpsEstimated { get; init; }

    /// <summary>Reported revenue in the company's reporting currency, or <see langword="null"/> on an unreported
    /// row. The endpoint sends no currency field, so the unit comes from the profile rather than from here.</summary>
    [JsonPropertyName("revenueActual")] public decimal? RevenueActual { get; init; }

    /// <summary>Consensus revenue estimate for this date.</summary>
    [JsonPropertyName("revenueEstimated")] public decimal? RevenueEstimated { get; init; }

    /// <summary>When FMP last refreshed this row — a plain <c>yyyy-MM-dd</c> date, not a timestamp, unlike the
    /// <c>date</c> field on <c>stable/shares-float</c> which carries a UTC time of day.
    ///
    /// <para>Non-null on all 165 measured rows and modelled nullable anyway. It moves independently of
    /// <see cref="Date"/>: on 2026-08-26 the two newest AAPL rows both read <c>2026-08-26</c> here while older ones
    /// read <c>2026-06-04</c>, so it tracks FMP's ingestion rather than the company's announcement.</para></summary>
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? LastUpdated { get; init; }

    // ---- Only populated when includeReportTimes=true. Null otherwise, meaning "not asked for". ----

    /// <summary>Session marker for the announcement — <c>"amc"</c> after market close, <c>"bmo"</c> before the
    /// open — or <see langword="null"/>.
    ///
    /// <para>Named and typed as <see cref="EarningsCalendarEntry.ReportTime"/> is, for the reason given there:
    /// the wire name is <c>time</c> and there is no clock time in it. Measured 2026-09-02 across AAPL's 165
    /// flagged rows: <c>amc</c> on 58, <see langword="null"/> on 107, and never <c>bmo</c>. The newest null is
    /// 2022-10-27 and the nulls are interspersed with <c>amc</c> rows from there back to 1997 — not a clean cut
    /// at some year. So a null under the flag means FMP has no marker for that announcement, and a null without
    /// the flag means nothing at all. Kept verbatim; fold case at the call site if matching on it.</para></summary>
    [JsonPropertyName("time")] public string? ReportTime { get; init; }

    /// <summary>Last day of the fiscal period the announcement reports on — <c>2026-09-27</c> for the
    /// <c>2026-10-29</c> announcement, a plain <c>yyyy-MM-dd</c> date. Non-null on all 165 flagged AAPL rows,
    /// measured 2026-09-02, and the property to key a quarter on: <see cref="Date"/> is when the company
    /// spoke, this is what it spoke about.</summary>
    [JsonPropertyName("periodEnding")]
    [JsonConverter(typeof(NullableLocalDateJsonConverter))]
    public LocalDate? PeriodEnding { get; init; }

    /// <summary>Fiscal period label as FMP writes it — <c>Q1</c> to <c>Q4</c>, measured 2026-09-02 as 41, 41,
    /// 41 and 42 rows respectively across AAPL's 165. A raw string rather than the
    /// <see cref="FmpDotNet.FiscalPeriod"/> enum, which is the request vocabulary; and relative to the company's
    /// own fiscal calendar, so Apple's <c>Q4</c> ends in September.</summary>
    [JsonPropertyName("fiscalPeriod")] public string? FiscalPeriod { get; init; }

    /// <summary>Fiscal year the period belongs to — <c>2026</c> for a period ending <c>2026-09-27</c>. Sent
    /// unquoted, non-null on all 165 flagged AAPL rows measured 2026-09-02.</summary>
    [JsonPropertyName("fiscalYear")] public int? FiscalYear { get; init; }

    /// <summary>Whether FMP considers the date confirmed by the company rather than estimated. Measured
    /// 2026-09-02: <see langword="true"/> on 15 of AAPL's 165 flagged rows — the eleven newest, two from 2023,
    /// and two isolated rows from 2006 and 1997 — and <see langword="false"/> on the other 150. A future date
    /// that reads <see langword="false"/> is FMP's estimate of when the company will report, and it
    /// moves.</summary>
    [JsonPropertyName("confirmed")] public bool? Confirmed { get; init; }
}
