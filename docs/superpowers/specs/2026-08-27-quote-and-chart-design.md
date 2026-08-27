# Quote and Chart coverage — design

**Issue:** [#24](https://github.com/jerbersoft/fmpdotnet/issues/24) — Coverage: Quote and Chart groups
**Measured:** 2026-08-27, Premium key, ~70 calls on the ordinary throttle
**Status:** approved

FMP's Quote (16 endpoints) and Chart (10) are the closest unmodelled surface to what the SDK already
covers. They were deprioritised because `trader` sources bars and quotes from Alpaca, not because they
are hard; they are the first thing to promote if the SDK is aimed at general consumers.

Everything below was measured before it was modelled. The raw capture is
`scratchpad/measurements-2026-08-27.md`; the load-bearing figures are repeated here so this document
stands alone.

## What is actually there

All 26 paths answered HTTP 200 on this key. The counts in the issue are exact: Quote 16, Chart 10.
`historical-chart/1day`, `historical-chart/2hour` and a bare `historical-price-eod` all answer HTTP 404
with the body `[]`, so the interval set is closed at six and EOD has no un-suffixed path.

**The shapes collapse hard.** Sixteen Quote endpoints return five distinct row shapes; ten Chart
endpoints return four. Nine of the sixteen Quote endpoints return the identical four fields
`symbol, price, change, volume`.

| shape | fields | endpoints |
|---|---|---|
| full quote | 17 | `quote`, `batch-quote`, and all 8 batch endpoints with `short=false` |
| short quote | 4 | `quote-short`, `batch-quote-short`, `batch-exchange-quote`, and the six `batch-*-quotes` |
| aftermarket trade | 4 | `aftermarket-trade`, `batch-aftermarket-trade` |
| aftermarket quote | 7 | `aftermarket-quote`, `batch-aftermarket-quote` |
| price change | 12 | `stock-price-change` |

| shape | fields | endpoints |
|---|---|---|
| light | 4 | `historical-price-eod/light` |
| full | 10 | `historical-price-eod/full` |
| adjusted | 7 | `historical-price-eod/non-split-adjusted`, `.../dividend-adjusted` |
| intraday | 6 | all six `historical-chart/{interval}` |

## The four things a shape-only reading would get wrong

### 1. `timestamp` is seconds on one endpoint and milliseconds on its sibling

```
quote.timestamp          1787774400     seconds -> 2026-08-26 20:00:00Z = 16:00 ET, the close
aftermarket-*.timestamp  1787819647000  millis  -> 2026-08-27 08:34:07Z = 04:34 ET, pre-market
```

Same field name, same group, adjacent endpoints. Both readings are self-consistent and only the
magnitude separates them. A single shared converter is wrong by a factor of 1000 on one of them, and
the error is invisible in a diff — which is why the SDK gets **two** converters rather than one.

### 2. `non-split-adjusted` returns *unadjusted* prices, under `adj*` field names

AAPL on 2020-08-28, the session before its 4-for-1 split effective 2020-08-31:

| endpoint | open | close | volume |
|---|---|---|---|
| `non-split-adjusted` | 504.04 | 499.24 | 46,907,500 |
| `full` | 126.01 | 124.81 | 187,630,000 |
| `dividend-adjusted` | 122.12 | 120.96 | 187,630,000 |

`499.24 = 4 × 124.81` exactly, and `187,630,000 = 4 × 46,907,500` exactly. The path parses as
*non-(split-adjusted)* — raw, as-traded prices — and the `adjOpen`/`adjHigh`/`adjLow`/`adjClose` field
names on it are simply a lie.

The consequence for the design: `non-split-adjusted` and `dividend-adjusted` are **shape-identical
while differing four-fold in value**. Nothing in the payload distinguishes them; only the path you
called does. So they share a model — pretending otherwise would invent a difference the wire does not
carry — and the *model* carries this table, because the shape is exactly what cannot tell them apart.

The method is named `GetUnadjustedAsync` for what it returns, with FMP's path quoted in the summary so
it stays greppable.

### 3. Both chart families truncate silently, by different mechanisms

**EOD: a hard 5000-row cap that drops the oldest end.**

| asked | rows | actually returned |
|---|---|---|
| 2025-08-26 … 2026-08-26 | 252 | full range honoured |
| 2021-08-26 … 2026-08-26 | 1255 | full range honoured |
| 2006-08-26 … 2026-08-26 | 5000 | **2006-10-10** … 2026-08-26 |
| 1980-01-01 … 2026-08-26 | 5000 | **2006-10-10** … 2026-08-26 |
| no `from`/`to` at all | 1253 | 2021-08-30 … — about five years, **not** everything |

Asking for 46 years and asking for 20 return the same answer. `to` is always honoured; `from` moves
silently.

**Intraday: a per-interval lookback window, not a row cap.** Each asked 2020-01-01 … 2026-08-26:

| interval | oldest bar returned | window | rows |
|---|---|---|---|
| 1min | 2026-08-24 | ~3 days | 1169 |
| 5min | 2026-08-17 | ~10 days | 624 |
| 15min | 2026-07-13 | ~45 days | 858 |
| 30min | 2026-07-28 | ~30 days | 286 |
| 1hour | 2026-05-29 | ~90 days | 434 |
| 4hour | 2026-03-02 | ~180 days | 247 |

The row counts are all far below 5000, so this is a time cap rather than a size cap. **15-minute
reaches back further than 30-minute**, which is recorded as measured and deliberately not explained —
inventing a rationale for it would be the kind of confident wrongness this project's docs exist to
avoid.

Unlike `economic-calendar`, EOD has a hard constant worth naming (`MaxEodRows = 5000`). But the honest
check on both families is positional, not a count: *did the oldest row returned actually reach the
`from` I asked for?* A caller has everything needed for it.

### 4. A backwards range fails differently on each family

```
historical-price-eod/light  from=2026-08-26 to=2026-08-24  ->  200 []
historical-chart/1min       from=2026-08-26 to=2026-08-24  ->  200, 390 rows dated 2026-08-24
```

The intraday form returns plausible, wrongly-dated data rather than nothing. That is what justifies a
guard rather than a paragraph: every chart method throws `ArgumentOutOfRangeException` when
`to < from`, before spending a call, matching `GetEconomicCalendarAsync`.

## `short=false` flips eight endpoints between two shapes

`batch-exchange-quote` and all six `batch-*-quotes` return the four-field row by default and the
seventeen-field row with `short=false`. The payload difference is not marginal:

```
batch-etf-quotes     short 1,345,381 B  ->  full 6,629,855 B   (4.9x, 14,537 rows)
batch-crypto-quotes  short   486,693 B  ->  full 2,200,708 B   (4.5x,  4,778 rows)
```

C# cannot return two types from one method, and the three ways out are not equal:

- a `bool full = false` parameter returning the wide model with nulls puts two meanings on one null —
  "this endpoint does not carry the field" and "FMP sent no value" — which is the exact defect
  `SharesFloat.Source` already documents and `TryGetListAsync` was deleted for;
- always sending `short=false` makes every caller pay 6.6 MB to learn a price;
- **two methods** give each shape its own type and put the 4.9× cost in the name the caller types.

Chosen: two methods. `GetEtfQuotesAsync()` returns `ShortQuote` rows, `GetEtfQuotesFullAsync()` returns
`Quote` rows.

## Surface

Two groups, both on `FmpTransport` — nothing here is a `*-bulk` path, so all of it runs on the ordinary
660/minute throttle rather than the bulk bucket.

**`fmp.Quote` — `QuoteEndpoints`, 23 methods over 16 paths**

- five single-symbol: `GetQuoteAsync`, `GetShortQuoteAsync`, `GetAftermarketTradeAsync`,
  `GetAftermarketQuoteAsync`, `GetPriceChangeAsync` — each returning `T?`, null when FMP answers `[]`,
  matching `GetProfileAsync`
- four multi-symbol: `GetQuotesAsync`, `GetShortQuotesAsync`, `GetAftermarketTradesAsync`,
  `GetAftermarketQuotesAsync`
- seven asset-class pairs plus the exchange pair: `GetEtfQuotesAsync`/`GetEtfQuotesFullAsync`,
  and the same for mutual fund, commodity, crypto, forex, index and exchange

**`fmp.Chart` — `ChartEndpoints`, 5 methods over 10 paths**

`GetEndOfDayAsync`, `GetEndOfDayFullAsync`, `GetUnadjustedAsync`, `GetDividendAdjustedAsync`, and
`GetIntradayAsync(symbol, interval, from, to)` covering all six intraday paths.

**Nine models.** `Quote`, `ShortQuote`, `AftermarketTrade`, `AftermarketQuote`, `PriceChange`,
`EndOfDayPrice`, `EndOfDayBar`, `AdjustedEndOfDayBar`, `IntradayBar`. Prices are `decimal?` and
volumes `long?`, following the 561-to-5 majority in the existing models.

`PriceChange` needs explicit `[JsonPropertyName]` throughout — the wire names `1D`, `5D`, `1M`, `3M`,
`6M`, `ytd`, `1Y`, `3Y`, `5Y`, `10Y`, `max` are not legal C# identifiers and are not even
self-consistent in casing.

`IntradayBar` carries **no symbol** — the intraday payload does not include one — and its wire field
order is `open, low, high, close`, with low before high. Both are recorded on the type.

## `ChartInterval`

An enum plus `ToPathSegment()`, mirroring `FiscalPeriod`/`ToQueryValue()`. Six members, each carrying
its measured retention window in its own doc comment, because the window is a real semantic difference
between the members rather than a formatting detail — asking for a year of 1-minute bars is not a
smaller version of asking for a year of 4-hour bars, it is a request that silently returns three days.

## Converters

`NodaConverters.cs` has no epoch converter today. Two are added, kept separate for the reason in §1:

- `EpochSecondsInstantJsonConverter` → `Quote.Timestamp`
- `EpochMillisecondsInstantJsonConverter` → `AftermarketTrade.Timestamp`, `AftermarketQuote.Timestamp`

Intraday `date` reuses the existing `NullableEasternInstantJsonConverter` — same
`uuuu-MM-dd HH:mm:ss` Eastern wall-clock shape as `acceptedDate` on the statement endpoints, confirmed
by the session running 09:30 to 15:59. EOD `date` reuses `NullableLocalDateJsonConverter`.

## The smoke sweep must be updated, and it will say so first

`Probe.Argument` maps *any* `string` parameter to `LiveApi.Symbol`, so `exchange` would be probed as
`"AAPL"`, and `ChartInterval` is unhandled entirely. `SweepCoverageTests` therefore fails on the commit
that adds these endpoints — which is #26's coverage guard working exactly as designed, catching an
endpoint the live suite would otherwise have silently never called.

The fix is name-dispatch for `exchange` → `"NASDAQ"` and `symbols` → a two-symbol list, plus a
`ChartInterval` case returning `OneHour` (inside every measured lookback window, and 434 rows rather
than 1169).

**Known cost, accepted deliberately.** Seven of the new methods are whole-universe downloads, adding
roughly 20 MB to each weekly run. Probing them anyway is the point: an endpoint the sweep skips is an
endpoint whose renamed field goes unnoticed until a consumer hits it. The new ordinary-tier runtime
will be measured and recorded in `smoke.yml` and the README the way the existing figures are, rather
than estimated.

## Testing

Stub-driven unit tests per group in `FmpDotNet.Tests`, following `CompanyEndpointsTests`: path and
query construction, both timestamp units against known instants, the backwards-range guard, and
empty-array-to-null on the five single-symbol methods. `EndpointCoverageTests` then regenerates the
README table — **39 of 230 paths becomes 65 of 230**.

Each new behaviour is mutation-checked: break the code, confirm the specific test fails, restore.

## Deliberately not in scope

- **Paging around the 5000-row EOD cap.** The SDK could chunk a wide range into several calls and
  stitch them. It would also then be inventing a result that no single FMP response ever returned, and
  hiding how many calls it spent doing so. Documented instead; the caller chunks, as they already do
  for `economic-calendar`.
- **Filtering `batch-quote` results back against the symbols asked for.** Unknown symbols are dropped
  silently — `AAPL,NOSUCHTICKER` returns one row — and duplicates are echoed back. Both are recorded
  on the method; neither is repaired, because repairing them means deciding what a missing symbol
  *means*, and that is the caller's question.
- **The asset-class facades.** Indexes, Commodity, Forex and Crypto re-document `stable/quote` and
  `stable/historical-price-eod` rather than adding paths. `GetQuoteAsync("BTCUSD")`,
  `GetQuoteAsync("EURUSD")`, `GetQuoteAsync("^GSPC")` and `GetQuoteAsync("GCUSD")` were all measured
  returning the ordinary full-quote shape. One implementation covers them; typed facades would add
  surface without adding reach, and belong to #25 if they are wanted at all.
