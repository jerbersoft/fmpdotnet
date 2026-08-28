using NodaTime;

namespace FmpDotNet;

/// <summary>The one guard against a transposed date range.
///
/// <para><b>Why one and not one per endpoint group.</b> This check was written four separate times — in
/// <c>ChartEndpoints</c>, in <c>CompanyEndpoints</c>, inline in <c>EconomicsEndpoints</c>, and inline again in
/// <c>CalendarEndpoints.GetEarningsCalendarAsync</c> — all four throwing the same exception type naming the same
/// parameter, though the fourth had drifted onto its own wording for the message. Four copies of a rule is well
/// past where the rule starts drifting, and the SEC Filings work would have made five.</para>
///
/// <para><b>Why it is a guard at all.</b> FMP does not report a transposed range; it answers one. Measured
/// 2026-08-27, <c>historical-chart</c> answered a backwards range with 390 well-formed rows dated to the
/// <c>to</c> day — plausible data for the wrong end of the range — while the daily endpoints and the economic
/// calendar answered <c>[]</c> with HTTP 200, which reads as "nothing happened that week". Both cost a call from
/// the key's quota to say something untrue. Rejecting before the request is the only place the endpoints can be
/// made to behave alike.</para>
///
/// <para>Nullable on both ends so one helper serves the optional ranges and the required ones alike: one end
/// alone cannot be backwards, so the guard fires only when both are supplied.</para></summary>
internal static class DateRange
{
    /// <summary>Throws when <paramref name="to"/> is earlier than <paramref name="from"/> and both are
    /// supplied.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The range runs backwards.</exception>
    internal static void ThrowIfBackwards(LocalDate? from, LocalDate? to)
    {
        if (from is { } start && to is { } end && end < start)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"'to' must not be earlier than 'from' ({start:uuuu-MM-dd}).");
    }
}
