# Recipes

Worked solutions to tasks people actually have. Each one includes the trap it avoids, because in most cases the
naive version returns plausible wrong data rather than failing.

All examples assume `fmp` is a resolved `FmpClient` and `ct` a `CancellationToken`.

---

## Screen the universe

```csharp
var criteria = new ScreenerCriteria
{
    MarketCapMoreThan   = 10_000_000_000m,
    Sector              = "Technology",
    Country             = "US",
    IsEtf               = false,
    IsActivelyTrading   = true,
    Limit               = 500,
};

IReadOnlyList<ScreenerResult> matches = await fmp.Search.ScreenAsync(criteria, ct);
```

Unset properties are **never sent**, so an empty `ScreenerCriteria` is a request for the whole universe rather
than a request for nothing.

### Three traps

**Get the vocabulary from the API, not from memory.** An unrecognised *value* returns `[]` with HTTP 200 —
indistinguishable from a real filter that matched nothing.

```csharp
var sectors = await fmp.Directory.GetSectorsAsync(ct);
if (criteria.Sector is { } sector && !sectors.Contains(sector, StringComparer.Ordinal))
    throw new InvalidOperationException($"'{sector}' is not one of FMP's sectors.");
```

**A typo'd filter *name* silently widens the query.** FMP ignores unrecognised parameter names, so
`bogusParam=1&limit=3` returns the same rows as `limit=3` alone — a query that looks like it worked.
`ScreenerCriteria` closes this one: a misspelled filter will not compile. This is the reason the criteria object
exists.

**The bounds are inclusive despite the names.** `PriceLowerThan = 1` returns securities priced at *exactly* 1. Two
adjacent ranges written as `LowerThan = x` and `MoreThan = x` **overlap on the boundary** rather than
partitioning it. If you are bucketing, decide which side owns the edge and offset one of them.

**The `Exchange` filter takes the short code only.** A result's own `Exchange` is `NASDAQ Global Select`, which
fed back into a query matches nothing. `ExchangeShortName` is the field that round-trips.

---

## Pull a fundamentals history

The period-shaped endpoints share one signature — `(symbol, period, limit)` — with `FiscalPeriod.Annual` and a
full history as the defaults.

```csharp
var income  = await fmp.Statements.GetIncomeStatementAsync("AAPL", FiscalPeriod.Annual, limit: 10, ct);
var balance = await fmp.Statements.GetBalanceSheetAsync("AAPL", FiscalPeriod.Quarter, limit: 20, ct);
var ratios  = await fmp.Statements.GetRatiosAsync("AAPL", FiscalPeriod.Quarter, limit: 20, ct);
var metrics = await fmp.Statements.GetKeyMetricsAsync("AAPL", FiscalPeriod.Annual, limit: 10, ct);
```

`FiscalPeriod` has **six** members: `Annual`, `Quarter`, and the four specific quarters `Q1`–`Q4`. `Quarter` means
"the rolling quarterly series"; `Q3` means "Q3s only".

### The trap: `(symbol, date)` is not a unique key

`enterprise-values` is not shaped like its siblings — it sends no `fiscalYear` and no `period`, so **a row cannot
say which series it came from**. `period=` is still honoured and does change the dates returned, so the SDK keeps
sending it.

The consequence is for *storage*: a Q4 end and a fiscal year end are **the same day**. `2025-09-27` appears in
Apple's annual series and in its quarterly one. If you write both cadences into one table keyed on
`(symbol, date)`, they collide.

```csharp
// Stamp the cadence yourself before merging the two series.
var annual  = (await fmp.Statements.GetEnterpriseValuesAsync("AAPL", FiscalPeriod.Annual, limit: 10, ct))
              .Select(r => (Cadence: "annual", Row: r));
var quarter = (await fmp.Statements.GetEnterpriseValuesAsync("AAPL", FiscalPeriod.Quarter, limit: 40, ct))
              .Select(r => (Cadence: "quarter", Row: r));
```

---

## Get forward consensus without silently losing rows

```csharp
var annual  = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Annual,  limit: 5, ct: ct);
var quarter = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter, limit: 8, ct: ct);
```

**`Period` is stamped from the request**, not read from the wire, because nothing on the response says which
cadence a row came from — and an annual row and a Q4 row share the same fiscal period end. Without that stamp,
concatenating the two calls silently collapses colliding rows. The SDK does it for you; just do not throw the
property away.

**Ordering is furthest-future first**, so `limit: 5` gives you the five *most distant* estimates, not the next
five. If you want the nearest, ask for more and take from the end.

There is **no revision or as-of stamp anywhere on the response**. If you need to know when a consensus was struck,
stamp it on arrival — the API will not tell you later.

---

## Build the whole-market earnings calendar

This is the recipe most worth reading, because the naive version loses data under HTTP 200.

```csharp
// Day at a time. It is the only chunk width measured to be safe.
var rows = new List<EarningsCalendarEntry>();

for (var day = from; day <= to; day = day.PlusDays(1))
{
    var chunk = await fmp.Calendar.GetEarningsCalendarAsync(day, day, includeReportTimes: true, ct: ct);

    if (EarningsCalendarResult.IsLikelyTruncated(chunk))
        logger.LogError("Even a single day truncated for {Day} — investigate.", day);

    rows.AddRange(chunk);
}
```

### Why day-at-a-time

`earnings-calendar` **truncates silently at exactly 4000 rows, dropping the earliest dates**. Measured: one day
answers 2039 rows; a two-day range answers exactly 4000, of which only 1969 fall on the first day — **70 rows of a
day that was complete on its own just vanish, mid-day**. A one-week request came back with an entire requested day
absent. `limit=6000` is accepted and ignored. There is no cursor, so the SDK cannot page around it.

Density ranges from ~60 rows a day in a quiet month to ~525 in a peak week, so a safe chunk width cannot be picked
from the calendar alone. A 7-day peak-season window measured 3676 rows — 92% of the cap without crossing it.

### Reading the truncation signal

The returned list is really an `EarningsCalendarResult` carrying `RowsReturned`, `AtRowCap`, `MissesStartOfRange`
and `LikelyTruncated`. If you hold it as a plain `IReadOnlyList<T>`, use the static helper:

```csharp
EarningsCalendarResult.IsLikelyTruncated(chunk)
```

It is **exact** for a list this SDK produced, because it reads the raw response's own row count. Handed anything
else — a concatenation of several days, a list you have already filtered — it falls back to `Count >= 4000`.

**Test each chunk, never the concatenation.** Concatenating discards the per-response evidence that made the check
exact. That is why the loop above tests inside the loop.

### `includeReportTimes` re-dates rows, it does not add them

A `from = to = 2026-05-13` request returns the identical 2039-symbol set either way — but with the flag on, 51 of
those rows report `2026-05-14`. **None of those 51 appear in the `2026-05-14` request**, checked symbol by symbol.

So clamping to `[from, to]` removes no duplicates — there are none — and permanently drops rows no other chunk
will ever return. The SDK returns rows **unclamped** by default. `clampToRange: true` exists only for callers
writing into a store that cannot reject a duplicate and would rather lose a row than double one.

---

## The macro calendar

```csharp
var macro = await fmp.Economics.GetEconomicCalendarAsync(day, day.PlusDays(7), ct);

var et = DateTimeZoneProviders.Tzdb["America/New_York"];
foreach (var r in macro.Where(r => r.Country == "US" && r.Impact == "High"))
    Console.WriteLine($"{r.Timestamp?.InZone(et).LocalDateTime}  {r.Event}");
```

It is **global and unfiltered** — filtering by country or impact is yours to do.

It truncates wide windows too, but **differently**, and there is no row cap to test against. Measured: one month →
1855 rows, three months → 4051, six months → **535** (fewer than the three-month window it contains), fifteen
months → 0.

A row-count guard is the wrong instinct here, because macro density legitimately varies enormously — January 2027
really does hold only 2 rows. **The honest completeness test is whether the returned rows reach both ends of the
range you asked for.**

`changePercentage` cannot distinguish zero from absent: across a 713-row week, of the 15 rows with `previous`,
`estimate`, `actual` and `change` all null, 12 carried `0` and 3 carried `null`. The only sound "was this
reported" gate is **`Actual is not null`**.

---

## Diff the symbol universe

```csharp
var listed = await fmp.Directory.GetStockListAsync(ct);
var live   = await fmp.Directory.GetActivelyTradingAsync(ct);

var liveSymbols   = live.Select(s => s.Symbol).ToHashSet(StringComparer.Ordinal);
var notTrading    = listed.Where(s => !liveSymbols.Contains(s.Symbol)).ToList();
```

**`actively-trading-list` is a strict subset of `stock-list`** — measured, every symbol, zero outside it. So
"listed but not actively trading" is a **defined set**, not an inference.

The two lists send the same value under different names — `stock-list` sends `companyName`, `actively-trading-list`
sends `name` — and the values agree character for character across every shared symbol. Both map to
`CompanySymbol`, so you do not have to care which endpoint spelled it which way.

---

## Walk the delisting archive

```csharp
const int PageSize = CompanyEndpoints.MaxDelistedPageSize;   // 100 — a hard cap, not a default

for (var page = 0; ; page++)
{
    var rows = await fmp.Company.GetDelistedAsync(page, PageSize, ct);
    if (rows.Count == 0) break;
    // ...
}
```

**`limit` is capped at 100 and FMP does not say so.** `limit=1000` and `limit=100` returned byte-identical bodies.
A caller who trusted the larger value and stepped `page` by their own limit would read **a tenth of the archive
with HTTP 200 throughout**. `GetDelistedAsync` therefore rejects a larger limit at the call site rather than
letting the clamp happen silently.

The archive is ordered **newest-first**, which is why **page 0 carries delistings scheduled for the future** — the
top row measured four months ahead of the call. Filter on the date if you only want what has actually happened.

---

## Stream a whole-universe bulk feed

```csharp
await foreach (var bar in fmp.Bulk.StreamEndOfDayAsync(new LocalDate(2025, 10, 22), ct))
    await writer.WriteAsync(bar, ct);
```

```csharp
await foreach (var p in fmp.Bulk.StreamAllProfilesAsync(ct))
    await store.UpsertAsync(p, ct);
```

**Stream it. Do not `.ToListAsync()`.** Payloads reach 69 MB and `etf-holder-bulk` has a single 298 MB part; the
whole point of the `IAsyncEnumerable` is that a row is mapped and released rather than accumulated. That part was
streamed at 2,571,137 rows and **0.2 MB of peak live memory**.

Read **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)** before running any of this — the bulk throttle is
two calls a minute by default, errors arrive under HTTP 200, and FMP restricts keys it considers abusive.

### The first page is not a sample

Neither whole-universe feed's first page samples the universe, for **opposite** reasons:

* `shares-float-all` pages **are** symbol-ordered, so page 0 is entirely Shenzhen listings. This was once read as
  a plan restriction when it was simply page zero of a global list.
* `profile-bulk` part 0 is **not** symbol-ordered at all.

Do not draw conclusions about coverage from either.

### Bulk shapes are not always the per-symbol shapes

The bulk float rows carry **five** fields where the per-symbol endpoint carries six — there is no `source`. A null
there means *"this shape omits it"*, not *"FMP names no source"*.

A bulk profile's `currency` is not always USD, and its `country` tracks the **issuer**, not the venue — a TSX
listing reports `CAD` and `US` on the same row. So summing `marketCap` across the universe mixes currencies
silently, and filtering a US universe on `country` is not the same as filtering on `exchange`.

---

## Price history

```csharp
var eod      = await fmp.Chart.GetEndOfDayAsync("AAPL", from, to, ct);            // light shape
var full     = await fmp.Chart.GetEndOfDayFullAsync("AAPL", from, to, ct);
var unadj    = await fmp.Chart.GetUnadjustedAsync("AAPL", from, to, ct);          // non-split-adjusted
var divAdj   = await fmp.Chart.GetDividendAdjustedAsync("AAPL", from, to, ct);

var intraday = await fmp.Chart.GetIntradayAsync("AAPL", ChartInterval.FiveMinutes, from, to, ct);
```

Six intraday intervals sit behind one method and a `ChartInterval` enum, so the path segment cannot be misspelled.

---

## Quote anything

One method serves every asset class:

```csharp
var equity    = await fmp.Quote.GetQuoteAsync("AAPL", ct);
var crypto    = await fmp.Quote.GetQuoteAsync("BTCUSD", ct);
var fx        = await fmp.Quote.GetQuoteAsync("EURUSD", ct);
var index     = await fmp.Quote.GetQuoteAsync("^GSPC", ct);
var commodity = await fmp.Quote.GetQuoteAsync("GCUSD", ct);

var batch     = await fmp.Quote.GetQuotesAsync(["AAPL", "MSFT", "NVDA"], ct);
```

Each of those five was measured returning the ordinary seventeen-field quote.

**`MarketCap` is not populated on the commodity and forex batches** — a market capitalisation is not a meaningful
thing to ask for there. That is data, not a mapping fault.

---

## Handle share counts and identifiers correctly

**Share counts are floating-point on the wire.** `floatShares` has been seen as `25595002.125`, a computation
artifact of outstanding × free-float %. Reading them into `long` throws and aborts the **whole** response, not just
the field, so the SDK reads `decimal` and lets you round. The fractions appear intermittently rather than for
particular symbols, so a clean sample proves nothing.

**ETFs report `freeFloat: 0` and `floatShares: 0`** against a real `outstandingShares`, with a null `source` — SPY,
QQQ, VOO and IWM all do. The zero means *"not computed for this security"*, not *"no shares freely tradable"*. Do
not feed it into a float-based calculation.

**Identifiers stay strings.** `cik` is zero-padded (`"0000320193"`); parsing it to a number loses the padding that
SEC filings use.

---

## Get the timezone right

FMP sends `"yyyy-MM-dd HH:mm:ss"` with no offset for timestamps in **two different zones**, so the shape tells you
nothing. The SDK converts each field through the correct one:

| Field | Zone |
|---|---|
| `acceptedDate` (filings) | **Eastern** — via the tz database, because the offset is −4 or −5 depending on the date |
| `shares-float`'s `date` | **UTC** |
| Economic calendar timestamps | **UTC** |

Reading `acceptedDate` as UTC — as a naive port would — puts every filing timestamp 4–5 hours early. This was
established against SEC EDGAR's own acceptance times: Apple's 10-K reads `2025-10-31 06:01:26` where EDGAR says
`10:01:26Z`, and JPM's reads `2026-02-13 16:20:00` where EDGAR says `21:20:00Z`. Two different offsets six months
apart is why a fixed `-5` is wrong for half the year.

You get all of this for free by consuming the SDK's `Instant` values and converting for display only:

```csharp
var et = DateTimeZoneProviders.Tzdb["America/New_York"];
Console.WriteLine(filing.AcceptedDate?.InZone(et).LocalDateTime);
```

---

## Reference

Every measured claim on this page is recorded, with its method, in
[upstream behaviour the SDK handles for you](../../README.md#upstream-behaviour-the-sdk-handles-for-you).
