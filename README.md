# fmpdotnet

A .NET 10 SDK for the [Financial Modeling Prep](https://site.financialmodelingprep.com/developer/docs) `stable` API.

Built to be adopted by the `trader` repository, so build order follows what trader calls rather than what FMP
documents first: FMP publishes 263 endpoints across 28 categories (230 unique paths — the asset-class sections
re-document `/stable/quote` and friends), and the first release targets 39 of them.

## Status

Foundation and one vertical slice per pipeline. `dotnet test` — 64 passing.

| Area | State |
|---|---|
| Options, validation, DI (`AddFmp`) | done |
| NodaTime throughout — no BCL date/time in the public surface | done |
| Throttling — separate reservoirs for standard and bulk | done |
| Timeouts — per-attempt, outside throttle wait | done |
| JSON pipeline → `IReadOnlyList<T>` | done |
| CSV bulk pipeline → `IAsyncEnumerable<T>` | done |
| `Company.GetProfileAsync` | done |
| `Bulk.StreamEndOfDayAsync` | done |
| Remaining 37 endpoints | not started |

## Usage

```csharp
services.AddFmp(configuration);              // binds the "Fmp" section
// or
services.AddFmp(o => o.ApiKey = "…");

var fmp = provider.GetRequiredService<FmpClient>();

var profile = await fmp.Company.GetProfileAsync("AAPL");

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
