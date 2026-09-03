# Rate Limits and Bulk Data

FMP runs **two separate throttles**, and they are nothing like each other. The SDK mirrors that split all the way
down — two clients, two reservoirs, two timeouts, two error conventions. This page is what to read before you
touch anything ending in `-bulk`.

## The two pipelines

| | Ordinary endpoints | `*-bulk` endpoints |
|---|---|---|
| Format | JSON array | **CSV** |
| Return shape | `IReadOnlyList<T>` | `IAsyncEnumerable<T>` |
| Payload | kilobytes | up to **69 MB** in one response |
| Throttle | `PerMinuteCap` — default **660/min** | `BulkPerMinuteCap` — default **2/min** |
| Timeout | `RequestTimeout` — 30 s | `BulkRequestTimeout` — **10 min** |
| Errors | status codes | **also HTTP 200 with a JSON error body** |

FMP keeps them apart, so the SDK does too. They do not share a reservoir, and spending one does not spend the
other.

## The ordinary throttle

A token bucket, **shared by every registration on the same API key** in the process. Every `FmpClient`, every
transport, every concurrent caller draws from the same reservoir — which is what makes the cap mean anything at all.

The default of **660/min** is ~88% of Premium's 750/min, the lowest paid tier the SDK targets. The headroom is
measured, not superstition: the emitted rate runs about 10% above target under real concurrency.

**On Ultimate, raise it to `2640`** — see **[Configuration](configuration.md)**. The default is deliberately not tuned to the key
you hold, because one sized for a higher tier would trip 429s for everyone below it.

### What happens on a 429

1. The shared bucket is **drained and held** for the advised `Retry-After`.
2. That hold is **clamped by `MaxRetryAfter`** (default 2 minutes) and the clamping is logged — an upstream value
   stops every FMP call in the process, so a `Retry-After: 86400` must not be able to idle the host for a day.
3. `FmpRateLimitedException` is raised, carrying the **unclamped** advised value in `RetryAfter`.

So by the time you catch it, the SDK has already reacted. A retry meets the SDK's own back-pressure rather than
the limit that just rejected it. **Re-queue the unit of work rather than dropping it.**

Timeouts are measured from **inside** the rate limiter, so a request that queued behind a hold gets its full
budget once it starts. Otherwise the SDK's own back-pressure would produce a wave of timeouts at exactly the wrong
moment.

## The bulk throttle

**Default: two requests a minute.** That is close to a trickle, and it is deliberate.

Measured: a second bulk call *moments* after the first was already refused. And FMP's own error text warns that

> frequent abuse on this API Endpoint may result in restrictions placed on this API Key

**The cost of getting bulk wrong is your key, not your latency.** That is the whole reason this page exists.

Bulk data is refreshed by FMP only **once every few hours**, so nothing is gained by asking more often. Download
once, cache the result yourself, and re-read your own copy.

## Bulk errors arrive as HTTP 200

The single most dangerous property of the bulk surface.

A throttled bulk call returns:

```
HTTP/1.1 200 OK
{"Error Message": "Limit Reach. …"}
```

JSON — on an endpoint whose success shape is **CSV**. `EnsureSuccessStatusCode` passes. A naive CSV parse yields
zero rows. **A caller reads "no data today" instead of "you were throttled"**, and if that caller is a scheduled
job, it writes an empty day into a store and moves on.

The SDK inspects the payload and raises `FmpApiException` with a **null** `StatusCode` — null being the signal
that the error arrived on a 200. See **[Error Handling](error-handling.md)**.

The converse also happens: `profile-bulk?part=99` answers **HTTP 400** with the plain text
`Query Error: Invalid or missing query parameter - part`, under a `content-type: application/json` that is a lie.
That text is surfaced rather than discarded behind a bare `HttpRequestException`. `StreamAllProfilesAsync` reads a
400 as *"past the last part"* — a documented heuristic, not a contract.

## Stream. Do not buffer.

```csharp
// Right — a row is mapped and released.
await foreach (var row in fmp.Bulk.StreamEndOfDayAsync(date, ct))
    await writer.WriteAsync(row, ct);
```

Every bulk model in the repository was verified by streaming **the whole** response through its mapper, not a
sample — across the milestone, **3.2 million rows and roughly 560 MB**. That includes `etf-holder-bulk`'s single
**298 MB** part, which streamed **2,571,137 rows at 0.2 MB of peak live memory**.

Collecting that into a list instead is the difference between 0.2 MB and hundreds.

**Three bulk endpoints send no `Content-Length`** — `profile-bulk`, `etf-holder-bulk` and `eod-bulk` — so nothing
can pre-size a buffer or show a progress percentage. Report progress in rows, not bytes.

## The developer disk cache

Working on a CSV mapper means re-reading the same response repeatedly, against payloads reaching 69 MB, on a
throttle that allows two calls a minute, for data that changes every few hours. Those repeat calls buy nothing and
spend the key's standing.

```json
{ "Fmp": { "DeveloperBulkCacheDirectory": ".fmp-bulk-cache" } }
```

The first call to each bulk URL is written to disk; every later call to the same URL is replayed. Delete the
directory to refetch.

> ### It is not a caching layer
>
> Entries **never expire**, nothing is invalidated, nothing is bounded, and a stale entry is served **forever**.
> Setting this in a deployed application means that application silently stops reading live data.

Guards that make it hard to leave on by accident:

* **Off by default.**
* **Bulk only** — never applies to per-symbol endpoints.
* **Logs a warning the first time it serves anything**, so it cannot be on without saying so.
* **Error payloads are delivered but never kept**, so a failure cannot be replayed forever as though it were data.
* Keyed by request URL **with the API key stripped**, so rotating your key does not orphan the cache.

Architecturally it is the **outermost** handler on the bulk client, which is the point rather than a detail: a
replay must not consume a bulk token or start a timeout. A cache hit never reaches the rate limiter at all.

## Whole-universe feeds: what the first page is not

Neither feed's first page samples the universe, for **opposite** reasons.

* **`shares-float-all` pages are symbol-ordered**, so page 0 is entirely Shenzhen listings. This was once read as
  a plan restriction when it was simply page zero of a global list requested without `page` or `limit`.
* **`profile-bulk` part 0 is not symbol-ordered at all.**

Draw no conclusions about coverage, geography or data quality from either.

## Bulk shapes differ from per-symbol shapes

* The bulk float rows carry **five** fields where the per-symbol endpoint carries six — there is no `source`. A
  null there means *"this shape omits it"*, not *"FMP names no source"*.
* A bulk profile's **`currency` is not always USD**, and its **`country` tracks the issuer, not the venue** — a
  TSX listing reports `CAD` and `US` on the same row. Summing `marketCap` across the universe therefore mixes
  currencies silently, and filtering a US universe on `country` is not the same as filtering on `exchange`.

## A null in a bulk sample is weak evidence

Relevant when reading the smoke suite's bulk baseline, or judging your own probe.

A bulk part is an **unordered shard** that FMP republishes every few hours. A sparse column can therefore read as
absent one week and populated the next. That is a property of the data, not a fault in the mapper — and no
affordable sample size fixes it: reading 200 rows instead of 25 was measured at **2 h 39 m** against 8 minutes,
and would still be sampling one shard.

See **[Live Smoke Suite](live-smoke-suite.md)**.

## `BulkFiscalPeriod` is a separate enum

Bulk endpoints do **not** accept the rolling `quarter` value that the ordinary statement endpoints do.
`BulkFiscalPeriod` therefore has five members — `Annual`, `Q1`, `Q2`, `Q3`, `Q4` — where `FiscalPeriod` has six.

Two enums make the invalid combination unrepresentable. One enum plus a runtime check would not.

## Checklist before you run a bulk job

- [ ] Are you **streaming**, not collecting?
- [ ] Do you catch `FmpApiException` with a **null `StatusCode`** and treat it as *retry later*, not *no data*?
- [ ] Are you writing the result somewhere so you do not have to ask twice?
- [ ] Is `DeveloperBulkCacheDirectory` **unset** in anything deployed?
- [ ] Is only **one process** running bulk against this key? Two would share the key but not the reservoir, which
      paces itself per process — so they would emit at twice the rate measured to be safe.

## Reference

* [Two pipelines, kept apart](../../README.md#two-pipelines-kept-apart)
* [Working on a bulk mapper](../../README.md#working-on-a-bulk-mapper)
