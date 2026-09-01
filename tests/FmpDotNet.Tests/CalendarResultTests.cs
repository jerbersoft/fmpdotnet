using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Tests;

/// <summary>The generic truncation signal, exercised on its own.
///
/// <para><c>T</c> is <see cref="string"/> throughout, deliberately. This class does arithmetic on four values
/// the endpoint hands it — a raw row count, the two requested bounds and the earliest date anywhere in the raw
/// response — and none of that arithmetic touches the row type. Using a real model here would only invite a
/// reader to think the type matters.</para>
///
/// <para>The three paths that return one, and what each was measured to do on 2026-08-28:</para>
/// <list type="bullet">
/// <item><description><c>dividends-calendar</c> — a 4000-row cap. A full year answers its last three days.</description></item>
/// <item><description><c>splits-calendar</c> and <c>ipos-calendar</c> — a 90-day window measured from
/// <c>to</c>. A full year answers Q4, at 737 and 358 rows: nowhere near any cap, so a row count cannot see
/// it.</description></item>
/// </list></summary>
public class CalendarResultTests
{
    private static LocalDate Day(int y, int m, int d) => new(y, m, d);

    private static CalendarResult<string> Result(
        int rowsReturned, LocalDate from, LocalDate to, LocalDate? earliest,
        int? rowCap = null, int? lookback = null, IReadOnlyList<string>? rows = null) =>
        new(rows ?? [], CalendarWalk.Single(rowsReturned), from, to, earliest, rowCap, lookback);

    // ---- it is a list first -------------------------------------------------------------------------------

    [Fact]
    public void It_is_the_list_it_was_given()
    {
        var result = Result(3, Day(2026, 1, 1), Day(2026, 1, 31), Day(2026, 1, 1),
                            rows: ["a", "b", "c"]);

        Assert.Equal(3, result.Count);
        Assert.Equal("b", result[1]);
        Assert.Equal(["a", "b", "c"], result);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(result);
    }

    [Fact]
    public void Count_is_what_the_caller_holds_and_RowsReturned_is_what_FMP_sent()
    {
        // The distinction the whole type exists for: a row dropped by the SDK must not be able to move the
        // signal. Two rows kept out of five returned.
        var result = Result(5, Day(2026, 1, 1), Day(2026, 1, 31), Day(2026, 1, 1), rows: ["a", "b"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result.RowsReturned);
    }

    // ---- AtRowCap: the dividends-calendar mechanism -------------------------------------------------------

    [Theory]
    [InlineData(3999, false)]
    [InlineData(4000, true)]
    [InlineData(4001, true)]
    public void AtRowCap_fires_at_and_above_the_cap(int rowsReturned, bool expected)
    {
        var result = Result(rowsReturned, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 1, 1), rowCap: 4000);

        Assert.Equal(expected, result.AtRowCap);
    }

    [Fact]
    public void AtRowCap_never_fires_where_no_cap_was_measured()
    {
        // splits-calendar and ipos-calendar pass rowCap: null, because no row cap was measured on them and an
        // invented one would be a fact nobody checked. 100000 rows must still read false here.
        var result = Result(100_000, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 1, 1), rowCap: null);

        Assert.False(result.AtRowCap);
    }

    // ---- ExceedsLookbackLimit: the splits/ipos mechanism --------------------------------------------------

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, false)]
    [InlineData(91, true)]
    [InlineData(364, true)]
    public void ExceedsLookbackLimit_fires_on_a_range_wider_than_the_window(int spanDays, bool expected)
    {
        var from = Day(2026, 1, 1);
        var result = Result(500, from, from.PlusDays(spanDays), from, lookback: 90);

        Assert.Equal(expected, result.ExceedsLookbackLimit);
    }

    [Fact]
    public void ExceedsLookbackLimit_never_fires_where_no_window_was_measured()
    {
        // dividends-calendar passes lookback: null. Its row cap always fires first, so no window limit is
        // observable on it, and asserting one would be inventing evidence.
        var result = Result(4000, Day(2020, 1, 1), Day(2026, 12, 31), Day(2026, 12, 29), lookback: null);

        Assert.False(result.ExceedsLookbackLimit);
    }

    // ---- MissesStartOfRange: the only tell that sees both mechanisms --------------------------------------

    [Fact]
    public void MissesStartOfRange_fires_when_the_earliest_row_is_later_than_the_requested_from()
    {
        // The measured splits-calendar case: from=2024-01-01 to=2024-12-31 answered 737 rows whose earliest
        // date was 2024-10-02. Nine months absent, at a row count nowhere near any cap.
        var result = Result(737, Day(2024, 1, 1), Day(2024, 12, 31), Day(2024, 10, 2), rowCap: null, lookback: 90);

        Assert.True(result.MissesStartOfRange);
        Assert.False(result.AtRowCap);          // 737 is not near a cap, and there is no cap here anyway
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void MissesStartOfRange_is_the_tell_that_catches_the_ninety_day_boundary()
    {
        // Measured 2026-08-28: from = to - 90 days is one day short — the response's earliest row was
        // 2026-05-31 against a requested 2026-05-30 — while from = to - 88 was honoured exactly. A span of
        // exactly 90 therefore does NOT trip ExceedsLookbackLimit, and this tell is what covers it.
        var result = Result(947, Day(2026, 5, 30), Day(2026, 8, 28), Day(2026, 5, 31), lookback: 90);

        Assert.False(result.ExceedsLookbackLimit);
        Assert.True(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void MissesStartOfRange_does_not_fire_when_the_range_starts_where_it_was_asked_to()
    {
        var result = Result(946, Day(2026, 6, 1), Day(2026, 8, 28), Day(2026, 6, 1), lookback: 90);

        Assert.False(result.MissesStartOfRange);
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public void An_empty_response_carries_no_earliest_date_and_reads_as_untruncated()
    {
        // Known and accepted: nothing came back at all, so there is no evidence of truncation and none of
        // completeness either. False is the answer that does not invent a signal.
        var result = Result(0, Day(2026, 1, 1), Day(2026, 1, 31), earliest: null, rowCap: 4000, lookback: null);

        Assert.Null(result.EarliestReturnedDate);
        Assert.False(result.MissesStartOfRange);
        Assert.False(result.LikelyTruncated);
    }

    // ---- the static helper --------------------------------------------------------------------------------

    [Fact]
    public void The_static_helper_reads_a_result_this_sdk_produced()
    {
        IReadOnlyList<string> rows = Result(4000, Day(2026, 1, 1), Day(2026, 12, 31), Day(2026, 12, 29),
                                            rowCap: 4000);

        Assert.True(CalendarResult<string>.IsLikelyTruncated(rows));
    }

    [Fact]
    public void The_static_helper_answers_false_for_a_list_it_did_not_produce()
    {
        // Documented rather than hidden. EarningsCalendarResult can fall back on `Count >= 4000` because it has
        // one known const cap; this type's cap is per-instance and null on two of its three paths, so there is
        // no honest fallback. A concatenation of chunks has thrown away the per-response evidence, and false
        // here means "no evidence", not "complete".
        IReadOnlyList<string> plain = new string[10_000];

        Assert.False(CalendarResult<string>.IsLikelyTruncated(plain));
    }

    [Fact]
    public void The_static_helper_refuses_null()
    {
        Assert.Throws<ArgumentNullException>(() => CalendarResult<string>.IsLikelyTruncated(null!));
    }

    // ---- the walk evidence (#49) --------------------------------------------------------------------------

    [Fact]
    public void A_one_page_result_reports_one_page_and_no_seam()
    {
        var result = new CalendarResult<string>(
            ["a", "b"], CalendarWalk.Single(2), Day(2026, 1, 1), Day(2026, 1, 31), Day(2026, 1, 1),
            rowCap: 4000, lookbackLimitDays: null);

        Assert.Equal(1, result.PagesFetched);
        Assert.Equal(0, result.SeamDuplicateRows);
        Assert.Equal(2, result.RowsReturned);
    }

    [Fact]
    public void AtRowCap_reads_the_LAST_page_fetched_and_not_the_total()
    {
        // The whole point of the change. Before #49 a 4000-row response meant "rows are gone". After it, a
        // 4000-row page 0 means "there is another page", and the walk fetched it, so a walk that ended on a
        // short page is NOT at the cap however many rows it gathered in total.
        var walked = new CalendarResult<string>(
            [], new CalendarWalk(RowsReturned: 9325, PagesFetched: 3, LastPageRowCount: 1325, SeamDuplicateRows: 0),
            Day(2026, 5, 1), Day(2026, 5, 31), Day(2026, 5, 1), rowCap: 4000, lookbackLimitDays: null);

        Assert.False(walked.AtRowCap);
        Assert.False(walked.LikelyTruncated);
    }

    [Fact]
    public void AtRowCap_fires_when_the_walk_stopped_with_a_full_page_in_hand()
    {
        // The walk hit its page ceiling, or a repeat, while the last page it appended was still full: rows
        // are still behind it, which is exactly what AtRowCap has always meant.
        var result = new CalendarResult<string>(
            [], new CalendarWalk(RowsReturned: 400_000, PagesFetched: 100, LastPageRowCount: 4000, SeamDuplicateRows: 0),
            Day(2016, 1, 1), Day(2026, 1, 1), Day(2016, 1, 1), rowCap: 4000, lookbackLimitDays: null);

        Assert.True(result.AtRowCap);
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public void An_overlapping_seam_is_truncation_even_when_the_walk_ended_cleanly()
    {
        // Measured 2026-09-01 over seven seams: an overlapping seam loses exactly as many rows as it
        // duplicates -- 381/381, 174/174, 315/315 -- and a clean seam loses none. So a walk that ran to a
        // short page and still overlapped somewhere is missing rows, and no row count can see it.
        var result = new CalendarResult<string>(
            [], new CalendarWalk(RowsReturned: 28_104, PagesFetched: 8, LastPageRowCount: 104, SeamDuplicateRows: 913),
            Day(2025, 1, 1), Day(2025, 12, 31), Day(2025, 1, 1), rowCap: 4000, lookbackLimitDays: null);

        Assert.False(result.AtRowCap);
        Assert.False(result.MissesStartOfRange);
        Assert.True(result.LikelyTruncated);
        Assert.Equal(913, result.SeamDuplicateRows);
    }
}
