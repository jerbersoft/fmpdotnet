# Error Handling

**Every failure is an exception. Nothing signals one by returning.**

There is no `Try`-prefixed method anywhere in the SDK, and no method that returns a sentinel to mean "refused".
That is a decision, not an omission — the reasoning is at the bottom of this page.

## The hierarchy

```
Exception
└── FmpException                      catch this to catch everything the SDK raises deliberately
    ├── FmpRateLimitedException       429 — Duration? RetryAfter
    ├── FmpApiException               the reason was in the BODY — string ErrorMessage, HttpStatusCode? StatusCode
    └── FmpPlanRestrictedException    402 or 403 — HttpStatusCode StatusCode, IsPlanLimitation, IsRejectedCredential
```

Plus one BCL type the SDK raises on purpose:

* **`TimeoutException`** — a single HTTP attempt exceeded `RequestTimeout` / `BulkRequestTimeout`. Deliberately
  *not* the `TaskCanceledException` that callers routinely misread as a shutdown signal.

## `FmpPlanRestrictedException` — 402 and 403 are not the same thing

FMP refuses an endpoint your key is not entitled to with **402**, and refuses a key it does not like with **403**.
The SDK raises one type for both, but **does not conflate them**:

```csharp
catch (FmpPlanRestrictedException ex)
{
    if (ex.IsRejectedCredential)   // 403 — check the key before the invoice
        logger.LogError("FMP rejected the key: {Message}", ex.Message);
    else                           // 402 — genuinely an entitlement answer
        logger.LogWarning("Not on this plan: {Message}", ex.Message);
}
```

**This matters more than it looks.** FMP's own error text warns that "frequent abuse on this API Endpoint may
result in restrictions placed on this API Key" — so a 403 is a plausible outcome of hammering the bulk endpoints.
Reporting that as *"upgrade your plan"* sends someone to the billing page over a broken credential.

The two messages are worded differently by the SDK for exactly this reason, so the causes do not read identically
in a log.

### Do not cache the gating

`profile-bulk` and `shares-float-all` were both recorded as 402 on Premium, and **both answered 200 when
re-probed**. Entitlement moves, and it varies per key.

Code that decides once that an endpoint is unavailable goes stale silently. **The SDK carries no tier map and
never will**, for the same reason — anything claiming "this needs Ultimate" would be confidently wrong sooner or
later. Probe, catch, and re-probe.

The right shape for an optional fast path is therefore a catch, not a capability check:

```csharp
try
{
    await foreach (var p in fmp.Bulk.StreamAllProfilesAsync(ct))
        await store.UpsertAsync(p, ct);
}
catch (FmpPlanRestrictedException ex) when (ex.IsPlanLimitation)
{
    logger.LogInformation("Bulk profiles not entitled; falling back to per-symbol.");
    foreach (var symbol in symbols)
        await store.UpsertAsync(await fmp.Company.GetProfileAsync(symbol, ct), ct);
}
```

## `FmpApiException` — when the reason is in the body

FMP puts the reason in the response **body** on two occasions where the status line cannot carry it, and both are
measured rather than theorised.

**A throttled bulk call returns HTTP 200** with `{"Error Message": "Limit Reach. …"}` — JSON, on an endpoint whose
success shape is CSV. `EnsureSuccessStatusCode` passes; a naive CSV parse yields zero rows; a caller reads *"no
data today"* instead of *"you were throttled"*. The transport inspects the payload and raises this instead.

**A bulk `part` past the end answers HTTP 400** with the plain text `Query Error: Invalid or missing query
parameter - part`, under a `content-type: application/json` that is a lie. That text is the only thing that says
what went wrong, so it is surfaced here rather than discarded behind a bare `HttpRequestException`.

`StatusCode` is what lets you tell those apart **without matching on message text**:

```csharp
catch (FmpApiException ex)
{
    if (ex.StatusCode is null)                       // arrived on a 200 — bulk throttling
        logger.LogWarning("Bulk throttled, retry later: {Error}", ex.ErrorMessage);
    else if (ex.StatusCode == HttpStatusCode.BadRequest)
        logger.LogError("FMP rejected the request: {Error}", ex.ErrorMessage);
}
```

`ErrorMessage` is the upstream's own text — unwrapped from the JSON envelope when the body was one, trimmed and
length-capped when it was not. **It never carries the API key**: requests are rendered through
`FmpRequest.ToString()`, which omits it.

### One more shape worth knowing

`/stable/company-symbol-list` **does not exist, and says so in the success shape** — it answers **404 with the
body `[]`**, a JSON array, which is what this API returns when a request *works*. A client that reads the body for
an explanation finds a valid empty result on a failed request. The SDK's own error path once did exactly that and
reported `FmpApiException: []`, naming neither the status nor the path. It now ignores an array body and reports
the status.

The working directory endpoints are `stock-list` and `actively-trading-list`.

## `FmpRateLimitedException` — 429

By the time this surfaces, **the SDK has already reacted**: the shared token bucket was drained and held for the
advised `Retry-After`, clamped by `MaxRetryAfter`. So a retry meets the SDK's own back-pressure rather than the
limit that just rejected it.

`RetryAfter` carries the value the response advised, **before** clamping, or null when the response carried none.

This exception is raised so a caller can **re-queue the unit of work rather than drop it**. If you are seeing it
often, `PerMinuteCap` is set above what your tier allows — see **[Configuration](configuration.md)**.

## `TimeoutException`

Raised when one HTTP **attempt** exceeds its budget. Two things make it more useful than it usually is:

* **It is not a cancellation.** `TaskCanceledException` from an `HttpClient` timeout is one of the most commonly
  misread exceptions in .NET; the SDK converts it at the handler boundary.
* **Throttle waiting does not count against it.** The deadline starts inside the rate limiter, so a request that
  queued for thirty seconds behind a 429 hold has its full `RequestTimeout` once it starts. Otherwise the SDK's
  own back-pressure would produce a wave of timeouts at exactly the wrong moment.

`HttpClient.Timeout` is set to infinite; the handler owns the deadline.

## Null is data, never an error

Endpoints returning `T?` use null for an answer FMP **genuinely gave**:

| Method | `null` means |
|---|---|
| `Company.GetProfileAsync` | FMP has no such symbol |
| `Company.GetSharesFloatAsync` | likewise |
| `Statements.GetScoresAsync` | an ETF, which genuinely has no scores |

An entitled call with nothing to say returns an **empty list** — not null, and not an exception.

Collapsing a 402 into an empty result is what makes a paywalled endpoint indistinguishable from a real empty
answer *and* from the provider being down. This SDK's predecessor shipped that defect.

**And an empty list is not always what it appears to be.** Two measured cases where `[]` means something else:

* A **class-share ticker in dotted form** — `BRK.B` answers `[]`, `BRK-B` answers a row.
* A **screener value FMP does not recognise** — returns `[]` under HTTP 200, indistinguishable from a filter that
  matched nothing. Check the spelling against `GetSectorsAsync()` / `GetIndustriesAsync()` before concluding the
  universe is empty.

See **[Troubleshooting](troubleshooting.md)**.

## Why there is no `TryGetProfileAsync`

C# forbids `out` parameters on async methods (**CS1988**), so the BCL's `bool TryX(out T)` shape cannot be written
for an async API at all. That is why the framework has no `TryReadAsync` either — and why `ChannelReader<T>` pairs
a *synchronous* `TryRead` with an *asynchronous* `ReadAsync` that throws.

An earlier version of this SDK imitated the pattern with a nullable return. That was worse than either option: it
put two error channels on one surface and gave `null` a meaning the signature could not carry, so you had to go
and read the docs to learn that it meant "refused" rather than "nothing there". It was removed in a breaking
change — see the **[Changelog](../changelog.md)**.

To degrade instead of failing, catch the exception. It is self-describing at the catch site and tells you *which*
refusal arrived.

## Reference

* [Plan gating — 402 and 403](../../README.md#plan-gating--402-and-403)
* [Upstream behaviour the SDK handles for you](../../README.md#upstream-behaviour-the-sdk-handles-for-you)
