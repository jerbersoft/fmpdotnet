using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's macroeconomic surface — the calendar of scheduled and published economic releases.
///
/// <para>Unlike the company endpoints, nothing here is keyed on a symbol. The calendar is <b>global</b>: a request
/// is a date range and the answer is every release in it, for every country FMP tracks. Narrowing that is the
/// caller's job, not the SDK's — see <see cref="EconomicRelease"/>.</para></summary>
public sealed class EconomicsEndpoints(FmpTransport transport)
{
    /// <summary>Every economic release FMP has scheduled or published between <paramref name="from"/> and
    /// <paramref name="to"/>, both ends inclusive, for every country.
    ///
    /// <para>Measured on 2026-08-26: <c>from=2026-08-25&amp;to=2026-09-01</c> answered 713 rows spanning 81
    /// countries, and the single day 2026-08-26 answered 78 across 19. Rows came back <b>newest first</b> in both,
    /// and both range ends were included — <c>from</c> returned rows dated 2026-08-25 and <c>to</c> returned rows
    /// dated 2026-09-01. There is no paging parameter, no country parameter and no impact parameter; a range is
    /// the entire query surface.</para>
    ///
    /// <para><b>Wide windows are silently truncated, and the SDK cannot page around it.</b> This is the one thing
    /// on this endpoint that will cost a caller data without telling them. Measured against the live API:</para>
    /// <list type="table">
    ///   <listheader><term>range</term><description>rows returned</description></listheader>
    ///   <item><term>2026-08-01 … 2026-08-31 (1 month)</term><description>1855</description></item>
    ///   <item><term>2026-08-01 … 2026-10-31 (3 months)</term><description>4051</description></item>
    ///   <item><term>2026-08-01 … 2027-01-31 (6 months)</term><description><b>535</b> — fewer than the three-month
    ///     window it wholly contains</description></item>
    ///   <item><term>−3 months … +12 months</term><description><b>0</b></description></item>
    /// </list>
    /// <para>A six-month window returning less than a quarter of the three-month window inside it is not sparse
    /// data, it is truncation, and the widest window collapses to nothing at all. FMP reports none of this: the
    /// status is 200 and the array is well-formed. So <b>chunking is the caller's responsibility</b> — 30-day
    /// chunks are what the consumer application this SDK replaces uses, and the widest range verified intact here
    /// is one week.</para>
    ///
    /// <para><b>Do not guard this with a row count.</b> It is the obvious instinct and it is wrong on this
    /// endpoint specifically, because macro density varies enormously and legitimately across the calendar: the
    /// week of 2026-08-25 carries 713 rows while <c>from=2027-01-25&amp;to=2027-02-01</c> carries a complete and
    /// entirely correct <b>2</b>. Set a count threshold and it rejects that genuinely quiet week while happily
    /// accepting the truncated 535-row half-year. The honest test is <b>edge coverage</b> — did the returned rows
    /// actually reach both ends of the range you asked for? A caller has everything needed for it: the range they
    /// passed, and <see cref="EconomicRelease.Timestamp"/> on every row. If the earliest row is well inside
    /// <paramref name="from"/> or the latest well inside <paramref name="to"/>, narrow the window and ask again.
    /// (Note that "well inside" has to allow for real quiet days at the edges, and that the timestamps are UTC
    /// while the range bounds are calendar dates.) This is a different failure from
    /// <c>stable/earnings-calendar</c>, which truncates at a hard, testable 4000 rows; here there is no constant
    /// to compare against, which is exactly why the check has to be positional.</para>
    ///
    /// <para>Results are returned exactly as FMP sends them: unsorted, unfiltered, and not clamped to the
    /// requested range.</para></summary>
    /// <param name="from">First calendar day of the range, inclusive.</param>
    /// <param name="to">Last calendar day of the range, inclusive. Must not be earlier than
    /// <paramref name="from"/>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The releases in the range, newest first as measured, or an empty list — never null.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>. Checked before the request is sent: FMP answers a backwards range with an empty
    /// array and HTTP 200, so an argument the caller has plainly got wrong would otherwise read as "no releases
    /// that week" and cost a call from the key's quota to say nothing.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<EconomicRelease>> GetEconomicCalendarAsync(
        LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentOutOfRangeException(
                nameof(to), to, $"'to' must not be earlier than 'from' ({from:uuuu-MM-dd}).");

        return await transport.GetListAsync(
            new FmpRequest("stable/economic-calendar").With("from", from).With("to", to),
            FmpJsonContext.Default.ListEconomicRelease, ct).ConfigureAwait(false);
    }
}
