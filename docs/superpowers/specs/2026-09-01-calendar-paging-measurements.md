# Paging the calendar endpoints past their 4000-row cap — measurements

Issue #49. Probed against the live FMP API on **2026-09-01 UTC**. Every number below came from a response
this session fetched; nothing is taken from FMP's documentation, and the one claim inherited from #46 is
**corrected here rather than repeated**.

The API key travels in the query string, so no built URL appears in this document and no capture was kept in
the repository.

## The correction that matters

#46 recorded, and #49 repeats, that the pages are a clean partition:

> **The pages are a clean partition — verified, not assumed.** Comparing `(date, symbol)` sets: earnings page
> 0 ∩ page 1 = **0 rows** […] No row is served twice and none is dropped at a boundary.

**That is true of the two short windows it was measured on and false at scale.** On a walk long enough to have
several seams, adjacent pages overlap, and for every row served twice a *different* row is never served at
all. It is not a race: the year's pages 0 and 1 were re-fetched and came back **byte-identical**, carrying the
identical defect.

So `page` does not make these endpoints complete. It makes them **97% instead of 14%**, with the shortfall
invisible unless something looks for it. Section *The seam defect* below is the whole of the finding, and
*The detector* is why the shortfall need not stay invisible.

## `page` is honoured, and the cap is escapable

Confirming #46 on freshly-fetched data. The counts drift by a row or two against #46's figures because FMP
keeps ingesting; the shape is identical.

| request | p0 | p1 | p2 | p3 | walk total | SDK returns today |
|---|---|---|---|---|---|---|
| `earnings-calendar?from=2026-05-13&to=2026-05-19` | **4000** | 2496 | 0 | 0 | **6496** | 4000 — **62%** |
| `dividends-calendar?from=2026-05-01&to=2026-05-31` | **4000** | 4000 | 1325 | 0 | **9325** | 4000 — **43%** |
| `dividends-calendar?from=2025-01-01&to=2025-12-31` | 4000 × 7 | | | 104 | **28,104** over 8 requests | 4000 — **14%** |
| `earnings-calendar?from=2025-01-01&to=2025-06-30` | 4000 × 11 | | | 1765 | **45,765** over 12 requests | 4000 — **8.7%** |

Four properties of the cursor, all measured this session:

- **`page=0` is byte-identical to sending no `page` at all.** SHA-256 equal on
  `earnings-calendar?from=2026-05-13&to=2026-05-13`. Adding the parameter changes nothing on the first page,
  so a walk cannot alter what a single-page caller already receives.
- **There is no page ceiling and no error past the end.** `page=101` and `page=1000` both answer `[]` under
  HTTP 200 on both paths. Compare `stable/latest-financial-statements`, where `page=101` is an HTTP 400, and
  `stable/news/*`, where it is also a 400 — this family needs no ceiling handling.
- **A short page is the last page.** Held on all four walks above, including the two long ones. No walk
  produced a short page followed by a full one.
- **`page=-1` is read as page 0** — byte-identical to `page=0`. Not something the SDK will send, recorded so
  the next probe does not mistake it for a cursor that reaches backwards.

## The seam defect

**Every seam in every walk falls inside a date**, never between two: the last row of page *n* and the first
row of page *n+1* carry the same date in all 22 seams measured. FMP sorts by `date` descending and by nothing
else, so rows sharing the seam date have no defined order — and the offset that produces page *n+1* is applied
to an ordering that is not the one page *n* was cut from.

### `dividends-calendar`, 2025-01-01 → 2025-12-31, 8 pages

| seam | seam date | rows on both sides |
|---|---|---|
| p0 \| p1 | 2025-12-29 | **381** |
| p1 \| p2 | 2025-12-19 | **174** |
| p2 \| p3 | 2025-12-11 | **127** |
| p3 \| p4 | 2025-11-28 | **94** |
| p4 \| p5 | 2025-11-14 | **72** |
| p5 \| p6 | 2025-10-30 | **20** |
| p6 \| p7 | 2025-10-02 | **45** |
| | **total** | **913** |

28,104 rows returned, 913 of them served twice, leaving **27,190 distinct**.

### `earnings-calendar`, 2025-01-01 → 2025-06-30, 12 pages

| seam | seam date | rows on both sides |
|---|---|---|
| p0 \| p1 | 2025-06-16 | 21 |
| p1 \| p2 | 2025-05-28 | 206 |
| p2 \| p3 | 2025-05-19 | 71 |
| p3 \| p4 | 2025-05-14 | **315** |
| p4 \| p5 | 2025-05-12 | 272 |
| p5 \| p6 | 2025-05-08 | 171 |
| p6 \| p7 | 2025-05-02 | 110 |
| p7 \| p8 … p10 \| p11 | 04-29, 04-27, 04-23, 04-14 | **0** each |
| | **total** | **1166** |

45,765 rows returned, 1,166 served twice, leaving **44,598 distinct**. Four of the eleven seams are clean, so
this is not a property of long walks as such — it fires per seam.

### An overlapping seam loses exactly as many rows as it duplicates

This is the part that turns the defect from "some duplicates" into "silent data loss". Each seam date was
re-requested on its own — a single-day request, which fits in one page and therefore has no seam — and the
walk's rows for that date were compared against it.

| path | seam date | single-day rows | walk had | **missing** | seam overlap |
|---|---|---|---|---|---|
| dividends | 2025-12-29 | 1772 | 1391 | **381** | 381 |
| dividends | 2025-12-19 | 857 | 683 | **174** | 174 |
| earnings | 2025-05-14 | 2043 | 1728 | **315** | 315 |
| earnings | 2025-04-29 | 2659 | 2659 | **0** | 0 |
| earnings | 2025-04-23 | 1057 | 1057 | **0** | 0 |

In every case the count of rows the walk never saw equals the count it served twice. Not one row appeared in
the walk that the single-day request did not have, so nothing is invented — rows are exchanged, one for one,
across the seam.

The rows are reachable; only this walk misses them. `dividends-calendar?from=2025-12-29&to=2025-12-30` answers
3244 rows of which **1772 are dated 2025-12-29** — the full set, from a request that spans the same date the
year-long walk cut in half.

**Deterministic, not a race.** Pages 0 and 1 of the 2025 dividends range were fetched a second time, minutes
later, and both were byte-identical to the first fetch, with the same 381-row overlap. Two requests made
seconds apart cannot be blamed for it.

## The detector

**Intersect adjacent pages.** Where the intersection is empty the seam is lossless, and where it is not, its
size *is* the number of rows lost. Seven seams checked directly against single-day requests, in both
directions, with no counterexample:

- overlap > 0 → loss = overlap (381/381, 174/174, 315/315)
- overlap = 0 → loss = 0 (2025-04-29, 2025-04-23, and both short walks below)

It is cheap for this SDK specifically: `Dividend` and `EarningsCalendarEntry` are `sealed record`s, so
structural equality is compiler-generated and the intersection is one `HashSet` pass per page.

**It is an estimator with a known bias, not a proof.** FMP's own data carries byte-identical duplicate rows —
page 5 of the dividends walk holds 4000 rows of which 3999 are distinct, and
`earnings-calendar?from=2026-05-13&to=2026-05-19` page 0 carries two `(date, symbol)` pairs twice, with
genuinely different values:

```
{"symbol":"688347.SS","date":"2026-05-14","epsActual":0.0828,"revenueActual":4560305000, …}
{"symbol":"688347.SS","date":"2026-05-14","epsActual":0.012,  "revenueActual":660925000,  …}
```

A pair of *byte-identical* rows straddling a seam would be counted as an overlap without a loss behind it. No
such case appeared in 22 seams, and the bias runs the safe way — it over-reports the shortfall rather than
under-reporting it.

## Short walks are clean, and that is why #46 read the way it did

Both windows #46 measured were verified here against single-day requests and lose nothing:

| walk | seams | overlap | seam date checked | missing |
|---|---|---|---|---|
| `earnings-calendar` 2026-05-13 → 05-19 | 1 | 0 | 2026-05-14 (2028 rows) | 0 |
| `dividends-calendar` 2026-05-01 → 05-31 | 2 | 0 | 2026-05-21 (710 rows) | 0 |

So #46's partition check was correct about what it looked at. It generalised from two clean walks, and the
generalisation is what failed.

## Only two of the four calendars page

The issue assumed splits was the exception. It is not the only one.

| path | `page=1` | verdict |
|---|---|---|
| `earnings-calendar` | fresh rows | **pages** |
| `dividends-calendar` | fresh rows | **pages** |
| `splits-calendar` | `[]` (940 rows on page 0, whole range) | no cursor; nothing to page past |
| `ipos-calendar` | **byte-identical to page 0** | **ignores `page` entirely** |

`ipos-calendar?from=2026-01-01&to=2026-08-31` answers 439 rows on page 0, and `page=1` and `page=5` return the
*same 439 rows*, SHA-256 identical. A walk that stops on a short page would never stop here: every page is
full-length and every page is the first one. This is the same shape as `stable/fmp-articles`, which
`FmpClient` already warns "has no page ceiling and repeats its last page for ever" — so the hazard is not
hypothetical, it is live on a sibling calendar path.

`splits-calendar`'s limit is a 90-day lookback window rather than a row cap, and no cursor reaches outside it —
confirming #46. 940 rows for 2026-01-01 → 2026-08-31, earliest row 2026-06-02, `page=1` empty.

## `includeReportTimes` pages, and the complete walk proves the old claim at range scale

`earnings-calendar?from=2026-05-13&to=2026-05-19&includeReportTimes=true` walks to the same page shape —
4000, 2496, 0 — and the completed walk is the **identical 6496-row symbol multiset** as the walk without the
flag. Symmetric difference: **0**.

The date histograms differ, which is the documented re-dating:

| date | plain walk | with `includeReportTimes` |
|---|---|---|
| 2026-05-13 | 2038 | 1987 |
| 2026-05-14 | 2028 | 2014 |
| 2026-05-15 | 1535 | 1597 |
| 2026-05-16 | 32 | 35 |
| 2026-05-17 | 16 | 15 |
| 2026-05-18 | 409 | 409 |
| 2026-05-19 | 438 | 435 |
| **2026-05-20** | — | **4** |

`GetEarningsCalendarAsync`'s remarks already say the flag re-dates rows rather than adding them, measured on
one day. This is that claim at range scale, and it holds: same rows, different dates, four of them past `to`.

One consequence for paging specifically — **the flag moves the page boundary**. Plain page 1 and flagged page 1
are both 2496 rows but differ by 102 symbols (51 each way), because the 51 re-dated rows cross the seam. The
completed walks still agree exactly, which is the point: the difference is a property of where a page is cut,
not of what the range contains.

## What was not measured

- **Whether an overlapping seam can also invent a row.** Every check found the walk to be a strict subset of
  the single-day answer, so the exchange is one-for-one in the direction tested. A row appearing in the walk
  and nowhere else was never observed and was not specifically hunted for.
- **Whether the seam defect has a threshold.** Two clean short walks and two defective long ones is not enough
  to say what predicts a bad seam; four of eleven seams in the earnings walk were clean, which rules out
  "long walks are bad" as the rule. The SDK detects rather than predicts, so this was not pursued.
- **Whether `page` interacts with a `to` in the future.** All four walks used ranges FMP already holds.
