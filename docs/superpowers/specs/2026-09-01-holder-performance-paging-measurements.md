# `holder-performance-summary` and its row offset — measurements

Issue #53. Probed against the live FMP API on **2026-09-01 UTC**. Every number below came from a response this
session fetched; nothing is taken from FMP's documentation. The API key travelled as a request header (#59), so
no built URL carried it, and no capture was kept in the repository.

## The question, and the answer

#46 measured that `page=n` on `stable/institutional-ownership/holder-performance-summary` skips *n* rows rather
than selecting a page, and #53 asked whether that is worth exposing under an honest name — `skip` or
`offset` — or leaving unmodelled. Its own instruction was to measure the largest response the path produces
before deciding, because 53 rows was the largest anyone had seen, and 53 rows is not a response that needs
paging.

**The largest response is 110 rows and 131 KB, it arrives whole, and `page=n` is exactly its rows *n*
onward.** Nothing the offset can reach is missing from the plain call. `limit` is ignored with or without it.
So there is nothing to page, and the decision is to model nothing: `GetHolderPerformanceAsync(cik)` stays as
it is, and a caller who wants rows *n* onward calls `.Skip(n)` on the result — the identical operation, with
no second request.

What the measurement corrected is the premise. **53 rows is Berkshire's history at FMP, not the endpoint's
reach.** The endpoint reaches back to 1998 Q3; Berkshire's rows begin at 2013 Q2, and so do those of eleven
other filers in the sample, which is why 53 kept appearing.

## The population

Filers were taken from the first five pages of `stable/institutional-ownership/latest` at `limit=100` — 500
rows, **281 distinct CIKs**, since a filer appears once per filing there — plus 18 chosen by hand for size and
age: Vanguard, BlackRock, State Street, FMR, Renaissance, Bridgewater, T. Rowe Price, Berkshire, the Gates
Foundation, Morgan Stanley, JPMorgan, Citadel, Bank of America, and five older CIKs. **299 filers, 299 answers,
0 errors.**

| rows answered | filers |
|---|---|
| 0 | 30 |
| 1–19 | 119 |
| 20–39 | 55 |
| 40–59 | 46 |
| 60–79 | 24 |
| 80–99 | 11 |
| 100–110 | 14 |

Mean 30.2, median 21, **maximum 110**. 58 of the 299 answer more than Berkshire's 53; 12 answer exactly 53.

The 30 that answer `[]` do so with HTTP 200, and they are not simply the newest registrants — `0000033179`
and `0000905729` are among them. An empty answer is therefore not a proxy for a fresh CIK, and the SDK's
existing reading of `[]` as "FMP has nothing" stands.

### Where each history begins

The earliest quarter across the population is **1998-09-30** (Muhlenkamp & Co, `0001133219`). Six filers begin
at 1999-03-31, which is 110 quarters before 2026-06-30 — so a 110-row answer is every quarter from then to now,
and the count is quarters present rather than a cap. Muhlenkamp begins earlier and answers 107 against the 112
quarters from there to 2026-06-30, so five are missing somewhere in its series; where was not measured.

**Fifteen filers begin at exactly 2013-06-30**, 53 quarters before 2026-06-30, and twelve of them answer
exactly 53 — Berkshire, State Street (`0000093751`), Morgan Stanley (`0000895421`) and JPMorgan
(`0000019617`) among them; the other three answer 52, 49 and 16. Every filer in the sample that answers 53
begins there. That cluster is what made 53 look like the endpoint's size. It is not: Vanguard
answers 104 from 1999-03-31, Bank of America 95 from 2001-06-30, the Gates Foundation 96 from 2002-09-30,
Bridgewater 83 from 2005-12-31, BlackRock 71 from 2006-03-30 (that date is FMP's, one day short of the quarter
end, and is reproduced here as sent).

### The largest answers

| CIK | filer | rows | earliest |
|---|---|---|---|
| `0000897070` | Ashford Capital Management | **110** | 1999-03-31 |
| `0000315066` | FMR | **110** | 1999-03-31 |
| `0000080255` | T. Rowe Price | **110** | 1998-12-31 |
| `0000200217` | — | **110** | 1999-03-31 |
| `0000741073` | Stock Yards Bank & Trust | 109 | 1999-03-31 |
| `0001133219` | Muhlenkamp & Co | 107 | 1998-09-30 |
| `0001067926` | Adelante Capital Management | 107 | 1999-03-31 |
| `0001037389` | Renaissance Technologies | 107 | 1999-09-30 |
| `0000814375` | — | 107 | 1999-09-30 |
| `0000051812` | Stonebridge Capital Management | 106 | 1999-06-30 |
| `0000936753` | — | 106 | 1999-12-31 |
| `0000102909` | Vanguard | 104 | 1999-03-31 |

T. Rowe Price begins a quarter earlier than the 1999-03-31 group and still answers 110, so one of its 111 is
missing too. The ceiling is not a cap FMP applies; it is how many quarters there have been since the earliest
one in the sample.

## The offset, measured at the far end

Everything #46 saw on 53 rows, re-checked on the largest history in the sample — FMR, `0000315066`:

| request | status | rows | first row | note |
|---|---|---|---|---|
| `dates?cik=` | 200 | 110 | — | 1999-03-31 … 2026-06-30, one per quarter |
| naked | 200 | **110** | 2026-06-30 | last row 1999-03-31; **130,704 bytes** as re-serialised JSON |
| `page=1` | 200 | 109 | 2026-03-31 | equal to the naked answer's rows 1 onward |
| `page=50` | 200 | 60 | 2013-12-31 | equal to rows 50 onward |
| `page=109` | 200 | 1 | 1999-03-31 | equal to rows 109 onward |
| `page=110` | 200 | **0** | — | `[]`, not an error |
| `page=111` | 200 | 0 | — | `[]` |
| `page=1000` | 200 | 0 | — | `[]` |
| `limit=5` | 200 | 110 | 2026-06-30 | ignored |
| `page=100&limit=5` | 200 | **10** | — | the offset applied, the limit not |

"Equal" is equality of the parsed JSON arrays, row for row and field for field — not a count match. So
`page=n` is `rows[n..]` in every case, including past the end, where it is empty with HTTP 200 rather than a
400 or 404. `dates` and the summary agree at 110 for FMR as they do at 53 for Berkshire; the one-row-per-quarter
relationship holds at both ends of the sample.

## Why no offset on the member

Three things an `offset` parameter could have offered, and what each is worth here:

- **Reaching rows the plain call omits.** There are none — `page=n` is a suffix of the plain answer, never
  more.
- **A smaller response.** The offset trims the *newest* rows and keeps the oldest. A caller who wants less
  usually wants the recent quarters, which is the part it cannot keep; and the whole is 131 KB at the largest,
  one response, once per filer.
- **A cheaper request.** Every response measured here arrived in one round trip. The offset adds a request
  and removes nothing from it.

Against that, exposing it puts a query parameter on the member whose semantics FMP could change — it already
uses the wrong name for it — for an operation `.Skip(n)` already performs locally and correctly. Not modelled.

## Corrections to the earlier record

- #53's premise, "53 rows is the whole answer for a large filer", was drawn from one filer in the 2013-06-30
  cluster. Berkshire's 53 stands; the endpoint's largest answer is 110.
- `GetHolderPerformanceAsync` and `HolderPerformance` described the whole history through Berkshire's 53.
  Both now say where 53 comes from and how far the endpoint reaches. The 2026-08-28 measurement is kept: it
  was a correct reading of the filer it looked at.
- The #46 audit's row for this path is right and unchanged; it gained a dated note pointing here.

## Not measured

- Filers outside the first five pages of `latest` and the 18 chosen by hand. A filer that began before
  1998-09-30 or answers more than 110 rows would have to be one of them; none was found among 299.
- Whether the 30 empty answers are "no performance computed" or "no history", which the response does not
  say.
- Whether `page` beyond the row count is ever anything but `[]` with 200 — three values past the end were
  tried, and all three were.
