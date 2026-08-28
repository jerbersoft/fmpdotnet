# Form 13F and Insider Trades — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-28** across eight probe passes, **76 captured responses**, all ordinary JSON endpoints. No `*-bulk`
path was touched.

Issue [#36](https://github.com/herbertsabanal/fmpdotnet/issues/36) lists fourteen paths. All fourteen were
probed. Where a claim rests on a single response, the row count is given so the claim can be read at its real
strength.

## Entitlement — all fourteen are reachable

No path returned 402. Four answered 200 with no parameters at all; the other ten returned 400 naming the one
parameter they wanted, which is itself proof of reachability.

| path | bare call |
|---|---|
| `stable/institutional-ownership/latest` | 200, 100 rows |
| `stable/insider-trading/latest` | 200, 100 rows |
| `stable/insider-trading/search` | 200, 100 rows |
| `stable/insider-trading-transaction-type` | 200, 18 rows |
| `stable/institutional-ownership/dates` | 400 — `cik` |
| `stable/institutional-ownership/extract` | 400 — `cik` |
| `stable/institutional-ownership/holder-industry-breakdown` | 400 — `cik` |
| `stable/institutional-ownership/holder-performance-summary` | 400 — `cik` |
| `stable/institutional-ownership/extract-analytics/holder` | 400 — `symbol` |
| `stable/institutional-ownership/symbol-positions-summary` | 400 — `symbol` |
| `stable/institutional-ownership/industry-summary` | 400 — `year` |
| `stable/acquisition-of-beneficial-ownership` | 400 — `symbol` |
| `stable/insider-trading/statistics` | 400 — `symbol` |
| `stable/insider-trading/reporting-name` | 400 — `name` |

Every 400 carried the same wording as the previous slices: `Query Error: Invalid or missing query parameter -
<name>`.

## The required parameters arrive one at a time

A 400 names exactly one missing parameter. Satisfying it can produce a second 400 naming the next. Four paths
needed two rounds to reach 200, and one needed three:

| path | round 1 | round 2 | round 3 |
|---|---|---|---|
| `extract` | `cik` | `year` | — (quarter not demanded, see below) |
| `holder-industry-breakdown` | `cik` | `year` | — |
| `extract-analytics/holder` | `symbol` | `year` | — |
| `symbol-positions-summary` | `symbol` | `year` | — |
| `industry-summary` | `year` | `quarter` | — |

**`quarter` is demanded only by `industry-summary`.** The other four reached 200 on `cik`/`symbol` + `year`
alone. `quarter` was supplied on every probe in this pass, so what those four return *without* it is not
measured — the design should not assume it is optional until that is checked.

## `dates` is the discovery endpoint for the other seven

`institutional-ownership/dates?cik=…` returns the filer's available quarters, newest first. Three keys, no
nulls:

```
{"date": "2026-06-30", "year": 2026, "quarter": 2}
```

Berkshire Hathaway (`cik=0001067983`) returned **53 rows**, spanning 2013-06-30 to 2026-06-30. `year` and
`quarter` arrive as JSON numbers, `date` as an ISO date string. This is the only path that tells a caller which
`year`/`quarter` pairs the other seven will answer for.

## CIK zero-padding is irrelevant

`cik=0001067983` and `cik=1067983` both returned the same 53 rows from `dates`. The API accepts either form.
Responses always echo the **zero-padded ten-digit** form regardless of which was sent.

## `limit` and `page` are accepted and ignored on `extract`

The headline parameter trap. Against `cik=0000093751&year=2026&quarter=2`:

| query | rows | response bytes |
|---|---|---|
| `&limit=5` | 4177 | 2,335,566 |
| (no limit) | 4177 | 2,335,566 |
| `&page=2` | 4177 | 2,335,566 |

**Byte-identical.** A caller who passes `limit=5` receives 2.3 MB and 4,177 rows. Neither parameter is
rejected, so nothing in the response tells the caller they were ignored.

Contrast `insider-trading/latest` and `institutional-ownership/latest`, where `limit` **is** honoured:
`limit=200` returned 200 rows, `limit=1000` returned 1000. **The 100 rows a bare call returns is a default, not
a cap.** `page=1` on `insider-trading/latest` returned 100 rows.

## Four numeric fields would silently become null, and seven overflow `int`

Measured across `extract` (7,346 rows over three filers), `extract-analytics/holder` (600 rows over six
symbols), `holder-performance-summary` (53 rows), `symbol-positions-summary` (6 symbols) and
`industry-summary` (757 rows over two quarters).

**Exceeding `int` is routine, not exceptional.** Largest absolute value seen per field:

| field | path | max observed | fits `int`? |
|---|---|---|---|
| `industryValue` | `industry-summary` | 9,306,785,606,770 | no |
| `totalInvested` | `symbol-positions-summary` | 2,840,158,192,185 | no |
| `marketValue` | `extract-analytics/holder` | 388,558,449,933 | no |
| `performanceSinceInception` | `holder-performance-summary` | 288,653,953,205 | no |
| `value` | `extract` | 202,047,567,909 | no |
| `numberOf13Fshares` | `symbol-positions-summary` | 16,201,347,267 | no |
| `changeInPerformance` | `extract-analytics/holder` | 72,987,083,136 | no |

`sharesNumber` on `extract-analytics/holder` topped out at **1,941,918,386** — inside `int`, but at 90% of its
ceiling. One larger holder or one penny stock puts it over. It is not a safe `int`.

## The same dollar quantity is integral on one path and fractional on another

This is the finding that decides the numeric types, and it is the same defect shape that shipped as
`CompanyProfile.Volume` (`long?` against a fractional wire value, corrected 2026-08-28).

`marketValue`, `value`, `performance` and their siblings were integral on **every one** of the 7,946 rows
sampled across `extract` and `extract-analytics/holder`. On that evidence alone a `long?` would look safe.

`industryValue` on `industry-summary` is the same kind of quantity — an aggregate dollar value — and it is
**fractional**:

| quarter | rows | fractional | rate |
|---|---|---|---|
| 2026 Q2 | 363 | 6 | 1.7% |
| 2025 Q4 | 394 | 53 | 13.5% |

Actual values, 2025 Q4:

```
BIOLOGICAL PRODUCTS, (NO DIAGNOSTIC SUBSTANCES)   523604028974.8208
BLANK CHECKS                                       41070972041.9478
COMMERCIAL BANKS, NEC                             381160351419.47
AGRICULTURAL SERVICES                               1769618150.15
```

`523604028974.8208` carries twelve digits before the point and four after — sixteen significant digits.
**Checked, because the obvious claim about it is false:** this value *does* round-trip through `double`. The
stored double is `523604028974.82080078125`, which formats back to the same string. `double` is not
disqualified by this value, and the case for `decimal` here rests on the integer-overflow and fractionality
findings below, not on precision loss.

**Ruling for the design: every money and share-count field on these paths is `decimal?`.** Typing
`marketValue` as `long?` because 7,946 rows looked integral is precisely the reasoning that produced the
`CompanyProfile.Volume` defect. The rate at which a field is fractional is not stable across paths in this
family, and `System.Text.Json` *throws* on a fractional value bound to an integer property — costing the whole
response, not the one field.

## Insider share counts arrive fractional 4–6% of the time

Not a corner case. Measured on `insider-trading/latest?limit=1000` (1,000 rows, 2026-08-28):

| field | fractional | rate | max observed |
|---|---|---|---|
| `price` | 586 / 1000 | 58.6% | 17,372.52 |
| `securitiesOwned` | 59 / 1000 | 5.9% | 61,721,535 |
| `securitiesTransacted` | 40 / 1000 | 4.0% | 33,586,045 |

Real values, for the regression tests — a share **count**, not a price:

```
IBM   securitiesOwned       28447.467
IBM   securitiesTransacted   8375.5601
INTU  securitiesOwned        1690.4042
INTU  securitiesTransacted     62.405
```

The same four values appear in `insider-trading/search`, which shares this shape.

## Nulls and blanks, by path

FMP distinguishes JSON `null` from the empty string here, and which one it uses is per-field, not per-path.

**`insider-trading/latest`, 1,000 rows:**

| field | null | blank |
|---|---|---|
| `directOrIndirect` | 20 | 0 |
| `transactionType` | 0 | 40 |
| `acquisitionOrDisposition` | 0 | 40 |
| `securityName` | 0 | 20 |
| `typeOfOwner` | 0 | 4 |

`directOrIndirect` is the only field of the five that uses `null`. The other four use `""`. Any model that
treats absence uniformly will be wrong on one side or the other.

**`extract`, 7,346 rows:** `symbol` is **null on 2,209 rows — 30.1%**. A 13F holding need not have a ticker
(bonds, warrants, private placements). `Symbol` must be nullable, and a consumer keying on it will drop
three holdings in ten.

**`holder-industry-breakdown`, 718 rows over three filers:** `performancePercentage` null on 29,
`changeInWeightPercentage` null on 13, `industryTitle` null on 2.

**`institutional-ownership/latest`, 1,000 rows:** no nulls, no blanks, in any of its eight fields.

## `putCallShare` on `extract` carries no information

Blank on **all 7,346 rows** across three filers. Never null, never populated — always `""`. The same field on
`extract-analytics/holder` *is* populated (`"Share"`). Modelling it on `extract` is modelling a constant.

## `acceptedDate` and `filingDate` change wire format by path

Same field names, two different formats, and the existing converters disagree about which is which.

| path | `filingDate` | `acceptedDate` |
|---|---|---|
| `institutional-ownership/latest` | `2026-08-28 00:00:00` | `2026-08-28 15:47:03` |
| `institutional-ownership/extract` | `2026-08-14` | `2026-08-14` |
| `extract-analytics/holder` | `2026-08-07` | — |
| `acquisition-of-beneficial-ownership` | `2026-04-29` | `2026-04-29` |
| `insider-trading/latest` | `2026-08-28` | — |

On `institutional-ownership/latest`, measured over 1,000 rows:

- `filingDate` is `00:00:00` on **1000 of 1000** — a date wearing a datetime's clothes.
- `acceptedDate` is at exactly midnight on **0 of 1000** — a real timestamp.

**Why this matters:** `NullableLocalDateJsonConverter` parses with `LocalDatePattern.Iso` and **returns null on
a parse failure rather than throwing** (`NodaConverters.cs:35-48`). Point it at
`institutional-ownership/latest.filingDate` and every row reads `null` — a silent data loss with nothing in a
diff or a test run to show for it.

**No new converter is needed.** The shipped set already covers both shapes:

- `NullableDateAtMidnightJsonConverter` (`NodaConverters.cs:184`) parses exactly `uuuu-MM-dd HH:mm:ss` and
  returns `.Date` — correct for `institutional-ownership/latest.filingDate`.
- `NullableLocalDateJsonConverter` — correct for the date-only paths.

The trap is choosing per field per path, and it earns a test that fails if a model is repointed at the wrong
one.

## `acquisition-of-beneficial-ownership` sends every number as a string

All six numeric fields arrive as JSON **strings**, not numbers:

```json
{"soleVotingPower": "0", "amountBeneficiallyOwned": "1099168953", "percentOfClass": "7.48"}
```

Measured over **422 rows** across AAPL, MSFT, TSLA and GME: every non-null value parses as a number — no
`"N/A"`, no `"-"`, no thousands separators. `sharedVotingPower` is null on 2 rows; no other field is ever null
and none is ever blank.

`TolerantDecimalJsonConverter` (`NodaConverters.cs:328`) already reads a `String` token via
`decimal.TryParse` with `NumberStyles.Float`, invariant culture, returning null on failure and never throwing.
It is the right converter here as shipped.

## `insider-trading/latest` and `/search` return an identical field set

Both return the same sixteen keys in the same order:

```
symbol, filingDate, transactionDate, reportingCik, companyCik, transactionType,
securitiesOwned, reportingName, typeOfOwner, acquisitionOrDisposition,
directOrIndirect, formType, securitiesTransacted, price, securityName, url
```

What separates them is the query, not the payload. `search` accepts and honours discriminators that `latest`
does not — each returned 100 rows filtered as asked:

| query | distinct symbols in 100 rows |
|---|---|
| `symbol=AAPL` | 1 |
| `reportingCik=0001214128` | 1 |
| `companyCik=0000320193` | 1 |
| `transactionType=S-Sale` | 53 |

One record type serves both.

## `reporting-name` is a name→CIK lookup, not a trade list

It does not return trades. Two keys only:

```json
[{"reportingCik": "0001902974", "reportingName": "Cook Timothy DeVere"},
 {"reportingCik": "0002088821", "reportingName": "Cook Timothy Patrick"}]
```

`name=Cook%20Timothy` returned 2 rows; neither is Apple's CEO. It matches on the reporting person's name as
EDGAR spells it (surname first) and is best described as a resolver feeding `reportingCik` into
`insider-trading/search`.

## The eighteen insider transaction-type codes

`insider-trading-transaction-type` returns a flat code list — one key, 18 rows, no nulls:

```
A-Award  C-Conversion  D-Return  E-ExpireShort  F-InKind  G-Gift
H-ExpireLong  I-Discretionary  J-Other  L-Small  M-Exempt  O-OutOfTheMoney
P-Purchase  S-Sale  U-Tender  W-Will  X-InTheMoney  Z-Trust
```

The `transactionType` values observed on 1,000 `insider-trading/latest` rows are all drawn from this list, plus
the empty string on 40 rows.

**Not an enum.** The list is served by an endpoint, which means FMP can extend it without an SDK release. The
blank on 40 of 1,000 rows would have no member to map to.

## Form types are a small closed set, but from two different vocabularies

- `institutional-ownership/latest`, 1,000 rows: `13F-HR`, `13F-HR/A`, `13F-NT`, `13F-NT/A`.
- `insider-trading/latest`, 1,000 rows: `3`, `4`, `4/A`.

Both are `formType`. A shared type over the two would be modelling two different vocabularies as one.

## `ownershipPercent` exceeds 100 and that is not an error

`symbol-positions-summary`, 2026 Q2:

| symbol | `ownershipPercent` |
|---|---|
| MSFT | 128.2744 |
| AAPL | 110.1329 |
| NVDA | 98.3707 |
| KO | 88.3561 |
| BAC | 82.4992 |
| GME | 38.64 |

Two of six exceed 100. 13F filings double-count shares held through multiple reporting managers, so the sum
over filers legitimately exceeds shares outstanding. **No clamp, no validation, no "percent" wrapper that
assumes a 0–100 range.**

## Unknown inputs return `[]` with HTTP 200 — the silent-green hazard

Every negative probe answered 200 with an empty array. None returned 404, and none returned an error body:

| probe | result |
|---|---|
| `extract?cik=0000000001&year=2026&quarter=2` | `[]` |
| `extract?cik=0001067983&year=1999&quarter=2` | `[]` |
| `insider-trading/statistics?symbol=NOSUCHTICKERXYZ` | `[]` |
| `acquisition-of-beneficial-ownership?symbol=NOSUCHTICKERXYZ` | `[]` |

This is the failure mode `SweepCoverageTests` exists to catch, and **the current `LiveApi` constants walk
straight into it.** `LiveApi.Cik` is `"320193"` — Apple's CIK, an *issuer*. The four `cik`-keyed 13F paths want
an *institutional filer's* CIK. Measured:

| path, with `cik=320193` | result |
|---|---|
| `institutional-ownership/dates` | `[]` |
| `institutional-ownership/extract` | `[]` |
| `institutional-ownership/holder-industry-breakdown` | `[]` |
| `institutional-ownership/holder-performance-summary` | `[]` |

All four would record `outcome empty`, and a baseline that agrees with itself forever. **The sweep needs a
filer CIK constant distinct from `LiveApi.Cik`** — Berkshire's `0001067983` returned 53, 29, 24 and 53 rows
respectively.

Two smaller notes on the same mechanism:

- **`insider-trading/reporting-name` escapes by luck.** `Probe.Argument` maps `name` to
  `LiveApi.AcquirerNameQuery`, which is `"Apple"`. That is a company name, but EDGAR has a reporting person
  named **`Apple Allan Victor`** (`cik 0001493086`), so the probe returns real rows. It works, for a reason
  that has nothing to do with intent.
- **`quarter` fails loudly, which is correct.** `Probe.Argument`'s `int` arm ends `_ => throw
  Unknown(parameter)` (`Probe.cs:404`), and `quarter` is not among its cases. It will throw rather than
  silently defaulting — unlike the `string` arm's `_ => LiveApi.Symbol` (`Probe.cs:318`), which is what makes
  the CIK problem above silent.

## JSON types, by field family

Across all fourteen paths, every field falls into one of five shapes:

| shape | fields | note |
|---|---|---|
| ISO date string | `date`, `firstAdded`, `transactionDate`, date-only `filingDate`/`acceptedDate` | `NullableLocalDateJsonConverter` |
| `uuuu-MM-dd HH:mm:ss` string | `filingDate`/`acceptedDate` on `institutional-ownership/latest` | see above |
| number, mixed `int`/`float` | all money, share, weight, percentage and performance fields | **`decimal?`** |
| numeric string | all six on `acquisition-of-beneficial-ownership` | `TolerantDecimalJsonConverter` |
| plain string | identifiers, names, codes, URLs | nullable or blank per the table above |

`bool` appears in exactly three places, all on `extract-analytics/holder`: `isNew`, `isSoldOut`,
`isCountedForPerformance`. None was ever null across 600 rows.

## What was not measured

Stated plainly rather than inferred:

- **Whether `quarter` is optional on the four paths that did not demand it.** Every probe supplied it.
- **Whether `limit`/`page` are honoured on the eleven paths other than `extract`, `insider-trading/latest` and
  `institutional-ownership/latest`.** Only those three were tested for it.
- **The upper bound of `limit`.** 1000 was honoured; nothing larger was tried.
- **Whether `marketValue`, `value` or `performance` are *ever* fractional.** 7,946 rows say no. The
  `industryValue` evidence says the family does it, which is why the design types them `decimal?` anyway —
  that is a deliberate choice under uncertainty, not a measurement.
- **Any `*-bulk` variant.** Untouched by policy.
