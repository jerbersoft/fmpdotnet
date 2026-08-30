# ETF and Mutual Funds — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-30** across **244 captured responses** — 229 JSON arrays, 32 of them empty, and 15 plain-text `400`
bodies. **139,166 rows over 154 distinct responses** after de-duplicating repeat fetches of the same query;
72.7 MB on the wire. Two cross-checks ran against endpoints the SDK already ships
(`sec-filings-search/cik`, twenty of them) and one against SEC EDGAR, which refused. No `*-bulk` path was
touched.

Issue [#34](https://github.com/jerbersoft/fmpdotnet/issues/34) groups nine paths under one heading. Unlike #32
and #33, **they do not consolidate at all**: nine paths, nine key tuples, no two alike. There is no shape
argument to make here.

The story is elsewhere. It is that **this slice contradicts itself on three separate fields that share a
name.** `weightPercentage` is a JSON number on one path and a percent-suffixed string on its sibling.
`updatedAt` is `"uuuu-MM-dd HH:mm:ss"` on one path and ISO-8601 with a `Z` and milliseconds on another.
And absence is spelled four ways across the nine — JSON `null`, `""`, `"N/A"` and the four-character string
`"NULL"` — with one field, `className`, using two of them. A caller who learns this slice one path at a time
will get the next path wrong.

Secondary, and just as load-bearing for the design: **not one of the nine honours `limit` or `page`.** Every
response is the complete set, and the complete set reaches 66,065 rows and 27.4 MB.

## Entitlement — all nine are reachable, and none is optional-parameter

Every path answered HTTP 200 on this plan. No 402, no 403. Every path also **refuses a bare request** with
HTTP 400 and a plain-text body naming the one parameter it wants next.

| path | required | bare response body |
|---|---|---|
| `stable/etf/asset-exposure` | `symbol` | `Query Error: Invalid or missing query parameter - symbol` |
| `stable/etf/country-weightings` | `symbol` | same |
| `stable/etf/holdings` | `symbol` | same |
| `stable/etf/info` | `symbol` | same |
| `stable/etf/sector-weightings` | `symbol` | same |
| `stable/funds/disclosure` | `symbol`, `year`, `quarter` | names them **one at a time**, in that order |
| `stable/funds/disclosure-dates` | `symbol` | `… - symbol` |
| `stable/funds/disclosure-holders-latest` | `symbol` | `… - symbol` |
| `stable/funds/disclosure-holders-search` | `name` | `Query Error: Invalid or missing query parameter - name` |

`funds/disclosure` was walked down: `symbol=SPY` alone returns `… - year`; adding `year=2026` returns
`… - quarter`; adding `quarter=1` returns 503 rows. `symbol=` (present but empty) is a 400, not a 200 with an
empty list.

**`symbol` takes exactly one symbol.** `symbol=SPY,QQQ` returned `[]` — HTTP 200, no error — on both
`etf/info` and `etf/sector-weightings`. The plural `symbols=SPY,QQQ` is a 400 naming `symbol`. So the
comma-joined form used by `QuoteEndpoints.Batch` has no place here: it silently answers nothing.

`symbol` is **case-insensitive**: `symbol=spy` returned the same 1 row from `etf/info` and the same 12 rows
from `etf/sector-weightings` as `symbol=SPY`, byte for byte.

**Unknown input is an empty list, never an error.** `symbol=ZZZZNOPE` returned `[]` with HTTP 200 on all eight
symbol paths, and `name=Zzzznotafund` returned `[]` on the ninth. So did `symbol=AAPL` on the four ETF-only
paths — a stock is not an error, it is simply not an ETF. Unknown parameters are ignored outright:
`etf/info?symbol=SPY&wibble=42` returned SPY's row unchanged.

## Nine paths, nine shapes

No key tuple is shared by any two paths. Each path emitted exactly **one** key order across the whole corpus,
and **no row was ever missing a key**.

| path | keys | distinct key orders | rows measured |
|---|---|---|---|
| `etf/asset-exposure` | 5 | 1 | 16,065 |
| `etf/country-weightings` | 2 | 1 | 227 |
| `etf/holdings` | 9 | 1 | 35,185 |
| `etf/info` | 19 | 1 | 33 |
| `etf/sector-weightings` | 3 | 1 | 95 |
| `funds/disclosure` | 23 | 1 | 11,522 |
| `funds/disclosure-dates` | 3 | 1 | 127 |
| `funds/disclosure-holders-latest` | 7 | 1 | 3,979 |
| `funds/disclosure-holders-search` | 13 | 1 | 71,934 |

The tuples, in wire order:

- **`etf/asset-exposure`** — `symbol`, `asset`, `sharesNumber`, `weightPercentage`, `marketValue`
- **`etf/country-weightings`** — `country`, `weightPercentage`
- **`etf/holdings`** — `symbol`, `asset`, `name`, `isin`, `securityCusip`, `sharesNumber`, `weightPercentage`,
  `marketValue`, `updatedAt`
- **`etf/info`** — `symbol`, `name`, `description`, `isin`, `assetClass`, `securityCusip`, `domicile`,
  `website`, `etfCompany`, `expenseRatio`, `assetsUnderManagement`, `avgVolume`, `inceptionDate`, `nav`,
  `navCurrency`, `holdingsCount`, `isActivelyTrading`, `updatedAt`, `sectorsList`
- **`etf/sector-weightings`** — `symbol`, `sector`, `weightPercentage`
- **`funds/disclosure`** — `cik`, `date`, `acceptedDate`, `symbol`, `name`, `lei`, `title`, `cusip`, `isin`,
  `balance`, `units`, `cur_cd`, `valUsd`, `pctVal`, `payoffProfile`, `assetCat`, `issuerCat`, `invCountry`,
  `isRestrictedSec`, `fairValLevel`, `isCashCollateral`, `isNonCashCollateral`, `isLoanByFund`
- **`funds/disclosure-dates`** — `date`, `year`, `quarter`
- **`funds/disclosure-holders-latest`** — `cik`, `holder`, `securityCusip`, `shares`, `dateReported`,
  `change`, `weightPercent`
- **`funds/disclosure-holders-search`** — `symbol`, `cik`, `classId`, `seriesId`, `entityName`,
  `entityOrgType`, `seriesName`, `className`, `reportingFileNumber`, `address`, `city`, `zipCode`, `state`

`funds/disclosure` is the only path in the SDK so far to send a **snake_case** key: `cur_cd`. It sits between
`units` and `valUsd`, both camelCase, in the same object.

`etf/asset-exposure` reverses the direction of the other four `etf/*` paths. Given `symbol=AAPL` it answers
**which ETFs hold AAPL** — 3,293 rows, each naming a different ETF in `symbol` with `asset` fixed at `AAPL`.
Confirmed constant: `asset` was identical across every row of all 8 responses. `symbol=SPY` works too (39
rows, ETFs that hold SPY), so the parameter is "any asset", not "any stock".

## Not one of the nine honours `limit` or `page`

Measured by fetching the same query with and without the parameters and comparing byte counts:

| query | rows | bytes |
|---|---|---|
| `etf/holdings?symbol=BND` | 17,252 | 4,949,598 |
| `etf/holdings?symbol=BND&limit=10` | 17,252 | 4,949,598 |
| `etf/holdings?symbol=BND&page=1` | 17,252 | 4,949,598 |
| `etf/holdings?symbol=BND&limit=10&page=1` | 17,252 | 4,949,598 |
| `etf/asset-exposure?symbol=NVDA` / `&limit=10` / `&page=1` | 3,860 each | 588,479 each |
| `funds/disclosure-holders-latest?symbol=AAPL` / `&limit=10` / `&page=1` | 3,209 each | 701,175 each |
| `funds/disclosure-holders-search?name=Fidelity` / `&limit=10` / `&page=1` | 2,379 each | 1,037,895 each |
| `funds/disclosure?symbol=SPY&year=2026&quarter=1&limit=10` | 503 | 325,046 |
| `funds/disclosure-dates?symbol=SPY&limit=5` | 28 | 1,962 |

Identical in every case. `limit` and `page` are ignored exactly the way `wibble` is. **There is no pagination
in this slice, so there is nothing to walk and no page ceiling to enforce** — and no way to ask for less than
everything.

Everything is therefore a single response, and single responses here get large:

| largest observed | rows | bytes |
|---|---|---|
| `funds/disclosure-holders-search?name=Trust` | 66,065 | 27,446,218 |
| `etf/holdings?symbol=BND` | 17,252 | 4,949,598 |
| `etf/holdings?symbol=VXUS` | 8,821 | 2,503,610 |
| `funds/disclosure-holders-search?name=Fidelity` | 2,379 | 1,037,895 |

## `weightPercentage` is a number on one path and a percent-string on its sibling

`etf/sector-weightings` and `etf/country-weightings` are the same idea, one letter apart in the URL, and they
disagree about the type of the field they share.

| path | key | wire | example |
|---|---|---|---|
| `etf/sector-weightings` | `weightPercentage` | JSON number | `1.62` |
| `etf/country-weightings` | `weightPercentage` | JSON **string with a `%`** | `"97.52%"` |
| `etf/holdings` | `weightPercentage` | JSON number | `8.29427804` |
| `etf/asset-exposure` | `weightPercentage` | JSON number | `0.34179638` |

227 of 227 country rows were strings; 95 of 95 sector rows were numbers. The string forms vary in decimals —
`"0%"`, `"0.01%"`, `"9.9%"`, `"97.52%"`, `"100%"` — so a fixed-width parse will not do. Both are percentages
out of 100, not fractions: sector weights summed to exactly `100.00` on all 13 ETFs, country weights to
between `99.95` and `100.01`.

The SDK has no converter for a percent-suffixed number today. `TolerantDecimalJsonConverter` reads a quoted
number but not a trailing `%`: `decimal.TryParse("97.52%", NumberStyles.Float, …)` is `false`, so it would
null the field silently on all 227 rows.

## `updatedAt` is two different wire formats, and `etf/holdings`' is UTC

Two paths send a key called `updatedAt`. They do not agree on the format.

| path | form | n | example |
|---|---|---|---|
| `etf/holdings` | `uuuu-MM-dd HH:mm:ss` | 35,185/35,185 | `2026-08-30 06:51:13` |
| `etf/info` | `uuuu-MM-dd'T'HH:mm:ss.fff'Z'` | 33/33 | `2026-08-29T23:12:50.006Z` |

`etf/info`'s form carries its own offset and needs no measurement: it is UTC because it says so.

`etf/holdings`' form is the ambiguous one the SDK has already met twice — read as UTC by
`NullableFmpInstantJsonConverter` and as Eastern by `NullableEasternInstantJsonConverter`, four or five hours
apart. **It is UTC here, and the falsifying evidence rode in the same HTTP response.**

`etf/holdings?symbol=SCHD` returned `updatedAt = 2026-08-30 06:51:13` in a response whose own `Date` header
read `Sun, 30 Aug 2026 10:05:35 GMT`. Read as Eastern, `06:51:13` EDT is `10:51:13Z` — **46 minutes after FMP
generated the response that carried it**. A cache stamp cannot postdate its own response, so Eastern is
falsified. Read as UTC it is 3h14m old, which is ordinary. A repeat fetch 18 seconds later reproduced it:
same wire value, header `10:05:53 GMT`, same 0.76-hour impossibility under Eastern.

The value is **constant across every row of a response** — 33 of 33 responses had exactly one distinct
`updatedAt` — so it is a per-symbol cache stamp, not a per-holding one. Staleness varies widely: on the same
sweep, SCHD read 3.2 hours old and IJH/IJR read 284 hours — **twelve days**. SCHD is the same response the
UTC falsification above turns on: `updatedAt` `2026-08-30 06:51:13` against its own `Date` header
`Sun, 30 Aug 2026 10:05:35 GMT`.

## `funds/disclosure`'s `acceptedDate` is the SEC-filings field verbatim, so it is Eastern

`funds/disclosure.acceptedDate` uses the same ambiguous `uuuu-MM-dd HH:mm:ss` form, 11,522 of 11,522 rows.
Rather than re-derive its zone, it was tested for identity against a field whose zone the SDK **already
measured**: `acceptedDate` on `sec-filings-*`, established as Eastern against EDGAR on 2026-08-26 and
implemented by `NullableEasternInstantJsonConverter`.

Twenty NPORT-P filings were pulled for two CIKs across ten quarters each (SPY trust `0000884394`, Vanguard
Index Funds `0000036405`, 2024 Q1 through 2026 Q2), then looked up a second time through
`stable/sec-filings-search/cik` over a ±1 day window.

- **12 of 19 matched to the second**, including **10 of 10** for the SPY trust.
- The 7 that did not are Vanguard's same-day sibling filings — one NPORT-P per series, filed minutes apart,
  with the `limit=100` window returning a different member of the batch.
- **Largest residual across all 19: 90 seconds.** The full set of closest-match deltas is
  `0 ×12, 4, 7, 19, 20, 25, 27, 90` seconds.

One hour is 3,600 seconds and four hours are 14,400. Nothing in that distribution is a timezone offset. The
two paths carry the same instant in the same encoding, so `funds/disclosure.acceptedDate` inherits the
measured Eastern reading.

**So this slice needs both converters, as #33 does — but for different reasons.** `etf/holdings.updatedAt` is
UTC by direct falsification; `funds/disclosure.acceptedDate` is Eastern by identity with an already-measured
field.

A third route was tried and closed: SEC EDGAR's own `data.sec.gov/submissions/CIK0000884394.json` answered
**HTTP 403** — *"Your Request Originates from an Undeclared Automated Tool"*. That is an access control, and
it was not worked around. It was not needed: the cross-path identity above is a stronger result anyway,
because it compares FMP against FMP.

## Absence is spelled four ways, and one field uses two of them

Rows counted over distinct responses only.

| path | field | spelling | count | of rows |
|---|---|---|---|---|
| `etf/holdings` | `asset` | `""` | 17,988 | 51.1% |
| `etf/holdings` | `isin` | `""` | 17,927 | 51.0% |
| `etf/holdings` | `securityCusip` | `""` | 8,036 | 22.8% |
| `funds/disclosure` | `symbol` | JSON `null` | 176 | 1.5% |
| `funds/disclosure` | `lei` | `"N/A"` | 495 | 4.3% |
| `funds/disclosure` | `cusip` | `"N/A"` | 202 | 1.8% |
| `funds/disclosure` | `isin` | `""` | 149 | 1.3% |
| `funds/disclosure` | `payoffProfile` | `"N/A"` | 123 | 1.1% |
| `funds/disclosure` | `name` | `"N/A"` | 120 | 1.0% |
| `funds/disclosure` | `invCountry` | `"N/A"` | 120 | 1.0% |
| `funds/disclosure-holders-latest` | `holder` | `""` | 16 | 0.4% |
| `funds/disclosure-holders-latest` | `securityCusip` | `"N/A"` | 3 | 0.1% |
| `funds/disclosure-holders-search` | `address` | JSON `null` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `symbol` | `"NULL"` | 1,622 | 27.6% |
| `funds/disclosure-holders-search` | `entityOrgType` | `"NULL"` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `reportingFileNumber` | `"NULL"` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `city` | `"NULL"` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `zipCode` | `"NULL"` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `state` | `"NULL"` | 1,540 | 26.2% |
| `funds/disclosure-holders-search` | `className` | `"NULL"` | 3 | 0.1% |

`funds/disclosure-holders-search` rates here exclude the `name=Trust` query, whose 66,065 rows would otherwise
dominate the denominator; including it the six `"NULL"` fields all read 64.3% and `className` picks up a
second spelling, **`"N/A"` ×192 alongside `"NULL"` ×1,278** — one field, two sentinels, in one corpus.

The `"NULL"` rows travel together. `entityOrgType`, `reportingFileNumber`, `city`, `zipCode` and `state` were
`"NULL"` on exactly the same 1,540 rows on which `address` was a real JSON `null` — a whole address block
missing, encoded two different ways in one object:

```
{"symbol":"NULL","cik":"0000110055","classId":"C000005579","seriesId":"S000002175",
 "entityName":"BLACKROCK SUSTAINABLE BALANCED FUND, INC.","entityOrgType":"NULL",
 "seriesName":"BLACKROCK SUSTAINABLE BALANCED FUND, INC.","className":"Investor B",
 "reportingFileNumber":"NULL","address":null,"city":"NULL","zipCode":"NULL","state":"NULL"}
```

`symbol` is `"NULL"` on 82 more rows than the address block is, so it is not purely the same population.

`entityOrgType` is otherwise a **numeric string** — `"30"` ×3,635, `"32"` ×17, `"33"` ×5 — which makes
`"NULL"` an outright parse failure for any caller who reaches for `int.Parse`. The same shape appears on
`funds/disclosure.fairValLevel`: `"1"` ×3,829, `"2"` ×28, `"3"` ×4, always quoted.

`etf/holdings.asset` being empty on 51% of rows is not an anomaly to route around — it is what a bond fund
looks like. BND's 17,252 holdings are mostly unlisted debt with no ticker; VXUS's 8,821 are mostly foreign
lines. `name` was populated on all 35,185 rows, so the human-readable identity is always there.

## `funds/disclosure`'s remaining categoricals

Measured over the 3,861-row single-quarter sample (SPY, QQQ, VFIAX, FXAIX, ARKK; 2026 Q1 and Q2):

| field | values |
|---|---|
| `units` | `NS` ×3,830, `NC` ×29, `PA` ×2 |
| `cur_cd` | `USD` ×3,832, **`USDUSD` ×29** |
| `payoffProfile` | `Long` ×3,831, `N/A` ×30 |
| `assetCat` | `EC` ×3,818, `DE` ×30, `STIV` ×10, `DBT` ×2, `EP` ×1 |
| `issuerCat` | `CORP` ×3,736, `OTHER` ×115, `RF` ×6, `UST` ×2, `PF` ×2 |
| `invCountry` | 17 distinct ISO-2 codes plus `N/A` |
| `isRestrictedSec` | `N` ×3,861 |
| `isNonCashCollateral` | `N` ×3,861 |
| `isCashCollateral` | `N` ×3,855, `Y` ×6 |
| `isLoanByFund` | `N` ×3,605, `Y` ×256 |

`cur_cd` = **`USDUSD`** is a doubled currency code, not a typo in this document. All 29 occurrences are equity
futures rows (`units: NC`, `assetCat: DE`, `payoffProfile: N/A`) — e.g. `ESH6`, "CHICAGO MERCANTILE EXCH INC".
It is a wire defect, recorded so that a strict three-letter currency type is not chosen by mistake.

The four `is*` fields are `Y`/`N` strings, not JSON booleans. Two of the four were constant at `N` across the
whole sample, so their `Y` form is unmeasured — the vocabulary is inferred from the other two, not observed.

## `etf/info.sectorsList` is `etf/sector-weightings`, byte for byte

`etf/info` carries a nested array under `sectorsList`, whose objects use `industry` and `exposure` where
`etf/sector-weightings` uses `sector` and `weightPercentage`. Different key names, same data:

**All 13 ETFs cross-checked agreed on both the key set and every value**, with no rounding difference —
including the 12-element SPY and VOO lists, the 11-element QQQ list, and the 1-element GLD, SLV, TLT and BND
lists. The nested `industry` key holds sector names (`Basic Materials`, `Cash & Others`, …), not industries.

So one of the nine paths is fully contained in another. That is a design decision, not a fact: the slice can
model it once and reuse it, or model it twice and say why.

## `etf/info.holdingsCount` is not the number of holdings

Cross-checked on 33 ETFs against the row count `etf/holdings` returned for the same symbol on the same day:

| symbol | `info.holdingsCount` | rows from `etf/holdings` |
|---|---|---|
| BND | 346 | **17,252** |
| ARKK | 10 | **47** |
| VXUS | 8,602 | 8,821 |
| VYM | 589 | 613 |
| VUG | 166 | 150 |
| SPY | 504 | 505 |
| QQQ | 103 | 107 |
| GLD, SLV | **0** | 1 each |
| SCHD | 103 | 103 |

**They agreed on 1 of 33.** Most disagreements are small — the two paths refresh from different snapshots on
different days — but BND is off by a factor of fifty and ARKK by a factor of five, and two ETFs report zero
holdings while returning one. The field cannot be used to pre-size a buffer, to page (there is no paging), or
to decide whether calling `etf/holdings` is worthwhile.

## Ordering

Measured per path; the SDK should document what it observed and promise nothing more.

| path | ordering |
|---|---|
| `etf/holdings` | `weightPercentage` **descending** — held on all 3 files checked, including 17,252 rows |
| `etf/country-weightings` | `weightPercentage` **descending** |
| `etf/sector-weightings` | **alphabetical by `sector`**, not by weight — `Basic Materials`, `Cash & Others`, … |
| `etf/asset-exposure` | not sorted by `weightPercentage` or `marketValue` |
| `funds/disclosure` | not sorted by `pctVal` |
| `funds/disclosure-dates` | `date` **descending** |
| `funds/disclosure-holders-latest` | not sorted by `weightPercent` |

So the two weightings paths, which look like a matched pair, sort differently from each other as well as
typing their shared field differently.

## `funds/disclosure-dates` and what `year`/`quarter` select

28 rows for SPY spanning **2019-09-30 to 2026-06-30**, descending. `year` and `quarter` are derived from
`date` consistently: over 80 rows across SPY, ARKK and FXAIX there were **0 mismatches** against
`year = date.Year` and `quarter = (date.Month - 1) / 3 + 1`.

The dates are **fiscal**, not calendar quarter-ends: FXAIX reports on `2026-05-31` and `2019-11-30`, ARKK on
`2026-04-30`. The `quarter` field still counts calendar quarters, so FXAIX's May year-end reads as Q2.

`year` and `quarter` on `funds/disclosure` must be integers — `quarter=Q1` and `year=abc` are both HTTP 400 —
but **out-of-range integers are accepted silently**: `quarter=0`, `quarter=5`, `year=1990` and `year=2030` all
returned HTTP 200 with `[]`. A caller who mistakenly sends `quarter=0` gets "no holdings", not "bad request".

Symbols with no N-PORT filings return `[]` rather than an error: `GLD`, `BND` and `AAPL` all did.

## `funds/disclosure-holders-latest` is per-holder latest, not a single as-of date

One response mixes reporting dates, and mixes far more of them than four. SPY's 220 rows carry **19
distinct dates spanning 2019-09-30 to 2026-06-30**; AAPL's 3,209 rows carry **66, spanning 2019-09-30 to
2026-07-31**. Four dates dominate both — SPY `2026-06-30` ×124, `2026-04-30` ×44, `2026-05-31` ×30,
`2026-03-31` ×2, and AAPL the same four at ×1,644 / ×755 / ×451 / ×52 — but they account for 200 of SPY's 220
rows and 2,902 of AAPL's 3,209. The tail is old: **18 of SPY's rows and 292 of AAPL's report a date before
2026 at all**, the oldest being 2019-09-30 in both.

So "latest" is each holder's own most recent filing, and a holder that stopped filing seven years ago is still
in the response with its 2019 position. Rows in one response are **not** comparable as of one date, and the
spread is years wide rather than one quarter wide.

`securityCusip` is also not constant per response. AAPL's response mixes `037833100` (the common stock, 867
rows) with `037833EF3` and `037833DZ0` — Apple's bonds — and SPY's mixes `78462F103` with `000000000` and
synthetic `AEI…` identifiers. The path answers "funds holding any security of this issuer".

`change` is signed and frequently zero — AAPL: 2,532 zero, 291 positive, 386 negative.

## `name` on `funds/disclosure-holders-search` is a whole-word match

Measured against `entityName`:

| query | rows | why |
|---|---|---|
| `Vanguard` / `vanguard` / `VANGUARD` | 548 each | case-insensitive; identical byte counts |
| `Vangua` | **0** | a prefix is not a word |
| `van` | 201 | matches `VAN KAMPEN EQUITY TRUST`, `Van Kampen Partners Trust`, … |
| `eck` | 0 | no standalone word `eck` |
| `Fid` / `fidelit` | 0 each | prefixes again |
| `Fidelity` | 2,379 | |
| `Vanguard Group` | **0** | multi-word queries match nothing |
| `Trust` | 66,065 | 27.4 MB |

So: case-insensitive, whole-word, single-word. A substring will not do, and a two-word company name is the
one thing a caller is most likely to type. The exact tokenisation was not established beyond this and the SDK
should not assert one.

## Numbers

`decimal` throughout, following the house rule the statement records state — every figure `decimal`, never
`double`, with `BulkEndOfDayPrice` the single deliberate exception. Nothing measured here argues against it.

**Negatives are ordinary.** Short and derivative positions put minus signs on fields a reader might assume
unsigned:

| path | field | min | max |
|---|---|---|---|
| `etf/holdings` | `sharesNumber` | −2,920,694,176 | 71,557,356,084 |
| `etf/holdings` | `weightPercentage` | −0.34898692 | 100 |
| `etf/holdings` | `marketValue` | −560,343,250 | 155,526,370,000 |
| `etf/asset-exposure` | `weightPercentage` | **−199.9869** | **50,506** |
| `etf/asset-exposure` | `marketValue` | −103,015,045.5 | 7,434,183,997,921.512 |
| `funds/disclosure` | `valUsd` | −41,402,229.68 | 125,580,304,518.46 |
| `funds/disclosure` | `pctVal` | −0.0032285713047007715 | **10.880031435864327** |
| `funds/disclosure-holders-latest` | `shares` | −990 | 1,016,998,069 |
| `funds/disclosure-holders-latest` | `weightPercent` | 1.2e-07 | **264.39824722** |

Three of those percentage fields exceed 100 and one reaches 50,506, so **none of them can be range-checked or
documented as a 0–100 percentage.** `sharesNumber` and `balance` are fractional as well as negative
(`0.0001383508577753182`, `0.668`), so an integer type is wrong for both.

**The wire uses exponent notation**, e.g. `-2.4437904357910156e-05` and `1.2e-07`. The most extreme value
found is `1.4210854715202004e-14` — SPY's `Cash & Others` sector weight, which is 2⁻⁴⁶, the residue of a
floating-point subtraction. It needs 30 decimal places and `decimal` has 28.

This was checked rather than assumed. `JsonSerializer.Deserialize<decimal>` on .NET 10 **rounds it and does
not throw**:

```
1.4210854715202004e-14   ->  0.0000000000000142108547152020
8.715470541117531e-10    ->  0.0000000008715470541117531
-2.4437904357910156e-05  ->  -0.000024437904357910156
7434183997921.512        ->  7434183997921.512
```

The loss is the trailing `04` — about 4e-31 of a percentage point, on a value that is itself numerical noise.
Recorded so that nobody later "fixes" it by switching the slice to `double`, which would round every other
figure in the table above far more damagingly.

## Constant and near-constant fields

Measured over 33 `etf/info` rows — a small sample, and stated as such:

| field | values |
|---|---|
| `domicile` | `US` ×33 |
| `navCurrency` | `USD` ×33 |
| `isActivelyTrading` | `true` ×33 — the only genuine JSON boolean in the slice |
| `assetClass` | `Equity`, `Fixed Income`, `Commodities`, `International Equity`, `Large Cap Equity`,
  `Core Investment Grade Bond` |

`assetClass` is already inconsistent at n=33 — `Equity` and `Large Cap Equity` and `International Equity` are
not one vocabulary — so it is a free string, not an enum.

Echo fields **are** reliable: `etf/holdings.symbol` was constant across every row of all 33 responses,
`etf/asset-exposure.asset` across all 8, and `funds/disclosure.cik` across all 27.

## Coverage extents

- `funds/disclosure-dates`: back to **2019-09-30** (SPY), **2019-11-30** (FXAIX), **2020-04-30** (ARKK).
- `funds/disclosure`: answered for every quarter 2024 Q1 – 2026 Q2 tried; 2026 Q3 and Q4 return `[]`.
- `etf/holdings` staleness on 2026-08-30 ranged from **3.2 hours** (SCHD) to **284 hours** (IJH, IJR).
- `funds/disclosure-holders-latest` reporting dates in one response spanned **2019-09-30 to 2026-07-31**.

## What the design has to decide

1. **One facade or two.** Nine paths under one heading, but `etf/*` and `funds/*` are different subjects with
   different parameters. `MarketPerformanceEndpoints` took eleven paths in one facade; the precedent exists
   either way.
2. **Nine records, or eight plus a reused one.** `etf/info.sectorsList` is `etf/sector-weightings` exactly.
   Reusing the record needs the nested `industry`/`exposure` names bound, not the outer `sector`/
   `weightPercentage` ones — the same wire-name-versus-property problem `MarketMover.ChangePercentage`
   already carries.
3. **The percent-string.** `etf/country-weightings.weightPercentage` needs a converter that strips `%` and
   returns `decimal?`, or the property stays a `string` and the caller parses. No existing converter does it.
4. **The sentinels.** Four spellings of absence, up to 27.6% of rows on one path. Either a converter maps
   `""`/`"N/A"`/`"NULL"` to `null` — which loses the distinction between "FMP sent nothing" and "FMP sent the
   word NULL", the same trade `TolerantDecimalJsonConverter` already documents — or every property keeps the
   sentinel and the XML doc names it. This is the largest single decision in the slice.
5. **`entityOrgType` and `fairValLevel`.** Numeric strings with a non-numeric sentinel. `string?`, or `int?`
   through a converter that nulls the sentinel.
6. **The `is*` quartet.** `Y`/`N` strings on `funds/disclosure`. `bool?` through a converter, or `string?`.
   Two of the four never emitted `Y` in the corpus, so a converter would be reading partly on inference.
7. **Whether to guard `quarter`.** FMP accepts `quarter=0` and `quarter=5` with HTTP 200 and `[]`. The
   `StatementEndpoints` precedent rejects client-side rather than letting FMP answer something the caller did
   not ask for.
8. **What to say about size.** No pagination and a 27.4 MB worst case, on a path whose most natural query
   (`name=Trust`) is the one that produces it. The XML doc is the only place a caller will find out.
