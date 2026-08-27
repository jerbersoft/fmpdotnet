# Statements — measured 2026-08-27, Ultimate key

The 19 unmodelled paths of issue #28, measured before any of them is modelled. Every number below
came from a live call on 2026-08-27; nothing here is taken from FMP's documentation.

## Coverage

**All 19 answered 200.** None is gated behind an add-on on this plan, so the whole group is
implementable now. `AAPL` is the reference filer; `JPM`, `XOM`, `O`, `TSM`, `SHOP` and `BRK-B` were
used wherever a single filer would have hidden the variance.

## Six paths share one envelope

Four `*-as-reported` paths and both segmentation paths answer the identical five-field envelope
around an open dictionary:

```
symbol, fiscalYear, period, reportedCurrency, date, data
```

`data` is a JSON object whose keys are **not a fixed schema**:

| `data` keys | AAPL | JPM | XOM | O | TSM | SHOP |
|---|---|---|---|---|---|---|
| `income-statement-as-reported` | 24 | 39 | 25 | 28 | 38 | 27 |
| `financial-statement-full-as-reported` | 300 | 923 | 453 | 466 | 589 | 365 |
| `revenue-product-segmentation` | 5 | 5 | 6 | 1 | 2 | 2 |
| `revenue-geographic-segmentation` | 5 | 4 | 2 | 1 | 4 | 5 |

The as-reported keys are lowercased, concatenated XBRL tags —
`revenuefromcontractwithcustomerexcludingassessedtax`, `costofgoodsandservicessold`. The
segmentation keys are the company's own segment names — `{"Mac": 33708000000, "iPhone":
209586000000}` for AAPL, `{"Consumer & Community Banking": …}` for JPM — so they carry spaces,
ampersands and commas.

**`data` values are not all numbers.** `financial-statement-full-as-reported` for AAPL FY2025 holds
234 ints, 47 strings and 19 floats in one dictionary. The strings are filing metadata:
`documenttype: "10-K"`, `documentannualreport: "true"`, `currentfiscalyearenddate: "--09-27"`,
`entityincorporationstatecountrycode: "CA"`. A `Dictionary<string, decimal>` throws on those 47.

Measured over AAPL, JPM, BRK-B and TSM: no null values, no keys colliding under
case-insensitive comparison, no non-ASCII keys, and the largest magnitude anywhere is 7.1e12 —
comfortably inside `decimal`.

**The two segmentation paths are the exception, and provably so.** Across AAPL, JPM, XOM, O, TSM,
SHOP, BRK-B and KO, both segmentation endpoints, and both `annual` and `quarter` — every row, not a
sample — the `data` values were 3,201 ints and 36 floats and **not one string**. The mixed-type
problem belongs to the four `*-as-reported` paths only; segmentation is genuinely
segment-name-to-number, even though it arrives in the same envelope.

## The remaining thirteen

| path | rows (AAPL) | notes |
|---|---|---|
| `income-statement-ttm` | 164, oldest 1985-09-30 | field set of `IncomeStatement` exactly |
| `balance-sheet-statement-ttm` | 152 | `BalanceSheetStatement` minus one field |
| `cash-flow-statement-ttm` | 147 | field set of `CashFlowStatement` exactly |
| `income-statement-growth` | 41 | field set of `income-statement-growth-bulk` exactly |
| `balance-sheet-statement-growth` | 41 | field set of `balance-sheet-statement-growth-bulk` exactly |
| `cash-flow-statement-growth` | 37 | field set of `cash-flow-statement-growth-bulk` exactly |
| `key-metrics-ttm` | 1 | field set of `key-metrics-ttm-bulk` exactly |
| `ratios-ttm` | 1 | field set of `ratios-ttm-bulk` exactly |
| `owner-earnings` | 50 | **hard cap, see below** |
| `financial-reports-dates` | 65 | FY + Q1–Q4, 2013–2026 |
| `financial-reports-json` | object, 73 keys | **not modellable as a record, see below** |
| `financial-reports-xlsx` | 1.4 MB binary | **lies about its content type, see below** |
| `latest-financial-statements` | 250/page × 101 pages | whole-market recency feed |

## `limit` and `period`

**The default `limit` is 5** on every per-symbol paged path — `income-statement-growth`,
`owner-earnings`, the three `*-ttm` statements, the four `*-as-reported`. Sending no `limit` returns
five rows out of a history up to 164 deep. There is no server cap above that: `limit=1000`,
`limit=10000` and `limit=100000` all return the same true total.

**This already affects the eight paths the SDK ships.** `Periodic()` omits `limit` when the caller
passes none, so `GetIncomeStatementAsync("AAPL")` sends no limit and FMP answers 5. Measured
2026-08-27, AAPL annual, no limit versus `limit=100000`:

```
income-statement 5/41   balance-sheet-statement 5/41   cash-flow-statement 5/37   ratios 5/41
key-metrics      5/41   financial-growth        5/41   enterprise-values   5/41
```

Nothing in those methods' XML documentation mentions it, and no `<param>` tag describes `limit` at
all. A caller asking for a company's history gets the last five periods and no indication that the
other thirty-six exist.

**`revenue-*-segmentation` and `financial-reports-dates` ignore `limit` entirely** and always
transfer the full set — the behaviour already recorded for `etf-list` and its siblings.

**`key-metrics-ttm` and `ratios-ttm` ignore both `limit` and `period`.** Each is a single row: the
current trailing-twelve-month snapshot, with no date field of any kind. Two calls days apart are not
comparable as a series and nothing in the payload says so.

**`period` takes five values, not two.** Beyond `annual` and `quarter`, the four fiscal quarters
work as filters:

```
period=annual   ->  FY2025, FY2024, FY2023, FY2022, FY2021, FY2020
period=quarter  ->  Q32026, Q22026, Q12026, Q42025, Q32025, Q22025
period=Q1       ->  Q12026, Q12025, Q12024, Q12023, Q12022, Q12021
period=Q4       ->  Q42025, Q42024, Q42023, Q42022, Q42021, Q42020
```

`FY` is accepted as a synonym for `annual`. **This also works on the eight statement paths the SDK
already ships** — `stable/income-statement?period=Q1` returns Q1-only rows today — so the existing
`Periodic()` helper under-exposes the live API, not just the new paths.

**An unrecognised `period` silently falls back to annual.** `period=bogus` answers FY rows with
HTTP 200 and no warning. A typo costs you the wrong series, quietly.

`owner-earnings` and the three `*-ttm` statements **accept `period` and ignore it** — they are
quarterly and rolling respectively, and always answer newest-first from the latest quarter.

## `owner-earnings` truncates at 50 and does not say so

Every long-history filer returns exactly 50 rows at `limit=100000`:

```
AAPL 50 (oldest 2014-03-29)   MSFT 50 (2014-03-31)   GE 50 (2014-05-09)   KO 50 (2014-03-28)
JPM  50 (oldest 2013-12-31)   IBM  50 (2014-03-31)   PG 50 (2014-03-31)
```

`income-statement-ttm` for the same filers returns 164 rows back to 1985-09-30, so the 50 is the
endpoint's ceiling and not the extent of FMP's data. SHOP returns 46, which is that company's real
history — so a count below 50 is data and a count of exactly 50 is a truncation you cannot detect
from the payload.

## `financial-reports-xlsx` lies about its content type

It answers `Content-Type: application/json; charset=utf-8` and a body beginning `PK\x03\x04` — a
1,399,564-byte XLSX zip. Pointing a JSON deserializer at it throws.

Worse, **a miss is also HTTP 200**: an unknown symbol or a year with no filing returns exactly
16 bytes, `Error with query`, still under `Content-Type: application/json`. That is neither a zip
nor JSON, so the only reliable success test is the `PK` magic number.

The `Content-Disposition` header carries a usable name with non-standard spacing around the equals
sign and a trailing underscore: `attachment; filename = AAPL_2025_FY_.xlsx`.

## `financial-reports-json` is a rendered document, not a record

The top level is an object of 73 keys: `symbol`, `period`, `year`, and 70 **report section names**
truncated to about 30 characters — `"CONSOLIDATED STATEMENTS OF OPER"`, `"CONSOLIDATED BALANCE
SHEETS (Pa"`. Section names carry spaces, parentheses and commas, and differ per filing; `period=Q1`
answers 45 keys against `FY`'s 73.

Each section is a list of single-key objects, the key being a full column header and the value a
list of cell strings:

```json
{"CONSOLIDATED BALANCE SHEETS - USD ($) shares in Thousands, $ in Millions": ["Sep. 27, 2025", "Sep. 28, 2024"]}
```

**A miss returns HTTP 200 with `{"Error Message": "No Data for this symbol or invalid API call…"}`** —
the same 200-carrying-an-error shape the SDK already handles for the bulk surface. `year` is
required; omitting it gives 400 `Query Error: Invalid or missing query parameter - year`.

## `latest-financial-statements` is a three-week window, not the universe

Sorted by `dateAdded` descending, 250 rows per page, and `page` is capped at **100** —
`page=101` returns 400 `Maxmium Query Parameter: The maximum page number for this endpoint is
'100'` (FMP's spelling). That makes 25,250 rows reachable in total; page 100 was still returning
rows dated 2026-08-05, so the ceiling cuts about three weeks back and the rest is unreachable.

It is the only path in this group keyed on **`calendarYear`** rather than `fiscalYear`, and its
`dateAdded` is a space-separated datetime — `"2026-08-27 11:03:21"` — not ISO-8601 with a `T`.

## `fiscalYear` is an int on six paths and a string on seven

| wire type | paths |
|---|---|
| `int` | the four `*-as-reported`, both segmentations, `financial-reports-dates` |
| `string` | the three `*-growth`, the three `*-ttm`, `owner-earnings` |
| absent | `latest-financial-statements` (uses `calendarYear`, int) |

`JsonNumberHandling.AllowReadingFromString` is already on the SDK's serializer context, so one `int?`
property reads both — but only because that option is set, which makes it load-bearing rather than
incidental.

## Error shapes

| condition | response |
|---|---|
| unknown symbol | **HTTP 200 and `[]`** on all eleven list-shaped paths |
| missing `symbol` | HTTP 400, plain text `Query Error: Invalid or missing query parameter - symbol` |
| missing `year` (reports) | HTTP 400, plain text `Query Error: Invalid or missing query parameter - year` |
| invalid `period=Q9` | HTTP 400, plain text `Query Error: Invalid or missing query parameter - period` |
| `financial-reports-json` miss | HTTP 200, `{"Error Message": …}` |
| `financial-reports-xlsx` miss | HTTP 200, 16 bytes of `Error with query` |

## Eight of the nineteen need no new model

Wire field sets compared exactly against what the SDK already ships:

| path | existing type | verdict |
|---|---|---|
| `income-statement-ttm` | `IncomeStatement` | identical set, 39 fields |
| `cash-flow-statement-ttm` | `CashFlowStatement` | identical set, 47 fields |
| `balance-sheet-statement-ttm` | `BalanceSheetStatement` | 60 of the model's 61 — see below |
| `ratios-ttm` | `RatiosTtm` | identical set, 62 fields |
| `key-metrics-ttm` | `KeyMetricsTtm` | identical set, 43 fields |
| `income-statement-growth` | `IncomeStatementGrowth` | identical set, 34 fields |
| `balance-sheet-statement-growth` | `BalanceSheetGrowth` | identical set, 56 fields |
| `cash-flow-statement-growth` | `CashFlowGrowth` | identical set, 42 fields |

`balance-sheet-statement-ttm` omits `capitalLeaseObligationsNonCurrent` **structurally, not per
filer**: across AAPL, JPM, XOM, O, TSM, SHOP, BRK-B, KO, GE and MSFT the TTM row carried exactly 60
keys and never that one, while the plain `balance-sheet-statement` row carried it for all ten. The
model's property is nullable, so it binds as null — but a caller reading it off a TTM row is reading
an absence, not a zero.

The five growth/TTM-metric types were built from the CSV bulk endpoints, and the JSON and CSV forms
of each carry **exactly the same field names** — including FMP's own typos
(`growthNetCashProvidedByOperatingActivites`) and its `TTM` suffixes (`grossProfitMarginTTM`).

**They cannot bind JSON as they stand.** Those five records carry no `[JsonPropertyName]` attributes
because the CSV reader maps them by an explicit wire-name lookup, and their C# property names
deliberately drop the `TTM` suffix (`GrossProfitMargin`). The serializer context sets
`PropertyNameCaseInsensitive` but no naming policy, so JSON binding falls back to the property name:
`grossProfitMarginTTM` would not match `GrossProfitMargin`, every metric would land null, and
`symbol` alone would populate. Reuse is the right call, but it means adding the attributes and
proving the binding with a test that fails when they are absent.

## Method

Eight passes, recorded in the session's scratchpad: reach all 19; classify shapes; probe `limit`,
`period` and caps; diff field sets against the shipped models and against the CSV headers cached
from 2026-08-26; vary the filer; and pin the error shapes. Bulk was read from the on-disk developer
cache rather than refetched, so no `*-bulk` call was made.

## Addendum — the report paths' `period` vocabulary, measured 2026-08-27 while planning

The design left open which vocabulary `financial-reports-json` and `financial-reports-xlsx` take. The
links `financial-reports-dates` hands out use the **response** vocabulary — `&period=Q3` — which
would have made the SDK's `FiscalPeriod` unusable on them. Measured rather than assumed:

| `period=` | `financial-reports-json` | `financial-reports-xlsx` |
|---|---|---|
| `FY` | 200, 73 keys, echoes `"period": "FY"` | 200, `AAPL_2025_FY_.xlsx`, 1,399,564 bytes |
| `annual` | 200, 73 keys, echoes `"period": "FY"` | 200, `AAPL_2025_FY_.xlsx`, 1,399,564 bytes |
| `Q3` | 200, 47 keys, echoes `"period": "Q3"` | 200, `AAPL_2025_Q3_.xlsx`, 785,087 bytes |
| `quarter` | 200, 45 keys, echoes **`"period": "Q1"`** | 200, **`AAPL_2025_Q1_.xlsx`**, 58,263 bytes |
| `bogus` | **HTTP 400** `Query Error: Invalid or missing query parameter - period` | — |

Both vocabularies work and normalise to the response one, so `FiscalPeriod` maps onto these paths
without a second enum.

**`period=quarter` is a trap on these two paths.** A report is identified by one fiscal period, and
there is no such document as "the 2025 quarterly report" — so FMP picks Q1 and says so only in a
field the caller has to go looking for. The size difference is the evidence that this is not what
anyone meant: 58 KB for the Q1 workbook against 785 KB for the Q3 one they asked for. The SDK
rejects `FiscalPeriod.Quarter` on the two document methods rather than passing it on.

**An unrecognised `period` is a 400 here**, not the silent annual fallback the statement paths give.
The two behaviours differ on the same query parameter of the same API.

**A miss carries no `Content-Disposition`.** Both a bad symbol and a good symbol in a year with no
filing answer HTTP 200, 16 bytes of `Error with query`, and the header absent — a second signal
agreeing with the `PK\x03\x04` test. The magic number stays the rule, because it describes the body
the SDK is about to hand back rather than a header FMP could stop sending.
