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

Foundation, both pipelines, the period-shaped fundamentals, and the first of the directory endpoints.
`dotnet test` — 122 passing.

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
| `Statements.*` — the seven period-shaped endpoints | done |
| `Directory.*` — available sectors and industries | done |
| `Bulk.StreamEndOfDayAsync` | done |
| Remaining 27 endpoints | not started |

## Usage

```csharp
using FmpDotNet;
using FmpDotNet.DependencyInjection;

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

await foreach (var bar in fmp.Bulk.StreamEndOfDayAsync(new LocalDate(2025, 10, 22), ct))
    Console.WriteLine($"{bar.Symbol} {bar.Close}");
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
- **`available-industries` is not alphabetical.** Its 159 rows are grouped by sector, and since no row carries a
  sector field that ordering is the only signal of which sector an industry belongs to. The SDK preserves wire
  order, trims labels and drops blanks, but deliberately does *not* de-duplicate — that would change the
  cardinality of a directory response without saying so.
- **Some numerics arrive as strings** — `"fiscalYear":"2026"`, `"fullTimeEmployees":"166000"`. Without
  `AllowReadingFromString` the first quoted number aborts the whole response, not just that field.
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
    "MaxRetryAfter": "00:02:00"
  }
}
```

Timeouts bind to NodaTime `Duration` and accept both `"00:00:30"` and a bare number of seconds (`"30"`). The
bare-number form is checked first on purpose: `TimeSpan.TryParse("45")` means *45 days*, so the other order would
turn `RequestTimeout=45` into a timeout that never fires.

The API key is not validated — an SDK cannot know whether its caller intends to make a request; assert it in the
host that does.

## Endpoints not yet modelled

`FmpTransport` is public. Reach an unmodelled endpoint through it rather than building a second `HttpClient`
without the throttle:

```csharp
var rows = await transport.TryGetListAsync<MyModel>(
    new FmpRequest("stable/ratings-snapshot").With("symbol", "AAPL"), ct);
```
