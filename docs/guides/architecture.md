# Architecture

How the pieces fit, and which arrangements are load-bearing rather than incidental.

## The shape

```
FmpClient                          disposable; owns a container only when FmpClientFactory built it
  ├── Company · Statements · Quote · Chart · Directory · Search · Calendar · Analyst · Economics
  │   · SecFilings · Congress · News · … 24 groups        → FmpTransport      (client "fmp")
  └── Bulk                                               → FmpBulkTransport  (client "fmp-bulk")

FmpTransport                       FmpBulkTransport : FmpTransport
  GetListAsync<T>()                  (same surface, bound to the bulk HttpClient)
  GetObjectAsync<T>()
  GetBytesAsync()
  StreamCsvAsync<T>()
```

Endpoint classes are thin. They build an `FmpRequest`, choose a transport method, and map the result. Everything
that could go wrong on the wire is the transport's problem, which is why there is exactly one place that classifies
an FMP error.

`FmpBulkTransport` is a **subclass bound to a different `HttpClient`**, not a reimplementation. It inherits the
whole surface; what differs is the handler stack behind its client.

## Four ways in, one wiring path

`AddFmp` on an `IServiceCollection`, `AddFmp` on an `IHostApplicationBuilder`, `FmpClientFactory.Create` for a
process with no container, and the named overloads for a process holding more than one FMP configuration all end
in one internal routine, `FmpRegistration.Register`. It is the only place the handler chain is spelled out, which
is what keeps the chain's order — contractual, see below — from drifting between entry points.

Everything that knows what a container is lives in **`FmpDotNet.Extensions.DependencyInjection`**. The core
**`FmpDotNet`** package references `Microsoft.Extensions.Options`, the logging abstractions and NodaTime, and
nothing else; `PackageBoundaryTests` reads the compiled assembly's references, so a stray container or host
dependency on the core fails the build.

`FmpClient` is a composition of two transports — `FmpClient(FmpTransport, FmpBulkTransport)` — and every endpoint
group takes exactly one of them. It is `IDisposable` so the factory path can own the private container it built;
on a container-resolved client `Dispose` is a no-op. The consequence for the container path is that a container
tracks a resolved client until its scope ends, so resolve it inside a scope, or hold one instance, rather than
resolving a new client per call from the root provider.

The four paths, with examples, are in the README's
[Registering the SDK](../../README.md#registering-the-sdk).

## Two clients, two handler stacks

Each registration gets two **named** `HttpClient`s — `"fmp"` and `"fmp-bulk"` for the default registration,
`"fmp:{name}"` and `"fmp-bulk:{name}"` for a named one. The handlers are constructed inside the chain rather than
registered as services, so each link reads its own registration's options and draws from its own registration's
reservoir. The order handlers are added in is the order they run, outermost first.

```
"fmp"        →  [yours]  →  FmpRetryHandler  →  FmpRateLimitHandler  →  FmpTimeoutHandler  →  network

"fmp-bulk"   →  [yours]  →  FmpDeveloperBulkCacheHandler  →  FmpBulkRetryHandler
                 →  FmpBulkRateLimitHandler  →  FmpBulkTimeoutHandler  →  network
```

Four placements matter.

**Consumer handlers are outermost.** A handler added through `IFmpBuilder` — a proxy, a tracing span, a stubbed
primary handler in a test — is entered once per logical call; the SDK's retry, throttle wait and timeout all
happen beneath it. The corollary is that a second retry policy stacked on top multiplies with the SDK's own: two
policies of three attempts each are nine sends per call. Tune `MaxAttempts` instead.

**The timeout sits inside the rate limiter.** Time spent waiting on the token bucket does not consume
`RequestTimeout`. That separation is load-bearing: a 429 can hold the bucket for up to `MaxRetryAfter`, and a
timeout that counted throttle waits would convert the SDK's own back-pressure into a wave of abandoned requests at
exactly the moment the upstream is already refusing traffic.

**The developer cache is the outermost SDK handler.** A replay must not count as an attempt, consume a bulk token
or start a timeout, so a cache hit never reaches the retry or the rate limiter at all.

**`HttpClient.Timeout` is infinite.** The handler owns the deadline, and raises `TimeoutException` rather than the
`TaskCanceledException` callers routinely misread as a shutdown signal.

## The reservoirs

`FmpBuckets` is **one object holding both** `TokenBucket`s — `Standard` and `Bulk` — rather than two separately
held buckets. One object makes "exactly one reservoir per traffic class" a structural invariant instead of a
registration convention.

Pairs are handed out by `FmpBucketRegistry`, **one per API key within a container**, because FMP meters per key.
Two registrations on the same key — `AddFmp(…)` beside `AddFmp("research", …)` with the same `ApiKey` — draw from
one pair, so their aggregate rate stays at the cap; registrations on different keys get their own, so an Ultimate
key is not paced down to a Premium key's cap. Two registrations sharing a key but declaring different caps cannot
both be honoured: the first to resolve sizes the pair, and the second is logged as a warning naming both. The
registry keys on a SHA-256 of the key, so a diagnostic dump of it is not a second legible copy of the secret.
Calling `AddFmp` again for a name re-configures its options and wires nothing twice.

A host that registers the SDK and also builds a side client through `FmpClientFactory.Create` on the same key has
two containers, and would emit at twice its cap. Hand both the same `FmpBucketRegistry` — `UseBucketRegistry` on
the builder, `registry:` on the factory — and they join reservoirs.

The corollary stands one level up: **two processes sharing one API key emit at twice the measured-safe rate** —
the SDK cannot pace what it cannot see.

`TokenBucket` takes its time from `IClock`, so `NodaTime.Testing.FakeClock` drives throttle behaviour in tests
without a real clock.

## Error classification lives in one place

The transport is the only thing that decides what an FMP failure *is*, because the decision needs the status, the
body and the content type together — and on this API those three disagree with each other routinely.

| What arrives | What it means | Raised as |
|---|---|---|
| 200 + `{"Error Message": …}` on a CSV endpoint | bulk throttling | `FmpApiException`, `StatusCode` **null** |
| 400 + plain text under `content-type: application/json` | bad parameter | `FmpApiException` carrying the text |
| 404 + body `[]` | the path does not exist | `FmpApiException` reporting the **status**, ignoring the array body |
| 402 / 403 | entitlement / credential | `FmpPlanRestrictedException` |
| 429 | rate limited | `FmpRateLimitedException`, after draining and holding the bucket |

The 404-with-`[]` case is the reason the body is not trusted as an explanation: `[]` is what this API returns when
a request **works**. An earlier version read the body first and reported `FmpApiException: []`, naming neither the
status nor the path.

Details and the measurements behind each row are in **[Error Handling](error-handling.md)**.

## The API key never appears in a string

`FmpRequest` builds the URL and appends the key itself, so no caller constructs one by hand. `UriRedaction`
removes it from any rendering, and `FmpRequest.ToString()` uses that — which is what exception messages and the
developer cache's keys are built from.

That is why cache entries survive a key rotation, and why an `FmpApiException` message can be logged without
scrubbing.

## Serialization: two pipelines, no reflection

### JSON

`FmpJsonContext` is a source-generated `JsonSerializerContext`. Nothing reflects over a model, which is what lets
the library declare `IsAotCompatible` — turning `IL2026` and `IL3050` into build **errors**, so reflection-based
serialization cannot creep back in without failing CI.

`NodaConverters` handles the time surface, including the fact that FMP sends two different zones under one
identical `"yyyy-MM-dd HH:mm:ss"` shape. `AllowReadingFromString` is on, because FMP sends some numerics quoted
(`"fiscalYear":"2026"`) and the first quoted number would otherwise abort the whole response rather than one field.

### CSV

`CsvStreamReader` maps a row at a time from the response stream. `CsvRow` gives a mapper typed accessors by column
name, so a `FromCsv` mapper reads like the JSON models do.

Nothing buffers. `etf-holder-bulk`'s single 298 MB part streams 2,571,137 rows at **0.2 MB of peak live memory**.

**`PrefixedStream` is what makes error detection possible without buying the buffering.** The transport must read
the first bytes of a bulk response to find out whether it is CSV or a JSON error object — but having read them, a
plain stream cannot un-read them. `PrefixedStream` replays that consumed prefix and then continues with the rest,
so the CSV reader sees a complete stream and the 69 MB payload is still never held in memory.

## Two enums where one would have been ambiguous

`FiscalPeriod` has six members (`Annual`, `Quarter`, `Q1`–`Q4`). `BulkFiscalPeriod` has five — no `Quarter` —
because the bulk endpoints genuinely do not accept the rolling quarterly value.

Two types make the invalid combination unrepresentable. One type plus a runtime check would push the failure to a
call that returns HTTP 200 with no rows.

The same reasoning produces `ChartInterval`: six intraday paths behind one method, with the path segment derived
from an enum member so it cannot be misspelled.

## Options bound by name

`ConfigurationBinder.Bind` is neither trim- nor AOT-safe, so the `Fmp` section is read property by property, and a
named registration reads `Fmp:{name}` the same way. An explicit read per option costs less than a source generator
or an SDK that breaks on a consumer's trimmed publish.

Validation runs at startup with `ValidateOnStart`, so a bad `BaseUrl` or a zero cap fails while you are looking at
the console rather than on a request hours later; a factory-built client validates in `Create` for the same reason,
and named options validate under their own name so the failure says which registration. The API key is
deliberately excluded — an SDK cannot know whether its caller intends to make a request.

The `Duration` parse tries the bare-seconds form **before** the clock form, because `TimeSpan.TryParse("45")`
yields forty-five *days*. See **[Configuration](configuration.md)**.

## How the coverage table stays true

`EndpointCoverageTests` drives every public endpoint method against a stub and records the path it **actually
requests**, then regenerates the table between the markers in the README. Adding, renaming or deleting a method
without regenerating fails the build.

This is why the coverage page can be trusted in a way a hand-maintained one cannot: it is derived from behaviour,
not from intent. See **[Development](development.md)**.

## What the tests cannot see, and what covers it

The unit suite runs entirely on stubs, and **a stub keeps saying what it always said**. It cannot notice FMP
renaming a field — and a rename does not even fail, because almost every property is nullable and not `required`,
so the rows keep arriving looking correct.

That blind spot is covered by a second suite that calls the real API weekly and records **which fields carried a
value**, not merely that a call succeeded. See **[Live Smoke Suite](live-smoke-suite.md)**.

## Reference

* [Registering the SDK](../../README.md#registering-the-sdk)
* [Two pipelines, kept apart](../../README.md#two-pipelines-kept-apart)
* [Dates and times are NodaTime](../../README.md#dates-and-times-are-nodatime)
