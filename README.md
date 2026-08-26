# FmpDotNet

A .NET 10 SDK for the [Financial Modeling Prep](https://site.financialmodelingprep.com/developer/docs) `stable` API.

The root namespace and assembly are `FmpDotNet`. That is deliberately not the vendor's own name: a package
called `FinancialModelingPrep` reads as something FMP publishes and supports, and this is an independent
client. Types keep the `Fmp` prefix (`FmpClient`, `FmpOptions`, `FmpTransport`) because they name the API
being spoken to, not the publisher.

Built to be adopted by the `trader` repository, so build order follows what trader calls rather than what FMP
documents first: FMP publishes 263 endpoints across 28 categories (230 unique paths — the asset-class sections
re-document `/stable/quote` and friends), and the first release targets 39 of them.

## Status

Foundation, both pipelines, the period-shaped fundamentals, the directory endpoints, every endpoint trader calls,
and the whole bulk surface. `dotnet test` — 305 passing.

| Area | State |
|---|---|
| Options, validation, DI (`AddFmp`) | done |
| NodaTime throughout — no BCL date/time in the public surface | done |
| Throttling — separate reservoirs for standard and bulk | done |
| Timeouts — per-attempt, outside throttle wait | done |
| JSON pipeline → `IReadOnlyList<T>` | done |
| CSV bulk pipeline → `IAsyncEnumerable<T>` | done |
| `Company.GetProfileAsync` | done |
| `Company.GetSharesFloatAsync` | done |
| `Company.TryGetAllSharesFloatAsync` — paged whole-universe float | done |
| `Statements.*` — the seven period-shaped endpoints | done |
| `Statements.GetScoresAsync` — Altman Z and Piotroski | done |
| `Directory.*` — available sectors and industries | done |
| `Calendar.GetEarningsAsync` — per-symbol earnings history | done |
| `Calendar.GetEarningsCalendarAsync` — whole-market, with a truncation signal | done |
| `Analyst.GetEstimatesAsync` — forward consensus | done |
| `Economics.GetEconomicCalendarAsync` — macro releases | done |
| `Bulk.StreamEndOfDayAsync` | done |
| `Bulk.StreamProfilesAsync` / `StreamAllProfilesAsync` | done |
| `Bulk.StreamPriceTargetSummariesAsync` | done |
| `Bulk.StreamAnalystConsensusAsync` | done |
| `Bulk.StreamEarningsSurprisesAsync` | done |
| `Bulk.StreamIncomeStatementsAsync` / `…BalanceSheetsAsync` / `…CashFlowsAsync` | done |
| `Bulk.Stream*GrowthAsync` — the three growth variants | done |
| `Bulk.StreamKeyMetricsTtmAsync` / `StreamRatiosTtmAsync` | done |
| `Bulk.StreamRatingsAsync` / `StreamDiscountedCashFlowsAsync` / `StreamScoresAsync` / `StreamPeersAsync` | done |
| `Bulk.StreamEtfHoldingsAsync` / `StreamAllEtfHoldingsAsync` | done |
| Developer disk cache for bulk responses | done |
| Remaining 4 endpoints — the universe and directory lists | not started |

Every endpoint `Trader.Adapters.MarketData.Fmp` calls is now modelled, which is what the adapter's removal was
waiting on.

## Usage

```csharp
using FmpDotNet;
using FmpDotNet.DependencyInjection;
using FmpDotNet.Models;
using NodaTime;

services.AddFmp(configuration);              // binds the "Fmp" section
// or
services.AddFmp(o => o.ApiKey = "…");

var fmp = provider.GetRequiredService<FmpClient>();

var profile = await fmp.Company.GetProfileAsync("AAPL");

// The seven period-shaped endpoints share one signature: symbol, cadence, limit.
var income  = await fmp.Statements.GetIncomeStatementAsync("AAPL", FiscalPeriod.Annual, limit: 5);
var ratios  = await fmp.Statements.GetRatiosAsync("AAPL", FiscalPeriod.Quarter, limit: 8);

// Share float. One row or none — the endpoint holds no history.
var shares = await fmp.Company.GetSharesFloatAsync("AAPL");

// The reference vocabularies the profile's `sector` and `industry` are drawn from.
IReadOnlyList<string> sectors    = await fmp.Directory.GetSectorsAsync();
IReadOnlyList<string> industries = await fmp.Directory.GetIndustriesAsync();

// Altman Z and Piotroski, plus the seven figures the Z score is computed from.
var scores = await fmp.Statements.GetScoresAsync("AAPL");

// Earnings history, newest first — note the head row is usually the NEXT report, unreported.
var earnings = await fmp.Calendar.GetEarningsAsync("AAPL", limit: 8);

// Forward consensus. `Period` is stamped from the request, so annual and quarterly rows
// stay distinguishable when concatenated — their fiscal period ends collide otherwise.
var annual  = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Annual, limit: 5);
var quarter = await fmp.Analyst.GetEstimatesAsync("AAPL", FiscalPeriod.Quarter, limit: 5);

// The whole-market earnings calendar. It truncates silently at 4000 rows, so ask.
var day = new LocalDate(2026, 5, 13);
var cal = await fmp.Calendar.GetEarningsCalendarAsync(day, day, includeReportTimes: true);
if (EarningsCalendarResult.IsLikelyTruncated(cal))
    { /* narrow the range and retry — see the note below */ }

// Macro releases. Global and unfiltered: filtering by country or impact is yours to do.
var macro = await fmp.Economics.GetEconomicCalendarAsync(day, day.PlusDays(7));
var et = DateTimeZoneProviders.Tzdb["America/New_York"];
foreach (var r in macro.Where(r => r.Country == "US" && r.Impact == "High"))
    Console.WriteLine($"{r.Timestamp?.InZone(et).LocalDateTime} {r.Event}");

await foreach (var bar in fmp.Bulk.StreamEndOfDayAsync(new LocalDate(2025, 10, 22), ct))
    Console.WriteLine($"{bar.Symbol} {bar.Close}");

// The whole-universe profile feed, streamed a part at a time.
await foreach (var p in fmp.Bulk.StreamAllProfilesAsync(ct))
    Console.WriteLine($"{p.Symbol} {p.Sector} {p.Industry}");
```

## Dates and times are NodaTime

The SDK's time surface is NodaTime throughout — `LocalDate`, `Instant`, `Duration`, `IClock`. No `DateOnly`,
`DateTime`, `DateTimeOffset`, `TimeSpan` or `TimeProvider` appears in any public signature.

`TimeSpan` survives only where a BCL API leaves no choice — `Task.Delay`, `CancellationTokenSource.CancelAfter`,
`HttpClient.Timeout`, and the `Retry-After` header's own type — and is converted at that boundary and nowhere else.

Substitute `NodaTime.Testing.FakeClock` for `IClock` to drive throttle behaviour in tests without a real clock.

## Two pipelines, kept apart

FMP keeps them apart, so the SDK does too.

| | Ordinary endpoints | `*-bulk` endpoints |
|---|---|---|
| Format | JSON array | CSV |
| Return shape | `IReadOnlyList<T>` | `IAsyncEnumerable<T>` |
| Payload | kilobytes | up to **69 MB** in one response |
| Throttle | `PerMinuteCap` (default 660) | `BulkPerMinuteCap` (default 2) |
| Timeout | `RequestTimeout` (30 s) | `BulkRequestTimeout` (10 min) |
| Errors | status codes | **also HTTP 200 with a JSON error body** |

## Upstream behaviour the SDK handles for you

Measured against the live API on 2026-08-26 unless noted.

- **Bulk errors arrive as HTTP 200.** A throttled bulk call returns `{"Error Message": "Limit Reach…"}` — JSON,
  on an endpoint whose success shape is CSV. `EnsureSuccessStatusCode` passes and a naive CSV parse yields zero
  rows, so a caller sees "no data today" instead of "you were throttled". The transport inspects the payload and
  raises `FmpApiException`.
- **Bulk is throttled separately** from the account's per-minute cap, and much more tightly. FMP warns that
  frequent use "may result in restrictions placed on this API Key". Bulk data refreshes only once every few hours
  — cache a successful download rather than repeating it.
- **Three bulk endpoints send no `Content-Length`** (`profile-bulk`, `etf-holder-bulk`, `eod-bulk`), so nothing
  can pre-size a buffer or show a progress percentage.
- **`acceptedDate` is Eastern, and the economic calendar is UTC.** Both use the same
  `"yyyy-MM-dd HH:mm:ss"` shape with no offset, so the shape tells you nothing. Cross-checked against SEC EDGAR's
  own UTC acceptance times: Apple's 10-K reads `2025-10-31 06:01:26` where EDGAR says `10:01:26Z` (4 hours, EDT),
  and JPM's reads `2026-02-13 16:20:00` where EDGAR says `21:20:00Z` (5 hours, EST). Two different offsets six
  months apart means a fixed `-5` is wrong for half the year, so the SDK converts through the tz database.
  Reading these as UTC — as a naive port would — puts every filing timestamp 4-5 hours early.
- **`enterprise-values` is not shaped like its six siblings.** It sends no `fiscalYear` and no `period`, so a row
  cannot say which series it came from. `period=` *is* still honoured and does change the dates returned, so the
  SDK keeps sending it. Consequence for storage: `(symbol, date)` is **not** a unique key across both cadences,
  because a Q4 end and a fiscal year end are the same day — `2025-09-27` appears in Apple's annual series and its
  quarterly one.
- **`shares-float`'s `date` is UTC — the opposite of `acceptedDate`.** Same `"yyyy-MM-dd HH:mm:ss"` shape as the
  Eastern one above, so the string cannot tell you which is which and the wrong converter is a silent 4-5 hour
  shift. Established by probing 40 symbols: the stamps spread evenly from `00:09:20` to `14:13:45`, the latest
  sitting 26 minutes *before* UTC-now and never ahead of it. Read as Eastern that stamp would be 3.5 hours in the
  future, which a value recording when a row was last refreshed cannot be.
- **Share counts are JSON floating-point.** `floatShares` has been seen as `25595002.125` — a computation artifact
  of outstanding x free-float %. Reading them into `long` throws and aborts the *whole* response, not just the
  field, so the SDK reads `decimal` and lets the caller round. A clean sample proves nothing here; the fractions
  appear intermittently rather than for particular symbols.
- **Class-share tickers need FMP's hyphenated spelling.** `BRK.B` and `BF.B` answer `[]`; `BRK-B` and `BF-B` answer
  a row. It affects `shares-float` and `profile` alike, and it surfaces as an empty result rather than an error, so
  a dotted ticker looks exactly like a symbol FMP has no data for.
- **ETFs report `freeFloat: 0` and `floatShares: 0`** against a real `outstandingShares`, with a null `source` —
  SPY, QQQ, VOO and IWM all do. The zero means "not computed for this security", not "no shares freely tradable",
  so it must not be fed into a float-based calculation as though it were measured.
- **`earnings-calendar` truncates silently at exactly 4000 rows, dropping the *earliest* dates.** One day
  (`2026-05-13`) answers 2039 rows; `from=05-13&to=05-14` answers exactly 4000, of which only 1969 fall on 05-13
  — 70 rows of a day that was complete on its own just vanish, mid-day. A one-week request came back with an
  entire requested day absent. `limit=6000` is accepted and ignored. There is no cursor, so the SDK cannot page
  around it and instead reports it: the returned list is an `EarningsCalendarResult` carrying `RowsReturned`,
  `AtRowCap`, `MissesStartOfRange` and `LikelyTruncated`. **Day-at-a-time is the only chunk width measured to be
  safe** — a 7-day peak-season window measured 3676 rows, 92% of the cap without crossing it.
- **That truncation signal is computed before clamping, and the order is load-bearing.** Filtering the rows first
  and then testing `Count >= 4000` is how a truncated response gets judged complete: measured live, a two-day
  request returned 4000 raw rows that clamping reduced to 3935. `Count` is what you were handed; `RowsReturned` is
  what FMP sent, and only the second can answer the question.
- **`includeReportTimes=true` re-dates rows; it does not add them.** A `from=to=2026-05-13` request returns the
  identical 2039-symbol set either way — but with the flag on, 51 of those rows report `2026-05-14`. None of those
  51 appear in the `2026-05-14` request, checked symbol by symbol, so selection happens on the un-shifted date and
  only the reported date moves. **Clamping to `[from, to]` therefore removes no duplicates — there are none — and
  permanently drops rows no other chunk will ever return.** The SDK returns rows unclamped and offers
  `clampToRange: true` for callers writing into a store that cannot reject a duplicate and would rather lose a row
  than double one. The flag also changes `lastUpdated`, not just `date`.
- **`economic-calendar` truncates wide windows too, but differently** — no row cap to test against, and the
  reduction is not proportional: one month → 1855 rows, three months → 4051, but six months → **535**, fewer than
  the three-month window it contains, and a 15-month window → 0. A row-count guard is the wrong instinct here,
  because macro density legitimately varies enormously: January 2027 really does hold only 2 rows. The honest
  completeness test is whether the returned rows reach both ends of the range you asked for.
- **`analyst-estimates` is ordered furthest-future first, so `limit=N` gives the N most distant estimates**, not
  the next N. Nothing on the wire says which cadence a row came from, and an annual row and a Q4 row share the
  same fiscal period end — so the SDK stamps `Period` from the request. Without it, concatenating an annual and a
  quarterly call silently collapses colliding rows. There is also no revision or as-of stamp anywhere on the
  response: if you need to know when a consensus was struck, stamp it on arrival.
- **`earnings` puts an unreported row at the head.** The list is newest-first and the newest row is the *next*
  report — `epsActual` and `revenueActual` null, estimates populated. "The last N earnings" therefore includes one
  that has not happened. With no `limit` the endpoint returns full history: 165 rows for Apple, back to 1985.
- **`financial-scores` carries no date, and its inputs are not the latest annual statement.** Eleven fields, no
  `date`, no `period`, no `fiscalYear` — nothing says when it was computed, yet it moves: the figures are
  trailing/quote-time, and Apple's `retainedEarnings` and `workingCapital` both come back with the *opposite sign*
  to the FY2025 balance sheet captured the same day. They cannot be reconciled against `balance-sheet-statement`.
  The seven accompanying figures do reproduce the reported Altman Z exactly, which is what they are there for.
- **`profile-bulk` terminates paging with an error, not an empty response.** An out-of-range `part` answers HTTP
  **400** carrying the plain text `Query Error: Invalid or missing query parameter - part` — under a
  `content-type` of `application/json` that is a lie, since the body is not JSON. The transport surfaces that text
  as an `FmpApiException` rather than discarding it behind a bare `HttpRequestException`. `StreamAllProfilesAsync`
  reads a 400 as "past the last part", which is a documented heuristic, not a contract.
- **Neither whole-universe feed's first page is a sample of the universe**, for opposite reasons.
  `shares-float-all` pages *are* symbol-ordered, so page 0 is entirely Shenzhen listings — which is exactly how a
  consumer once read "a partial, mostly foreign page" as a plan restriction when it was simply page zero of a
  global list requested without `page` or `limit`. `profile-bulk` part 0 is *not* symbol-ordered at all. The bulk
  float rows also carry five fields where the per-symbol endpoint carries six: there is no `source`, so a null
  there means "this shape omits it", not "FMP names no source".
- **`available-industries` is not alphabetical.** Its 159 rows are grouped by sector, and since no row carries a
  sector field that ordering is the only signal of which sector an industry belongs to. The SDK preserves wire
  order, trims labels and drops blanks, but deliberately does *not* de-duplicate — that would change the
  cardinality of a directory response without saying so.
- **Some numerics arrive as strings** — `"fiscalYear":"2026"`, `"fullTimeEmployees":"166000"`. Without
  `AllowReadingFromString` the first quoted number aborts the whole response, not just that field. It rescues a
  quoted `"9"` and does nothing for an unquoted `9.0`, which is why integral-looking counts are still read as
  `decimal`: a `piotroskiScore` of `9.0` would throw on `int` and cost the caller all eleven fields.
- **On the economic calendar, `changePercentage` cannot distinguish zero from absent.** Across a 713-row week it
  was null on 153 rows — but of the 15 rows with `previous`, `estimate`, `actual` and `change` all null, 12
  carried `0` and 3 carried `null`. Both shapes occur on rows that mean the same thing, so neither the zero nor
  the null is a usable "unreported" marker. The only sound gate is `Actual is not null`.
- **A bulk profile's `currency` is not always USD and its `country` tracks the issuer, not the venue.** A TSX
  listing reports `CAD` and `US` on the same row. Summing `marketCap` across the universe therefore mixes
  currencies silently, and filtering a US universe on `country` is not the same as filtering on `exchange`.
- **Identifiers stay strings.** `cik` is zero-padded (`"0000320193"`); parsing it to a number loses the padding
  SEC filings use.
- **429 is answered, not just reported.** The shared reservoir is drained and held for `Retry-After`, clamped by
  `MaxRetryAfter` so an upstream value cannot idle the process for a day.
- **Timeouts sit inside the throttle,** so waiting on the rate limiter never consumes the request budget, and
  expiry raises `TimeoutException` rather than the `TaskCanceledException` callers mistake for a shutdown.
  `HttpClient.Timeout` is deliberately infinite.
- **Plan gating changes.** `profile-bulk` and `shares-float-all` were previously recorded as 402-on-Premium; both
  answered 200 when re-probed. `TryGetListAsync` returns null rather than throwing so a fast path can degrade.
- **`/stable/company-symbol-list` does not exist** (404). The working directory endpoints are `stock-list` and
  `actively-trading-list`.

## Plan gating — 402 and 403

FMP refuses an endpoint your key is not entitled to with **402**, and refuses a key it does not like with **403**.
The SDK treats both as `FmpPlanRestrictedException`, but **does not conflate them**:

```csharp
catch (FmpPlanRestrictedException ex)
{
    if (ex.IsRejectedCredential)   // 403 — check the key before the invoice
        logger.LogError("FMP rejected the key: {Message}", ex.Message);
    else                           // 402 — genuinely an entitlement answer
        logger.LogWarning("Not on this plan: {Message}", ex.Message);
}
```

`ex.StatusCode` carries the actual status. This matters more than it looks: FMP's own error text warns that
"frequent abuse on this API Endpoint may result in restrictions placed on this API Key", so a 403 is a plausible
outcome of hammering the bulk endpoints — and reporting that as "upgrade your plan" sends someone to the wrong
page entirely.

**Every failure is an exception. Nothing signals one by returning.**

There is no `Try`-prefixed method anywhere in the SDK, and that is a decision rather than an omission. C# forbids
`out` parameters on async methods (CS1988), so the BCL's `bool TryX(out T)` shape cannot be written for an async
API at all — which is why the framework has no `TryReadAsync` either, and why `ChannelReader<T>` pairs a
*synchronous* `TryRead` with an *asynchronous* `ReadAsync` that throws. An earlier version of this SDK imitated
the pattern with a nullable return, which was worse than either option: it put two error channels on one surface
and gave `null` a meaning the signature could not carry, so you had to read the docs to learn that it meant
"refused" rather than "nothing there".

To degrade instead of failing — an optional whole-universe fast path falling back to a per-symbol loop, say —
catch the exception. It is self-describing at the catch site and tells you *which* refusal arrived.

**Null still means something, just never an error.** Endpoints returning `T?` use null for an answer FMP
genuinely gave:

| Returns null when | Meaning |
|---|---|
| `Company.GetProfileAsync` | FMP has no such symbol |
| `Company.GetSharesFloatAsync` | likewise |
| `Statements.GetScoresAsync` | an ETF, which genuinely has no scores |

An entitled call with nothing to say returns an **empty list**, not null and not an exception. Collapsing a 402
into an empty result is what makes a paywalled endpoint indistinguishable from a real empty answer *and* from the
provider being down — a defect the SDK's predecessor shipped.

**The SDK carries no tier map**, and will not. `profile-bulk` and `shares-float-all` were both recorded as 402 on
Premium and both answered 200 when re-probed on 2026-08-26. Entitlement moves and varies per key, so anything
claiming "this needs Ultimate" would be confidently wrong sooner or later. Probe, catch, and re-probe.

## Configuration

```json
{
  "Fmp": {
    "ApiKey": "…",
    "BaseUrl": "https://financialmodelingprep.com",
    "PerMinuteCap": 660,
    "BulkPerMinuteCap": 2,
    "RequestTimeout": "00:00:30",
    "BulkRequestTimeout": "00:10:00",
    "MaxRetryAfter": "00:02:00",
    "DeveloperBulkCacheDirectory": null
  }
}
```

Timeouts bind to NodaTime `Duration` and accept both `"00:00:30"` and a bare number of seconds (`"30"`). The
bare-number form is checked first on purpose: `TimeSpan.TryParse("45")` means *45 days*, so the other order would
turn `RequestTimeout=45` into a timeout that never fires.

The API key is not validated — an SDK cannot know whether its caller intends to make a request; assert it in the
host that does.

## Working on a bulk mapper

Set `Fmp:DeveloperBulkCacheDirectory` while you are writing or changing a `*-bulk` model. The first call to each
bulk URL is written to that directory; every later call to the same URL is replayed from disk, so you can iterate
on a `FromCsv` mapper without re-downloading it.

```json
{ "Fmp": { "DeveloperBulkCacheDirectory": ".fmp-bulk-cache" } }
```

Delete the directory to refetch. Entries are keyed by the request URL with the API key stripped, so rotating your
key does not orphan the cache.

Every bulk model in this repository was written against a response captured this way and verified by streaming
the whole of it through the mapper, not a sample. Across the milestone that is **3.2 million rows and roughly
560 MB** — including `etf-holder-bulk`'s single 298 MB part, which streamed 2,571,137 rows at 0.2 MB of peak live
memory.

**Why it exists.** Bulk is throttled separately and far more tightly than the ordinary endpoints — measured
2026-08-26, a second call moments after the first was already refused — and FMP's own error text warns that
"frequent abuse on this API Endpoint may result in restrictions placed on this API Key". The payloads reach 69 MB,
and FMP refreshes them only once every few hours, so re-fetching while you iterate buys nothing and spends your
key's standing.

**It is not a caching layer.** Entries never expire, nothing is invalidated, nothing is bounded, and a stale entry
is served forever. Setting this in a deployed application means that application silently stops reading live data.
It is off by default, it applies only to the bulk client — never to the per-symbol endpoints — and it logs a
warning the first time it serves anything, so it cannot be on without saying so. Responses that look like an error
payload are delivered but never kept, so a failure cannot be replayed forever as if it were data.

## Endpoints not yet modelled

`FmpTransport` is public. Reach an unmodelled endpoint through it rather than building a second `HttpClient`
without the throttle:

```csharp
var rows = await transport.TryGetListAsync<MyModel>(
    new FmpRequest("stable/ratings-snapshot").With("symbol", "AAPL"), ct);
```
