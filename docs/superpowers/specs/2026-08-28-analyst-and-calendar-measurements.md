# Analyst and Calendar — measurements

Every fact the [design](2026-08-28-analyst-and-calendar-design.md) rests on, with the date it was measured.
Measured against the live API on **2026-08-28** across six probe passes, roughly 110 calls, all ordinary JSON
endpoints. No `*-bulk` path was touched.

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

## The other three calendar-shaped paths do not hit the cap

Probed over 2000-01-01 .. 2026-08-28:

| path | rows |
|---|---|
| `splits-calendar` | 947 |
| `ipos-calendar` | 443 |
| `ipos-prospectus` | 26,876 |
| `ipos-disclosure` | **132,332** |

`ipos-disclosure` and `ipos-prospectus` are not capped and not paginated — a wide range returns the whole
matching set in one response. 132,332 rows is a payload concern rather than a truncation one, and the
opposite failure mode from the cap above: the caller gets everything they asked for and may not want it.

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

## `splitType` says nothing in two different ways

Across 961 `splits-calendar` rows, `splitType` took four values: `stock-split`, `stock-dividend`, `spin-off`,
and the literal string **`"None"`** — on top of being JSON-null in 16 rows. A caller checking for null misses
the `"None"` rows and vice versa. This is the same `"None"` sentinel measured on the SEC filing paths in the
previous slice.

## Low-cardinality string fields, and why none becomes an enum

| path | field | distinct | values |
|---|---|---|---|
| `dividends` (AAPL) | `frequency` | 2 | Quarterly, Irregular |
| `dividends-calendar` | `frequency` | **8** | Quarterly, Semi-Annual, Monthly, Annual, Weekly, Irregular, Special, Bi-Weekly |
| `grades` | `action` | 3 | maintain, downgrade, upgrade *(lower case)* |
| `grades` | `newGrade` | **20** | Buy, Outperform, Overweight, Neutral, Hold, Market Perform, Equal Weight, Underweight, … |
| `splits-calendar` | `splitType` | 4 | stock-split, None, stock-dividend, spin-off |
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
| `ipos-calendar` | `symbol`, `date`, `daa`, `company`, `exchange`, `actions` |
| `ipos-prospectus` | `symbol`, `acceptedDate`, `filingDate`, `ipoDate`, `cik`, `form`, `url` |

`cik` arrives zero-padded to ten characters (`"0001610590"`), matching every other path in this SDK.

`numerator` and `denominator` on the split paths are JSON numbers and were **whole in 961 of 961 rows** —
including awkward real-world ratios like 707/500 and 729/1000 from non-US listings. No fractional value was
observed.

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
