# Technical Indicators — design

Issue [#35](https://github.com/jerbersoft/fmpdotnet/issues/35), nine paths, one new facade, **one** method.

Every claim here rests on [the measurements](2026-08-29-technical-indicators-measurements.md) taken on
**2026-08-29** across 99 captured responses, 55,231 rows and 386,617 field slots. Where this document says a
value "was measured", the measurements file gives the row count behind it.

**Spec authority:** where this design and the measurements disagree, the measurements win and this document is
wrong.

## Scope

Nine paths, all reachable on the current key, none of them `*-bulk`.

Coverage moves from **178 of 243 to 187 of 243**.

The issue asked one question up front — whether the nine share one shape parameterised by indicator. They do.
Every response carries `date, open, high, low, close, volume` plus **one** column named after the path
segment. Across all 88 non-empty captures there were exactly nine distinct key tuples, differing in that one
element. So this slice adds one record, not nine, and one method, not nine.

## Public surface

### `fmp.TechnicalIndicators` — new facade, 9 paths, 1 method

```csharp
Task<IReadOnlyList<TechnicalIndicatorBar>> GetAsync(
    string symbol,
    TechnicalIndicator indicator,
    int periodLength,
    TechnicalIndicatorTimeframe timeframe,
    LocalDate? from = null,
    LocalDate? to = null,
    CancellationToken ct = default)
```

One method reaches all nine paths because `indicator` selects the **path segment**, not a query value.
`EndpointCoverageTests` already handles this: `Discover()` drives each method once per combination of enum
arguments precisely because "an enum can select the PATH rather than a query value", a comment added when
`ChartEndpoints.GetIntradayAsync` covered six paths from one method. The nine README rows will be generated
without new machinery.

`symbol`, `periodLength` and `timeframe` are required because **FMP requires them**: omitting any one answers
HTTP 400 with `Query Error: Invalid or missing query parameter - <name>`. There are no server-side defaults to
mirror, so the SDK invents none.

## `TechnicalIndicator` — a closed type over a path segment

Nine members. The wire segment is all-lowercase in every case; `ToPathSegment()` holds the mapping.

| member | segment | JSON field |
|---|---|---|
| `Adx` | `adx` | `adx` |
| `Dema` | `dema` | `dema` |
| `Ema` | `ema` | `ema` |
| `Rsi` | `rsi` | `rsi` |
| `Sma` | `sma` | `sma` |
| `StandardDeviation` | `standarddeviation` | **`standardDeviation`** |
| `Tema` | `tema` | `tema` |
| `WilliamsR` | `williams` | `williams` |
| `Wma` | `wma` | `wma` |

Two renames are deliberate. `StandardDeviation`'s segment is all-lowercase while its JSON field is camelCase —
**the one case in nine where the segment does not equal the field name**, and the reason the converter below
resolves the indicator from the field rather than deriving the field from the segment. `WilliamsR` is named
for the indicator it is (Williams %R) rather than for the segment, following `EconomicIndicator`, which renames
freely from the wire.

**Why an enum, given casing is forgiving.** `stable/technical-indicators/SMA` returned a response
byte-identical to the lowercase form, so unlike `EconomicIndicator` — where `GDP` works and `gdp` does not —
spelling case is not a trap here. The enum earns its place for two other reasons. An *unknown* segment answers
**HTTP 404 with the body `[]`**, which surfaces as `FMP answered HTTP 404 (NotFound) with no explanation in the
body` — an exception that names neither the mistake nor the fix. And the enum is the only place the
per-indicator warm-up behaviour below can live where a caller will actually read it.

### `NeedsWarmUp()` and `SuggestedWarmUpBars(int periodLength)`

These exist because of the single most dangerous measured behaviour in this slice: **the value returned for a
given date depends on the range requested.**

Measured 2026-08-29, AAPL, `periodLength=10`, `1day`, a 10-row window (2026-08-17 … 2026-08-28) compared row
for row against the same dates inside the 1254-row series:

| indicator | worst row | mean | classification |
|---|---|---|---|
| `sma` | 0.0000% | 0.0000% | exact |
| `wma` | 0.0000% | 0.0000% | exact |
| `williams` | 0.0000% | 0.0000% | exact |
| `standardDeviation` | 0.0000% | 0.0000% | exact |
| `rsi` | 0.0000% | 0.0000% | exact |
| `ema` | 0.1616% | 0.0766% | drifts |
| `tema` | 0.1540% | 0.1405% | drifts |
| `dema` | 0.4021% | 0.2302% | drifts |
| `adx` | **276.9981%** | **152.4274%** | **unusable** |

**The classification is by measured behaviour, not by textbook nature.** `rsi` is a recursive indicator by
construction — Wilder smoothing — and it came back exact to every digit on every row. FMP evidently buffers
some history before the requested range, and that buffer is sufficient for five of the nine and insufficient
for four. Classifying `rsi` as "recursive, therefore risky" would be reasoning from theory against a
measurement, which this project does not do.

Convergence against history depth, `to=2026-08-28`, newest row, `periodLength=10`:

| rows in range | `adx` | `ema` | `dema` | `tema` | `rsi` |
|---|---|---|---|---|---|
| 10 | 264.377% | 0.026% | 0.106% | 0.119% | 0.000% |
| 42 | 10.876% | 0.000% | 0.001% | 0.002% | 0.000% |
| 83 | 0.139% | 0.000% | 0.000% | 0.000% | 0.000% |
| 145 | 0.001% | 0.000% | 0.000% | 0.000% | 0.000% |
| 271 | 0.000% | 0.000% | 0.000% | 0.000% | 0.000% |

`adx` was then re-measured at a **second period** so the threshold is not extrapolated from one point.
`periodLength=20`: 83 rows → 35.61%, 145 → 3.30%, 271 → 0.003%, 521 → exact, 773 → exact. Reaching the
full-series value took **271 bars at `periodLength=10` and 521 at `periodLength=20`** — about **26–27× the
period in both cases**.

The two members this supports:

```csharp
bool NeedsWarmUp(this TechnicalIndicator indicator)
int  SuggestedWarmUpBars(this TechnicalIndicator indicator, int periodLength)
```

`NeedsWarmUp()` returns `true` for `Adx`, `Dema`, `Ema` and `Tema` — the four that drifted — and `false` for
the five that were exact. **It is deliberately not called `IsRecursive`**: `rsi` is recursive by construction
and measured exact, so a name asserting the textbook property would contradict the measurement it encodes.
The name describes what was observed, which is the only thing the SDK knows. `SuggestedWarmUpBars` returns extra bars to request **before** the range the caller
wants, then discard:

| indicator | returns | basis |
|---|---|---|
| `Sma` `Wma` `WilliamsR` `StandardDeviation` `Rsi` | `0` | measured exact at the narrowest window tested |
| `Ema` `Dema` `Tema` | `4 * periodLength` | ≤0.002% by 42 bars at `periodLength=10`; ≤0.07% at 42 bars at `periodLength=20` |
| `Adx` | `27 * periodLength` | exact at 271 (p=10) and 521 (p=20) |

**`SuggestedWarmUpBars` is a recommendation derived from the table above, not a measured constant**, and its
doc comment must say exactly that. Only `Adx` was measured at two periods across a full convergence sweep;
`Ema`/`Dema`/`Tema` were measured at two periods but only at the narrow end. The XML doc carries both tables so
a caller can second-guess the multiple with the evidence in front of them.

`SuggestedWarmUpBars` throws `ArgumentOutOfRangeException` for `periodLength < 1`, matching `GetAsync`.

**The SDK does not act on this itself.** It sends exactly the range it was given. Over-fetching and trimming
was considered and rejected: it would transfer up to 27× the requested bytes, silently diverge from the URL
sent, and cannot always succeed because of the ~5-year ceiling described below. This follows the stance
`EconomicsEndpoints` already takes on its own truncation — document at length, refuse to guess.

## `TechnicalIndicatorTimeframe` — a separate enum, not `ChartInterval`

Seven members: `OneMinute`, `FiveMinutes`, `FifteenMinutes`, `ThirtyMinutes`, `OneHour`, `FourHours`,
`OneDay`, mapping to `1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day` via `ToQueryValue()`.

**Reusing `ChartInterval` is not an option, and the reason is measured.** `1day` is valid here and
`stable/historical-chart/1day` answered **404 with `[]`** when measured on 2026-08-27. A shared enum would
either omit the one timeframe most callers want, or hand `ChartEndpoints.GetIntradayAsync` a member that
breaks it. The duplication is six near-identical members; the alternative is a type whose validity depends on
which method receives it.

The two enums also fail differently, which is worth documenting on both. In `ChartInterval` the value is a
**path segment**, so a wrong one is 404 + `[]`. Here it is a **query value**, so a wrong one is **HTTP 400
with the body `Invalid timeframe provided.`** — 27 bytes of bare text under `content-type: application/json`.
`1week`, `1month` and `2hour` were all measured doing exactly this. That body reaches the caller intact
because `FmpTransport.ReadFailureAsync` preserves non-JSON failure text; **no transport change is needed**,
and a test pins it.

### Reachable windows, documented per member

Bare call, AAPL, `periodLength=10`, measured 2026-08-29:

| member | rows | span |
|---|---|---|
| `OneMinute` | 1170 | 2 days |
| `FiveMinutes` | 702 | 10 days |
| `FifteenMinutes` | 988 | **51 days** |
| `ThirtyMinutes` | 273 | **28 days** |
| `OneHour` | 441 | 88 days |
| `FourHours` | 249 | 178 days |
| `OneDay` | 1254 | 1823 days |

**Fifteen-minute bars reach back nearly twice as far as thirty-minute bars.** This independently reproduces
the same inversion recorded for `ChartInterval` on 2026-08-27 (45 days vs 30), on a different endpoint two
days apart. No explanation is offered because none was established.

## Ranges: honoured, then silently capped

`from` and `to` are honoured on every timeframe — a narrow range returns exactly what was asked for (21 rows
on `OneHour` for a two-day window; 390 on `OneMinute` for one session). A range **wider** than the timeframe's
ceiling silently returns the ceiling: HTTP 200, well-formed array, nothing reporting the truncation.

On `OneDay` the ceiling is a **~5-year span anchored at `to`**:

| requested | returned |
|---|---|
| 2010-01-01 … 2015-01-01 | full range, 1258 rows |
| 2010-01-01 … 2020-01-01 | **2015-01-05** … 2019-12-31, 1257 rows |
| 2010-01-01 … 2026-08-28 | **2021-08-30** … 2026-08-28, 1255 rows |

There is **no history floor** — 2010 data is reachable in full when asked for in a five-year window. It is a
span limit, not an age limit, and the half that vanishes is the older one.

Two further behaviours the method documents and does not guard:

- **A wholly future range returns five years of the past.** `from=2027-01-01&to=2027-06-01` answered
  byte-identically to the bare call.
- **A backwards range is not an error.** `from=2026-08-28&to=2026-08-01` answered 200 with 1254 rows: `to`
  honoured, `from` discarded.

`GetAsync` guards the backwards case with `DateRange.ThrowIfBackwards(from, to)` — the existing helper — for
the reason it exists: FMP answers a plainly wrong argument with a plausible result, so the caller would spend
a call from their quota to be misled. **The ceilings are not guarded**, following `EconomicsEndpoints`: no row
count distinguishes a truncated window from a genuinely short one, and the honest check is positional — did
the returned rows reach both ends of the range you asked for? The caller has everything needed for it.

## `periodLength`: guarded below 1

| value | FMP's answer |
|---|---|
| `0` | **200 with `[]`** |
| `-5` | **200 with `[]`** |
| `1.5` | 200, byte-identical to `1` — the fraction is discarded |
| `abc` | 400, `Query Error: Invalid or missing query parameter - periodLength` |
| `100000` | 200, 1254 non-null values — expanding-window averages, not 100000-period ones |

`GetAsync` throws `ArgumentOutOfRangeException` when `periodLength < 1`, before the request is sent. A caller
whose computed period lands on zero would otherwise read "this symbol has no data" — a plausible, wrong
answer, bought with a call from their quota.

The upper end is **not** guarded. `periodLength=100000` against 1254 bars returned 1254 distinct non-null
values computed over whatever history existed — 128.567 on the newest row, 62.498 on the oldest. The SDK
cannot know how many bars FMP holds for a symbol, so any threshold would be invented. The method documents
that a period longer than the available history is quietly satisfied with less.

`1.5` is unreachable through this signature: `periodLength` is `int`.

## Models

### `TechnicalIndicatorBar` — 8 properties

| property | type | wire | notes |
|---|---|---|---|
| `Timestamp` | `LocalDateTime?` | `date` | see below |
| `Open` | `decimal?` | `open` | |
| `High` | `decimal?` | `high` | |
| `Low` | `decimal?` | `low` | |
| `Close` | `decimal?` | `close` | |
| `Volume` | `decimal?` | `volume` | see below |
| `Indicator` | `TechnicalIndicator` | *the column's name* | resolved from the wire, not asserted |
| `Value` | `decimal?` | *the column* | the one non-OHLCV column |

Every property is nullable despite **zero nulls across 386,617 field slots**. House convention: a field is
nullable unless absence is impossible, and 99 responses on one symbol family cannot establish that.

`Indicator` is not nullable — it is resolved from a key that must be present for the row to have been parsed
at all, and its absence is a parse failure rather than a missing value.

#### `Volume` is `decimal?`, and BTCUSD is why

This SDK already types volume two ways, deliberately: `EndOfDayBar.Volume` and `EndOfDayPrice.Volume` are
`long?` because daily equity bars showed no fractions, while `IntradayBar.Volume` is `decimal?` because
intraday bars did. **This endpoint serves both daily and intraday from one shape**, so it must pick one — and
the daily case is not safe either:

| timeframe | fractional volumes |
|---|---|
| `1min` | 31 / 1170 |
| `5min` | 12 / 702 |
| `15min` | 16 / 988 |
| `30min` | 13 / 273 |
| `1hour` | 15 / 441 |
| `4hour` | 29 / 249 |
| `1day` (AAPL) | 0 / 1254 |
| **`1day` (BTCUSD)** | **75 / 1825** |

Rounding to `long` would invent precision FMP did not send. `decimal?` it is, and the doc names BTCUSD as the
case that settles it.

#### `Timestamp` is `LocalDateTime?`, and asserts no zone

`IntradayBar.Timestamp` is an `Instant?` read as **Eastern**, and the evidence for that zone reproduces here
exactly: intraday bars run 09:30 to 15:59 and stop, which is the US regular session in New York local time.
Read as UTC they would place the market open at 05:30 ET.

But **`1day` rows carry `00:00:00` on every one of 1254 rows.** Binding those through the Eastern converter
would yield `2026-08-28T04:00:00Z` and assert that a daily bar "opened at midnight in New York" — false, and a
daily bar is not an instant at all. `NullableDateAtMidnightJsonConverter` exists in this codebase for exactly
that objection, but it discards the time half, which is real data on six of the seven timeframes.

One property honestly serving all seven means asserting no zone: **`LocalDateTime?`**, bound with the existing
`NullableLocalDateTimeJsonConverter`. The XML doc states both readings — Eastern wall clock on the six
intraday timeframes, date-plus-`00:00:00`-padding on `OneDay` — and spells out the tzdb conversion for a
caller who needs an `Instant`, never arithmetic on an offset.

This is a deliberate divergence from `IntradayBar`, and both types' docs should point at each other so the
difference reads as a decision rather than an inconsistency.

#### `Value` and the nine wire names

`Value` has nine possible JSON names, so no single `[JsonPropertyName]` binds it. A
`TechnicalIndicatorBarJsonConverter` reads the object, binds the six known keys by name, and treats **the
single remaining key** as `Value`, resolving `Indicator` from that key's name.

Resolving from the wire rather than stamping the facade's own argument is the point: if FMP ever answers a
column other than the one requested, the SDK reports what arrived instead of mislabelling it. It also makes
`standardDeviation` fall out naturally rather than needing a special case.

The converter throws `JsonException` when the object carries no unrecognised key, or more than one. Both are
shapes never observed in 88 captures, and both mean the row is not what this record models.

Custom converters that deserialise through the source-generated context are established here —
`NetWorthRangeJsonConverter` does it, and `FmpJsonContext` carries a bare-type registration for it. This
assembly declares `IsAotCompatible`, so a reflection-based path would fail the build on IL2026/IL3050.

### What the record does not carry

**The symbol.** No response includes it. A caller fanning out across symbols and concatenating the results
cannot tell them apart afterwards, and the facade does not stamp it — that would be inventing a field FMP does
not send. The method's doc names the consequence.

## Serialisation

- `FmpJsonContext` gains `[JsonSerializable(typeof(List<TechnicalIndicatorBar>))]`.
- `TechnicalIndicatorBarJsonConverter` is applied to the record with `[JsonConverter]`.
- `Timestamp` uses the existing `NullableLocalDateTimeJsonConverter`. No new NodaTime converter.

## Testing

Unit tests bind captured fixtures. **No fixture contains an API key**, and no test logs a built URL.

**Shape, one per path (9):** each of the nine binds and lands its value on `Value` with the right `Indicator`
— including `StandardDeviation`, whose fixture is the one where segment and field name differ.

**Traps, each failing if the trap is reintroduced:**

1. Unknown segment → 404 with `[]` → `FmpApiException` naming the status, not an empty list.
2. `1week` → 400 with `Invalid timeframe provided.` → `FmpApiException` carrying that sentence, proving the
   non-JSON failure body survives.
3. `periodLength` of `0` and `-5` → `ArgumentOutOfRangeException`, no HTTP call made.
4. Backwards range → `ArgumentOutOfRangeException`, no HTTP call made.
5. A fractional volume binds to `decimal?` without loss — fixture from a BTCUSD daily row.
6. A negative `williams` value binds — the measured range is −99.5844 … 0.0000, and a model assuming
   non-negative indicator columns is wrong on one of nine.
7. `OneDay` yields `00:00:00` on `Timestamp`; an intraday timeframe yields a real time. Pins the
   `LocalDateTime` decision against a future "tidy-up" to `LocalDate`.
8. The converter rejects a row with two unrecognised keys, and one with none.
9. `SuggestedWarmUpBars` returns the documented value per indicator, and throws below `periodLength = 1`.
10. `ToPathSegment()` and `ToQueryValue()` throw `ArgumentOutOfRangeException` on an undeclared member.

**Existing suites:** `EndpointCoverageTests` must generate all nine README rows unaided.
`SweepCoverageTests` runs without a key and fails when `Probe.Argument` meets an unknown parameter — `Probe`
gains arms for `indicator`, `periodLength` and `timeframe`, and the new `periodLength` arm must not capture
the parameter of that name on any other facade. `AddFmpTests` asserts the facade count: **17 → 18**.

## Live smoke sweep

One block, `[TechnicalIndicators.GetAsync]`, recording `outcome rows`. `LiveApi` gains constants for the
indicator, period and timeframe used. `FMPDOTNET_SMOKE_BULK` is not set and no `*-bulk` path is touched.

## Documentation

- `README.md`'s generated coverage block regenerates to **187 of 243**, nine new rows under
  `fmp.TechnicalIndicators`.
- Both enums carry their measured tables — windows on the timeframe, warm-up on the indicator.
- `ChartInterval` gains a cross-reference explaining why the two enums are separate, so the next reader does
  not "fix" the duplication.

## Out of scope

- Computing indicators client-side. This SDK models FMP's answers; it does not become a TA library.
- Over-fetching and trimming on the caller's behalf — rejected above.
- Any guard on the window ceilings or on a `periodLength` above the available history.
- `FinancialRatios.cs`'s stale "56 properties" comment (it has 66), which this slice does not touch.
