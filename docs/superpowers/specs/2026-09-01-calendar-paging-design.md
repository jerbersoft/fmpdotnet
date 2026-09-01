# Paging the calendar endpoints — design

What issue [#49](https://github.com/jerbersoft/fmpdotnet/issues/49) fixes: `GetEarningsCalendarAsync` and
`GetDividendsCalendarAsync` return the first 4000 rows of a range and stop, when the range holds several times
that and FMP will serve the rest. No new endpoint, no change to the coverage count.

Every fact this document argues from was measured against the live API on 2026-09-01 and is recorded in
[the measurements](2026-09-01-calendar-paging-measurements.md). Nothing here was read from FMP's
documentation.

## The decision, and what makes it awkward

**Page internally.** Both methods walk `page=0, 1, 2, …` until a page comes back short, and return the
concatenation. A caller asking for a year of dividends goes from **14% of the range to about 97%** and their
code does not change.

The awkward part is the 3%. `page` is applied to a result ordered by `date` alone, so at a page seam — which
always falls *inside* a date, in all 22 seams measured — some rows are served on both sides and an equal
number of different rows are served on neither. Measured on the 2025 dividends year: 913 rows duplicated, 913
rows lost, deterministic across re-fetches. So paging does not make these endpoints complete, and a design
that quietly claimed it did would replace a loud defect with a silent one.

That is the tension this design resolves, and it resolves it the way the rest of this SDK resolves such
things: **serve what FMP served, and hand the caller the evidence.**

### Why not the three alternatives

- **Leave it and document.** Rejected: a path returning 14% of a year is not a documentation problem.
- **Expose a `page` parameter and let the caller loop.** Rejected. It matches the house style for the
  `-latest` feeds (`CongressEndpoints`, `NewsEndpoints`, `DirectoryEndpoints`), but those feeds have no
  natural whole — a date range does, and it is the only thing anyone asks these two methods for. It also
  leaves the silent loss as the default for every caller who does not read the remarks, which is the bug.
- **Chunk by day and never page.** Day-at-a-time is measured lossless, and it is 365 requests for a year
  against 8. Kept as the documented remedy when the tell fires, not as the default.

## Two of the four calendars page, and the other two must not

| method | behaviour | this change |
|---|---|---|
| `GetEarningsCalendarAsync` | 4000-row cap, `page` works | **walks** |
| `GetDividendsCalendarAsync` | 4000-row cap, `page` works | **walks** |
| `GetSplitsCalendarAsync` | 90-day lookback, `page=1` empty | unchanged |
| `GetIpoCalendarAsync` | 90-day lookback, **`page` ignored entirely** | unchanged — and see below |

`ipos-calendar` answers `page=1` and `page=5` byte-identically to `page=0`. A walk that terminates on a short
page would never terminate there: every page is full and every page is the first. That is the same shape
`FmpClient` already warns about on `stable/fmp-articles` — "no page ceiling and repeats its last page for
ever" — so the guard below is written against a measured hazard on a sibling path, not an imagined one.

## The walk

One private helper on `CalendarEndpoints`, used by both methods. It returns the raw pages rather than a
flattened list, because the seam evidence is computed between pages and cannot be recovered after
concatenation.

```
page 0, 1, 2, …
  stop when a page returns fewer than RowCap rows          (measured: a short page is the last page)
  stop when a page is row-for-row identical to the previous (the ipos-calendar shape; page count unbounded otherwise)
  stop at MaxCalendarPages                                  (belt; a fired ceiling is a truncation tell)
```

Three terminators, and each earns its place:

- **Short page.** The measured rule, and it holds on all four walks including the two long ones. A page past
  the end is `[]`, which is short, so the walk needs no ceiling handling — unlike
  `StreamLatestStatementsAsync`, where page 101 is an HTTP 400 and the bound is load-bearing.
- **Repeat.** Cheap given the seam intersection is being computed anyway: a page whose intersection with its
  predecessor is the whole of both pages is a repeat. This is the terminator that would save a caller if FMP
  ever gave `earnings-calendar` the behaviour `ipos-calendar` has today.
- **Ceiling.** `MaxCalendarPages = 100`, a public constant, documented as a guard rather than a measurement.
  100 pages is 400,000 rows — about fourteen years of dividends at the measured 28,104 rows a year — so it is
  not reachable by a request anyone means to make. Hitting it sets the truncation tell rather than throwing,
  because the rows already fetched are real and the SDK reports rather than fails.

**Rows are concatenated in walk order and otherwise untouched.** Not sorted, not de-duplicated. Duplicates at
a seam are FMP's rows, served twice by FMP; removing them would be a guess about which of two byte-identical
rows is the real one, and FMP's data contains genuine duplicate rows (page 5 of the dividends walk holds 4000
rows and 3999 distinct ones). The existing per-row filter — drop a row whose `date` will not parse, clamp to
range on the earnings method when asked — applies to the concatenation exactly as it applies to one page
today.

## The result types

`EarningsCalendarResult` and `CalendarResult<T>` each gain two properties, and one existing property changes
meaning. Both are shipped public API, so the change is additive except where it cannot be.

| member | before | after |
|---|---|---|
| `RowsReturned` | rows in the one response | rows across every page fetched |
| `PagesFetched` | — | **new.** 1 on a path that does not page, so it is never 0 |
| `SeamDuplicateRows` | — | **new.** Rows appearing on both sides of a page seam, summed over seams |
| `AtRowCap` | `RowsReturned >= RowCap` | **the last page fetched** came back at the cap |
| `LikelyTruncated` | `AtRowCap \|\| …` | adds `SeamDuplicateRows > 0` |
| `MissesStartOfRange` | unchanged | unchanged, and now usually quiet on the two paged paths |

**`AtRowCap` is the one that changes meaning, and it has to.** A page-0 count of 4000 used to mean "rows are
gone". After this change it means "there was another page", and the walk fetched it. Computed against the
*last* page instead, it keeps its old sense exactly — the walk stopped with a full page in hand, so something
is still behind it — and on the two non-paging paths, where `PagesFetched` is 1, first page and last page are
the same page and nothing about its behaviour moves at all.

**`SeamDuplicateRows` is a loss counter, not a duplicate counter.** Measured on seven seams in both
directions, an overlapping seam loses exactly as many rows as it duplicates and a clean seam loses none. It
over-reports rather than under-reports: a pair of byte-identical rows in FMP's own data straddling a seam
would be counted without a loss behind it. That bias is stated on the property.

`IsLikelyTruncated(IReadOnlyList<T>)` keeps both its current shapes. `EarningsCalendarResult`'s fallback for a
foreign list — `Count >= 4000` — stays correct but becomes much weaker, since a walked result of 28,104 rows
is not a multiple of anything: the doc gains a line saying so, and saying that the fallback now under-reports
by construction.

## What happens to the day-at-a-time advice

It survives, demoted from *requirement* to *remedy*, and it is now the only remedy that is measured lossless.

The current remarks say a caller must chunk day-at-a-time because nothing else was measured safe. After this
change the sequence is: call it for the range you want; if `LikelyTruncated` is false you have the range; if
it is true, `SeamDuplicateRows` says roughly how many rows are missing and re-requesting narrower — narrow
enough to fit one page, which has no seam — is what recovers them. A single-day request cannot lose anything,
because there is nothing to cut.

The 90-day advice on `GetSplitsCalendarAsync` and `GetIpoCalendarAsync` is untouched; those paths are clamped
by a window, not a cap, and no cursor reaches outside it.

## Documentation to correct

Six places currently assert something this change makes false, and each is corrected with the measurement
behind it:

1. **`GetEarningsCalendarAsync` remarks** — the "day-at-a-time is the only chunk width measured to be safe"
   paragraph, and the #46 paragraph that says the fix is deferred.
2. **`GetDividendsCalendarAsync` remarks** — the same pair.
3. **`GetIpoCalendarAsync` remarks** — gains the new finding: `page` is accepted and ignored here, which is
   worth stating precisely because its two siblings behave two other ways.
4. **`EarningsCalendarResult.RowCap`** — its second paragraph says "until `GetEarningsCalendarAsync` sends
   `page`, the detectors on this type stay exactly as useful as they were". They do not, after this.
5. **`CalendarResult<T>` class remarks** — the "two different mechanisms" list becomes three, the third being
   a cursor with an unstable seam.
6. **`CalendarEndpoints` class remarks** and `FmpClient.Calendar` — the drop-undated-rows note is unaffected,
   but the group summary should name which two methods walk.

The #46 measurements document keeps its "clean partition" claim, with a correction note pointing here — the
same treatment the endpoint inventory's 403 note received in #55. It was a correct reading of what it looked
at, and deleting it would erase why the wrong conclusion was reasonable.

## Testing

Offline, in `CalendarEndpointsTests`, driven by `StubHandler`'s response queue — which already serves a
different canned body per request, so a multi-page walk is expressible without new infrastructure:

| test | what would break it |
|---|---|
| a 4000-row page followed by a short page produces one concatenated list | the walk stops at page 0 |
| the walk sends `page=0`, `page=1`, … in order, and nothing else changes in the query | a request built wrong |
| a single short page issues exactly one request | the walk pages a range that fits, doubling every small call |
| an empty second page ends the walk and contributes nothing | an off-by-one that appends `[]` or loops |
| a page identical to its predecessor ends the walk | the `ipos-calendar` shape looping for ever |
| `MaxCalendarPages` bounds the walk and sets `LikelyTruncated` | an unbounded loop against a pathological feed |
| rows shared across a seam are counted in `SeamDuplicateRows` and **left in the list** | a dedupe creeping in |
| `SeamDuplicateRows > 0` sets `LikelyTruncated` even when the last page is short | the tell failing on the case it exists for |
| `AtRowCap` reads the last page, not the first | the old semantics surviving the change |
| `RowsReturned` sums the pages while `Count` reflects what survived the filter | the two being confused |
| undated rows are dropped across the whole walk, not per page | a filter applied at the wrong level |
| `clampToRange` on the earnings method clamps the concatenation | same |

The fixtures are the real captured shapes, trimmed: a 4000-row page is expressed as a page at whatever cap the
test injects rather than by shipping 4000 rows, since the cap is a constant the walk reads.

**Live**, in the smoke sweep: both methods already appear there. The sweep assertion gains the property that a
walked result's `RowsReturned` is at least its page-0 count — which is the whole claim, and is true of a
one-page range too.

## Out of scope

- **Recovering the lost 3%.** Re-requesting a seam date day-at-a-time and splicing it in is a real option and
  a much larger contract; it is worth its own issue if `SeamDuplicateRows` turns out to bother anyone.
- **Folding `EarningsCalendarResult` into `CalendarResult<T>`.** Still public API surgery on a shipped path,
  still a separate decision, and this change makes the two types' member lists match more closely rather than
  less.
- **`splits-calendar` and `ipos-calendar` behaviour.** Documentation only.
