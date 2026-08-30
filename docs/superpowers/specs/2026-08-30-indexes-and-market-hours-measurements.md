# Indexes and Market Hours — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-30** across **59 captured responses** — 52 JSON arrays, three of them empty, and 7 plain-text error
bodies. **6,326 rows**, 1,461,701 bytes on the wire. Two cross-checks ran against endpoints the SDK already
ships (`stable/available-exchanges`) and one path was cross-checked against another path in this same slice.
No `*-bulk` path was touched.

Issue [#38](https://github.com/jerbersoft/fmpdotnet/issues/38) groups nine paths under one heading. Unlike
[#34](https://github.com/jerbersoft/fmpdotnet/issues/34), where nine paths produced nine key tuples,
**these nine produce four.** Three constituent paths share one 8-key tuple exactly; three historical paths
share one 7-key tuple exactly; the two market-hours paths return the *same row for the same exchange, byte for
byte*; and `holidays-by-exchange` stands alone. The consolidation is the design's central fact.

The second fact is the one that will cost a careless implementer the most: **three of the four shapes carry
keys that are absent from most rows**, and in two of them the absent key is the one that changes the
meaning of a field that *is* present.

## Entitlement — all nine are reachable on this plan

Every path answered HTTP 200. No 402, no 403. Seven of the nine answer a **bare** request with data; two
require `exchange`.

| path | bare | required |
|---|---|---|
| `stable/dowjones-constituent` | 200, 30 rows | — |
| `stable/sp500-constituent` | 200, 503 rows | — |
| `stable/nasdaq-constituent` | 200, 102 rows | — |
| `stable/historical-dowjones-constituent` | 200, 86 rows | — |
| `stable/historical-sp500-constituent` | 200, 1,525 rows | — |
| `stable/historical-nasdaq-constituent` | 200, 444 rows | — |
| `stable/all-exchange-market-hours` | 200, 81 rows | — |
| `stable/exchange-market-hours` | **400** | `exchange` |
| `stable/holidays-by-exchange` | **400** | `exchange` |

The bare 400 body is plain text — `Query Error: Invalid or missing query parameter - exchange` — served
under `content-type: application/json; charset=utf-8`. **The content type is a lie on every error body
measured.** `exchange=` (present but empty) produces the same 400, not a 200 with an empty list.

### Unknown input is an error here, not an empty list

This slice **inverts the #34 convention** and the difference is load-bearing.

| input | #34 paths (measured 2026-08-30) | this slice (measured 2026-08-30) |
|---|---|---|
| unknown identifier | `[]`, HTTP 200 | **`Invalid Exchange Provided.`, HTTP 400** |
| comma-joined pair | `[]`, HTTP 200 | **`Invalid Exchange Provided.`, HTTP 400** |

`exchange=ZZZZ`, `exchange=NASDAQ,NYSE` and `exchange=NASDAQ%20Global%20Market` (the exchange's *name*
rather than its code) each returned HTTP 400 with the body `Invalid Exchange Provided.` on both
`exchange-market-hours` and `holidays-by-exchange`. Only the code works, and only one at a time.

`exchange` is **case-insensitive**: `exchange=nasdaq` returned a response **byte-identical** to
`exchange=NASDAQ` on both paths.

## Parameters — what is honoured, and what is silently ignored

Every claim below is a byte-comparison against the same call without the parameter.

| parameter | dowjones / sp500 / nasdaq | historical-* | all-exchange-market-hours | exchange-market-hours | holidays-by-exchange |
|---|---|---|---|---|---|
| `limit` | ignored | ignored | ignored | ignored | ignored |
| `page` | ignored | ignored | ignored | ignored | ignored |
| `symbol` | ignored | ignored | — | — | — |
| `year` | — | — | — | — | **ignored** |
| `from` / `to` | — | **ignored** | — | — | **honoured** |
| unknown (`wibble=42`) | ignored | ignored | ignored | ignored | ignored |

**Not one of the nine honours `limit` or `page`.** Every response is the complete set. The largest is
`historical-sp500-constituent` at 1,525 rows and 365,284 bytes.

`from`/`to` is honoured on **exactly one** of the nine. On `historical-dowjones-constituent`,
`from=2020-01-01&to=2026-12-31` returned a response byte-identical to the bare call — the range is accepted
and discarded. On `holidays-by-exchange` it is real, and it is the only way to reach most of the data
(below).

Two further `holidays-by-exchange` behaviours, both measured:

- A **reversed** range (`from=2026-12-31&to=2024-01-01`) returns `[]` with HTTP 200.
- An **unparseable** date (`from=nonsense`) is silently dropped: the response was byte-identical to omitting
  `from` altogether. A typo in a date is not reported.

### The `from` boundary is exclusive — the range is `(from, to]`

Measured 2026-08-30 against NASDAQ's Independence Day row, dated `2026-07-03`:

| range | rows returned |
|---|---|
| `from=2026-07-03&to=2026-07-03` | **0** |
| `from=2026-07-03&to=2026-07-04` | **0** |
| `from=2026-07-02&to=2026-07-03` | 1 — `2026-07-03` |
| `from=2026-07-02&to=2026-07-04` | 1 — `2026-07-03` |
| `from=2023-12-31&to=2024-01-02` | 1 — `2024-01-01` |

`to` is inclusive; **`from` is not.** A range whose `from` equals the holiday's own date excludes it, and a
single-day range therefore always returns `[]` regardless of what falls on that day.

This was found by a discrepancy rather than looked for: filtering the 446-row full history to
2024-01-01 .. 2026-12-31 yields **39** rows, while the live call `from=2024-01-01&to=2026-12-31` returns
**38**. The missing row is exactly `{"date": "2024-01-01", "name": "New Year's Day"}` — the row sitting on the
`from` boundary.

### The default holiday window is the trailing year, and it hides every upcoming holiday

Measured 2026-08-30 across five exchanges, the bare `holidays-by-exchange?exchange=…` call returned **67 rows,
every one of them dated between 2025-08-30 and 2026-08-30, and not one dated after today.**

| exchange | bare rows | bare range |
|---|---|---|
| NASDAQ | 12 | 2025-09-01 .. 2026-07-03 |
| NYSE | 12 | 2025-09-01 .. 2026-07-03 |
| JPX | 20 | 2025-09-15 .. 2026-08-11 |
| LSE | 9 | 2025-12-24 .. 2026-05-25 |
| KLS | 14 | 2025-09-01 .. 2026-08-25 |

Row counts differ (9 to 20) and start dates differ, so it is neither a row cap nor a fixed calendar window:
it is the trailing twelve months, containing whichever holidays fall in it.

The data behind it is far larger. `exchange=NASDAQ&from=1990-01-01&to=2035-12-31` returned **446 rows spanning
1990-02-19 to 2032-12-31** — six years of *future* holidays that the default call never shows. A caller who
asks this endpoint "when is the market next closed?" and passes no range gets an answer that is always in the
past.

## Four shapes for nine paths

Each path emitted its key tuple in exactly one order. No row was ever missing a key from its path's *base*
tuple; three of the four shapes add optional keys on a minority of rows.

| shape | paths | base keys | optional keys | rows measured |
|---|---|---|---|---|
| constituent | `dowjones-`, `sp500-`, `nasdaq-constituent` | 8 | none | 635 |
| index change | the three `historical-*-constituent` | 7 | none | 2,055 |
| market hours | `all-exchange-market-hours`, `exchange-market-hours` | 6 | **2** | 81 |
| holiday | `holidays-by-exchange` | 6 | **1** | 446 |

The tuples, in wire order:

- **constituent** — `symbol`, `name`, `sector`, `subSector`, `headQuarter`, `dateFirstAdded`, `cik`, `founded`
- **index change** — `dateAdded`, `addedSecurity`, `removedTicker`, `removedSecurity`, `date`, `symbol`,
  `reason`
- **market hours** — `exchange`, `name`, `openingHour`, `closingHour`, *[`openingAdditional`,
  `closingAdditional`]*, `timezone`, `isMarketOpen`
- **holiday** — `exchange`, `date`, `name`, `isClosed`, `adjOpenTime`, `adjCloseTime`, *[`isFullyClosed`]*

Note the optional market-hours keys are **inserted mid-tuple**, between `closingHour` and `timezone`, not
appended.

### The two market-hours paths are one shape, and the rows are byte-equal

For each of NASDAQ, NYSE, JPX, LSE, ASX, SET and EGX, the single row returned by
`exchange-market-hours?exchange=X` compared **equal, key for key and value for value**, to that exchange's row
inside the 81-row `all-exchange-market-hours` response. `exchange-market-hours` is a filter over the same
data, not a different view of it. One record type serves both.

## The traps

### 1. `founded` is not a date, and which paths prove it depends on which path you read

The same key, in the same tuple, on three sibling paths. Measured across 635 rows:

| path | ISO `uuuu-MM-dd` | bare year `uuuu` | other |
|---|---|---|---|
| `dowjones-constituent` | **30 of 30** | 0 | 0 |
| `nasdaq-constituent` | **102 of 102** | 0 | 0 |
| `sp500-constituent` | 23 of 503 | **477 of 503** | **3** |

An implementer who models this field from the Dow Jones response — 30 rows, 100% ISO — types it `LocalDate`
and is correct on 155 of 635 rows. On `sp500-constituent` that binding drops **95.4%** of the values.

The three "other" values are not malformed dates; they are genuinely multi-valued text:

| symbol | `founded` |
|---|---|
| `KLAC` | `1975/1977` |
| `LOW` | `1904/1946/1959` |
| `NSC` | `1881/1894` |

`founded` is a **string**. There is no date to parse.

### 2. `"CLOSED"` is a valid value of `openingHour` and `closingHour`

Measured 2026-08-30 at 17:58 UTC, **62 of 81 exchanges** returned the literal string `"CLOSED"` in both
`openingHour` and `closingHour` — **124 of the 176 hour-string slots in the response.** The remaining 52 slots
carried times.

Any converter that parses these fields as a time-of-day must survive `"CLOSED"`. This is not an error
condition and not a null: it is the sentinel for "this exchange is not trading on its current local date".

**The sentinel tracks the exchange's own calendar day, not UTC.** Resolving each row's `timezone` against the
capture's HTTP `Date` header (`Sun, 30 Aug 2026 17:58:46 GMT`):

| local day at capture | has hours | `"CLOSED"` |
|---|---|---|
| weekday | 15 | **1** |
| weekend | 4 | 61 |

The four weekend-with-hours rows are exactly EGX, DOH, KUW and SAU — the Gulf exchanges, whose local Sunday is
a trading day. The 15 weekday-with-hours rows are the Asia-Pacific exchanges, already into Monday 2026-08-31
at capture time.

**The single weekday exception resolves against another path in this slice.** KLS (Bursa Malaysia) was on
local Monday 2026-08-31 at 01:58 and reported `"CLOSED"`. `holidays-by-exchange?exchange=KLS&from=2026-01-01&to=2026-12-31`
lists `{"exchange":"KLS","date":"2026-08-31","name":"National Day","isClosed":true}`. The two paths agree, and
the rule has no unexplained exception. (That row is absent from the *bare* KLS call — it falls outside the
trailing-year window, which is trap 5 arriving early.)

### 3. Seven exchanges take a lunch break, and their afternoon session lives in keys most rows lack

`openingAdditional` and `closingAdditional` were present on **7 of 81 rows** and absent from the other 74:

| exchange | morning | afternoon |
|---|---|---|
| SET (Bangkok) | 10:00 AM – 12:30 PM +07:00 | 02:00 PM – 04:40 PM +07:00 |
| JKT (Jakarta) | 09:30 AM – 11:30 AM +07:00 | 01:30 PM – 03:00 PM +07:00 |
| JPX (Tokyo) | 09:00 AM – 11:30 AM +09:00 | 12:30 PM – 03:30 PM +09:00 |
| SHH (Shanghai) | 09:30 AM – 11:30 AM +08:00 | 01:00 PM – 03:00 PM +08:00 |
| SHZ (Shenzhen) | 09:30 AM – 11:30 AM +08:00 | 01:00 PM – 03:00 PM +08:00 |
| SES (Singapore) | 09:00 AM – 12:00 PM +08:00 | 01:00 PM – 05:00 PM +08:00 |
| HOSE (Ho Chi Minh) | 09:15 AM – 11:30 AM +07:00 | 01:00 PM – 02:30 PM +07:00 |

A record modelled from the first row of the response — ASX, six keys — silently discards the afternoon
session for the Tokyo, Shanghai, Shenzhen and Singapore exchanges. `closingHour` alone reports Tokyo closing
at 11:30 AM.

### 4. `isClosed` is `true` or `null`, never `false`, and the field that disambiguates it is usually absent

Measured across 446 holiday rows (NASDAQ, 1990–2032):

| | count |
|---|---|
| `isClosed: true`, `isFullyClosed` **absent** | 396 |
| `isClosed: null`, `isFullyClosed: false` | 50 |
| `isClosed: false` | **0** |

The two sets are exact complements: `isFullyClosed` is present on precisely the rows where `isClosed` is
`null`. Those 50 rows are early closes — Thanksgiving, Christmas Eve, 3 July — and they are the only rows
carrying an `adjCloseTime` (49× `"13:00"`, 1× `"13:30"` on 2015-11-27).

**`adjOpenTime` was `null` on all 446 rows.** It has never been observed populated.

So "is the exchange fully closed that day?" is not answerable from `isClosed` alone: `null` means *not fully
closed* here, not *unknown*. A `bool IsClosed` binding is wrong on 50 rows; a `bool?` binding is right about
the wire and useless to the caller.

### 5. Two spellings of a time, in one slice

| path | field | example | shape |
|---|---|---|---|
| `*-market-hours` | `openingHour` | `"09:30 AM +09:00"` | 12-hour, uppercase AM/PM, explicit `±HH:MM` offset |
| `holidays-by-exchange` | `adjCloseTime` | `"13:00"` | 24-hour, **no offset at all** |

All 52 non-`CLOSED` hour strings matched `HH:mm AM|PM ±HH:MM` exactly; all 50 `adjCloseTime` values matched
`HH:mm`. Neither is ISO-8601. The holiday time's zone must be inferred from the exchange, which that response
does not carry — `holidays-by-exchange` has no `timezone` key.

### 6. Absence is spelled two ways on the historical paths, and which ways is path-dependent

Measured across 2,055 index-change rows:

| path | `addedSecurity` | `removedTicker` | `removedSecurity` | `reason` |
|---|---|---|---|---|
| `historical-dowjones-constituent` | 26 `""`, 0 null | 55 `""`, 0 null | 55 `""`, 0 null | 0, 0 |
| `historical-sp500-constituent` | 77 `""`, 0 null | 254 `""`, **7 null** | 282 `""`, **7 null** | 210 `""`, **6 null** |
| `historical-nasdaq-constituent` | 25 `""`, **1 null** | 25 `""`, **3 null** | 33 `""`, **3 null** | 0 `""`, **1 null** |

`historical-dowjones-constituent` uses **only** `""` across all 86 rows. An implementer who tests against the
Dow Jones path alone will not discover that `null` occurs on the other two. The existing
`SentinelStringJsonConverter` already folds `""`, `"N/A"` and `"NULL"` to `null`, so it covers both spellings —
but the choice to apply it must be a measured one, not an accident.

**The nulls are row-level, not field-level.** In `historical-sp500-constituent` the 7 rows with
`removedTicker: null` are the *same 7 rows* as `removedSecurity: null` (indices 535, 592, 649, 736, 806, 877,
1124), and the 6 rows with `reason: null` are a strict subset of them.

### 7. A row is an addition *or* a removal, and `symbol` names whichever it is

This is a semantic finding, measured from the data rather than documented:

- **Addition** — `addedSecurity` populated, `removedTicker`/`removedSecurity` empty, `symbol` = added ticker.
- **Removal** — `addedSecurity` is `""`, `removedTicker`/`removedSecurity` populated, `symbol` = removed
  ticker.

Verbatim, a removal row from `historical-sp500-constituent`:

```json
{"dateAdded": "June 24, 2024", "addedSecurity": "", "removedTicker": "RHI",
 "removedSecurity": "Robert Half", "date": "2024-06-24", "symbol": "RHI",
 "reason": "Market capitalization change."}
```

The record is a *change to the index*, not a constituent. Naming it after a constituent would misdescribe
every removal row.

### 8. `dateAdded` and `date` are two independent fields that usually agree

`dateAdded` is US long form — `"June 29, 2026"` — and parsed as `MMMM d, yyyy`; all **2,055 of 2,055** rows
parsed. `date` is ISO on all 2,055. They are not two renderings of one value:

| `date` − `dateAdded` | rows |
|---|---|
| 0 days | 1,850 |
| **−1 day** | **202** |
| −2 days | 1 |
| +10 days | 1 |
| −366 days | 1 |

**205 of 2,055 rows (10.0%) disagree, and 202 of those are exactly one day, with `date` the earlier.**

The disagreement is not a historical artifact. 151 of the 205 come from a single 1957 backfill — 151 rows say
`"March 04, 1957"` / `1957-03-03` while 54 rows with the *identical* `dateAdded` say `1957-03-04`, which alone
proves the two fields are stored separately. But **40 disagreements fall in 2024–2026**, against 47 agreeing
rows in the same span: in recent data the fields disagree on 46% of rows.

The three non-±1 outliers, verbatim:

| path | delta | row |
|---|---|---|
| sp500 | +10 | `"July 7, 2003"` / `2003-07-17`, PLD, "Market capitalization changes" |
| nasdaq | −2 | `"June 9, 2009"` / `2009-06-07`, CSCO, "Annual Re-ranking" |
| nasdaq | −366 | `"January 9, 2026"` / `2025-01-08`, VSNT removal |

Both fields must be surfaced. Deriving either from the other is wrong on 205 rows.

## Field formats — the constituent shape

Measured across all 635 rows:

| field | finding |
|---|---|
| `symbol` | never null, never empty; **unique within each path** (30/30, 503/503, 102/102 distinct) |
| `name` | never null, never empty; unique within each path |
| `sector` | 11 distinct values across 635 rows, **all 11 inside the existing `Sector` enum**, none outside |
| `subSector` | 114 distinct values, free text; no enum is defensible |
| `headQuarter` | free text, never null (`"Mountain View, California"`, `"San Francisco, CA"` — no fixed form) |
| `dateFirstAdded` | ISO on all 628 non-null values; **null on 7 of 102 Nasdaq rows**, never null on the other two paths |
| `cik` | zero-padded 10 digits on **635 of 635** — one pattern, no exceptions |
| `founded` | four patterns — see trap 1 |

The 7 null `dateFirstAdded` rows are ADBE, AMAT, CSCO, FAST, MSFT, PAYX and QCOM — Nasdaq-100 members with no
recorded entry date.

`sector` binding cleanly to all 11 enum members is worth stating precisely: the enum exists to build a
`sector=` **query** value, and this measurement says the response vocabulary on these three paths coincides
with it exactly. It does not license binding the response to the enum — nothing here measures what happens
when FMP adds a twelfth sector.

## Cross-checks

**Against `stable/available-exchanges`, which this SDK already ships.** That path returned 63 exchange codes
on 2026-08-30. **All 63 appear in `all-exchange-market-hours`**, which carries 18 more (AQS, ASE, BTS, BUD,
BVC, CME, EBS, EGX, EURONEXT, FGI, HOSE, ICEF, KUW, NIM, PNK, RIS, SSX, TAL). The hours vocabulary is a strict
superset, so any code from `GetExchangesAsync` is safe to pass to the two market-hours paths.

**Against the IANA tz database.** All **81 of 81** `timezone` values resolved as IANA zone identifiers (52
distinct). None is an abbreviation or a fixed offset.

**Row counts explained by the data itself.** `sp500-constituent` returns 503 rows but only 500 distinct CIKs —
three dual-class pairs (FOX/FOXA, NWS/NWSA, GOOGL/GOOG). `nasdaq-constituent` returns 102 rows and 101
distinct CIKs — one pair (GOOG/GOOGL). Every `name` is distinct within each path, so `name` does not identify
a company either.

**The historical feed is not a complete ledger.** Of the 628 current constituents carrying a
`dateFirstAdded`, **24 have no addition row at all** in the matching historical path. Membership cannot be
reconstructed from the change feed, and the design must not imply it can.

Comparing `dateFirstAdded` to the historical rows is *consistent with* `date` being the early field —
across 604 matched pairs it agreed with `dateAdded` 228 times and with `date` 192 times — but symbols added
and removed repeatedly make the pairing ambiguous, so this is corroboration, not proof. The proof is the
within-response measurement in trap 8.

**Stability.** `dowjones-constituent`, `all-exchange-market-hours` and `historical-dowjones-constituent` were
each fetched twice and returned **byte-identical** bodies. No `Cache-Control`, `Age` or `Last-Modified` header
was present on any capture; a weak `ETag` was.

## What was NOT measured

Stated plainly so the design does not quietly assume it.

- **`isMarketOpen` was `false` on all 81 rows, on every capture.** Every capture in this corpus was taken on
  **Sunday 2026-08-30**, when no exchange in the list was inside its trading window — including the four Gulf
  exchanges, whose local time at capture was 20:58, after their close. The field is a JSON boolean on all 81
  rows, so the *type* is measured; the value `true` has **never been observed**. Confirming it needs one call
  during any exchange's session, which is a weekday task.
- **Every observed UTC offset in an hour string was positive** (`+03:00` through `+12:00`), for the same
  reason: only Asia-Pacific and Gulf exchanges were on a trading day. The negative-offset form that NYSE and
  LSE will emit on a weekday is **unobserved**, though nothing in the format suggests it differs.
- **`adjOpenTime` has never been observed non-null** — 446 of 446 rows null. Whether it is a live field or a
  vestigial one is unknown.
- **No `isClosed: false` row was ever seen** across 446 rows.
- **The 400-vs-`[]` convention was measured only on the two `exchange=` paths.** The seven bare paths take no
  identifier, so there is nothing to feed them a bad value through.

Both weekday items above are answerable with three calls on any weekday and should be settled before the
design's converter choices are frozen.
