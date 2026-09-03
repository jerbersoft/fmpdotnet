# Configuration

Everything binds from the **`Fmp`** configuration section, or is set in code through `AddFmp(o => …)`. A named
registration — `AddFmp("research", configuration)` — binds the same keys under **`Fmp:research`** unless the call
names another section, and validates under its own name, so a bad `research` fails at startup saying so.

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

## The options

| Option | Type | Default | What it does |
|---|---|---|---|
| `ApiKey` | `string` | `""` | Sent as an `apikey` **query parameter** on every request — FMP takes no header. Never validated; see below. |
| `BaseUrl` | `string` | `https://financialmodelingprep.com` | Bare host. The `/stable/` segment belongs to the request path, not here, so a future `/v4/` path can sit on the same client. |
| `PerMinuteCap` | `int` | `660` | Ordinary-endpoint throttle, requests per minute, shared by every registration on the same API key. |
| `BulkPerMinuteCap` | `int` | `2` | `*-bulk` throttle. Independent of `PerMinuteCap` and far tighter. |
| `RequestTimeout` | `Duration` | 30 s | One ordinary HTTP **attempt**. Not `HttpClient.Timeout`. |
| `BulkRequestTimeout` | `Duration` | 10 min | One bulk HTTP attempt. Payloads reach 69 MB. |
| `MaxRetryAfter` | `Duration` | 2 min | Ceiling on how long one 429's `Retry-After` may hold the shared reservoir. |
| `DeveloperBulkCacheDirectory` | `string?` | `null` | Replay bulk responses from disk. **A development aid. Never set it in a deployed application.** |

## Durations: the parsing order is load-bearing

The three `Duration` options bind from NodaTime, and accept **two** forms:

```json
{ "Fmp": { "RequestTimeout": "00:00:30" } }   // clock form
{ "Fmp": { "RequestTimeout": "30" } }         // bare seconds
```

The bare-number form is tested **first**, deliberately. `TimeSpan.TryParse("45")` succeeds and yields **forty-five
days**. Trying the clock form first would therefore turn `RequestTimeout=45` — the most natural thing anyone
setting this from an environment variable would write — into a timeout that never fires, silently, with no parse
error to notice.

So: **a bare number always means seconds.** A value containing `:` is parsed as a clock duration.

## What is validated, and when

Validation runs at **startup** (`ValidateOnStart`), so a bad value fails while you are looking at the console
rather than on a request hours later.

| Rule | Why |
|---|---|
| `BaseUrl` must be an absolute URI | Otherwise it reaches `new Uri(…)` inside `HttpClientFactory` on first resolve and throws a `UriFormatException` that never mentions configuration. |
| `PerMinuteCap > 0` | At `0` the reservoir never refills and the first acquire blocks **forever** — calls hang rather than fail, which is the worst of both. |
| `BulkPerMinuteCap > 0` | Same reason. |
| `RequestTimeout > 0` | It bounds a single attempt. |
| `BulkRequestTimeout > 0` | Same. |
| `MaxRetryAfter >= 0` | Zero is meaningful — it means "honour no hold at all". |

**`ApiKey` is deliberately not validated.** An SDK cannot know whether its caller intends to make a request; a
host that resolves `FmpClient` and never calls it is not misconfigured. Assert the key in the host that does know.

## Sizing the throttle to your tier

`PerMinuteCap` defaults to **660**, which is ~88% of **Premium's 750/min** — the lowest paid tier this SDK
targets. The headroom is not superstition: the measured emitted rate runs about 10% above target under real
concurrency.

The default is deliberately **not** tuned to the key you happen to hold. One sized for a higher tier would trip
429s for everyone below it.

| Tier | Published limit | Suggested `PerMinuteCap` |
|---|---|---|
| Premium | 750/min | `660` (the default) |
| Ultimate | 3,000/min | `2640` |

Leaving the default in place on Ultimate spends roughly a fifth of the budget you are paying for.

The reservoir is **shared by every registration on the same API key** — every `FmpClient`, every transport,
every concurrent caller draws from the same bucket. That is what makes the cap mean something. It also means a
second `HttpClient` built by hand to reach an unmodelled endpoint would not count against it; go through
`FmpTransport` instead, which **[Endpoint Coverage](endpoint-coverage.md)** explains.

## Timeouts sit *inside* the throttle

`RequestTimeout` is measured from inside the rate-limit handler, so **time spent waiting on the token bucket does
not consume it**.

That separation is load-bearing. A 429 can hold the bucket for up to `MaxRetryAfter`. A timeout that counted
throttle waits would convert the SDK's own back-pressure into a wave of abandoned requests at exactly the moment
the upstream is already refusing traffic.

`HttpClient.Timeout` is set to **infinite** on purpose — the handler owns the deadline. Expiry raises
`TimeoutException`, not the `TaskCanceledException` callers routinely mistake for a shutdown signal.

## `MaxRetryAfter`, and why there is a ceiling

`Retry-After` is honoured, but not without bound. It is an upstream-controlled value that stops **every** FMP call
in the process, so a misparse — or a hostile `Retry-After: 86400` — would otherwise idle the host for a day while
its logs said only that it was waiting. Clamping is logged when it happens.

## `DeveloperBulkCacheDirectory`

```json
{ "Fmp": { "DeveloperBulkCacheDirectory": ".fmp-bulk-cache" } }
```

The first call to each bulk URL is written to that directory; every later call to the same URL is replayed from
disk. It exists so you can iterate on a CSV mapper without re-downloading a 69 MB payload against a throttle that
allows two calls a minute.

**It is not a caching layer, and the distinction is load-bearing.** Entries never expire, nothing is invalidated,
nothing is bounded, and a stale entry is served forever. Setting this in a deployed application means that
application silently stops reading live data.

Guards that make it hard to leave on by accident:

* Off by default.
* Applies **only** to the bulk client — never to per-symbol endpoints.
* **Logs a warning the first time it serves anything**, so it cannot be on without saying so.
* Responses that look like an error payload are delivered but never kept, so a failure cannot be replayed forever
  as though it were data.
* Entries are keyed by request URL **with the API key stripped**, so rotating your key does not orphan the cache.

Delete the directory to refetch. Details in **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)**.

## Substituting the clock

The SDK's time surface is NodaTime, including its clock. `AddFmp` registers `IClock` with `TryAddSingleton`, so
registering your own **first** wins:

```csharp
services.AddSingleton<IClock>(new FakeClock(Instant.FromUtc(2026, 1, 1, 0, 0)));
services.AddFmp(o => o.ApiKey = "test");
```

That is how the throttle's behaviour is driven in tests without a real clock. `TimeProvider` is deliberately not
used — see the **[FAQ](faq.md)**.

## Reference

* [Configuration section in the README](../../README.md#configuration)
* [Two pipelines, kept apart](../../README.md#two-pipelines-kept-apart)
