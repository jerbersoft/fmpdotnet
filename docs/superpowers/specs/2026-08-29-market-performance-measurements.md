# Market Performance — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-29** across **68 captured responses** — 65 against the eleven paths of
[#32](https://github.com/jerbersoft/fmpdotnet/issues/32) and three cross-checks against endpoints the SDK
already ships (`available-sectors`, `available-industries`, `quote`). Fifty-nine were JSON arrays, nine of them
empty; the other nine were plain-text `400` bodies. **9,855 rows and 39,523 field slots** in total. All
ordinary JSON endpoints; no `*-bulk` path was touched.

Issue #32 groups eleven paths under one heading. They are not eleven shapes. They are **three row shapes**, and
two of those three differ by a single key. The savings are real but they are not the story.

The story is that **eight of the eleven can answer a question you did not ask, with HTTP 200 and no marker.**
All eight sector and industry paths default to **NASDAQ alone**, not the market. The four historical ones
additionally default to a hard-coded window in **February 2024** — thirty months stale — when you omit
`from`/`to`. And the four snapshot ones, asked for a date past the end of the data, return rows carrying
**three different dates in one response**. Every one of those is a silent wrong answer, not an error. Only the
three movers lists are free of it, and they have a smaller problem of their own: no date at all.

## Entitlement — all eleven are reachable

Every path answered HTTP 200 on this plan. No 402, no 403. The three movers need no parameters at all; the
other eight refuse without one.

| path | bare request | rows |
|---|---|---|
| `stable/biggest-gainers` | 200 | 50 |
| `stable/biggest-losers` | 200 | 50 |
| `stable/most-actives` | 200 | 50 |
| `stable/sector-performance-snapshot` | 400 — `date` | 11 once dated |
| `stable/sector-pe-snapshot` | 400 — `date` | 11 once dated |
| `stable/industry-performance-snapshot` | 400 — `date` | 126 once dated |
| `stable/industry-pe-snapshot` | 400 — `date` | 126 once dated |
| `stable/historical-sector-performance` | 400 — `sector` | 21 on defaults |
| `stable/historical-sector-pe` | 400 — `sector` | 21 on defaults |
| `stable/historical-industry-performance` | 400 — `industry` | 21 on defaults |
| `stable/historical-industry-pe` | 400 — `industry` | 21 on defaults |

The 400 body is plain text in the shape already recorded for Technical Indicators (measured 2026-08-29):
`Query Error: Invalid or missing query parameter - date`. An unparseable date gets the same sentence —
`date=notadate` is reported as *missing*, not as malformed.

## Five key tuples, three shapes

The 56 JSON captures taken against the eleven paths carry **exactly five distinct key tuples**, and they
collapse to **three shapes** because `sector` and `industry` occupy the same slot:

```
symbol, price, name, change, changesPercentage, exchange     ← the three movers lists          (1 tuple)
date,   sector|industry, exchange, averageChange             ← the four performance paths      (2 tuples)
date,   sector|industry, exchange, pe                        ← the four PE paths               (2 tuples)
```

The eight sector/industry paths are one shape under two dimension names and two metrics: swap `sector` for
`industry`, swap `averageChange` for `pe`. Nothing else varies — not the key order, not the types, not the
nullability. `snapshot` and `historical` are the same rows selected differently, not different rows.

Whether three shapes should become three records, or fewer, is a design question and is listed as one at the
bottom. What is measured is that there are three, not eleven.

**No row in the movers lists carries a date.** The three lists describe a session and never name it. Measured
2026-08-29 (a Saturday), `biggest-gainers` row `FNGR` read `price 0.398, change 0.2246, changesPercentage
129.5271`, and `stable/quote?symbol=FNGR` returned those three values **identically**, with
`timestamp 1787947201` — `2026-08-28 20:00:01Z`, Friday's close. So the lists are the last completed session,
and a caller has to reach a second endpoint to learn which one that was.

That cross-check also turned up a spelling divergence worth pinning: the movers say **`changesPercentage`**
and `quote` says **`changePercentage`** for the same number.

### The movers take no parameters at all

Three parameters were tried against `biggest-gainers` and all three were **ignored** — each response was
byte-identical (SHA-256 `1b9326e8…`) to the bare request:

| parameter tried | effect |
|---|---|
| `limit=10` | none — still 50 rows |
| `exchange=NYSE` | none — still the same 50 rows, `exchange` values unchanged |
| `page=1` | none |

So the three lists are fixed at **50 rows**, span every exchange at once, and cannot be narrowed, paged or
extended. That is the opposite of the eight sector/industry paths, which are single-exchange by default and
must be widened one exchange at a time.

### Each movers list's own ordering, and how the three lists overlap

Measured 2026-08-29: each of the three movers lists returned exactly **50 rows**. `biggest-gainers` is sorted
**strictly descending** by `changesPercentage`, from `129.5271` down to `9.85667`. `biggest-losers` is sorted
**most-negative-first** — `-74.76349` down to `-16.6362` — which is the *opposite* direction from
`biggest-gainers`: ascending numerically rather than descending. Each list is "biggest first" by magnitude, not
by signed value, and the two sort directions do not generalise from one list to the other.

Symbol overlap across the three lists: `biggest-losers` shared **no symbol** with `biggest-gainers`, and
exactly **one** symbol, `BTAI`, with `most-actives`. `biggest-gainers` shared **8** symbols with
`most-actives`: `AEMD, CHAI, CYAB, DUO, FNGR, NCPL, SOXS, XTNT`. So the near-disjointness is a property of the
losers list specifically, not of the movers lists in general.

## The historical default window is February 2024

**This is the sharpest trap in the group.** With `sector` or `industry` supplied and `from`/`to` omitted, all
four historical paths answer 21 rows spanning **2024-02-01 to 2024-03-01** — thirty months before the date of
measurement. HTTP 200, well-formed, and wrong for any caller who meant "recently".

The two bounds were measured separately:

| request | rows | window returned |
|---|---|---|
| `sector=Technology` | 21 | 2024-03-01 → 2024-02-01 |
| `sector=Technology&to=2026-08-28` | 665 | 2026-08-28 → **2024-02-01** |
| `sector=Technology&from=2024-02-20` | 9 | **2024-03-01** → 2024-02-20 |
| `sector=Technology&from=2026-01-02` | **0** | — |

Read together: `from` defaults to **2024-02-01** and `to` defaults to **2024-03-01**, both hard-coded. The
fourth row is the same defaulting seen from the other side — `from=2026-01-02` against a default
`to=2024-03-01` is a backwards range, and a backwards range answers `[]` (confirmed directly with
`from=2026-08-28&to=2026-08-01`, also `[]`).

Recent data exists and is reachable — `from=2026-08-01&to=2026-08-28` returns 20 rows ending 2026-08-28. The
endpoint is not stale. Only its defaults are.

`limit` does not move the window: `limit=100` returned the same 21 rows as the bare request, byte for byte.

## The default exchange is NASDAQ, not the market

All eight sector and industry paths carry an `exchange` key on every row, and every row of every
default-exchange response read `NASDAQ`. There is no "all exchanges" value among those measured — the
parameter selects one exchange, and omitting it selects NASDAQ.

This is not a cosmetic difference. Same sector, same day, same path:

| | 2026-08-28 | 2026-08-27 |
|---|---|---|
| Technology, default (NASDAQ) | −0.6192 | +0.5854 |
| Technology, `exchange=NYSE` | −1.7398 | +1.0842 |

Across the 20 shared dates in that window, **not one value matched**. A caller who reads
`historical-sector-performance?sector=Technology` as "how did technology do" gets an answer about one exchange.

`NASDAQ`, `NYSE` and `AMEX` were each verified to return rows. `exchange` is case-insensitive —
`exchange=nasdaq` returned a response byte-identical to the default. An unrecognised value is **not** an
error: `exchange=BOGUS` answers HTTP 200 and `[]`.

## A snapshot past the end of the data is not a snapshot

Asked for a date beyond the available data, the four snapshot paths do not return empty and do not fail. They
return a full row set in which **the rows do not share a date**.

`sector-performance-snapshot?date=2026-09-01`, measured 2026-08-29:

| sector | date on the row |
|---|---|
| Basic Materials, Communication Services, Consumer Defensive, Energy, Financial Services, Healthcare, Technology, Utilities | 2026-08-28 |
| Consumer Cyclical | 2026-08-27 |
| Industrials, Real Estate | **2026-08-25** |

`date=2027-01-04` produced that split **sector for sector, identically**, and `sector-pe-snapshot` did too.
Three requests, two different future dates, two different metrics, one identical assignment of stale dates to
sectors — this is systematic, not a one-off.

**The fallback is not "each sector's latest row."** Asked for `date=2026-08-28` directly, Industrials and Real
Estate both return rows dated 2026-08-28 (`−0.4894` and `−3.1168`). The future-date response gave those two
sectors their **2026-08-25** values instead (`−0.1856` and `+1.6557`, matching
`historical-sector-performance` for that date exactly). The values are real and the dates are honest; the row
set is simply not a coherent day.

The mitigation is available in the payload: `date` is on every row. A caller who compares it to the date asked
for can detect this. A caller who trusts the parameter cannot.

## Dates that answer empty

Distinct from the above — these return `[]` with HTTP 200 rather than a ragged set:

| request | result |
|---|---|
| `date=2026-08-29` (the Saturday of measurement) | `[]` |
| `date=2026-08-22` (an earlier Saturday) | `[]` |
| `date=1990-01-15` (before the data) | `[]` |

A market holiday is **not** in this list. `date=2026-01-01` returned 11 rows all dated 2026-01-01.

## Silent empties on the dimension parameter

An unrecognised `sector` or `industry` is never reported. Every one of these answered HTTP 200 with `[]`:

- `historical-sector-performance?sector=Technlogy` (a typo)
- `sector-performance-snapshot?date=2026-08-28&sector=Technlogy`
- `historical-industry-pe?industry=Banks` — **not a typo; see below**

The dimension value is case-insensitive: `sector=technology` returned a response byte-identical to
`sector=Technology`.

Values containing `&` and `,` — `Oil & Gas Midstream`, `Aerospace & Defense` — work when URL-encoded and were
verified to return rows.

## The industry vocabulary is a superset

`stable/available-industries` returned **159** industries on 2026-08-29 — the same count the SDK recorded
against that endpoint on 2026-08-26, in `DirectoryNames.cs`. The industry snapshots do not carry that many:

| source | industries |
|---|---|
| `available-industries` | 159 |
| `industry-performance-snapshot`, NASDAQ, 2026-08-28 | 126 |
| `industry-performance-snapshot`, NYSE, 2026-08-28 | 128 |
| union of the two exchanges | **139** |

Twenty names in the documented list appear on neither exchange — among them `Banks`, `Asset Management`,
`Environmental Services`, `Silver`, `Media & Entertainment`. Nothing appeared in a snapshot that was absent
from the vocabulary, so the list is a strict superset. Feeding it to these endpoints unfiltered produces `[]`
for one name in eight, indistinguishable from a typo.

`stable/available-sectors` returned **11** sectors, and every unfiltered sector snapshot measured — eight of
them, across five dates and three exchanges — carried exactly those 11 names, no more and no fewer.

## Numbers and nullability

**No null appeared anywhere.** Across 9,855 rows and 39,523 field slots, zero nulls in any field of any shape.

**No number arrived as a string.** Every numeric slot was a JSON number — unlike `financial-scores` and the
directory lists, where quoting is measured, recurring behaviour.

The only integer-typed numerics in the whole corpus are twelve `pe` values of exactly `0`, emitted as `0`
rather than `0.0`:

| exchange | industries reading `pe: 0` on 2026-08-28 |
|---|---|
| NASDAQ | Agricultural Inputs, Business Equipment & Supplies, Financial - Mortgages, Industrial Materials, Manufacturing - Textiles, Medical - Equipment & Services, Oil & Gas Integrated, REIT - Industrial |
| NYSE | Biotechnology, Construction, Electronic Gaming & Multimedia, Solar |

`pe` was **never negative and never null** across 359 measured values; the range was `0` to `194.1360`. Zero
is therefore carrying the meaning "no meaningful aggregate PE" — Biotechnology on the NYSE is not a
zero-earnings-multiple industry — and it is doing so in-band, where a caller cannot distinguish it from a
measurement.

**The 359 measured `pe` values split by path and shape, dated 2026-08-29.** 295 came from the industry-PE
paths (254 snapshot + 41 historical) and 64 from the sector-PE paths (23 snapshot + 41 historical) —
295 + 64 = 359. All twelve zeros are among the 254 industry-PE **snapshot** rows: none appeared on any
historical row of either shape, and none on any sector row. So the twelve are twelve of the 254, which is why
the snapshot qualifier matters whenever the twelve are cited.

`averageChange` ranged `−74.8932` to `+73.6983` across 9,016 values. Both metrics arrive as **unrounded
float64 expansions**, not as the two- and four-decimal figures the price endpoints return: the longest plain
fractional part measured was **22 digits** (`-0.0000026524148173594842`, 17 significant), and the greatest
number of significant digits on any value was 17.

### Ten values arrive in scientific notation

Found while preparing fixtures, and not in the first pass over this corpus. **Ten values are written in
exponent form** rather than as plain decimals — every one of them in the 4,025-row deep-history capture
(`sector=Technology&from=2000-01-01&to=2016-01-01`):

| date | wire form |
|---|---|
| 2005-09-02 | `5.735079118365113e-7` |
| 2005-08-19 | `2.501738239332157e-7` |
| 2005-07-20 | `-5.321997112956712e-7` |
| 2005-06-06 | `-7.14739747686903e-7` |
| 2005-06-02 | `-3.082984342411342e-7` |
| 2005-01-19 | `-4.1106220347016403e-7` |
| 2004-09-28 | `3.406022919364493e-7` |
| 2004-09-17 | `-5.002072944342045e-7` |
| 2004-09-13 | `-4.967505081155261e-7` |
| 2003-07-25 | `4.774871561710473e-7` |

**The threshold is exact and it is 1e-6.** Those ten are precisely the ten rows in the whole corpus whose
absolute value is below `1e-6`, and every value at or above it — including the 22-digit
`-0.0000026524148173594842`, which is `-2.65e-06` — is written out in full. So the switch is FMP's serialiser
choosing the shorter of the two forms, not a different code path for a different kind of number.

This matters because it would only ever appear in a deep-history request, which is exactly the request a
fixture is least likely to be cut from. **Verified 2026-08-29 on .NET 10** with the source generator, the same
`[JsonSourceGenerationOptions]` this SDK uses: `System.Text.Json` binds exponent form to `decimal?` without a
custom converter, and `-2.6524148173594842e-06` and `-0.0000026524148173594842` deserialise to values that
compare **equal**. No transport or converter work is needed — but a fixture should carry one of these so the
next person to touch the numeric typing cannot break it silently.

## Coverage extents

| series | oldest row measured | newest row measured |
|---|---|---|
| `historical-sector-performance`, Technology, NASDAQ | **2000-01-03** | 2026-08-28 |
| `sector-performance-snapshot` | rows returned for 2010-01-15; `[]` for 1990-01-15 | 2026-08-28 |

A single request for `sector=Technology&from=2000-01-01&to=2016-01-01` returned **4,025 rows** and one for
`from=2015-01-01&to=2026-08-28` returned **2,950**. No row cap was reached at 4,025, and `limit` was ignored
where it was tried.

## What the design has to decide

Recorded here as questions, not answers — the design doc settles them.

1. **Three records or fewer.** The performance and PE shapes differ in one key. Whether that is one record with
   both metrics nullable, or two records, is a real choice with a real cost either way.
2. **The stale default.** The SDK can pass `from`/`to` through and inherit February 2024, or it can refuse to
   issue a request without a window. The endpoint's default is measured to be a wrong answer for any live
   caller.
3. **The exchange default.** Same shape of decision: inherit NASDAQ silently, or make `exchange` explicit at
   the call site.
4. **The ragged snapshot.** `date` is on every row and can be compared to the argument. Whether the SDK does
   that comparison, or documents it and leaves it to the caller, is open.
5. **Sector and industry typing.** 11 sectors is enum-sized; 159 industries is not, and 20 of those 159 are
   measured to return `[]`. `Directory.GetSectorsAsync` and `GetIndustriesAsync` already ship the vocabularies.
6. **`pe: 0`.** In-band sentinel or a real zero — the design has to pick a reading and defend it.
