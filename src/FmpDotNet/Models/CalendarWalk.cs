namespace FmpDotNet.Models;

/// <summary>What one calendar call cost and what it turned up, gathered while the pages were still separate.
///
/// <para>Three of these four numbers cannot be recovered from the concatenated rows, which is why they travel
/// as a group rather than being recomputed later. <see cref="LastPageRowCount"/> is gone the moment the pages
/// are joined; <see cref="SeamDuplicateRows"/> is defined between two pages and has no meaning inside one; and
/// <see cref="PagesFetched"/> is a count of pages the rows themselves never carried.</para>
///
/// <para>Internal because it is plumbing between <see cref="Endpoints.CalendarEndpoints"/> and the two result
/// types. Its numbers reach callers as properties on <see cref="CalendarResult{T}"/> and
/// <see cref="EarningsCalendarResult"/>.</para></summary>
/// <param name="RowsReturned">Rows across every page kept, counted raw — before undated rows are dropped
/// and before any clamp.</param>
/// <param name="PagesFetched">Pages of rows kept. 1 on a path that does not page, never 0. A walk can spend
/// one request more than this: a page that merely repeats its predecessor is fetched, recognised and discarded
/// without being counted.</param>
/// <param name="LastPageRowCount">Rows on the last page appended. Compared against the path's cap, this is
/// what says whether the walk stopped because it ran out of data or because it ran out of patience.</param>
/// <param name="SeamDuplicateRows">Rows present on both sides of a page seam, summed over seams. Measured
/// 2026-09-01 to equal the number of rows the walk never saw — see
/// <see cref="CalendarResult{T}.SeamDuplicateRows"/>.</param>
internal readonly record struct CalendarWalk(
    int RowsReturned,
    int PagesFetched,
    int LastPageRowCount,
    int SeamDuplicateRows)
{
    /// <summary>The evidence for a response that was not paged: one request, one page, no seam to be unstable.
    /// Used by the four calendar paths that have no working cursor.</summary>
    internal static CalendarWalk Single(int rowsReturned) => new(rowsReturned, 1, rowsReturned, 0);
}
