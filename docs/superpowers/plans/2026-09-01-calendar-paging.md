# Calendar Paging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `GetEarningsCalendarAsync` and `GetDividendsCalendarAsync` walk FMP's `page` cursor so a caller receives the whole range instead of its first 4000 rows, and report the seam defect that walking exposes rather than hiding it.

**Architecture:** One private generic walk helper on `CalendarEndpoints` fetches `page=0, 1, 2, …` until a page comes back short, concatenates the pages untouched, and counts rows shared between adjacent pages. That count travels to the result type in a new internal `CalendarWalk` record struct alongside the page count and the last page's size. `EarningsCalendarResult` and `CalendarResult<T>` gain `PagesFetched` and `SeamDuplicateRows`, recompute `AtRowCap` against the last page rather than the only page, and fire `LikelyTruncated` on an overlapping seam.

**Tech Stack:** .NET 10, C#, NodaTime, source-generated `System.Text.Json` (`FmpJsonContext`), xUnit, `StubHandler` for offline HTTP.

**Spec:** [`docs/superpowers/specs/2026-09-01-calendar-paging-design.md`](../specs/2026-09-01-calendar-paging-design.md), argued from [`docs/superpowers/specs/2026-09-01-calendar-paging-measurements.md`](../specs/2026-09-01-calendar-paging-measurements.md). Both are committed on this branch (`0fd2753`, `87869f1`). Read the design before Task 1 and the measurements before Task 4.

## Global Constraints

Copied from `CONTRIBUTING.md` and the repository's existing conventions. Every task's requirements implicitly include this section.

- **A claim in this repository should have a measurement behind it.** Every number written into a doc comment in this plan comes from the measurements document. Do not invent one, and do not round one.
- **No reflection in the library.** `FmpDotNet.csproj` declares `IsAotCompatible`; `IL2026` and `IL3050` are build errors. Tests may reflect — they have `InternalsVisibleTo`.
- **NodaTime only in public signatures.** No `DateTime`, `DateOnly`, `DateTimeOffset` or `TimeSpan`.
- **Everything throws.** No `Try`-prefixed methods and no sentinel returns. `null` means "an answer FMP gave", never "a failure".
- **Nullable models, nothing `required`.**
- **`decimal` over `long`/`int`** for anything numeric off the wire, unless the quantity is whole by its own nature — a count is, so the new `int` properties in this plan are correct.
- **Never paste an API key, including inside a URL.** No captured response and no built URL is committed.
- **Branch is `fix/calendar-paging`**, already created, already carrying the two spec commits. Commit in conventional-commit form referencing `#49`. End every commit message with `Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy`.
- **Build must be clean under `-warnaserror`.** Run `dotnet build FmpDotNet.slnx -warnaserror` before every commit.
- **Full suite must be green.** `dotnet test FmpDotNet.slnx` — 1,413 unit tests on `master`, and this plan adds to that count without removing any.

## File Structure

| file | responsibility | task |
|---|---|---|
| `src/FmpDotNet/Models/CalendarWalk.cs` | **Create.** Internal record struct carrying the four numbers a walk produces, so neither result-type constructor grows a fifth, sixth and seventh loose `int`. | 1 |
| `src/FmpDotNet/Models/CalendarResult.cs` | **Modify.** Constructor takes a `CalendarWalk`; gains `PagesFetched` and `SeamDuplicateRows`; `AtRowCap` reads the last page; `LikelyTruncated` gains a term. | 1 |
| `src/FmpDotNet/Models/EarningsCalendarResult.cs` | **Modify.** The same four changes, plus its `RowCap` remarks. | 1 |
| `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` | **Modify.** Adds `MaxCalendarPages`, the private `WalkAsync<T>` helper, and rewires the two capped methods. The other four calendar methods pass `CalendarWalk.Single(...)` and are otherwise untouched. | 1, 2, 3, 5 |
| `tests/FmpDotNet.Tests/CalendarResultTests.cs` | **Modify.** New tests for the two new properties and the redefined `AtRowCap`; existing tests adapt to the constructor. | 1 |
| `tests/FmpDotNet.Tests/CalendarWalkTests.cs` | **Create.** The walk helper's terminators, tested at their own level with a small `rowCap` so the page-ceiling case costs 100 tiny pages instead of 400,000 rows. | 2 |
| `tests/FmpDotNet.Tests/CalendarEndpointsTests.cs` | **Modify.** The walk as the earnings method uses it; two existing cap tests are rewritten because a 4000-row page 0 now means "fetch page 1". | 2 |
| `tests/FmpDotNet.Tests/DividendTests.cs` | **Modify.** New walk tests for the dividends calendar. | 3 |
| `tests/FmpDotNet.Tests/StockSplitTests.cs`, `IpoTests.cs` | **Modify.** One test each pinning that these two paths do **not** walk. | 3 |
| `tests/FmpDotNet.SmokeTests/Probe.cs` | **Modify.** The non-nullable-value-property census in the `Populated` doc goes from nineteen to twenty-three. | 4 |
| `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` | **Modify.** Two comments justify the narrow sweep windows by a claim this change makes false; the windows themselves stay. | 4 |
| `docs/superpowers/specs/2026-09-01-query-parameter-audit-measurements.md` | **Modify.** Correction note on the "clean partition" claim. | 4 |

---

### Task 1: The seam evidence on both result types

Nothing walks yet. This task changes what the two result types *can* report and leaves every caller passing a one-page walk, so the whole suite stays green and the change is reviewable on its own.

**Files:**
- Create: `src/FmpDotNet/Models/CalendarWalk.cs`
- Modify: `src/FmpDotNet/Models/CalendarResult.cs`
- Modify: `src/FmpDotNet/Models/EarningsCalendarResult.cs`
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (four constructor call sites: lines 200, 307, 408, 452)
- Test: `tests/FmpDotNet.Tests/CalendarResultTests.cs`

**Interfaces:**
- Produces: `internal readonly record struct CalendarWalk(int RowsReturned, int PagesFetched, int LastPageRowCount, int SeamDuplicateRows)` with `internal static CalendarWalk Single(int rowsReturned)`.
- Produces: `CalendarResult<T>(IReadOnlyList<T> rows, CalendarWalk walk, LocalDate requestedFrom, LocalDate requestedTo, LocalDate? earliestReturnedDate, int? rowCap, int? lookbackLimitDays)` — the `int rowsReturned` parameter is **replaced** by `CalendarWalk walk` in the same position.
- Produces: `EarningsCalendarResult(IReadOnlyList<EarningsCalendarEntry> rows, CalendarWalk walk, LocalDate requestedFrom, LocalDate requestedTo, LocalDate? earliestReturnedDate)` — same replacement.
- Produces on both types: `public int PagesFetched { get; }`, `public int SeamDuplicateRows { get; }`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/FmpDotNet.Tests/CalendarResultTests.cs`, inside the class:

```csharp
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
        // 4000-row page 0 means "there is another page" and the walk fetched it, so a walk that ended on a
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
```

Then change the file's existing `Result(...)` helper so every other test in it keeps compiling:

```csharp
    private static CalendarResult<string> Result(
        int rowsReturned, LocalDate from, LocalDate to, LocalDate? earliest,
        int? rowCap = null, int? lookback = null, IReadOnlyList<string>? rows = null) =>
        new(rows ?? [], CalendarWalk.Single(rowsReturned), from, to, earliest, rowCap, lookback);
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test FmpDotNet.slnx --filter FullyQualifiedName~CalendarResultTests`

Expected: **build failure**, `CS0246: The type or namespace name 'CalendarWalk' could not be found`. That is the correct red for a type that does not exist yet — do not proceed until you have seen it.

- [ ] **Step 3: Create `CalendarWalk`**

Create `src/FmpDotNet/Models/CalendarWalk.cs`:

```csharp
namespace FmpDotNet.Models;

/// <summary>What one calendar call cost and what it turned up, gathered while the pages were still separate.
///
/// <para>Three of these four numbers cannot be recovered from the concatenated rows, which is why they travel
/// as a group rather than being recomputed later. <see cref="LastPageRowCount"/> is gone the moment the pages
/// are joined; <see cref="SeamDuplicateRows"/> is defined between two pages and has no meaning inside one; and
/// <see cref="PagesFetched"/> is the request count, which the rows never carried.</para>
///
/// <para>Internal because it is plumbing between <see cref="Endpoints.CalendarEndpoints"/> and the two result
/// types. Its numbers reach callers as properties on <see cref="CalendarResult{T}"/> and
/// <see cref="EarningsCalendarResult"/>.</para></summary>
/// <param name="RowsReturned">Rows across every page fetched, counted raw — before undated rows are dropped
/// and before any clamp.</param>
/// <param name="PagesFetched">Requests made. 1 on a path that does not page, never 0.</param>
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
```

- [ ] **Step 4: Rework `CalendarResult<T>`**

In `src/FmpDotNet/Models/CalendarResult.cs`, replace the constructor and the four affected members.

Constructor — `int rowsReturned` becomes `CalendarWalk walk`:

```csharp
    internal CalendarResult(
        IReadOnlyList<T> rows,
        CalendarWalk walk,
        LocalDate requestedFrom,
        LocalDate requestedTo,
        LocalDate? earliestReturnedDate,
        int? rowCap,
        int? lookbackLimitDays)
    {
        _rows = rows;
        _walk = walk;
        RequestedFrom = requestedFrom;
        RequestedTo = requestedTo;
        EarliestReturnedDate = earliestReturnedDate;
        RowCap = rowCap;
        LookbackLimitDays = lookbackLimitDays;
    }
```

Add the field next to `_rows`:

```csharp
    private readonly CalendarWalk _walk;
```

Replace the `RowsReturned` property, and add the two new ones after it:

```csharp
    /// <summary>How many rows FMP's responses actually carried, counted before the SDK dropped anything and
    /// summed over every page fetched. This, and not <see cref="Count"/>, is what the truncation tells are
    /// computed from.</summary>
    public int RowsReturned => _walk.RowsReturned;

    /// <summary>How many requests this result cost. <b>1 on a path that does not page</b>, never 0.
    ///
    /// <para>Only <c>dividends-calendar</c> walks among the three paths that return this type — measured
    /// 2026-09-01, a full year of dividends is 8 requests and 28,104 rows against the 4000 a single request
    /// answers. <c>splits-calendar</c> answers <c>page=1</c> with an empty array and <c>ipos-calendar</c>
    /// ignores <c>page</c> altogether, serving page 5 byte-identically to page 0, so neither is walked and
    /// both report 1 here.</para></summary>
    public int PagesFetched => _walk.PagesFetched;

    /// <summary>Rows that arrived on both sides of a page seam — <b>a count of rows lost, not of rows
    /// duplicated</b>.
    ///
    /// <para>FMP orders these responses by <c>date</c> and by nothing else, and a page seam always falls
    /// <i>inside</i> a date rather than between two — true of all 22 seams measured on 2026-09-01. So the
    /// offset that cuts page <i>n+1</i> is applied to an ordering page <i>n</i> was not cut from: some rows
    /// are served on both sides, and an equal number of different rows are served on neither.</para>
    ///
    /// <para><b>The equality is measured, not assumed.</b> Seven seams were re-requested a day at a time — a
    /// single-day request fits one page and so has no seam — and compared against what the walk held for that
    /// date: 381 duplicated / 381 missing, 174/174, 315/315, and three clean seams losing nothing. Not one row
    /// appeared in a walk that the single-day request did not have, so rows are exchanged one for one rather
    /// than invented.</para>
    ///
    /// <para><b>It over-reports rather than under-reports.</b> FMP's own data carries byte-identical duplicate
    /// rows — one page of the measured dividends year held 4000 rows and 3999 distinct ones — and such a pair
    /// straddling a seam would be counted here with no loss behind it. No such case appeared in 22 seams, and
    /// the bias runs the safe direction.</para>
    ///
    /// <para>The remedy is a narrower range: one that fits in a single page has no seam and cannot lose
    /// anything.</para></summary>
    public int SeamDuplicateRows => _walk.SeamDuplicateRows;
```

Replace `AtRowCap`:

```csharp
    /// <summary>The <b>last page fetched</b> came back at or above <see cref="RowCap"/>, so the walk stopped
    /// with a full page in hand and something is still behind it.
    ///
    /// <para><b>This reads the last page, not the total, and before #49 there was only ever one page for it to
    /// read.</b> A 4000-row response used to mean "rows are gone". Now it means "there is another page", and
    /// <see cref="PagesFetched"/> says whether it was fetched — so a walk that ended on a short page is not at
    /// the cap however many rows it gathered. What still fires this is a walk stopped by
    /// <see cref="Endpoints.CalendarEndpoints.MaxCalendarPages"/>, or by a page repeating its predecessor,
    /// with a full page as the last one appended.</para>
    ///
    /// <para>Always <see langword="false"/> where <see cref="RowCap"/> is null. Exact at the cap and blind
    /// just under it, so a false reading here is "complete" and never "truncated";
    /// <see cref="MissesStartOfRange"/> and <see cref="SeamDuplicateRows"/> are the tells that cover what it
    /// cannot see.</para></summary>
    public bool AtRowCap => RowCap is { } cap && _walk.LastPageRowCount >= cap;
```

Replace `LikelyTruncated`:

```csharp
    /// <summary>Any tell fired, so these rows are not all of them.
    ///
    /// <para><b>Three mechanisms, three tells.</b> <see cref="AtRowCap"/> catches a walk that stopped with a
    /// full page in hand. <see cref="ExceedsLookbackLimit"/> and <see cref="MissesStartOfRange"/> catch the
    /// 90-day window clamp on <c>splits-calendar</c> and <c>ipos-calendar</c>, which no row count can see.
    /// <see cref="SeamDuplicateRows"/> catches the one that only appears once a path is walked: a page seam
    /// that dropped as many rows as it duplicated.</para>
    ///
    /// <para><b>The remedy depends on which fired.</b> A window clamp is answered by moving <c>to</c>; an
    /// unstable seam by narrowing the range until it fits one page, which is the only width measured
    /// lossless. See the remarks on the method that returned this.</para></summary>
    public bool LikelyTruncated =>
        AtRowCap || ExceedsLookbackLimit || MissesStartOfRange || SeamDuplicateRows > 0;
```

- [ ] **Step 5: Rework `EarningsCalendarResult` the same way**

In `src/FmpDotNet/Models/EarningsCalendarResult.cs`: identical constructor change (`int rowsReturned` → `CalendarWalk walk`), identical `private readonly CalendarWalk _walk;` field, identical `RowsReturned`, `PagesFetched` and `SeamDuplicateRows` properties — except that `PagesFetched`'s remarks name this path's own figures:

```csharp
    /// <summary>How many requests this result cost, and it is no longer always 1.
    ///
    /// <para>Measured 2026-09-01: <c>from=2026-05-13&amp;to=2026-05-19</c> is 3 requests and 6,496 rows against
    /// the 4000 one request answers, and the first half of 2025 is 12 requests and 45,765 rows against the same
    /// 4000. A range that fits in one page costs one request, exactly as before.</para></summary>
    public int PagesFetched => _walk.PagesFetched;
```

`AtRowCap` becomes:

```csharp
    public bool AtRowCap => _walk.LastPageRowCount >= RowCap;
```

with the same remarks as `CalendarResult<T>.AtRowCap` minus the "always false where RowCap is null" paragraph, since this type's cap is a constant.

`LikelyTruncated` becomes:

```csharp
    public bool LikelyTruncated => AtRowCap || MissesStartOfRange || SeamDuplicateRows > 0;
```

- [ ] **Step 6: Fix the four endpoint call sites**

In `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`, replace `rows.Count` with `CalendarWalk.Single(rows.Count)` in all four constructions. They are at (current) lines 200, 307, 408 and 452:

```csharp
        return new EarningsCalendarResult(kept, CalendarWalk.Single(rows.Count), from, to, earliest);
```
```csharp
        return new CalendarResult<Dividend>(
            kept, CalendarWalk.Single(rows.Count), from, to, earliest, rowCap: 4000, lookbackLimitDays: null);
```
```csharp
        return new CalendarResult<StockSplit>(
            kept, CalendarWalk.Single(rows.Count), from, to, earliest, rowCap: null, lookbackLimitDays: 90);
```
```csharp
        return new CalendarResult<IpoCalendarEntry>(
            kept, CalendarWalk.Single(rows.Count), from, to, earliest, rowCap: null, lookbackLimitDays: 90);
```

- [ ] **Step 7: Run the whole suite**

Run: `dotnet build FmpDotNet.slnx -warnaserror && dotnet test FmpDotNet.slnx`

Expected: PASS, with four more tests than before. If `CalendarEndpointsTests` or `StockSplitTests` fail here, you changed a tell's behaviour rather than only its inputs — `CalendarWalk.Single(n)` sets `LastPageRowCount = n`, so `AtRowCap` must evaluate exactly as it did before on every one-page result.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet/Models/CalendarWalk.cs src/FmpDotNet/Models/CalendarResult.cs \
        src/FmpDotNet/Models/EarningsCalendarResult.cs src/FmpDotNet/Endpoints/CalendarEndpoints.cs \
        tests/FmpDotNet.Tests/CalendarResultTests.cs
git commit -m "feat(calendar): give the two result types somewhere to report a page walk (#49)

AtRowCap now reads the last page fetched rather than the only one, which is identical arithmetic while
PagesFetched is 1 and is the whole difference once a path walks: a 4000-row page 0 stops meaning 'rows are
gone' and starts meaning 'there is another page'. PagesFetched and SeamDuplicateRows join both types, carried
together in an internal CalendarWalk because LastPageRowCount and SeamDuplicateRows cannot be recovered once
the pages are concatenated.

SeamDuplicateRows counts rows arriving on both sides of a seam, and measured 2026-09-01 across seven seams
that count equals the number of rows the walk never saw -- 381/381, 174/174, 315/315, and three clean seams
losing none. LikelyTruncated gains it as a term.

No path walks yet; every call site passes CalendarWalk.Single and behaviour is unchanged.

Refs #49

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 2: The walk, on `earnings-calendar`

**Files:**
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`
- Create: `tests/FmpDotNet.Tests/CalendarWalkTests.cs`
- Test: `tests/FmpDotNet.Tests/CalendarEndpointsTests.cs`

**Interfaces:**
- Consumes: `CalendarWalk`, `CalendarWalk.Single`, `EarningsCalendarResult.RowCap` (`public const int` = 4000).
- Produces: `public const int MaxCalendarPages = 100;` on `CalendarEndpoints`.
- Produces: `internal async Task<(List<T> Rows, CalendarWalk Walk)> WalkAsync<T>(Func<int, FmpRequest> buildRequest, JsonTypeInfo<List<T>> typeInfo, int rowCap, CancellationToken ct)` on `CalendarEndpoints`.
- Produces: extends the test file's existing `SyntheticCalendar` helper with a `startIndex` parameter so two pages can be made disjoint or overlapping.

**Why `internal` and not `private`.** The three terminators are the interesting logic and two of them are awkward to reach through an endpoint: proving the page ceiling bounds the loop needs 100 *full* pages, which at the real cap is 400,000 rows and roughly 100 MB of parsed models for one assertion. Called directly with `rowCap: 2`, the same proof costs 100 pages of two rows. `FmpDotNet.csproj` already grants `InternalsVisibleTo("FmpDotNet.Tests")`, and the repository already tests internals this way — `BulkCsvColumnParityTests` drives `internal` `CsvRow` and `FromCsv` directly. This is a visibility choice, not a behaviour switch: no production code branches on being under test.

- [ ] **Step 1: Write the failing tests**

First extend the existing helper at the bottom of `tests/FmpDotNet.Tests/CalendarEndpointsTests.cs` — add `startIndex`, which decides the symbols and therefore whether two pages share rows:

```csharp
    private static string SyntheticCalendar(
        int rowCount, LocalDate day, int overshootRows = 0, int startIndex = 0)
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            // The overshoot rows sit one day past `to`, exactly as the re-dated real rows do.
            var date = i < overshootRows ? day.PlusDays(2) : day;
            if (i > 0) json.Append(',');
            json.Append(CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{startIndex + i}}","date":"{{date:uuuu-MM-dd}}","epsActual":1,"epsEstimated":1,"revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"}""");
        }
        return json.Append(']').ToString();
    }
```

Add a multi-response builder next to the existing `Build`, because the walk needs a different body per request:

```csharp
    // A response per page, in order. StubHandler repeats its last response once the queue runs dry, which is
    // the ipos-calendar shape and is what the walk's repeat terminator exists for -- so a test that wants the
    // walk to STOP must end its queue with a short page.
    private static (CalendarEndpoints Endpoints, StubHandler Handler) BuildPages(params string[] pages)
    {
        var handler = new StubHandler([.. pages.Select(p => StubHandler.Json(p))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }
```

Now the tests. Replace the two existing cap tests — `The_truncation_signal_fires_at_exactly_four_thousand_rows` and `The_truncation_signal_survives_clamping_because_it_is_taken_before_the_clamp` — with the versions below, and add the rest:

```csharp
    // ---- the 4000-row cap, and the walk past it (#49) -----------------------------------------------------

    [Fact]
    public async Task A_full_page_is_followed_by_the_next_one_and_the_two_are_returned_as_one_list()
    {
        // Measured 2026-09-01: from=2026-05-13&to=2026-05-19 answers 4000 rows on page 0, 2496 on page 1 and
        // 0 on page 2 -- 6496 in total, of which this method used to return 4000.
        var (endpoints, handler) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(2496, Day(2026, 5, 13), startIndex: 4000));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(6496, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(6496, result.RowsReturned);
        Assert.False(result.AtRowCap);              // the walk ended on a short page
        Assert.False(result.LikelyTruncated);
    }

    [Fact]
    public async Task The_walk_omits_page_on_the_first_request_and_numbers_the_rest_from_one()
    {
        // page=0 was measured byte-identical to sending no page at all, so the first request of a walk is the
        // request this method already made. That keeps every single-page caller's URL, cache key and log line
        // exactly as they were.
        var (endpoints, handler) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(4000, Day(2026, 5, 13), startIndex: 4000),
            SyntheticCalendar(7, Day(2026, 5, 13), startIndex: 8000));

        await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("?from=2026-05-13&to=2026-05-19&apikey=k", handler.Requests[0].Query);
        Assert.Equal("?from=2026-05-13&to=2026-05-19&page=1&apikey=k", handler.Requests[1].Query);
        Assert.Equal("?from=2026-05-13&to=2026-05-19&page=2&apikey=k", handler.Requests[2].Query);
    }

    [Fact]
    public async Task A_range_that_fits_in_one_page_costs_exactly_one_request()
    {
        // The common case, and the one a walk must not make more expensive. 3999 rows is one below the cap.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(3999, Day(2026, 5, 13)));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 14));

        Assert.Equal(3999, rows.Count);
        Assert.Single(handler.Requests);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(1, result.PagesFetched);
        Assert.False(result.AtRowCap);
        Assert.False(EarningsCalendarResult.IsLikelyTruncated(rows));
    }

    [Fact]
    public async Task An_empty_page_ends_the_walk_and_contributes_nothing()
    {
        // Measured: page 2 of the earnings week answers [] rather than an error, and page 101 and page 1000
        // answer [] too. There is no ceiling response to handle on this family.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13)), "[]");

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(4000, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(0, result.SeamDuplicateRows);
    }

    [Fact]
    public async Task A_page_that_repeats_its_predecessor_ends_the_walk_and_is_not_appended()
    {
        // ipos-calendar does exactly this today: page=1 and page=5 are byte-identical to page=0, every page
        // full, no page ever short. Without this terminator such a path walks to MaxCalendarPages and returns
        // the same rows a hundred times. StubHandler repeating its last response reproduces the shape exactly.
        var (endpoints, handler) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13)));

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(4000, rows.Count);                 // once, not twice and not a hundred times
        Assert.Equal(2, handler.Requests.Count);        // the repeat was fetched, recognised and discarded
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(1, result.PagesFetched);
        Assert.True(result.AtRowCap);                   // stopped with a full page in hand
        Assert.True(result.LikelyTruncated);
    }

    [Fact]
    public async Task Rows_shared_across_a_seam_are_counted_and_left_in_the_list()
    {
        // Measured 2026-09-01: an overlapping seam duplicates and loses the same number of rows. The SDK
        // reports rather than repairs -- removing a duplicate would be guessing which of two identical rows
        // is the real one, and FMP's own data carries genuine duplicate rows.
        var (endpoints, _) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            SyntheticCalendar(2496, Day(2026, 5, 13), startIndex: 3900));   // 100 rows on both sides

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        Assert.Equal(6496, rows.Count);                                     // nothing removed
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(100, result.SeamDuplicateRows);
        Assert.False(result.AtRowCap);                                      // ended on a short page
        Assert.True(result.LikelyTruncated);                                // and is still missing ~100 rows
    }

    [Fact]
    public async Task Undated_rows_are_dropped_across_the_whole_walk_and_the_raw_count_still_says_so()
    {
        var (endpoints, _) = BuildPages(
            SyntheticCalendar(4000, Day(2026, 5, 13)),
            """
            [{"symbol":"BAD.X","date":"","epsActual":1,"epsEstimated":null,
              "revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-17"},
             {"symbol":"GFH.AE","date":"2026-05-17","epsActual":0.03708,"epsEstimated":0.08026,
              "revenueActual":350977000,"revenueEstimated":638486100,"lastUpdated":"2026-08-17"}]
            """);

        var rows = await endpoints.GetEarningsCalendarAsync(Day(2026, 5, 13), Day(2026, 5, 19));

        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(4002, result.RowsReturned);        // raw, both pages
        Assert.Equal(4001, result.Count);               // the undated row is gone
    }

    [Fact]
    public async Task The_truncation_signal_survives_clamping_because_it_is_taken_before_the_clamp()
    {
        // This is a live bug in the consumer this SDK replaces: it clamps first, then tests rows.Count >= 4000.
        // Clamping removes the overshoot rows, so a genuinely truncated response reaches the test already under
        // the cap and is judged complete. Here page 1 repeats page 0, so the walk stops with a full page in
        // hand; 12 of the 4000 rows fall outside the range and the clamp takes the count to 3988.
        var (endpoints, _) = BuildPages(SyntheticCalendar(4000, Day(2026, 5, 13), overshootRows: 12));

        var rows = await endpoints.GetEarningsCalendarAsync(
            Day(2026, 5, 13), Day(2026, 5, 14), clampToRange: true);

        Assert.Equal(3988, rows.Count);                                   // what a naive count test would see
        Assert.True(rows.Count < EarningsCalendarResult.RowCap);          // and it would call this complete
        Assert.True(EarningsCalendarResult.IsLikelyTruncated(rows));      // the SDK does not
        var result = Assert.IsType<EarningsCalendarResult>(rows);
        Assert.Equal(4000, result.RowsReturned);                          // what FMP actually sent
        Assert.True(result.AtRowCap);
    }
```

Then create `tests/FmpDotNet.Tests/CalendarWalkTests.cs`, which drives the helper directly so the two terminators that are expensive to reach through an endpoint can be reached cheaply:

```csharp
using FmpDotNet.Endpoints;
using FmpDotNet.Models;
using FmpDotNet.Serialization;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The page walk's three terminators, exercised on the helper itself rather than through a calendar
/// method (#49).
///
/// <para><b>Because the interesting cases are the expensive ones.</b> Proving the page ceiling bounds the loop
/// needs 100 <i>full</i> pages, and a full page at the real cap is 4000 rows — 400,000 parsed models for one
/// assertion. Called here with <c>rowCap: 2</c>, the identical proof costs 100 pages of two rows. The row cap
/// is a parameter of the walk, so nothing about production behaviour is being lowered: the two calendar
/// methods pass 4000, and <c>CalendarEndpointsTests</c> covers them at that width.</para></summary>
public class CalendarWalkTests
{
    private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(params string[] pages)
    {
        var handler = new StubHandler([.. pages.Select(p => StubHandler.Json(p))]);
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://financialmodelingprep.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return (new CalendarEndpoints(new FmpTransport(http, Options.Create(new FmpOptions { ApiKey = "k" }))), handler);
    }

    /// <summary>A page of <paramref name="rowCount"/> earnings rows, all dated the same day, with symbols
    /// numbered from <paramref name="startIndex"/> — which is what decides whether two pages share rows.</summary>
    private static string Page(int rowCount, int startIndex = 0) =>
        "[" + string.Join(",", Enumerable.Range(startIndex, rowCount).Select(i =>
            $$"""{"symbol":"S{{i}}","date":"2026-05-13","epsActual":1,"epsEstimated":1,"revenueActual":1,"revenueEstimated":1,"lastUpdated":"2026-08-26"}""")) + "]";

    private static Task<(List<EarningsCalendarEntry> Rows, CalendarWalk Walk)> WalkAsync(
        CalendarEndpoints endpoints, int rowCap) =>
        endpoints.WalkAsync(
            page => new FmpRequest("stable/earnings-calendar").With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListEarningsCalendarEntry,
            rowCap,
            CancellationToken.None);

    [Fact]
    public async Task A_short_page_ends_the_walk()
    {
        var (endpoints, handler) = Build(Page(2), Page(1, startIndex: 2));

        var (rows, walk) = await WalkAsync(endpoints, rowCap: 2);

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new CalendarWalk(RowsReturned: 3, PagesFetched: 2, LastPageRowCount: 1, SeamDuplicateRows: 0), walk);
    }

    [Fact]
    public async Task The_page_ceiling_bounds_a_feed_that_never_shortens_and_never_repeats()
    {
        // The guard. No page ceiling was measured on either calendar path -- page=1000 answers [] -- so this
        // is never reached in practice. It exists because a sibling path already breaks the "a short page
        // ends it" reasoning: ipos-calendar serves every page full and every page identical.
        var pages = Enumerable.Range(0, CalendarEndpoints.MaxCalendarPages + 20)
            .Select(i => Page(2, startIndex: i * 2))
            .ToArray();
        var (endpoints, handler) = Build(pages);

        var (rows, walk) = await WalkAsync(endpoints, rowCap: 2);

        Assert.Equal(CalendarEndpoints.MaxCalendarPages, handler.Requests.Count);
        Assert.Equal(CalendarEndpoints.MaxCalendarPages, walk.PagesFetched);
        Assert.Equal(CalendarEndpoints.MaxCalendarPages * 2, rows.Count);
        Assert.Equal(2, walk.LastPageRowCount);          // full, so AtRowCap will fire on the result type
    }

    [Fact]
    public async Task A_repeated_page_ends_the_walk_before_the_ceiling_and_is_not_appended()
    {
        // The ipos-calendar shape: page 1 and page 5 byte-identical to page 0, every page full. StubHandler
        // repeats its last response once its queue runs dry, which reproduces it exactly.
        var (endpoints, handler) = Build(Page(2));

        var (rows, walk) = await WalkAsync(endpoints, rowCap: 2);

        Assert.Equal(2, rows.Count);                     // once, not a hundred times
        Assert.Equal(2, handler.Requests.Count);         // fetched, recognised, discarded
        Assert.Equal(1, walk.PagesFetched);
        Assert.Equal(0, walk.SeamDuplicateRows);         // a repeat is not a seam overlap
    }

    [Fact]
    public async Task A_partial_overlap_is_a_seam_and_does_not_end_the_walk()
    {
        // Two rows in common is not the same page: the walk must count them and keep going. This is the case
        // that separates the repeat terminator from the seam counter, and getting it wrong either truncates a
        // good walk or lets a repeating feed run to the ceiling.
        var (endpoints, handler) = Build(
            Page(4),                        // S0..S3
            Page(4, startIndex: 2),         // S2..S5 -- two shared
            Page(1, startIndex: 6));

        var (rows, walk) = await WalkAsync(endpoints, rowCap: 4);

        Assert.Equal(9, rows.Count);                     // 4 + 4 + 1, nothing removed
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(2, walk.SeamDuplicateRows);
        Assert.Equal(1, walk.LastPageRowCount);
    }

    [Fact]
    public async Task An_empty_first_page_costs_one_request_and_reports_one_page()
    {
        var (endpoints, handler) = Build("[]");

        var (rows, walk) = await WalkAsync(endpoints, rowCap: 2);

        Assert.Empty(rows);
        Assert.Single(handler.Requests);
        Assert.Equal(new CalendarWalk(RowsReturned: 0, PagesFetched: 1, LastPageRowCount: 0, SeamDuplicateRows: 0), walk);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test FmpDotNet.slnx --filter "FullyQualifiedName~CalendarEndpointsTests|FullyQualifiedName~CalendarWalkTests"`

Expected: **build failure**, `CS0117: 'CalendarEndpoints' does not contain a definition for 'MaxCalendarPages'` and `CS1061: 'CalendarEndpoints' does not contain a definition for 'WalkAsync'`. Add nothing but the constant and an empty `WalkAsync` that fetches page 0 only, re-run, and expect the walk tests to fail on assertion instead — `A_full_page_is_followed_by_the_next_one` failing with `Assert.Equal() Failure: Expected 6496, Actual 4000`, and `The_page_ceiling_bounds_a_feed…` with `Expected 100, Actual 1`. Those second failures are the ones that prove the tests test the walk rather than the existence of a symbol.

- [ ] **Step 3: Add the constant and the walk helper**

In `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`, add `using System.Text.Json.Serialization.Metadata;` to the usings, and put these at the top of the class body, above `GetEarningsAsync`:

```csharp
    /// <summary>The most pages <see cref="GetEarningsCalendarAsync"/> and
    /// <see cref="GetDividendsCalendarAsync"/> will fetch for one call.
    ///
    /// <para><b>A guard, not a measurement, and it is the one number here with no probe behind it.</b> Neither
    /// path has a page ceiling: measured 2026-09-01, <c>page=101</c> and <c>page=1000</c> both answer <c>[]</c>
    /// under HTTP 200, so a walk that stops on a short page provably terminates and this bound is never
    /// reached in practice. It exists because a sibling path already breaks that reasoning —
    /// <c>ipos-calendar</c> serves <c>page=5</c> byte-identically to <c>page=0</c>, every page full, so a walk
    /// there would never end. 100 pages is 400,000 rows, about fourteen years of dividends at the measured
    /// 28,104 rows a year.</para>
    ///
    /// <para>Reaching it is reported rather than thrown: the rows already fetched are real, and this SDK
    /// reports rather than fails. A walk stopped here ends with a full page as its last, so
    /// <see cref="Models.CalendarResult{T}.AtRowCap"/> fires.</para></summary>
    public const int MaxCalendarPages = 100;

    /// <summary>Walks <c>page=0, 1, 2, …</c> until a page comes back short, and hands back the concatenation
    /// with the evidence gathered between pages.
    ///
    /// <para><b>Rows are concatenated in walk order and otherwise untouched</b> — not sorted, not
    /// de-duplicated. A row arriving on both sides of a seam is FMP's row, served twice by FMP; removing one
    /// would be a guess about which of two identical rows is real, and FMP's own data carries genuine
    /// duplicate rows. The count goes to
    /// <see cref="Models.CalendarResult{T}.SeamDuplicateRows"/> instead, where it doubles as the count of
    /// rows the walk never saw.</para>
    ///
    /// <para><b>Three terminators.</b> A short page is the last page — measured on four walks, none of which
    /// produced a short page followed by a full one. A page whose distinct rows are exactly its predecessor's
    /// is that predecessor served again, which is what <c>ipos-calendar</c> does today; it is discarded rather
    /// than appended. And <see cref="MaxCalendarPages"/> bounds the loop whatever FMP does.</para>
    ///
    /// <para>Internal rather than private so the terminators can be tested at their own level. Reaching the
    /// page ceiling through a calendar method would need 100 full pages — 400,000 rows at the real cap — for
    /// one assertion; <c>CalendarWalkTests</c> reaches it with <c>rowCap: 2</c>. Nothing here branches on being
    /// under test.</para></summary>
    internal async Task<(List<T> Rows, Models.CalendarWalk Walk)> WalkAsync<T>(
        Func<int, FmpRequest> buildRequest,
        JsonTypeInfo<List<T>> typeInfo,
        int rowCap,
        CancellationToken ct)
    {
        var all = new List<T>();
        var pages = 0;
        var seamDuplicates = 0;
        var lastPageRowCount = 0;
        HashSet<T>? previous = null;

        for (var page = 0; page < MaxCalendarPages; page++)
        {
            var rows = await transport.GetListAsync(buildRequest(page), typeInfo, ct).ConfigureAwait(false);

            // Distinct rows, because the seam is measured between SETS: a row FMP sends twice within one page
            // is not a paging artefact and must not be counted as one. The models are records, so this is
            // structural equality and costs one hash pass per page.
            var current = new HashSet<T>(rows);

            if (previous is not null)
            {
                var shared = 0;
                foreach (var row in current)
                    if (previous.Contains(row)) shared++;

                // Every row already seen and nothing new: this page IS the previous one. Stop before appending
                // it, or a path that ignores `page` returns the same rows MaxCalendarPages times.
                if (shared == current.Count && shared == previous.Count) break;

                seamDuplicates += shared;
            }

            all.AddRange(rows);
            pages++;
            lastPageRowCount = rows.Count;
            previous = current;

            if (rows.Count < rowCap) break;
        }

        return (all, new Models.CalendarWalk(all.Count, pages, lastPageRowCount, seamDuplicates));
    }
```

- [ ] **Step 4: Rewire `GetEarningsCalendarAsync`**

Replace the request-building and fetching block in the method body — everything from `var request = new FmpRequest("stable/earnings-calendar")` through the `transport.GetListAsync` call — with:

```csharp
        var (rows, walk) = await WalkAsync(
            page => new FmpRequest("stable/earnings-calendar")
                .With("from", from)
                .With("to", to)
                // Sent only when true. The measured plain request omits the parameter rather than sending
                // false, and there is no evidence about how FMP reads an explicit false.
                .With("includeReportTimes", includeReportTimes ? true : (bool?)null)
                // Omitted on page 0, where it was measured byte-identical to sending nothing: the first
                // request of a walk is the request this method made before it walked.
                .With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListEarningsCalendarEntry,
            EarningsCalendarResult.RowCap,
            ct).ConfigureAwait(false);
```

Leave the `earliest` loop and the `kept` loop exactly as they are — they already iterate `rows`, which is now the concatenation. Change only the return:

```csharp
        return new EarningsCalendarResult(kept, walk, from, to, earliest);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build FmpDotNet.slnx -warnaserror && dotnet test FmpDotNet.slnx --filter FullyQualifiedName~CalendarEndpointsTests`

Expected: PASS, all of them. The four existing query-shape tests (`Hits_its_own_path_carrying_from_and_to_only`, `A_single_day_range_is_allowed_and_is_the_recommended_chunk_width`, `Sends_includeReportTimes_only_when_it_is_asked_for`, `Clamping_is_a_client_side_decision_and_changes_no_query_parameter`) must pass **unchanged** — they build with an empty `[]` body, which is a short page, so the walk makes exactly one request and the query is byte-identical to before. If any of them now sees two requests, `page` is being sent on page 0.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test FmpDotNet.slnx`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/CalendarEndpointsTests.cs \
        tests/FmpDotNet.Tests/CalendarWalkTests.cs
git commit -m "feat(calendar): walk the page cursor on earnings-calendar (#49)

Measured 2026-09-01: from=2026-05-13&to=2026-05-19 answers 4000 rows on page 0 and 2496 more on page 1, and
the first half of 2025 is 45,765 rows over 12 pages. This method returned the first 4000 of each -- 62% and
8.7% -- as a well-formed 200 indistinguishable from a complete answer.

The walk stops on a short page, on a page that repeats its predecessor, and at MaxCalendarPages. The middle
terminator is not defensive padding: ipos-calendar serves page 5 byte-identically to page 0 today, every page
full, so a short-page-only walk would never end there. page is omitted on page 0, where it was measured
byte-identical to sending nothing, so a single-page call is the request this method already made.

Rows are concatenated untouched. Seam duplicates are counted, not removed.

Refs #49

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 3: The walk on `dividends-calendar`, and a guard on the two paths that must not walk

**Files:**
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`
- Test: `tests/FmpDotNet.Tests/DividendTests.cs`
- Test: `tests/FmpDotNet.Tests/StockSplitTests.cs`
- Test: `tests/FmpDotNet.Tests/IpoTests.cs`

**Interfaces:**
- Consumes: `WalkAsync<T>`, `MaxCalendarPages`, `CalendarWalk` from Task 2.
- Produces: `private const int DividendsCalendarRowCap = 4000;` on `CalendarEndpoints`, replacing the two `rowCap: 4000` literals.

- [ ] **Step 1: Write the failing tests**

Add a multi-page builder to `tests/FmpDotNet.Tests/DividendTests.cs` mirroring `BuildPages` in Task 2 — copy it, changing only the endpoint type if that file's `Build` differs — then add:

```csharp
    // ---- the walk past the cap (#49) ----------------------------------------------------------------------

    [Fact]
    public async Task A_full_page_of_dividends_is_followed_by_the_next_one()
    {
        // Measured 2026-09-01: May 2026 answers 4000 / 4000 / 1325 / 0 -- 9325 rows, of which this method
        // used to return 4000. A full year is 8 requests and 28,104 rows against the same 4000.
        var (endpoints, handler) = BuildPages(
            SyntheticDividends(4000, Day(2026, 5, 1)),
            SyntheticDividends(4000, Day(2026, 5, 1), startIndex: 4000),
            SyntheticDividends(1325, Day(2026, 5, 1), startIndex: 8000));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 5, 1), Day(2026, 5, 31));

        Assert.Equal(9325, rows.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("?from=2026-05-01&to=2026-05-31&apikey=k", handler.Requests[0].Query);
        Assert.Equal("?from=2026-05-01&to=2026-05-31&page=1&apikey=k", handler.Requests[1].Query);
        Assert.Equal("?from=2026-05-01&to=2026-05-31&page=2&apikey=k", handler.Requests[2].Query);

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(3, result.PagesFetched);
        Assert.Equal(9325, result.RowsReturned);
        Assert.False(result.AtRowCap);
    }

    [Fact]
    public async Task A_dividend_range_that_fits_in_one_page_costs_one_request()
    {
        var (endpoints, handler) = BuildPages(SyntheticDividends(41, Day(2026, 8, 24)));

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 8, 24), Day(2026, 8, 25));

        Assert.Equal(41, rows.Count);
        Assert.Single(handler.Requests);
        Assert.Equal(1, Assert.IsType<CalendarResult<Dividend>>(rows).PagesFetched);
    }

    [Fact]
    public async Task An_overlapping_dividend_seam_is_counted_and_reported_as_truncation()
    {
        // Measured on the 2025 dividends year: 8 pages, 913 rows served twice, and 913 different rows served
        // on neither side. The first seam duplicated 381 rows and lost exactly 381.
        var (endpoints, _) = BuildPages(
            SyntheticDividends(4000, Day(2026, 5, 1)),
            SyntheticDividends(1325, Day(2026, 5, 1), startIndex: 3619));   // 381 rows on both sides

        var rows = await endpoints.GetDividendsCalendarAsync(Day(2026, 5, 1), Day(2026, 5, 31));

        var result = Assert.IsType<CalendarResult<Dividend>>(rows);
        Assert.Equal(5325, rows.Count);          // nothing removed
        Assert.Equal(381, result.SeamDuplicateRows);
        Assert.False(result.AtRowCap);
        Assert.True(result.LikelyTruncated);
    }
```

Add the synthetic-page helper at the bottom of `DividendTests`:

```csharp
    /// <summary>A dividends-calendar payload of a given size. Synthetic on purpose — the cap needs 4000 rows
    /// to exercise and nothing about those rows matters except how many there are, which dates they carry and
    /// whether two pages share any. <paramref name="startIndex"/> is what decides that last one.</summary>
    private static string SyntheticDividends(int rowCount, LocalDate day, int startIndex = 0)
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(CultureInfo.InvariantCulture,
                $$"""{"symbol":"S{{startIndex + i}}","date":"{{day:uuuu-MM-dd}}","recordDate":"{{day:uuuu-MM-dd}}","paymentDate":"{{day:uuuu-MM-dd}}","declarationDate":"","adjDividend":1,"dividend":1,"yield":1,"frequency":"Annual"}""");
        }
        return json.Append(']').ToString();
    }
```

Check the top of `DividendTests.cs` for `using System.Globalization;` and `using System.Text;` and add whichever is missing.

Now the two guards. Append to `tests/FmpDotNet.Tests/StockSplitTests.cs`:

```csharp
    [Fact]
    public async Task The_splits_calendar_does_not_walk_because_it_has_nothing_to_walk_to()
    {
        // Measured 2026-09-01: splits-calendar?from=2026-01-01&to=2026-08-31 answers 940 rows whose earliest
        // is 2026-06-02 -- the 90-day edge, not a row cap -- and page=1 answers 0. The limit here is a
        // lookback window and no cursor reaches outside it, so a walk would spend a request to learn nothing.
        var (endpoints, handler) = Build(Binding.Fixture("splits-calendar.head.json"));

        await endpoints.GetSplitsCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Single(handler.Requests);
        Assert.DoesNotContain("page", handler.Requests[0].Query, StringComparison.Ordinal);
    }
```

Append to `tests/FmpDotNet.Tests/IpoTests.cs`:

```csharp
    [Fact]
    public async Task The_ipo_calendar_does_not_walk_because_page_does_nothing_here()
    {
        // The reason the walk needs a repeat terminator at all. Measured 2026-09-01:
        // ipos-calendar?from=2026-01-01&to=2026-08-31 answers 439 rows, and page=1 and page=5 answer the SAME
        // 439 rows, SHA-256 identical. Every page is full and every page is the first, so a walk that stopped
        // only on a short page would never stop. This path is left un-walked deliberately.
        var (endpoints, handler) = Build(Binding.Fixture("ipos-calendar.head.json"));

        await endpoints.GetIpoCalendarAsync(Day(2026, 6, 1), Day(2026, 8, 28));

        Assert.Single(handler.Requests);
        Assert.DoesNotContain("page", handler.Requests[0].Query, StringComparison.Ordinal);
    }
```

Both guard tests reuse helpers that already exist in those files, verified: each has `private static (CalendarEndpoints Endpoints, StubHandler Handler) Build(string body = "[]")` and `private static LocalDate Day(int y, int m, int d)`, and both load fixtures through `Binding.Fixture(...)` rather than a local `Fixture` helper. Write `Binding.Fixture("splits-calendar.head.json")` and `Binding.Fixture("ipos-calendar.head.json")` — both files exist under `tests/FmpDotNet.Tests/Fixtures/` and both are already loaded by other tests in those same files.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test FmpDotNet.slnx --filter "FullyQualifiedName~DividendTests|FullyQualifiedName~StockSplitTests|FullyQualifiedName~IpoTests"`

Expected: the three dividend tests FAIL. `A_full_page_of_dividends_is_followed_by_the_next_one` fails with `Assert.Equal() Failure: Expected 9325, Actual 4000`. The two guard tests PASS immediately, because neither path walks yet — that is correct for a guard over existing behaviour. **Prove each guard can fail** before moving on: temporarily point `GetSplitsCalendarAsync` at `WalkAsync` and watch its guard fail, then revert. A guard you never saw fail is a guard you have not tested.

- [ ] **Step 3: Rewire `GetDividendsCalendarAsync`**

In `src/FmpDotNet/Endpoints/CalendarEndpoints.cs`, add next to `MaxCalendarPages`:

```csharp
    /// <summary>FMP's undocumented hard cap on one <c>stable/dividends-calendar</c> page. Measured 2026-08-28
    /// and again 2026-09-01: a request for the whole of 2025 answers exactly 4000 rows, and
    /// <c>limit=10000</c> is accepted and ignored. <c>page</c> is what escapes it.</summary>
    private const int DividendsCalendarRowCap = 4000;
```

Replace the fetch:

```csharp
        var (rows, walk) = await WalkAsync(
            page => new FmpRequest("stable/dividends-calendar")
                .With("from", from)
                .With("to", to)
                // Omitted on page 0, where it was measured byte-identical to sending nothing.
                .With("page", page == 0 ? (int?)null : page),
            FmpJsonContext.Default.ListDividend,
            DividendsCalendarRowCap,
            ct).ConfigureAwait(false);
```

Leave the `earliest` and `kept` loops alone. Change the return:

```csharp
        // rowCap 4000, lookbackLimitDays null: the cap always fires first at 340-876 rows a day, so no window
        // limit is observable on this path and asserting one would be inventing evidence.
        return new CalendarResult<Dividend>(
            kept, walk, from, to, earliest, rowCap: DividendsCalendarRowCap, lookbackLimitDays: null);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build FmpDotNet.slnx -warnaserror && dotnet test FmpDotNet.slnx`

Expected: PASS. `DividendTests.The_calendar_hits_its_own_path_carrying_from_and_to_only` (around line 159) must still pass unchanged, for the same reason as the earnings query tests: its stubbed body is short, so the walk makes one request.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet/Endpoints/CalendarEndpoints.cs tests/FmpDotNet.Tests/DividendTests.cs \
        tests/FmpDotNet.Tests/StockSplitTests.cs tests/FmpDotNet.Tests/IpoTests.cs
git commit -m "feat(calendar): walk the page cursor on dividends-calendar, and pin that its two siblings do not (#49)

Measured 2026-09-01: May 2026 answers 4000 / 4000 / 1325 / 0, and the whole of 2025 is 8 requests and 28,104
rows. This method returned 4000 of each -- 43% and 14%.

The two other date-ranged calendars stay un-walked, each for its own measured reason, and each now has a test
saying so. splits-calendar answers page=1 with an empty array: its limit is a 90-day lookback window and no
cursor reaches outside it. ipos-calendar answers page=5 byte-identically to page=0 -- every page full, every
page the first -- so a walk there would never terminate. That second one is why WalkAsync carries a repeat
terminator.

Refs #49

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 4: Correct every claim this change makes false

No behaviour changes here. Six doc sites currently assert something measured false, and a repository whose one principle is "a claim should have a measurement behind it" cannot leave them.

**Files:**
- Modify: `src/FmpDotNet/Endpoints/CalendarEndpoints.cs` (four doc comments)
- Modify: `src/FmpDotNet/Models/EarningsCalendarResult.cs` (`RowCap` remarks, `IsLikelyTruncated` remarks)
- Modify: `src/FmpDotNet/Models/CalendarResult.cs` (class remarks)
- Modify: `src/FmpDotNet/FmpClient.cs` (the `Calendar` property remarks)
- Modify: `tests/FmpDotNet.SmokeTests/Probe.cs` (the property census)
- Modify: `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` (two justifying comments)
- Modify: `docs/superpowers/specs/2026-09-01-query-parameter-audit-measurements.md`

**Interfaces:** none — documentation only.

- [ ] **Step 1: `GetEarningsCalendarAsync` remarks**

Two paragraphs change. In paragraph **1**, the sentence "This method detects the truncation" and the closing "**Day-at-a-time is the only chunk width measured to be safe**…" both describe a method that no longer only detects. Replace the trailing bold sentence with:

```
/// <b>The cap is escapable and this method now escapes it</b> — see below. What the cap still costs is a
/// request per 4000 rows: measured 2026-09-01, the first half of 2025 is 45,765 rows over 12 requests.
```

Replace the whole "**There IS a cursor, this method does not use it…**" paragraph with:

```
    /// <para><b>2. This method walks the cursor, and the walk is not lossless.</b> Measured 2026-09-01 (#49):
    /// <c>page</c> is honoured here even though <c>limit</c> is not, so <c>from=2026-05-13&amp;to=2026-05-19</c>
    /// answers 4000 rows on page 0, 2496 on page 1 and 0 on page 2 — <b>6496 in total, where this method used
    /// to return 4000</b>. It now fetches all of them, at one request per page.</para>
    ///
    /// <para><b>But a page seam loses rows, and the loss is reported rather than repaired.</b> FMP orders
    /// these responses by <c>date</c> and by nothing else, and a seam always falls <i>inside</i> a date — all
    /// 22 measured. So some rows arrive on both sides of a seam and an equal number of different rows arrive
    /// on neither: measured over the first half of 2025, 1,166 rows across 7 of 11 seams, deterministic across
    /// re-fetches. <see cref="EarningsCalendarResult.SeamDuplicateRows"/> counts them, and seven seams
    /// re-checked a day at a time put that count at exactly the number of rows lost — 381/381, 174/174,
    /// 315/315, and three clean seams losing none. <b>A range narrow enough to fit one page has no seam and
    /// cannot lose anything</b>, which is what to fall back on when
    /// <see cref="EarningsCalendarResult.LikelyTruncated"/> fires.</para>
```

- [ ] **Step 2: `GetDividendsCalendarAsync` remarks**

Same treatment. Replace the "**There IS a cursor, this method does not use it…**" paragraph with:

```
    /// <para><b>This method walks the cursor, and the walk is not lossless.</b> Measured 2026-09-01 (#49):
    /// <c>page</c> is honoured here even though <c>limit</c> is not. May 2026 answers 4000 rows on page 0,
    /// 4000 on page 1 and 1325 on page 2 — <b>9325 where this method used to return 4000</b> — and the whole
    /// of 2025 is <b>28,104 rows over 8 requests</b> against the same 4000. All of them are now fetched.</para>
    ///
    /// <para><b>A page seam loses rows.</b> Over that 2025 walk, 913 rows arrived on both sides of a seam and
    /// 913 different rows arrived on neither, deterministically — re-fetching the year's first two pages
    /// minutes later returned byte-identical responses carrying the identical 381-row overlap.
    /// <see cref="CalendarResult{T}.SeamDuplicateRows"/> counts them and, measured, that count is the number
    /// lost. A range that fits one page has no seam; that is the remedy when
    /// <see cref="CalendarResult{T}.LikelyTruncated"/> fires.</para>
```

Also correct the "**A safe width cannot be read off the calendar**" paragraph's last sentence — "which is exactly why this method reports rather than guesses a chunk size" — to "…which is why this method walks rather than guesses a chunk size, and reports the seam rather than hiding it."

- [ ] **Step 3: `GetIpoCalendarAsync` remarks**

Add a paragraph after the existing 90-day one. This is a new finding, not a correction:

```
    /// <para><b><c>page</c> is accepted here and does nothing at all</b>, which is worth stating precisely
    /// because the other three date-ranged calendars each do something different with it. Measured 2026-09-01
    /// (#49): <c>from=2026-01-01&amp;to=2026-08-31</c> answers 439 rows, and <c>page=1</c> and <c>page=5</c>
    /// answer the <b>same 439 rows, byte-identically</b>. Compare <c>splits-calendar</c>, where <c>page=1</c>
    /// is an empty array, and <see cref="GetEarningsCalendarAsync"/> and
    /// <see cref="GetDividendsCalendarAsync"/>, where it is a working cursor those two methods walk. A walk
    /// here would never terminate — every page is full and every page is the first — so this method makes one
    /// request and the limit stays the 90-day window.</para>
```

- [ ] **Step 4: `GetSplitsCalendarAsync` remarks**

Its "**`page` does not rescue this one**" paragraph is still true. Update only the clause that describes its siblings as un-fixed — "where it pages past their 4000-row cap (#49)" — to "where it pages past their 4000-row cap, which those two methods now walk (#49)".

- [ ] **Step 5: `EarningsCalendarResult.RowCap` and `IsLikelyTruncated`**

`RowCap`'s second paragraph currently ends "Until `GetEarningsCalendarAsync` sends `page`, the detectors on this type stay exactly as useful as they were… See #49." Replace that paragraph's last two sentences with:

```
    /// <para>It now sends it. Reaching <see cref="RowCap"/> on a page means "there is another page", and the
    /// walk fetches it — so <see cref="AtRowCap"/> reads the <i>last</i> page rather than the only one, and
    /// what a caller must watch instead is <see cref="SeamDuplicateRows"/>, the rows a page seam swallowed
    /// (#49).</para>
```

`IsLikelyTruncated`'s remarks describe the fallback `Count >= RowCap` for a foreign list. Add:

```
    /// <para><b>The fallback got weaker when this path started walking (#49).</b> A walked result is not a
    /// multiple of <see cref="RowCap"/> — a measured week is 6,496 rows and a measured half-year 45,765 — so a
    /// bare list of those rows reads as complete on a count test even where the walk lost rows at a seam. It
    /// under-reports by construction. Hold the <see cref="EarningsCalendarResult"/> itself, or test each chunk
    /// as it arrives.</para>
```

- [ ] **Step 6: `CalendarResult<T>` class remarks**

Its "**Two different mechanisms, measured 2026-08-28, and they need different tells**" list becomes three. Change the heading to "**Three different mechanisms, and they need different tells**", leave the two existing bullets, and add:

```
/// <item><description><c>dividends-calendar</c> also has a <b>cursor with an unstable seam</b>, measured
/// 2026-09-01. Walking it past the cap recovers most of a wide range — 28,104 rows for 2025 against the 4000
/// one request answers — but a seam falls inside a date, so some rows arrive twice and an equal number never
/// arrive. <see cref="SeamDuplicateRows"/> is the tell, and it is the only one of the three that a row count
/// and a date comparison both miss.</description></item>
```

- [ ] **Step 7: `FmpClient.Calendar` remarks**

Add one sentence to the property's existing summary, naming which methods walk, in the style of the other facade summaries (each opens with a bolded measured warning):

```
/// <para><b>Two of the nine methods make more than one request.</b>
/// <see cref="CalendarEndpoints.GetEarningsCalendarAsync"/> and
/// <see cref="CalendarEndpoints.GetDividendsCalendarAsync"/> walk FMP's <c>page</c> cursor past a 4000-row
/// cap — a full year of dividends is 8 requests — and both report a seam defect that costs about 3% of a wide
/// range. Read either method before asking for a range wider than a page.</para>
```

- [ ] **Step 8: The smoke-test property census**

In `tests/FmpDotNet.SmokeTests/Probe.cs`, the `Populated` doc says **Nineteen** properties are non-nullable value types, eight of them on `CalendarResult<T>` and seven on `EarningsCalendarResult`. This change adds `PagesFetched` and `SeamDuplicateRows` to both. Update the count to **twenty-three**, the `CalendarResult<T>` list to ten names — adding `PagesFetched` and `SeamDuplicateRows` — and "seven of those same names" to "nine of those same names". Verify the arithmetic against the file rather than trusting this plan: 10 + 9 + 4 others = 23.

- [ ] **Step 9: The sweep window justifications**

In `tests/FmpDotNet.SmokeTests/SweepCoverageTests.cs` and `tests/FmpDotNet.SmokeTests/Probe.cs`, two comments justify the narrow earnings-calendar window by quoting "day-at-a-time as the only chunk width measured to be safe". **The windows do not change** — a one-day earnings window and a one-week dividends window are still the right probes — but the reason does. Replace that justification with:

```
// Its own doc used to record day-at-a-time as "the only chunk width measured to be safe". Since #49 the
// method walks the cursor instead, so a wider window is no longer WRONG -- it is expensive. A 7-day
// peak-season window measured 3676 rows, and a week that crossed the cap would cost the sweep an extra
// request per 4000 rows on every run. One day keeps the sweep to one request.
```

- [ ] **Step 10: The #46 measurements correction**

In `docs/superpowers/specs/2026-09-01-query-parameter-audit-measurements.md`, leave the "clean partition" claim in place and add a blockquote directly beneath it — the same treatment `2026-08-27-endpoint-inventory.md` gave its 403 note in #55:

```markdown
> **The partition is clean on these two windows and not in general — corrected 2026-09-01 (#49).** Both
> windows above were re-verified against single-day requests and do lose nothing. On a walk with more seams
> they do: the 2025 dividends year duplicates 913 rows across 7 seams and loses 913 others, deterministically.
> See [the calendar paging measurements](2026-09-01-calendar-paging-measurements.md). The claim is kept
> because it was a correct reading of what it looked at, and deleting it would erase why the wrong conclusion
> was reasonable.
```

- [ ] **Step 11: Build, test, and commit**

Run: `dotnet build FmpDotNet.slnx -warnaserror && dotnet test FmpDotNet.slnx`

Expected: PASS. Doc comments are compiled — a broken `<see cref="…"/>` is a build error under `-warnaserror`, which is the point of running the build here.

```bash
git add -A src/ tests/ docs/
git commit -m "docs(calendar): correct the six claims that paging makes false (#49)

Both calendar methods' remarks said the cap could only be detected, and EarningsCalendarResult.RowCap said the
detectors would stay as useful as they were until page was sent. It is sent. Day-at-a-time drops from standing
requirement to the remedy when the tell fires, and it is now the only width measured lossless.

Three additions rather than corrections. GetIpoCalendarAsync gains the finding that page is accepted and does
nothing there -- page 5 byte-identical to page 0 -- which is why the walk carries a repeat terminator.
CalendarResult<T>'s two mechanisms become three. And the #46 measurements keep their 'clean partition' claim
with a correction note pointing at the new measurements, the same treatment the endpoint inventory's 403 note
got in #55: it was a correct reading of what it looked at.

The smoke sweep's narrow earnings window stays, for a new reason -- a wider one is no longer wrong, it is an
extra request per 4000 rows on every run.

Refs #49, #46

Claude-Session: https://claude.ai/code/session_019SRWzUTmqwLZcGA5yxL1Xy"
```

---

### Task 5: Live verification, the three steps people skip, and the PR

**Files:**
- Modify: `README.md` (regenerated, expected to be unchanged)
- Modify: smoke baseline, if the recorded diff warrants it

**Interfaces:** none.

- [ ] **Step 1: Regenerate the README coverage table**

Run: `FMPDOTNET_UPDATE_README=1 dotnet test FmpDotNet.slnx`

Expected: **no change to `README.md`.** This task adds no endpoint. Run `git diff --stat README.md` and confirm it is empty. If it is not, something in Task 1–3 changed a method signature the generator reads, and that is a finding to investigate before continuing.

- [ ] **Step 2: Run the live smoke suite**

Run: `FMP_API_KEY=$(grep '^FMP_API_KEY=' .env | cut -d= -f2-) dotnet test tests/FmpDotNet.SmokeTests`

Never echo the key and never paste it into a URL. Expected: 22 smoke tests green.

- [ ] **Step 3: Read the smoke baseline diff before re-recording it**

Run: `git diff -- tests/FmpDotNet.SmokeTests/`

Two changes are expected and correct: `PagesFetched` and `SeamDuplicateRows` appear as newly-observed properties on the two calendar results. Anything else — a row count moving, a property that stopped arriving — is a real finding and must be understood before it is recorded. Re-record only after reading, per the wiki's Live Smoke Suite page.

- [ ] **Step 4: Verify the walk against the live API once, by hand**

This is the measurement that proves the feature end to end, and it is the one thing the offline suite cannot do. Write a scratch console call — in the scratchpad, not the repository — that runs:

```csharp
var rows = await fmp.Calendar.GetDividendsCalendarAsync(new LocalDate(2025, 1, 1), new LocalDate(2025, 12, 31));
var result = (CalendarResult<Dividend>)rows;
Console.WriteLine($"{result.Count} rows, {result.PagesFetched} pages, {result.SeamDuplicateRows} seam duplicates, truncated={result.LikelyTruncated}");
```

Expected, from the 2026-09-01 measurements: **8 pages, 28,104 raw rows, roughly 913 seam duplicates, `LikelyTruncated` true.** The row counts will have drifted by a few — FMP keeps ingesting — so treat the page count and the order of magnitude of the seam count as the assertion, not the exact figures. Record what you actually saw in the PR body. Delete the scratch project afterwards.

- [ ] **Step 5: Full green, then push and open the PR**

```bash
dotnet build FmpDotNet.slnx -warnaserror
dotnet test FmpDotNet.slnx
git push -u origin fix/calendar-paging
```

Open the PR with `gh pr create`, body covering: what was returned before and after on the four measured ranges; the seam defect and the 1:1 duplicate-to-loss equality with the seven seams behind it; the three terminators and why the middle one exists; what `AtRowCap` used to mean and what it means now; and the live figures from Step 4. Link `#49`, and note that `#46`'s partition claim is corrected rather than deleted.

Wait for **`.NET — build + test`** to go green before merging. `mergeStateStatus=BLOCKED` on the unsigned-commit rule is expected and is not a failing check.

---

## Self-Review

**Spec coverage.** Every section of the design maps to a task: the walk and its three terminators → Task 2, the two cheap ones through the earnings method and the two expensive ones directly in `CalendarWalkTests`; the two paths that must not walk → Task 3, with a guard test each; the result-type table → Task 1, all six rows; the day-at-a-time demotion → Task 4 Steps 1–2; the six documentation sites → Task 4 Steps 1–7; the twelve-row testing table → distributed across Tasks 1–3, with the generated-full-page decision honoured by extending the existing `SyntheticCalendar` helper rather than adding a production knob; the live sweep → Task 5. The design's *Out of scope* items are absent from the plan, correctly.

**One place the plan departs from the design, deliberately.** The design's testing table put "`MaxCalendarPages` bounds the walk" alongside the other endpoint-level tests. At the real 4000-row cap that assertion costs 400,000 parsed rows, so the plan makes `WalkAsync` `internal` and tests it at `rowCap: 2` instead. That is a wider visibility than the design implied and it is the plan's call, not the spec's — it is called out here so a reviewer can reject it without reading the diff. The alternative considered and rejected was an injectable page size, which is production code that exists only for tests and would let the suite pass against a walk comparing to the wrong number.

**One thing the design left implicit and this plan decides.** The design does not say whether the walk sends `page=0` on its first request. It omits it, because `page=0` was measured byte-identical to sending nothing, and omitting keeps every existing query-shape assertion — four in `CalendarEndpointsTests`, one in `DividendTests` — passing unchanged, along with every caller's cache key and log line. Task 2 Step 1 tests it explicitly.

**Type consistency.** `CalendarWalk(int RowsReturned, int PagesFetched, int LastPageRowCount, int SeamDuplicateRows)` is used with those exact member names in Tasks 1, 2 and 3, including in two `Assert.Equal` calls that compare whole `CalendarWalk` values — which is only legal because it is a `record struct` and gets structural equality. `CalendarWalk.Single(int)` is defined in Task 1 Step 3 and consumed in Task 1 Step 6. `WalkAsync<T>` returns `(List<T> Rows, CalendarWalk Walk)` in Task 2 Step 3 and is destructured as `var (rows, walk)` in Task 2 Step 1's helper, Task 2 Step 4 and Task 3 Step 3. `MaxCalendarPages` is `public` and asserted from `CalendarWalkTests`; `WalkAsync` is `internal` and reached the same way; `DividendsCalendarRowCap` is `private`, used only inside the class.

**Helpers and fixtures were verified against the files rather than assumed.** `Build(string body = "[]")` and `Day(int, int, int)` exist in `DividendTests`, `StockSplitTests`, `IpoTests` and `CalendarEndpointsTests`; the first three load captures through `Binding.Fixture(...)` while `CalendarEndpointsTests` has a local `Fixture(...)`; `splits-calendar.head.json` and `ipos-calendar.head.json` both exist and are already loaded by neighbouring tests; and `SyntheticCalendar` already generates full pages programmatically, so Task 2 extends it rather than introducing the idea.

**Known cost of the `AtRowCap` change.** Any caller reading `AtRowCap` on a *paged* result to mean "the response held 4000 rows" now reads something different. That is the intended change and is documented on the property, but it is the one place where a consumer could be silently affected, so the PR body must call it out.
