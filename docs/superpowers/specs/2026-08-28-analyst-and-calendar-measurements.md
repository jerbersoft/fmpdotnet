# Analyst and Calendar — measurements

Every fact the [design](2026-08-28-analyst-and-calendar-design.md) rests on, with the date it was measured.
Measured against the live API on **2026-08-28** across six probe passes, roughly 110 calls, all ordinary JSON
endpoints. No `*-bulk` path was touched.

**A second pass on the same day, while the implementation plan was being written, re-verified every finding a
signature or a trap test rests on and corrected four of them.** Each correction is marked in place below. Three
were transcription faults — a sentinel conflated with one from the previous slice, a field left out of a type
table, a magnitude never checked. The fourth was an inference stated as a measurement: two paths were recorded
as not truncating, on the strength of a row count, when in fact they truncate by a mechanism a row count cannot
see. Everything not marked as corrected was re-measured and held exactly.

## Entitlement — all fourteen are reachable

No path returned 402. Six answered 200 with no parameters at all; the other eight returned 400 naming the
parameter they wanted, which is itself proof of reachability.

| path | bare call |
|---|---|
| `stable/dividends-calendar` | 200, **4000 rows** — the cap, see below |
| `stable/ipos-calendar` | 200, 450 rows |
| `stable/ipos-disclosure` | 200, 8,838 rows |
| `stable/ipos-prospectus` | 200, 165 rows |
| `stable/splits-calendar` | 200, 961 rows |
| `stable/grades` | 400 — requires `symbol` |
| `stable/grades-consensus` | 400 — requires `symbol` |
| `stable/grades-historical` | 400 — requires `symbol` |
| `stable/price-target-consensus` | 400 — requires `symbol` |
| `stable/price-target-summary` | 400 — requires `symbol` |
| `stable/ratings-historical` | 400 — requires `symbol` |
| `stable/ratings-snapshot` | 400 — requires `symbol` |
| `stable/dividends` | 400 — requires `symbol` |
| `stable/splits` | 400 — requires `symbol` |

Every 400 carried the same wording: `Query Error: Invalid or missing query parameter - symbol`.

## `dividends-calendar` truncates at 4000 rows and eats the front of the range

The headline finding, and the same defect already documented on `stable/earnings-calendar`.

| requested window | rows | earliest returned | latest returned |
|---|---|---|---|
| 2025-01-01 .. 2025-12-31 | **4000** | **2025-12-29** | 2025-12-31 |
| 2025-06-01 .. 2025-06-30 | **4000** | **2025-06-26** | 2025-06-30 |
| 2025-06-01 .. 2025-06-07 | 2147 | 2025-06-01 | 2025-06-06 |
| 2025-06-02 .. 2025-06-02 | 876 | 2025-06-02 | 2025-06-02 |

A request for a **full year returns the last three days of it.** A request for one month returns the last five.
The rows that vanish are the *oldest* ones, so a caller who asks for a year and reads `rows[0]` is handed
December while believing they hold January.

`limit=10000` was accepted and ignored — still exactly 4000. A `from`/`to` spanning 2020-01-01 to 2026-08-28
also returned exactly 4000. There is no cursor, so the SDK cannot page around it.

**Density, which is why a fixed safe chunk width cannot be derived from the calendar alone:**

| day | rows |
|---|---|
| 2025-06-02 | 876 |
| 2025-03-14 | 673 |
| 2025-11-20 | 340 |

At 340–876 rows a day, the cap falls somewhere between five and eleven days depending on the season. The
six-day window above returned 2147 and was complete; the thirty-day window was not.

**`earnings-calendar` still shows the same signature.** Measured the same day for comparison: 2025-01-01 to
2025-12-31 answered 4000 rows with an earliest date of **2025-11-25**. The shipped
`EarningsCalendarResult` already detects and reports this; `dividends-calendar` has no equivalent.

## `splits-calendar` and `ipos-calendar` truncate too — by a 90-day window, not by a row cap

**Found while planning, 2026-08-28.** This section first read "the other three calendar-shaped paths do not hit
the cap", inferred from their row counts over a wide range: `splits-calendar` 947, `ipos-calendar` 443. Those
counts are real. The inference from them was wrong — a response can be far below a row cap and still be
truncated, and both of these are.

**Neither path will reach more than 90 days back from `to`.** Measured against four different `to` values
spanning twenty months, with `from` fixed at 2015-01-01 in every call:

| `to` | `splits-calendar` rows | earliest returned | `ipos-calendar` rows | earliest returned | gap |
|---|---|---|---|---|---|
| 2024-12-31 | 737 | 2024-10-02 | 358 | 2024-10-02 | **90 days** |
| 2025-06-30 | 620 | 2025-04-01 | 446 | 2025-04-01 | **90 days** |
| 2026-03-31 | 632 | 2025-12-31 | 449 | 2025-12-31 | **90 days** |
| 2026-08-28 | 947 | 2026-05-31 | 443 | 2026-06-01 | 89 / 88 days |

So **a request for the whole of 2024 answers Q4 of 2024** — nine months of the requested range are silently
absent, and the caller is told nothing. The window is measured from `to`, not from today: four `to` values
twenty months apart each produced their own 90-day window.

Walking `from` backwards against a fixed `to=2026-08-28` on `splits-calendar` shows the edge exactly:

| `from` | days before `to` | rows | earliest returned |
|---|---|---|---|
| 2026-06-29 | 60 | 628 | 2026-06-29 — honoured |
| 2026-06-09 | 80 | 825 | 2026-06-09 — honoured |
| 2026-06-01 | 88 | 946 | 2026-06-01 — honoured |
| 2026-05-30 | 90 | 947 | 2026-05-31 |
| 2026-05-20 | 100 | 947 | 2026-05-31 — saturated |
| 2026-04-30 | 120 | 947 | 2026-05-31 — saturated |
| 2026-03-01 | 180 | 947 | 2026-05-31 — saturated |

Past 90 days the answer stops changing: same row count, same earliest date, however far back you ask.

**This is the same failure as the row cap and a different mechanism, which matters for the defence.** Both drop
rows from the front of the range without saying so. But 737 rows is nowhere near 4000, so a row-count test can
never see this one — only comparing the earliest returned date against the requested `from` can.

**`ipos-disclosure` and `ipos-prospectus` do not do this.** Both answered the full range asked for:

| path | request | rows | span returned |
|---|---|---|---|
| `ipos-disclosure` | 2024-01-01 .. 2024-12-31 | **25,689** | 2024-01-02 .. 2024-12-31 |
| `ipos-prospectus` | 2024-01-01 .. 2024-12-31 | 1,048 | 2024-01-04 .. 2024-12-31 |
| `ipos-disclosure` | 2020-01-01 .. 2026-08-28 | **123,678** | whole range |
| `ipos-prospectus` | 2020-01-01 .. 2026-08-28 | 15,726 | whole range |

Neither is capped and neither is paginated — a wide range returns the whole matching set in one response.
123,678 rows is a payload concern rather than a truncation one, and the opposite failure mode from the two
above: the caller gets everything they asked for and may not want it.

**Three of the five date-ranged paths in this slice therefore truncate**, by two different mechanisms:
`dividends-calendar` by a 4000-row cap, `splits-calendar` and `ipos-calendar` by a 90-day window.

One observation recorded without an explanation, because it appears only on already-truncated responses: a
`dividends-calendar` request with `to=2025-06-30` returned a latest date of 2025-07-01, and `to=2026-03-31`
returned 2026-04-01. Four narrow windows that came back **under** the cap — one day, two days, three days —
returned nothing past `to` and nothing before `from`. So the boundary overshoot is a property of the truncated
responses only, and is one more reason not to trust a truncated one about its own extent.

## Parameters that are accepted and ignored

**`from` and `to` are ignored on every per-symbol path.** Identical row counts with and without a
2024-01-01..2024-12-31 range:

| path | all | 2024 only |
|---|---|---|
| `dividends` | 92 | 92 |
| `splits` | 5 | 5 |
| `grades` | 1791 | 1791 |
| `grades-historical` | 92 | 92 |
| `ratings-historical` | 1000 | 1000 |

**`grades` ignores `limit` and `page` as well.** `limit=5` and `limit=10000` both returned 1791 rows for
AAPL; `page=1` returned the same 1791 rows with a byte-identical first row. Row count does vary by symbol —
AAPL 1791, MSFT 967, BRK-B 93 — so this is the whole set each time, not a fixed cap.

## `ratings-historical` returns exactly one row unless asked otherwise

| `limit` | rows |
|---|---|
| *(absent)* | **1** |
| 5 | 5 |
| 100 | 100 |
| 1000 | 1000 |
| 5000 | 5000 |
| 10000 | 6292 |
| 50000 | 6292 |

The default is one row on an endpoint whose name promises history. 6292 is AAPL's whole series, not a cap —
it stops growing because the data does.

By contrast `grades-historical` and `dividends` honour `limit` but top out at their real sizes (92 each for
AAPL) regardless of how much is asked for.

## Three pairs of paths return byte-identical field sets

| record | paths | fields |
|---|---|---|
| dividend | `dividends`, `dividends-calendar` | `symbol`, `date`, `recordDate`, `paymentDate`, `declarationDate`, `adjDividend`, `dividend`, `yield`, `frequency` |
| split | `splits`, `splits-calendar` | `symbol`, `date`, `numerator`, `denominator`, `splitType` |

The rating pair differs by exactly one field: `ratings-snapshot` sends nine, `ratings-historical` sends the
same nine plus `date`.

```
ratings-snapshot    symbol, rating, overallScore, discountedCashFlowScore, returnOnEquityScore,
                    returnOnAssetsScore, debtToEquityScore, priceToEarningsScore, priceToBookScore
ratings-historical  symbol, date, rating, overallScore, …the same seven…
```

**The shipped `BulkCompanyRating` cannot serve them.** It carries `symbol, date, rating,
discountedCashFlowScore, returnOnEquityScore, returnOnAssetsScore, debtToEquityScore,
priceToEarningsScore, priceToBookScore` — nine fields, with **no `overallScore`**. The ordinary paths have it.

## `grades-consensus` is not the latest row of `grades-historical`

The two carry five analyst-count fields under different names, and a caller could reasonably assume one is a
current view of the other. The values say otherwise. Both for AAPL, measured the same minute:

```
grades-historical row 0   date 2026-08-01   strongBuy 6   buy 22   hold 14   sell 3   strongSell 2   (total 47)
grades-consensus          (no date)         strongBuy 1   buy 70   hold 32   sell 9   strongSell 0   (total 112)
```

Different populations, not a stale copy — the totals differ by more than a factor of two and the shape of the
distribution is different. `grades-historical` names them `analystRatingsStrongBuy` … `analystRatingsStrongSell`;
`grades-consensus` names them `strongBuy` … `strongSell` and adds a `consensus` string (`"Buy"` for AAPL).

## `ipos-calendar.daa` carries no information

Every one of the 450 rows was checked. The date part of `daa` equalled `date` in **450 of 450**, and the time
part took exactly **one** distinct value across the whole response: `T04:00:00.000Z`.

```
"date": "2026-08-26",  "daa": "2026-08-26T04:00:00.000Z"
```

04:00 UTC is midnight Eastern. `daa` is therefore `date` at midnight in EDT, expressed as UTC — the same
value twice, in two formats, under a name that explains neither.

## `acceptedDate` means something different here than on the SEC filing paths

On `ipos-disclosure` and `ipos-prospectus`, every date-shaped field is a plain 10-character ISO date:

| path | field | lengths observed | example |
|---|---|---|---|
| `ipos-disclosure` | `filingDate` | 10 | `2026-08-26` |
| `ipos-disclosure` | `acceptedDate` | 10 | `2026-08-26` |
| `ipos-disclosure` | `effectivenessDate` | 10 | `2026-08-26` |
| `ipos-prospectus` | `filingDate` | 10 | `2026-05-29` |
| `ipos-prospectus` | `acceptedDate` | 10 | `2026-05-29` |
| `ipos-prospectus` | `ipoDate` | 10 | `1989-03-02` |

`SecFiling.AcceptedDate` reads a **19**-character `uuuu-MM-dd HH:mm:ss` stamp through
`NullableEasternInstantJsonConverter`. Pointing that converter at these fields would answer null for every
row, silently.

## Nulls and blanks, by path

Surveyed over the row counts shown. Fields not listed were populated in every row.

| path | n | absent values |
|---|---|---|
| `grades` | 1791 | none |
| `grades-consensus` | 1 | none |
| `grades-historical` | 92 | none |
| `price-target-consensus` | 1 | none |
| `price-target-summary` | 1 | none |
| `ratings-historical` | 200 | none |
| `ratings-snapshot` | 1 | none |
| `dividends` | 92 | `declarationDate` **blank** ×15 |
| `dividends-calendar` | 4000 | `recordDate` blank ×30, `paymentDate` blank ×41, `declarationDate` **blank ×2232** |
| `ipos-calendar` | 450 | `shares` **null ×349**, `priceRange` **null ×441**, `marketCap` **null ×354** |
| `ipos-disclosure` | 8838 | none |
| `ipos-prospectus` | 165 | none |
| `splits` | 5 | none |
| `splits-calendar` | 961 | `splitType` **null ×16** |

Two different conventions for "no value" appear in the same slice: the dividend paths send an **empty string**,
`ipos-calendar` sends **JSON null**. Over half of `dividends-calendar`'s rows have no declaration date.

## `splitType` is null on 16 of 961 rows, and that is the whole of it

Across 961 `splits-calendar` rows, `splitType` took three string values — `stock-split` ×934,
`stock-dividend` ×10, `spin-off` ×1 — and was **JSON-null on 16**. Nothing else.

**Correction, 2026-08-28 (re-measured while planning).** This section first read that `splitType` also took
the literal string `"None"`, "the same `"None"` sentinel measured on the SEC filing paths in the previous
slice". That was wrong, and it was wrong by conflation rather than by observation: the `"None"` sentinel is
real, and it is on the *classification* paths of the previous slice, where `symbol` reads `"None"`. Re-measured
field by field over all 961 rows on all five fields, the string `"None"` appears **zero times anywhere in the
response**. The "four values" in the cardinality table below is correct only if JSON-null is counted as one of
the four, which is how it is now written.

## Low-cardinality string fields, and why none becomes an enum

| path | field | distinct | values |
|---|---|---|---|
| `dividends` (AAPL) | `frequency` | 2 | Quarterly, Irregular |
| `dividends-calendar` | `frequency` | **8** | Quarterly, Semi-Annual, Monthly, Annual, Weekly, Irregular, Special, Bi-Weekly |
| `grades` | `action` | 3 | maintain, downgrade, upgrade *(lower case)* |
| `grades` | `newGrade` | **20** | Buy, Outperform, Overweight, Neutral, Hold, Market Perform, Equal Weight, Underweight, … |
| `splits-calendar` | `splitType` | 4 | stock-split ×934, **JSON-null ×16**, stock-dividend ×10, spin-off ×1 |
| `ipos-calendar` | `actions` | 2 | Expected, Priced |
| `ipos-calendar` | `exchange` | 2 | NASDAQ, NYSE |

`frequency` alone shows two on one path and eight on another, which is the argument against an enum: the
observed set depends on which path and which symbol was sampled, so it is a sample, not a domain.

## JSON types

Only these fields arrive as JSON strings; everything else is a real number or boolean.

| path | string-typed fields |
|---|---|
| `splits` | `symbol`, `date`, `splitType` |
| `dividends` | `symbol`, `date`, `recordDate`, `paymentDate`, `declarationDate`, `frequency` |
| `price-target-summary` | `symbol`, **`publishers`** |
| `ratings-snapshot` | `symbol`, `rating` |
| `ipos-calendar` | `symbol`, `date`, `daa`, `company`, `exchange`, `actions`, **`priceRange`** |
| `ipos-prospectus` | `symbol`, `acceptedDate`, `filingDate`, `ipoDate`, `cik`, `form`, `url` |

`cik` arrives zero-padded to ten characters (`"0001610590"`), matching every other path in this SDK.

`numerator` and `denominator` on the split paths are JSON numbers and were **whole in 961 of 961 rows** —
including awkward real-world ratios like 707/500 and 729/1000 from non-US listings. No fractional value was
observed. Their largest measured values are 1,011,977 and 1,000,000, comfortably inside `int`.

## Four numeric fields exceed `int`, and three arrive with a fractional part

**Added while planning, 2026-08-28**, from a magnitude sweep over every numeric field on all fourteen paths.
It was run because `priceRange` had been mistyped, and it found more:

| path | field | observed range | verdict |
|---|---|---|---|
| `ipos-calendar` | `marketCap` | 15,000,000 .. **74,999,999,925** | **exceeds `int`** |
| `ipos-calendar` | `shares` | 1,875,000 .. 555,555,555 | fits `int`, but a share count |
| `ipos-prospectus` | `pricePublicTotal` | 0 .. **74,999,999,925** | **exceeds `int`**, 13 of 165 fractional |
| `ipos-prospectus` | `proceedsBeforeExpensesTotal` | 0 .. **74,499,999,925** | **exceeds `int`**, 18 of 165 fractional |
| `ipos-prospectus` | `discountsAndCommissionsTotal` | 0 .. 500,000,000 | 11 of 165 fractional |
| `ipos-prospectus` | `pricePublicPerShare` | 0.12 .. 12,183,292 | 51 of 165 fractional |

`int.MaxValue` is 2,147,483,647, so `marketCap`, `pricePublicTotal` and `proceedsBeforeExpensesTotal` overflow
it by a factor of about 35. An `int?` property does not read such a value as null — `System.Text.Json` throws,
and because `FmpTransport` does not wrap `DeserializeAsync`, one row would cost the whole response.

Two fields on `dividends-calendar` mix integer and fractional JSON in the same column — `adjDividend` arrived
as an integer on 32 of 622 rows and as a float on 590 — which `decimal?` reads either way.

## `ipos-calendar.priceRange` is a formatted string

**Corrected while planning, 2026-08-28.** The JSON-types table above first omitted `priceRange` from
`ipos-calendar`'s string-typed fields, because it is null on 441 of 450 rows and the nine populated ones were
not typed. They are strings, all nine:

```
"5.00 - 7.00"   "10.00"   "15 - 17"   "8.00 - 10.00"   "11.25 - 13.25"   "16.00 - 18.00"   "15.00 - 17.00"
```

Six are ranges and three are single prices, so the field is not a number in either form — it is the same kind
of formatted string as `SecProfile.FiftyTwoWeekRange` measured in the previous slice. Typed `decimal?` it would
read **null on all 450 rows**: null on the 441 that are null, and null on the nine that are not, with nothing
in the data to show the difference.

## `publishers` is a string containing JSON

`price-target-summary` sends its publisher list as a **string whose content is a JSON array**:

```
"publishers": "[\"StreetInsider\",\"Benzinga\",\"Pulse 2.0\",\"MarketWatch\",\"Investing\",\"Barrons\",\"Investor's Business Daily\"]"
```

MSFT returned the same shape with six entries. Note `Investor's Business Daily` — the apostrophe is inside a
double-quoted JSON string and is therefore **correctly escaped**, unlike the `businessAddress` field measured
in the previous slice, where a stringified Python list broke on the same character. This one is real JSON and
survives a real parse.

The shipped `BulkPriceTargetSummary.Publishers` is already `IReadOnlyList<string>`, so the bulk path and the
ordinary path currently disagree about the type of this field.
