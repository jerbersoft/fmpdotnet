# Query parameters FMP accepts that the SDK never sends — measurements

Issue #46. **40 parameters across 26 paths**, probed against the live FMP API on **2026-09-01 UTC** in 154
ordinary requests. Every verdict below came from a response comparison. Nothing here is taken from FMP's
documentation, and nothing is taken from `fmpsdk`'s parameter list — that list is the thing under test.

The API key travels in the query string, so no built URL appears in this document and no capture was kept in
the repository.

## The headline is not a missing parameter

**`page` is a working cursor on `earnings-calendar` and `dividends-calendar`, and the SDK does not send it.**
`CalendarEndpoints.GetEarningsCalendarAsync` currently documents the opposite — "There is no cursor, so the SDK
cannot page around this" — and that claim is false. Callers have been receiving a fraction of busy ranges as a
well-formed HTTP 200 array indistinguishable from a complete one.

| request | page 0 | page 1 | page 2 | page 3 | total |
|---|---|---|---|---|---|
| `earnings-calendar?from=2026-05-13&to=2026-05-19` | **4000** | 2497 | 0 | 0 | **6497** |
| `dividends-calendar?from=2026-05-01&to=2026-05-31` | **4000** | 4000 | 1332 | — | **9332** |

The SDK returns the first column. That is **62%** of the earnings range and **43%** of the dividends month.

**The pages are a clean partition, verified rather than assumed.** Comparing `(date, symbol)` sets:

- earnings-calendar page 0 ∩ page 1 = **0 rows**;
- dividends-calendar page 0 ∩ page 1 = **0**, page 1 ∩ page 2 = **0**, and the union of all three is
  **9332** — exactly 4000 + 4000 + 1332, so no row is served twice and none is dropped between pages.

> **The partition is clean on these two windows and not in general — corrected 2026-09-01 (#49).** Both
> windows above were re-verified against single-day requests and do lose nothing. On a walk with more seams
> they do: the 2025 dividends year duplicates 913 rows across 7 seams and loses 913 others, deterministically.
> See [the calendar paging measurements](2026-09-01-calendar-paging-measurements.md). The claim is kept
> because it was a correct reading of what it looked at, and deleting it would erase why the wrong conclusion
> was reasonable.

**And page 1 is precisely the data the existing measurement says is lost.** `GetEarningsCalendarAsync`'s
remarks record that `from=2026-05-13&to=2026-05-19` returns 4000 rows with *no `2026-05-13` row at all*, against
2039 that the single day answers on its own. Reproduced here — page 0's date histogram starts at `2026-05-14`.
Page 1 holds **2038 rows dated `2026-05-13`** plus 459 more of `2026-05-14`. The truncation eats the front of
the range, and page 1 is the front of the range.

`limit` remains inert on both, which is why this was missed: `limit=10000` still answers exactly 4000 on each.
The earlier conclusion that the cap could not be escaped was drawn from `limit` alone.

The order is date-descending and contiguous across the boundary — dividends page 0 runs `2026-05-31` to
`2026-05-21` and page 1 opens on `2026-05-21`.

## Method

The trap this audit had to avoid is that **a parameter FMP accepts is not a parameter FMP honours**. `fmpsdk`
validates nothing it sends — its list is what FMP's documentation claims — and this repository has twice
measured the gap (`bogusParam=1&limit=3` answering as `limit=3` alone; `limit=6000` ignored on the earnings
calendar). A status check would have recorded all 40 as present.

So every probe was a comparison, over four requests per path:

| call | establishes |
|---|---|
| **N1**, **N2** — the SDK's own request, twice | the path's volatility floor |
| **X** — the same request plus `fmpAuditProbe=1` | what an *unknown* parameter does here |
| **P** — the same request plus the parameter under test | the measurement |

`X` is the discriminator and the reason this is not just a diff. FMP answers an unknown parameter with 200 and
ignores it, so "decorative" has a specific fingerprint on each path, and it has to be measured there rather
than assumed to generalise.

**All 27 base requests came back byte-identical across N1 and N2, and byte-identical again under `X`.** There
is no volatility floor on any of these paths to subtract, so raw SHA-256 equality is a sound discriminator
throughout: a parameter whose response is byte-identical to the naked one did nothing at all.

**No probe was refused.** All 154 requests answered 200 — including `fmpAuditProbe=1` on all 27 paths. The
"rejected" outcome the issue anticipated does not occur anywhere in this set.

### Filter values were seeded from the response, not guessed

A filter given a value that is absent returns nothing and looks exactly like a filter given a value it ignores.
So `date` on `insider-trading/latest` was seeded from the unfiltered page's own `filingDate`, `cik` on
`fundraising-latest` from its own row 0, `sicCode` and `industryTitle` from their own list. Filtering by a value
demonstrably present and getting the whole set back is conclusive; guessing a CIK and getting zero rows is not.

**One probe was a no-op by construction and the check caught it.** `funds/disclosure?symbol=SPY` was seeded with
row 0's `cik`, which turns out to be **SPY's own CIK on all 504 rows** — the column is the filer, not the
holding. Filtering by the only value present returns everything, so the byte-identical answer meant nothing. Re-run
as `symbol=QQQ&cik=<SPY's CIK>` it answers **0 rows**, and `symbol=SPY&cik=0000000001` also answers 0. The
parameter is honoured; the first reading would have recorded it decorative.

## Verdicts

**34 of 40 honoured, 5 decorative, 1 honoured only for a single value.** The issue's premise — that the
asymmetry against `fmpsdk` is real — holds. Its expectation that a useful share would prove decorative does not:
that share is an eighth, and four of the five are on one endpoint family.

### Group A — individual parameters

| path | parameter | verdict | measurement |
|---|---|---|---|
| `historical-chart/{interval}` | `extended` | **honoured** | AAPL 1min, 2026-08-28: **390 → 957** bars; span `09:30–15:59` → `04:00–19:59` |
| `historical-chart/{interval}` | `nonadjusted` | **honoured** | MNST 1hour across its 2:1 split of 2026-08-11: the `2026-08-04 09:30` close reads **46.52 → 93.04**, exactly 2×, while the post-split `2026-08-18 15:30` bar is unchanged at 47.40 |
| `available-exchanges` | `extended` | **honoured** | **63 → 71** exchanges; adds AQS, BUD, BVC, EGX, HOSE, KUW, RIS, TAL. Identical six fields — it widens the universe, it does not widen the row |
| `all-exchange-market-hours` | `timestamp` | **honoured** | 81 rows either way, but `isMarketOpen` moves: 42 open naked, **54** at epoch 1787925600 (2026-08-28 14:00 UTC), **1** at 1788091200 (2026-08-30 12:00 UTC, a Sunday). `openingHour`/`closingHour` read `CLOSED` for an exchange shut on that instant's local day |
| `exchange-market-hours` | `timestamp` | **honoured** | same behaviour on the single-exchange form (NASDAQ) |
| `insider-trading/latest` | `date` | **honoured for today's date only** | see below |
| `search-cik` | `limit` | **decorative by construction** | see below |
| `symbol-change` | `invalid` | **honoured** | **5469 → 1** row: `{"date":"2005-11-23","companyName":"Agilent Technologies Inc.","oldSymbol":"A","newSymbol":"AWD"}`. A separate dataset, not a filter over the same one |
| `economic-calendar` | `country` | **honoured** | 2026-08-17…24: **551 → 77**, every row `country=US` |
| `earnings` | `includeReportTimes` | **honoured** | same 165 rows, **five fields added**: `time`, `periodEnding`, `fiscalPeriod`, `fiscalYear`, `confirmed` |
| `earning-call-transcript` | `limit` | **decorative by construction** | see below |
| `fundraising-latest` | `cik` | **honoured** | **100 → 1**, seeded from row 0 |
| `funds/disclosure` | `cik` | **honoured** | `symbol=QQQ` (105 rows) + SPY's CIK → **0**; `symbol=SPY` + `cik=0000000001` → **0**. An AND filter with `symbol` |
| `funds/disclosure-dates` | `cik` | **honoured** | `symbol=QQQ` (28 rows) + SPY's CIK → **0** |
| `standard-industrial-classification-list` | `sicCode` | **honoured** | **444 → 1** |
| `standard-industrial-classification-list` | `industryTitle` | **honoured** | **444 → 1** |

#### `insider-trading/latest?date=` answers only today, and ignores every other date in silence

This is the sharpest trap in the set, and it points the opposite way from the issue's premise: the parameter is
real, it works, and using it as a date filter would be wrong.

The unfiltered page (`page=0&limit=100`) held 89 rows dated `2026-08-31` and 11 dated `2026-09-01`, the day of
the probe.

| `date=` | rows | response |
|---|---|---|
| `2026-09-01` (today) | **11** | the 11 rows of that date |
| `2026-08-31` | 100 | **byte-identical to the unfiltered page** — though 89 rows of that date are in it |
| `2026-08-30` | 100 | byte-identical |
| `2026-08-27` | 100 | byte-identical |
| `2026-01-15` | 100 | byte-identical |

Identical SHA-256 on all four, and `2026-08-31` rules out "filtered to nothing": 89 matching rows were sitting
in the page it returned unchanged. Dropping `page`/`limit` changes nothing. A caller who modelled this as
`GetLatestAsync(date)` would get a silently unfiltered page for every historical date they asked for.

#### Two `limit`s that cannot do anything, for a reason in the path rather than in FMP

`search-cik` is an exact-CIK lookup, not a search: `cik=0000320193` → 1 row, `cik=320193` → 1 row, the prefixes
`1`, `32` and `320` → **0 rows each**, and a non-numeric value → **400 `Query Error: Invalid or missing query
parameter - cik`**. It cannot return more than one row, so `limit` has nothing to bound.

`earning-call-transcript` requires `symbol`, `year` *and* `quarter` — **400** without them, individually
tested — and answers exactly one transcript. Same conclusion.

Both are honest "decorative" verdicts, but the cause is that `fmpsdk` sends `limit` to endpoints that return one
row, not that FMP discards it. Neither is worth modelling and neither is evidence about `limit` elsewhere.

### Group B — the paging cluster

The issue framed this as one decision. It is three: `page` works and matters on the calendars, works with
ordinary semantics on the Congress trade paths, means something else entirely on one path, and is inert on one
more.

| path | parameter | verdict | measurement |
|---|---|---|---|
| `earnings-calendar` | `page` | **honoured — data is being lost today** | 4000 / 2497 / 0, disjoint. See the headline |
| `dividends-calendar` | `page` | **honoured — data is being lost today** | 4000 / 4000 / 1332, disjoint, union 9332 |
| `splits-calendar` | `page` | **honoured** | 2026-01-01…08-28 answers 944 rows, under the cap, and `page=1` answers **0** rather than repeating them. Read, but with no truncation to escape at this volume |
| `institutional-ownership/holder-performance-summary` | `page` | **honoured, as a row offset** | CIK 0001067983: pages 0/1/2/5 → **53 / 52 / 51 / 48** rows, each starting one row later. `page` skips *n* rows; it is not a page index |
| `house-trades` | `limit` | **honoured** | 100 → 5 |
| `house-trades` | `page` | **honoured**, page index of size 100 | `page=1` answers a different 100; naked opens `2026-08-13`, page 1 opens `2025-06-03` |
| `house-trades-by-id` | `limit`, `page` | **honoured** | 100 → 5; `page=1` a different 100 |
| `senate-trades` | `limit`, `page` | **honoured** | 100 → 5; `page=1` a further 100 |
| `senate-trades-by-id` | `limit`, `page` | **honoured**, page index | senateID M001243: 100 / 45 / 0 across pages 0–2 — 145 rows total |
| `senate-net-worth` | `limit` | **decorative** | `limit=5` and `limit=1` both answer the full **250** rows, byte-identical to naked |
| `senate-net-worth` | `page` | **decorative** | `page=1` and `page=2` both answer the same 250, byte-identical |

`holder-performance-summary` is the one to be careful with. Offset semantics means `page=1` re-serves 52 of the
53 rows `page=0` already returned, so a caller looping pages the ordinary way gets *n*·(*n*+1)/2 duplicates
rather than more data. Modelling it as `page` would be worse than not modelling it.

> **Measured at the far end and left unmodelled under any name — settled 2026-09-01 (#53).** The 53 rows above
> are Berkshire's history at FMP, not the endpoint's reach: across 299 filers the largest answer is **110 rows**
> (FMR, `0000315066`, every quarter since 1999 Q1, 131 KB in one response), and `page=n` is exactly the plain
> answer's rows *n* onward, `[]` with 200 past the end. A history that always arrives whole has nothing to page,
> so no `skip` or `offset` either; `.Skip(n)` on the result is the same operation. See
> [the holder-performance paging measurements](2026-09-01-holder-performance-paging-measurements.md).

#### Paging does not generalise, so the other two "cannot page" claims were re-probed

Finding a working cursor where the docs denied one makes every other such claim suspect. Both were checked
rather than left standing, and both survive — for different reasons, neither of which is the one the calendars
had.

**`economic-calendar` genuinely ignores `page`.** `from=2025-01-01&to=2025-12-31` answers 7301 rows, and
`page=1`, `page=2` and `limit=10000` each answer the identical 7301 — same first row, same last row, 6741 shared
`(date, event)` pairs against 6741 distinct ones. A range really is that endpoint's entire query surface.

**`splits-calendar` reads `page` but has nothing to page to.** Its limit is a 90-day lookback rather than a row
cap: `from=2026-01-01&to=2026-08-28` answers 944 rows whose earliest is `2026-05-31` — 89 days before `to` — and
`page=1` answers **0** rather than the missing January-to-May. No cursor reaches outside a window.

Three mechanisms across four calendar paths, and the parameter that escapes one does nothing on the next. That
is the case for measuring each path rather than reasoning from a sibling.

### Group C — the Senate filter cluster

The issue estimated sixteen parameters here; there are **ten**, and nine are honoured. This is the largest
genuine gap by count and the one where a criteria object earns its keep.

| path | parameter | verdict | measurement |
|---|---|---|---|
| `senate-positions` | `limit` | **honoured, capped at 300** | `limit=5` → 5; `limit=5000` → **300**, the naked count |
| `senate-positions` | `party` | **honoured** | naked 300 = 191 Republican + 109 Democrat; `party=Republican` → **300, all Republican** — so the filter runs over the dataset and *then* pages, rather than over the page |
| `senate-positions` | `position` | **honoured** | naked 300 = 263 Representative + 37 Senator; `position=Senator` → **300, all Senator** |
| `senate-positions` | `senateID` | **honoured** | 300 → **4** |
| `senate-profile` | `active` | **honoured** | naked 500 rows all `active: true`; `active=false` → **500 rows all `active: false`** |
| `senate-profile` | `latestParty` | **honoured** | 500 → **259**, all Democrat |
| `senate-profile` | `latestPosition` | **honoured** | 500 → **99**, all Senator |
| `senate-profile` | `limit` | **honoured, capped at 500** | `limit=5` → 5; `limit=5000` → 500 |
| `senate-profile` | `senateID` | **honoured** | 500 → **1** |
| `senate-net-worth-aggregated` | `totalsCol` | **decorative** | `total`, `stock`, `1` and `true` all answer the identical 3 rows, byte-identical to naked |

Two page-size facts fall out and are worth keeping: `senate-positions` caps at **300** per page and
`senate-profile` at **500**, and `limit` can only reduce those, never raise them. `senate-profile?page=1`
answers 35, so the profile universe is **535**.

#### These two `limit`s were already documented — as ignored — and that was wrong

`GetPositionsAsync` and `GetProfilesAsync` both carried "**No `limit` parameter, because FMP ignores it**",
measured 2026-08-29 from `?limit=500` answering 300 and `?limit=1000` answering 500. Those numbers reproduce.
The conclusion does not: **every value tried was above the page size, and no value above a cap can distinguish
"discarded" from "clamped".** `limit=5` answers 5 on both.

That is the same failure as the calendar cursor, approached from the other side — there one parameter was tested
and the conclusion generalised to a different parameter; here one region of a parameter's range was tested and
the conclusion generalised to the rest of its range. Both are now corrected on the members. Together they are the
argument for the four-call protocol used throughout this audit: a single probe establishes what *that value* did,
never what the parameter does.

`totalsCol` was probed across four plausible vocabularies rather than one, because a single wrong value cannot
tell "ignored" from "unrecognised". All four are byte-identical to the naked request. If it has a working
vocabulary, none of a column name, a category name, an index and a boolean is in it.

## Not measured, and why

**`ratios-ttm-bulk?year=` is left unprobed deliberately.** The issue costed it at one call. It is a bulk
endpoint: two responses of roughly 69 MB, two minutes of a reservoir that refills at 2/min, against an endpoint
whose own error text warns that "frequent abuse on this API Endpoint may result in restrictions placed on this
API Key". And the answer would not be worth the spend — a bulk part is an unordered shard FMP republishes every
few hours, so two samples that look alike prove nothing and two that differ prove nothing either. The method
that works for all 26 ordinary paths does not transfer, and no cheaper method was found. It stays open.

## Counts, reconciled against the issue

The issue's own figures were approximations it said not to lean on. For the record:

| | issue | measured |
|---|---|---|
| shared paths carrying a parameter we omit | 26 | 26 |
| parameters | ~26 implied | **40** |
| group C parameters | "sixteen across three paths" | **10 across three paths** |
| probe cost | ~35 calls | **154** — four per path is the floor once the controls are counted |
