# Technical Indicators — measurements

Every fact the design will rest on, with the date it was measured. Measured against the live API on
**2026-08-29** across **71 captured responses**, 60 of which carried rows: **46,132 rows and 322,924 field
slots** in total. All ordinary JSON endpoints; no `*-bulk` path was touched.

Issue [#35](https://github.com/jerbersoft/fmpdotnet/issues/35) lists nine paths and asks one question up
front — *do they share one shape parameterised by indicator?* **They do**, and the answer is the first section
below. That makes this the cheapest slice by record count in the project so far: one record, not nine.

It is not the cheapest by trap count. The through-line is this: **the number you get for a given date depends
on the date range you asked for.** Five of the nine indicators change value when the window narrows, and one
of them — `adx` — was wrong by **264%**. Nothing in the status, the shape, or the body reports it.

## Entitlement — all nine are reachable

Every path answered HTTP 200 with a well-formed array on this plan. No 402, no 403.

| path | rows | bytes |
|---|---|---|
| `stable/technical-indicators/adx` | 1254 | 221,681 |
| `stable/technical-indicators/dema` | 1254 | 223,214 |
| `stable/technical-indicators/ema` | 1254 | 221,956 |
| `stable/technical-indicators/rsi` | 1254 | 221,333 |
| `stable/technical-indicators/sma` | 1254 | 214,882 |
| `stable/technical-indicators/standarddeviation` | 1254 | 239,144 |
| `stable/technical-indicators/tema` | 1254 | 223,171 |
| `stable/technical-indicators/williams` | 1254 | 228,927 |
| `stable/technical-indicators/wma` | 1254 | 221,445 |

Probed with `symbol=AAPL&periodLength=10&timeframe=1day`. Identical row counts are not a coincidence: the nine
are the same price series with one column swapped.

## One shape, nine columns

Across all 60 non-empty responses there are **exactly nine distinct key tuples**, and they differ in one
element:

```
date, open, high, low, close, volume, <indicator>
```

The base six are byte-for-byte the same series across all nine paths. **The ninth key is the path segment**,
with one exception:

| path segment | JSON field |
|---|---|
| `adx` `dema` `ema` `rsi` `sma` `tema` `williams` `wma` | same as the segment |
| `standarddeviation` | **`standardDeviation`** |

The path is all-lowercase; the field is camelCase. A binder that derives one from the other will get eight
right and this one wrong.

**The row does not carry the symbol.** A caller who fans out across symbols and concatenates the results has
no way to tell them apart afterwards.

### The path segment is case-insensitive; the set of segments is not open

`stable/technical-indicators/SMA` returned a response **byte-identical** to the lowercase form, as did
`.../standardDeviation` against `.../standarddeviation`. Casing is therefore not a trap here — unlike
`stable/economic-indicators?name=`, where `GDP` works and `gdp` does not (measured 2026-08-29, previous
slice).

An unknown segment is a trap of a different kind. `stable/technical-indicators/macd` answers **HTTP 404 with
the body `[]`** — the success shape arriving on a failure status. This is the same trap already recorded for
`stable/company-symbol-list` (measured 2026-08-26), and `FmpTransport.ReadFailureAsync` already handles it:
the JSON-array branch refuses to use `[]` as an explanation and the caller gets
`FMP answered HTTP 404 (NotFound) with no explanation in the body.` **No new transport work is needed. A test
should pin it.**

## All three parameters are required

There are no defaults. Omitting any one gives HTTP 400 with a plain-text body:

| omitted | status | body |
|---|---|---|
| `symbol` | 400 | `Query Error: Invalid or missing query parameter - symbol` |
| `periodLength` | 400 | `Query Error: Invalid or missing query parameter - periodLength` |
| `timeframe` | 400 | `Query Error: Invalid or missing query parameter - timeframe` |

Same wording as every previous slice. These bodies are **not JSON** despite
`content-type: application/json; charset=utf-8`; `ReadFailureAsync` preserves the text, which is the whole
reason that method exists (measured 2026-08-26 on `profile-bulk?part=99`).

## `timeframe` — seven valid values, and an invalid one is not JSON either

| value | result |
|---|---|
| `1min` `5min` `15min` `30min` `1hour` `4hour` `1day` | 200, rows |
| `1week` `1month` `2hour` | **400**, `Invalid timeframe provided.` |

That failure body is **27 bytes of bare text** — no braces, no quotes — under a JSON content-type. It reaches
the caller as `FmpApiException` carrying the sentence, because the status is non-2xx and `ReadFailureAsync`
takes the text as-is.

Note the shape of this against `ChartInterval`: there the interval is a **path segment**, so a wrong one is a
404 with `[]`; here it is a **query value**, so a wrong one is a 400 with a sentence. Both argue for an enum,
for different reasons.

`1day` is valid here and has no counterpart in `ChartInterval` — `stable/historical-chart/1day` was measured
on 2026-08-27 to answer 404 with `[]`. **The two enums are not interchangeable and must not be shared.**

### The reachable window depends on the timeframe, and is not monotonic

Bare call, `symbol=AAPL&periodLength=10`, measured 2026-08-29:

| timeframe | rows | oldest | newest | span |
|---|---|---|---|---|
| `1min` | 1170 | 2026-08-26 09:30 | 2026-08-28 15:59 | 2 days |
| `5min` | 702 | 2026-08-18 09:30 | 2026-08-28 15:55 | 10 days |
| `15min` | 988 | 2026-07-08 09:30 | 2026-08-28 15:45 | **51 days** |
| `30min` | 273 | 2026-07-31 09:30 | 2026-08-28 15:30 | **28 days** |
| `1hour` | 441 | 2026-06-01 09:30 | 2026-08-28 15:30 | 88 days |
| `4hour` | 249 | 2026-03-03 09:30 | 2026-08-28 13:30 | 178 days |
| `1day` | 1254 | 2021-08-31 | 2026-08-28 | 1823 days |

**15-minute bars reach back nearly twice as far as 30-minute bars.** That inversion is measured, not mistyped,
and it independently reproduces the same oddity recorded for `ChartInterval` on 2026-08-27 (45 days vs 30).
Two measurements two days apart on different endpoints agreeing on a strange result is the strongest evidence
available that it is FMP's behaviour and not a sampling accident.

## `from` and `to` are honoured — but each timeframe has a ceiling

An earlier reading of this — that the range parameters were ignored on intraday timeframes — was **wrong**,
and the test that overturned it is worth naming. A *wide* range on `1hour` returned a response byte-identical
to the bare call, which looks exactly like the parameters being dropped. A *narrow* range distinguishes the
two, and the parameters are honoured:

| request | rows | window returned |
|---|---|---|
| `1hour`, from 2026-08-25 to 2026-08-27 | 21 | 2026-08-25 09:30 … 2026-08-27 15:30 |
| `1min`, from 2026-08-27 to 2026-08-27 | 390 | 2026-08-27 09:30 … 2026-08-27 15:59 |
| `1hour`, from 2024-01-01 to 2026-08-28 | 441 | **byte-identical to the bare call** |
| `1min`, from 2026-01-01 to 2026-08-28 | 1170 | **byte-identical to the bare call** |

So: **ask for less than the ceiling and you get what you asked for; ask for more and you silently get the
ceiling.** HTTP 200, well-formed array, no field reporting the truncation.

### On `1day` the ceiling is a ~5-year span anchored at `to`

| from | to | rows | window returned | span |
|---|---|---|---|---|
| 2010-01-01 | 2015-01-01 | 1258 | 2010-01-04 … 2014-12-31 | full range |
| 2011-01-01 | 2016-01-01 | 1258 | 2011-01-03 … 2015-12-31 | full range |
| 2010-01-01 | 2016-01-01 | 1258 | **2011-01-03** … 2015-12-31 | 1823 days |
| 2010-01-01 | 2020-01-01 | 1257 | **2015-01-05** … 2019-12-31 | 1821 days |
| 2020-01-01 | 2026-08-28 | 1255 | **2021-08-30** … 2026-08-28 | 1823 days |
| 2010-01-01 | 2026-08-28 | 1255 | **2021-08-30** … 2026-08-28 | 1823 days |

Two things follow, and the first contradicts the obvious guess:

1. **There is no history floor.** 2010 data is reachable — the first two rows above return it in full. An
   early reading of the 2010→2026 result as "FMP only keeps five years" was wrong; it is a span limit, not an
   age limit.
2. **The window kept is the newest ~5 years of the requested range**, anchored at `to`. Ask for ten years and
   the older half vanishes without comment.

This is not a row cap. `BTCUSD` on `1day` returned **1825 rows** — crypto trades every calendar day — which is
more rows than any equity request above and still exactly five years.

### A future range returns five years of the past

`from=2027-01-01&to=2027-06-01` answered **byte-identically to the bare call**: 1254 rows, 2021-08-31 …
2026-08-28. A caller who computes a window from a wrong clock gets a full, plausible, well-formed answer for
entirely the wrong dates.

### A backwards range is not an error

`from=2026-08-28&to=2026-08-01` — `from` after `to` — answered 200 with 1254 rows spanning 2021-08-03 …
2026-07-31. `to` was honoured and `from` was discarded. FMP raises nothing, so
`DateRange.ThrowIfBackwards` is load-bearing on this endpoint rather than merely tidy.

## `periodLength` — three silent failures and one silent lie

| value | result |
|---|---|
| `10` | 200, 1254 rows |
| `1` | 200, 1254 rows — `sma` equals `close` on **1254 of 1254** rows |
| `1.5` | 200 — **byte-identical to `periodLength=1`**; the fraction is discarded |
| `0` | **200 with `[]`** |
| `-5` | **200 with `[]`** |
| `100000` | **200 with 1254 rows** |
| `abc` | 400, `Query Error: Invalid or missing query parameter - periodLength` |

Two of these deserve names.

**Zero and negative return an empty array, not an error.** A caller who passes a computed period that lands on
zero gets "this symbol has no data" — a plausible, wrong answer. The SDK should reject `periodLength < 1`
before the call rather than pass it on.

**A period longer than the available history is not rejected; it is quietly satisfied with less.**
`periodLength=100000` against 1254 bars returned 1254 non-null values, all distinct — 128.567 on the newest
row, 62.498 on the oldest. Those are expanding-window averages over whatever history existed, not
100000-period averages. Nothing in the response says the window was short.

`periodLength=1` yielding `sma == close` on every row is the sanity check that the column means what its name
says.

## The range you ask for changes the values you get back

This is the finding that matters most, and it was found by asking one question: *does a 10-row window agree
with the same ten dates inside the 1254-row series?*

For `sma`, it did — exactly. For `adx`, the newest row came back **57.743123** against **15.847068** in the
full series. Not a rounding difference. A different number.

All nine, AAPL, `periodLength=10`, `1day`, window `2026-08-17 … 2026-08-28` (10 rows) against the same dates
in the 1254-row series:

| indicator | rows disagreeing | narrow (newest) | full (newest) | relative error |
|---|---|---|---|---|
| `sma` | **0 / 10** | 312.107000 | 312.107000 | 0.0% |
| `standardDeviation` | **0 / 10** | 3.884719 | 3.884719 | 0.0% |
| `williams` | **0 / 10** | −13.741637 | −13.741637 | 0.0% |
| `wma` | **0 / 10** | 313.368182 | 313.368182 | 0.0% |
| `rsi` | 9 / 10 | 61.425246 | 61.425246 | 0.0% |
| `ema` | 10 / 10 | 313.093576 | 313.011611 | 0.0% |
| `dema` | 10 / 10 | 314.096975 | 314.431778 | 0.1% |
| `tema` | 10 / 10 | 317.222202 | 316.844261 | 0.1% |
| `adx` | 10 / 10 | 57.743123 | 15.847068 | **264.4%** |

The split is not arbitrary. The four exact ones are **finite-window** functions — a value depends on exactly
`periodLength` bars. The five that drift are **recursive**: each value is computed from the previous one, so
they carry state from before the window and must warm up. FMP computes them from the start of the returned
range rather than from a buffer of prior data.

`ema`, `dema` and `tema` are the insidious cases: wrong by a tenth of a percent, which is small enough to look
right and large enough to be wrong. `adx` is the loud one, and loud is safer.

### How much history `adx` needs

`periodLength=10`, `to=2026-08-28`, comparing the newest row against the 1254-row series:

| rows in range | newest `adx` | relative error |
|---|---|---|
| 10 | 57.743123 | 264.4% |
| 42 | 17.570665 | 10.9% |
| 83 | 15.869124 | 0.14% |
| 145 | 15.846890 | 0.001% |
| 271 | 15.847068 | **exact** |
| 521 | 15.847068 | exact |

Convergence to the full-series value needs roughly **270 bars — about 27× `periodLength`**. Five significant
figures arrive at about 145 bars (~15×).

The practical guidance this yields: **ask for far more history than you intend to use, and discard the oldest
part client-side.** That is the opposite of what a caller naturally does, and neither FMP's documentation nor
its responses hint at it.

## Types

**Zero nulls.** Across 46,132 rows and 322,924 field slots, not one field was null — including the oldest rows
of every series, where a windowed indicator arguably cannot be defined. FMP returns a value there rather than
`null`.

That says nothing about whether the SDK should type the properties as nullable. House convention is that a
field is nullable unless absence is impossible, and 71 responses on one symbol family cannot prove that. **All
properties nullable**, as everywhere else in this SDK.

`volume` across the whole corpus: **45,887 integral, 245 fractional**. The fractional ones are not confined to
intraday:

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

This settles a typing question that `IntradayBar` and `EndOfDayBar` answer differently. `EndOfDayBar.Volume`
is `long?` because daily equity bars showed no fractions; `IntradayBar.Volume` is `decimal?` because intraday
bars did. **This endpoint serves both from one shape**, and BTCUSD proves daily bars are not safe either.
`Volume` must be `decimal?`.

Value ranges, AAPL, 1254 daily rows, `periodLength=10`:

| field | min | max | fractional |
|---|---|---|---|
| `open` | 126.0100 | 340.0300 | 1221 / 1254 |
| `high` | 127.7700 | 344.5700 | 1218 / 1254 |
| `low` | 124.1700 | 337.3500 | 1223 / 1254 |
| `close` | 125.0200 | 340.0800 | 1228 / 1254 |
| `volume` | 17,910,600 | 318,679,900 | 0 / 1254 |
| `adx` | 11.8807 | 66.7312 | 1254 / 1254 |
| `dema` | 124.9394 | 338.6786 | 1254 / 1254 |
| `ema` | 129.1082 | 331.3100 | 1254 / 1254 |
| `rsi` | 12.1140 | 86.3063 | 1254 / 1254 |
| `sma` | 128.2560 | 331.7250 | 1252 / 1254 |
| `standardDeviation` | 0.6703 | 18.9556 | 1254 / 1254 |
| `tema` | 124.6991 | 339.0553 | 1254 / 1254 |
| `williams` | **−99.5844** | 0.0000 | 1251 / 1254 |
| `wma` | 127.6542 | 333.4025 | 1254 / 1254 |

**`williams` is negative.** Williams %R is defined on [−100, 0]; 1252 of 1254 rows were strictly negative and
two were exactly `0.0`. No positive value was observed. A model that assumes indicator columns are
non-negative is wrong on one of the nine.

Every indicator column is `decimal?` — all nine are measured floats, not counts. `int?` appears nowhere in
this slice.

### `date` carries a time, and only sometimes means one

`1day` rows are always `YYYY-MM-DD 00:00:00` — midnight on every one of 1254 rows. Every intraday timeframe
carries a real bar time (`2026-08-28 15:59:00`). Since one record serves all seven timeframes, the property
has to be a date-and-time, and on `1day` its time half is padding rather than data. The design has to decide
what to expose; it cannot make the wire format vary.

## Ordering and integrity

Checked on the 1254-row daily set:

- **strictly descending by date** — newest first, no ties
- **no duplicate dates** — 0 across 1254
- `high >= low` on every row; `close` within `[low, high]` on every row

## Unknown symbol

`symbol=ZZZZNOTREAL` answers **HTTP 200 with `[]`**. Silent, like the rest of FMP.

## Asset classes — all four work, and one changes the row count

`rsi`, `periodLength=14`, `1day`, bare:

| symbol | kind | rows | window |
|---|---|---|---|
| `AAPL` | equity | 1254 | 2021-08-31 … 2026-08-28 |
| `SPY` | ETF | 1254 | 2021-08-31 … 2026-08-28 |
| `^GSPC` | index | 1254 | 2021-08-31 … 2026-08-28 |
| `EURUSD` | forex | 1374 | 2021-08-31 … 2026-08-28 |
| `BTCUSD` | crypto | 1825 | 2021-08-31 … **2026-08-29** |

Same seven keys in every case. The row count tracks the trading calendar — five sessions a week for equities,
about five and a half for forex, seven for crypto — and BTCUSD is the only one carrying a bar for *today*,
because its session never closed.

## What the design has to decide

1. **One record or nine.** The measurement says one shape with a swapped column. Nine records would duplicate
   six properties nine times; one record with nine nullable columns would leave eight null on every row. A
   third option — one record with a single `Value` plus the indicator that produced it — is the one the shape
   actually suggests.
2. **Whether the indicator is an enum.** Casing is forgiving, so the enum is not needed for correctness of
   spelling — but an unknown segment gives 404 + `[]`, which surfaces as a confusing exception rather than an
   obvious one, and the enum is where the per-indicator warm-up behaviour above can be documented.
3. **Whether `timeframe` reuses `ChartInterval`.** It must not: `1day` is valid here and 404s there.
4. **`Volume` is `decimal?`** — forced by BTCUSD's 75 fractional daily volumes.
5. **Guarding `periodLength < 1`** before the call, since FMP answers it with an empty array.
6. **How loudly to document the range-sensitivity.** `adx` at 264% error on a short window is the single most
   dangerous measured behaviour in this slice, and it is invisible at every layer below the caller's own
   judgement.
7. **What `date` becomes** given `1day` pads it with midnight.
