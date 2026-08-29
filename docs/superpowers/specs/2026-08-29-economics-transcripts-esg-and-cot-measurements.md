# Economics, Earnings Transcripts, ESG and COT — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-29** across nineteen probe passes, **133 captured responses**, all ordinary JSON endpoints. No
`*-bulk` path was touched.

Issue [#40](https://github.com/jerbersoft/fmpdotnet/issues/40) lists twelve paths. All twelve were probed.
Where a claim rests on a single response, the row count is given so the claim can be read at its real strength.

This is four unrelated groups behind one issue number, and they share no parameter vocabulary. What they do
share is a single failure mode, described under "Row caps" below: **four of the twelve silently return less
data the more you ask for.** That is the through-line of this slice.

## Entitlement — all twelve are reachable

No path returned 402. Seven answered 200 with no parameters at all; the other five returned 400 naming the one
parameter they wanted, which is itself proof of reachability.

| path | bare call |
|---|---|
| `stable/market-risk-premium` | 200, 192 rows |
| `stable/treasury-rates` | 200, 63 rows |
| `stable/earning-call-transcript-latest` | 200, 100 rows |
| `stable/esg-benchmark` | 200, 1003 rows |
| `stable/commitment-of-traders-analysis` | 200, 545 rows |
| `stable/commitment-of-traders-list` | 200, 65 rows |
| `stable/commitment-of-traders-report` | 200, 545 rows |
| `stable/economic-indicators` | 400 — `name` |
| `stable/earning-call-transcript` | 400 — `symbol` |
| `stable/earning-call-transcript-dates` | 400 — `symbol` |
| `stable/esg-disclosures` | 400 — `symbol` |
| `stable/esg-ratings` | 400 — `symbol` |

Every 400 carried the same wording as the previous slices: `Query Error: Invalid or missing query parameter -
<name>`.

## The worst trap in this slice: a 200 whose body is not JSON

`economic-indicators` answers an unrecognised `name` with **HTTP 200**, `content-type: application/json;
charset=utf-8`, and a body of **twelve bytes that are not JSON at all**:

```
Invalid name
```

Not `"Invalid name"` — no quotes. `json.loads` fails at line 1 column 1. Every other error surface in this SDK
is either a 4xx with a JSON envelope or a well-formed empty array; this one is a success status carrying a
payload that no deserializer can read. `JsonSerializer.Deserialize<List<EconomicObservation>>` will throw
`JsonException` from inside the transport's success path.

And the name is **case-sensitive**, so this is easy to hit by accident:

| call | status | body |
|---|---|---|
| `name=GDP` | 200 | `[{"name":"GDP","date":"2025-10-01","value":31422.526}]` |
| `name=gdp` | 200 | `Invalid name` |
| `name=notAnIndicator` | 200 | `Invalid name` |

A caller who lower-cases an indicator name gets an exception from a 200 response. The facade has to close this,
and the design has to decide what it becomes — a typed argument that cannot be spelled wrong, a translated
exception, or both.

### All 23 documented indicator names are valid

Every name in FMP's documented list was probed individually; **23 of 23 returned an array**, none returned
`Invalid name`. Two returned a well-formed empty array rather than data: `inflation` and
`3MonthOr90DayRatesAndYieldsCertificatesOfDeposit`. The other 21 returned between 1 and 61 rows, the count
tracking the series' own frequency — quarterly series 1 row, monthly 2–3, weekly 13, daily 61.

That the documented set is exactly the valid set is what makes a closed type viable here rather than a bare
string.

## Row caps — four paths silently drop data as the range widens

This is the same class of failure already documented on `stable/economic-calendar` in `EconomicsEndpoints`, and
it recurs four times in this slice with four different constants. In each case the status is 200, the array is
well-formed, and nothing reports the loss.

| path | cap | which rows survive |
|---|---|---|
| `economic-indicators` | ~3 months | newest in range |
| `treasury-rates` | ~3 months | newest in range |
| `commitment-of-traders-analysis` | **13 rows** | newest in range |
| `commitment-of-traders-report` | none measured | — |
| `earning-call-transcript-latest` | 100 rows | `limit` above 100 is clamped |

Measured on `treasury-rates`:

| range | rows | span returned |
|---|---|---|
| 2024-01-01 … 2024-01-31 (1 month) | 21 | 2024-01-02 … 2024-01-31 — complete |
| 2024-01-01 … 2024-03-31 (3 months) | 61 | 2024-01-02 … 2024-03-28 — complete |
| 2023-01-01 … 2024-12-31 (2 years) | **61** | **2024-10-02 … 2024-12-31 — 21 months missing** |
| 2026-05-23 … 2026-08-21 (90 days) | 62 | 2026-05-26 … 2026-08-21 — complete |

The 61 in the first two rows is a coincidence, not a cap: it is simply the number of trading days in those
spans, and the 90-day window re-measured 2026-08-29 answered **62**. What FMP truncates to is a **window of
about three months**, keeping the newest; the row count follows from the observation frequency. An earlier
draft of this file called it a 61-row cap.

Measured on `commitment-of-traders-analysis` versus its sibling `commitment-of-traders-report`, same symbol,
same query, issued together:

| range (`symbol=NG`) | analysis | report |
|---|---|---|
| 2024-01-01 … 2024-03-31 | 13 rows, 2024-01-02 … 2024-03-26 | 13 rows, identical |
| 2024-01-01 … 2024-06-30 | **13 rows, 2024-04-02 … 2024-06-25** | 26 rows, 2024-01-02 … 2024-06-25 |
| 2023-01-01 … 2024-12-31 | **13 rows, 2024-10-08 … 2024-12-31** | 105 rows, 2023-01-03 … 2024-12-31 |

Two sibling endpoints, one query, and `analysis` returns an eighth of what `report` returns while looking
exactly as healthy. A caller asking both for a two-year history and joining them on date gets 13 rows back and
no indication that 92 were dropped.

### The GDP family goes further: widen the range and get *nothing*

`economic-indicators` on the four quarterly series returns **zero rows** for any range of a year or more, while
the same series returns data for a 90-day range inside it:

| name | 2025-09-01 … 2025-11-30 | 2025-01-01 … 2025-12-31 |
|---|---|---|
| `GDP` | 1 | **0** |
| `realGDP` | 1 | **0** |
| `nominalPotentialGDP` | 1 | **0** |
| `realGDPPerCapita` | 1 | **0** |

All four measured, all four identical. `GDP` over 1900-01-01 … 2026-08-29 also returns 0. The monthly, weekly
and daily series do not do this — `CPI`, `federalFunds`, `unemploymentRate`, `initialClaims` and
`inflationRate` all return their capped row count for the same wide ranges. A row-count guard cannot catch
this, for the reason already written into `GetEconomicCalendarAsync`: sparse is legitimate here, and 0 is a
value real quarters produce. The check has to be positional — did the rows reach the ends of the range asked
for?

`limit` is **silently ignored** on `economic-indicators`: `name=CPI&limit=100` and `name=CPI` return the same
2 rows; `name=GDP&limit=100` returns the same 1 row.

## Silently ignored parameters

Two measured, both confirmed byte-identical to the call without the parameter:

| path | parameter | evidence |
|---|---|---|
| `esg-benchmark` | `sector` | `?sector=APPAREL RETAIL` is **byte-identical** to the bare call — 1003 rows across 291 sectors |
| `economic-indicators` | `limit` | see above |

`esg-benchmark` accepts `year` and honours it (`year=2020` → 966 rows, `year=2023` → 1003, `year=1800` → 0
rows under a 200). Its **default year is 2023** — the bare call is byte-identical to `year=2023`, three years
stale as of today. Periods present: `FY`, `Q1`, `Q2`, `Q3`.

## Wire misspellings that must be transcribed exactly

Three field names on the COT paths are misspelled on the wire, and in every case a correctly-spelled sibling
sits beside them, which is what makes these dangerous to model from memory.

| path | wire field | the correct spelling sits at |
|---|---|---|
| `commitment-of-traders-analysis` | `netPostion` | `previousNetPosition`, `changeInNetPosition` — both correct, same record |
| `commitment-of-traders-report` | `changeInNoncommSpeadAll` | `noncommPositionsSpreadAll`, `pctOfOiNoncommSpreadAll` — correct |
| `commitment-of-traders-report` | `tradersNoncommSpeadOl` | `tradersNoncommSpreadAll`, `tradersNoncommSpreadOther` — correct |

Eight `Spread`-family fields on the report are spelled correctly and exactly two are spelled `Spead`.

There is a second, larger inconsistency in the same record: the "old crop" suffix is spelled **`Old` on 10
fields and `Ol` on 26**. `openInterestOld` and `noncommPositionsLongOld` carry the full word; `pctOfOpenInterestOl`,
`tradersTotOl` and `concGrossLe4TdrLongOl` do not. Both spellings are live in the same response.

## `commitment-of-traders-report` is the widest record in the SDK

**128 fields on one record**, measured across 545 rows. For scale, the widest record in the codebase today is
`FinancialRatios` at 66 properties, and this is nearly double it. The record is four near-identical blocks — `All`, `Old`, `Other`, and a `changeIn…`
block — over positions, percentages, trader counts and concentration ratios.

**The `Other` block is not dead weight and must be modelled.** 36 fields end in `Other`, and while they are
zero on most rows, **118 of 545 rows carry a non-zero value in at least one of them**, across 14 distinct
symbols (`CC`, `CT`, `HE`, `KC`, `KE`, `MW` among them). Dropping the block would silently lose real data for
those contracts.

## Type traps

| path | field | wire type | the wrong guess |
|---|---|---|---|
| `esg-ratings` | `industryRank` | string `'3 out of 9'` | `int` — it is a sentence, not a rank |
| `esg-ratings` | `ESGRiskRating` | string `'B'` | — letter grade |
| `esg-disclosures` | `cik` | string `'0000320193'` | `int`/`long` — the leading zeros are significant |
| COT both | `date` | string `'2024-02-27 00:00:00'` | a bare `date` — these carry a time component |
| `commitment-of-traders-analysis` | `reversalTrend` | real JSON `bool` | — genuinely boolean, unlike `capitalGainsOver200Usd` in #31, which was the string `"False"` |

Every numeric field measured in this slice is a JSON number, and none is a bare `string?`, so the
`ScalarAsStringJsonConverter` written for #31 is not needed here. Mixed `int`/`float` within a column is
pervasive and ordinary — `countryRiskPremium` is `float` on 179 rows and `int` on 13, `pctOfOiNoncommLongAll`
`float` on 489 and `int` on 56 — which is the usual argument for `decimal?` throughout rather than any integer
type. The counted exceptions are the position and trader columns on the report, which are `int` on all 545
rows.

## `changeInNetPosition` is a percentage, not a delta

Measured across all 545 rows of the bare `commitment-of-traders-analysis` response. The field sits between two
absolute position counts and is neither of them:

| field | first row |
|---|---|
| `netPostion` | -12315 |
| `previousNetPosition` | -12453 |
| arithmetic difference | **138** |
| `changeInNetPosition` | **1.11** |

`138 / 12453` is 1.108%. Tested against every row where the previous position is non-zero: **545 of 545 match
the percent-change reading**, and 4 of 545 match an absolute-delta reading — those four being values where the
two happen to coincide.

This is why `ChangeInNetPosition` is `decimal?` while `NetPosition` and `PreviousNetPosition` are `int?`. A
caller who reads the name as a delta and adds it to a position count gets a number that is wrong by three
orders of magnitude, with nothing to signal it.

## The three transcript endpoints disagree on their own field names

The same two concepts are spelled three different ways across three sibling paths. This is the trap most likely
to produce a plausible-looking model that binds nothing.

| path | quarter is | year is | payload |
|---|---|---|---|
| `earning-call-transcript` | `period` — string `'Q3'` | `year` — int | `content`, `symbol`, `date` |
| `earning-call-transcript-dates` | `quarter` — int `3` | `fiscalYear` — int | `date` |
| `earning-call-transcript-latest` | `period` — string `'Q2'` | `fiscalYear` — int | `symbol`, `date` |

And the **request** parameter disagrees with the response on the same endpoint: `earning-call-transcript` is
queried with `quarter=3` and answers with `period: "Q3"`.

`earning-call-transcript` requires three parameters, discovered one 400 at a time — `symbol`, then `year`, then
`quarter`. It returns exactly one row, whose `content` is a single string of **46,487 characters** for
AAPL 2025 Q3 — the length of the decoded string a caller receives. The JSON-escaped literal in the response
body is 46,544, and an earlier draft of this file quoted that number as the character count.

`earning-call-transcript-dates?symbol=AAPL` returned 84 rows, newest first, spanning 2026-07-30 back to
2005-10-13 — full history, no cap observed.

## Paging on `earning-call-transcript-latest` advances by less than a page

Re-measured 2026-08-29 after an initial reading conflated two separate effects. Both are real, and they operate
on different timescales.

**Within a burst of calls the endpoint is deterministic.** `page=0` issued twice, either side of a `page=1`,
returned the **same 100 rows both times** — a set intersection of 100 of 100. Nothing here is random.

**Paging advances with a stride smaller than the page size.** Every page returns 100 distinct rows, but
consecutive pages share some of them:

| comparison | rows in common |
|---|---|
| `page=0` vs `page=0` (same burst) | **100** of 100 — identical |
| `page=0` vs `page=1` | 28 |
| `page=1` vs `page=2` | 21 |
| `page=0` vs `page=2` | **0** — disjoint |
| union of pages 0, 1, 2 | 251 distinct of 300 returned |

The stride is therefore roughly 72–79 rows against a page size of 100. Adjacent pages overlap, pages two apart
do not. **Paging is usable for enumeration provided the caller de-duplicates** — it is not the broken pagination
the overlap alone suggests.

**Separately, the feed does churn — but over tens of minutes, not seconds.** Two bare calls about twenty
minutes apart shared **90 of 100** rows. That is the effect that makes index-based assertions unsafe; it is not
what produces the page overlap above.

**The bare call is not `page=0`.** Issued at the same moment, they share 71 of 100 rows, so the default is its
own offset rather than the first page. `limit=10` is a strict subset of `page=0`.

As in #31, nothing here may be tested by index against live data. The live sweep must assert on counts and
sets, never on `rows[0]`.

## Default windows are stale on three paths

Worth stating plainly because each looks like current data and is not. Measured 2026-08-29:

| path | bare call returns | staleness |
|---|---|---|
| `economic-indicators` | 2025-08-29 … 2025-11-26 | window starts exactly one year back |
| `esg-benchmark` | fiscal year 2023 only | ~3 years |
| COT both | 2024-01-02 … 2024-02-27 | ~2.5 years |

`treasury-rates` is the exception: its bare call returned 2026-05-29 … 2026-08-27, current as of the
measurement date.

**Every indicator series stops in late 2025**, which is what makes the staleness above a property of the data
rather than of the default window. Measured 2026-08-29, the newest row across all 23 names:

| newest row | names |
|---|---|
| 2025-11-26 | `inflationRate`, `30YearFixedRateMortgageAverage`, `15YearFixedRateMortgageAverage` |
| 2025-11-22 | `initialClaims` |
| 2025-11-01 | `CPI`, `federalFunds`, `consumerSentiment`, `durableGoods`, `industrialProductionTotalIndex`, `newPrivatelyOwnedHousingUnitsStartedTotalUnits`, `retailMoneyFunds`, `retailSales`, `smoothedUSRecessionProbabilities`, `totalNonfarmPayroll`, `totalVehicleSales`, `unemploymentRate`, `commercialBankInterestRateOnCreditCardPlansAllAccounts` |
| 2025-10-01 | `GDP`, `realGDP`, `realGDPPerCapita`, `nominalPotentialGDP` |
| — | `inflation`, `3MonthOr90DayRatesAndYieldsCertificatesOfDeposit` (empty) |

Nine months before the measurement date, on every one of them. **A date range computed relative to today
returns nothing from this endpoint**: `name=GDP&from=2026-05-23&to=2026-08-21` — the window the live smoke
sweep's own `RangeStart`/`SettledWeekday` constants produce — answered HTTP 200 and an empty array,
measured 2026-08-29.

Windows measured against `name=GDP` the same day:

| range | days | rows |
|---|---|---|
| 2026-05-23 … 2026-08-21 | 90 | **0** |
| 2025-09-01 … 2025-11-30 | 90 | 1 — 2025-10-01 |
| 2025-08-01 … 2025-10-31 | 91 | 1 — 2025-10-01 |
| 2025-07-01 … 2025-12-31 | 183 | **0** |
| 2024-01-01 … 2024-12-01 | 335 | 1 — 2024-10-01 |

The 183-day miss between two hits either side of it is why no width rule is stated. A ~90-day window over a
span the data actually covers is the only shape measured to work every time.

`esg-benchmark?year=2025` answered **622 rows**, measured 2026-08-29 — fewer than 2023's 1003, but not empty.
Only 2020, 2023 and 2025 were probed by year; an unrecognised year answers `[]`.

## Record shapes

Field counts measured from the responses named above:

| path | fields | rows in the measured response |
|---|---|---|
| `commitment-of-traders-report` | 128 | 545 |
| `commitment-of-traders-analysis` | 16 | 545 |
| `treasury-rates` | 13 | 63 |
| `esg-disclosures` | 11 | 130 (AAPL, 2026-06-27 … 1993-12-31) |
| `esg-benchmark` | 7 | 1003 |
| `esg-ratings` | 7 | 32 (AAPL) |
| `earning-call-transcript` | 5 | 1 |
| `market-risk-premium` | 4 | 192 |
| `earning-call-transcript-latest` | 4 | 100 |
| `economic-indicators` | 3 | 1–61, by series frequency |
| `earning-call-transcript-dates` | 3 | 84 (AAPL) |
| `commitment-of-traders-list` | 2 | 65 |

`market-risk-premium` is the simplest record in the slice — `country`, `continent`, `countryRiskPremium`,
`totalEquityRiskPremium` — 192 rows, one per country, no parameters, no cap observed.

## What the design has to decide

1. **The non-JSON 200 on `economic-indicators`.** Whether the indicator name becomes a closed type (all 23
   documented names are valid, so it can), whether the transport learns to translate this body, or both.
2. **Whether `commitment-of-traders-report`'s 128 fields become one record or several.** It is three times the
   width of anything else in the SDK, and the `All`/`Old`/`Other` blocks are structurally identical.
3. **How the four row caps are surfaced** — 61, 61, 13, 100 — given that a row-count guard is the wrong
   instrument and the existing house answer is an edge-coverage check the caller performs.
4. **Facade placement.** `fmp.Economics` exists and takes 3 of these; the transcript, ESG and COT groups have
   no home yet.
