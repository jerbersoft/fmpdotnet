# FAQ

The *why is it like this* questions. For *it is broken* questions see **[Troubleshooting](troubleshooting.md)**.

---

### Why is it called `FmpDotNet` and not `FinancialModelingPrep`?

Because a package called `FinancialModelingPrep` reads as something FMP publishes and supports, and this is an
independent client.

Types keep the `Fmp` prefix — `FmpClient`, `FmpOptions`, `FmpTransport` — because those name the **API being
spoken to**, not the publisher. The distinction is the whole point: the assembly name is a claim about who made
it, and a type name is a claim about what it talks to.

---

### Why is 0.10.0 not 1.0?

Because the surface is still being shaped by what the live API turns out to do. Two releases so far have **removed
public members** after measurement showed they were the wrong shape, and the endpoint surface is still growing.

1.0 is a promise that a minor bump cannot break you; until then a minor bump can, and the version number says so.
0.10.0 is the standing example: it made `GetAllSharesFloatAsync` throw on a `limit` above 5,000 where the call
used to succeed and quietly return a fraction of the universe. Better behaviour, and still a throw where a
caller had none.

What 0.10.0 does promise is that the packages are on nuget.org, restore anonymously, and will not vanish —
nuget.org has no delete, only unlisting, so a pin keeps restoring whatever happens next.

---

### Why does every push publish a new version?

So that what has landed on `master` is installable without waiting for a release, and so that *"which SDK did this
commit build against"* is answerable from your own git history.

Every push that passes CI publishes the version being prepared with `-ci.<CI run number>` on the end. NuGet orders
every prerelease **below** the release of the same version, so those builds never overtake a release and
`dotnet add package` ignores them unless you pass `--prerelease` or name one exactly. Run numbers never reset, so
they are monotonic; a re-run keeps its number and is pushed with `--skip-duplicate`, which makes re-running a
green build a no-op rather than a failure. And nuget.org refuses to overwrite an existing version, so the suffix
is not decoration — a fixed version would fail the publish on the second push. Full detail in
**[Releases and Versioning](releases-and-versioning.md)**.

---

### Why NodaTime rather than `DateTime` / `DateTimeOffset`?

Because FMP sends `"yyyy-MM-dd HH:mm:ss"` **with no offset** for timestamps in at least two different zones, and
the shape tells you nothing about which is which.

`acceptedDate` on filings is Eastern. `shares-float`'s `date` is UTC. Same string, opposite meaning, and the wrong
converter is a **silent 4–5 hour shift** — not an exception, just wrong numbers that look right.

`DateTime` is the type that makes that bug easy to write, because it carries a `Kind` flag that is trivially lost
or wrong. NodaTime forces the question to be answered at the boundary: `LocalDate` cannot be an instant, and an
`Instant` cannot be printed without naming a zone.

`TimeSpan` survives only where a BCL API leaves no choice — `Task.Delay`, `CancellationTokenSource.CancelAfter`,
`HttpClient.Timeout`, and the `Retry-After` header's own type — and is converted at that boundary and nowhere
else.

---

### Why `IClock` rather than `TimeProvider`?

Consistency. The SDK's time surface is NodaTime throughout, and `TimeProvider` would put a second, BCL-shaped
time abstraction alongside it — so a test would have to substitute both, and a reader would have to know which
code used which.

`NodaTime.Testing.FakeClock` substitutes for `IClock` and drives the throttle without a real clock. See
**[Configuration](configuration.md)**.

---

### Why is almost every model property nullable?

Because FMP populates fields inconsistently and renames them without notice, and a `required` property turns
either into an exception that costs the caller **the whole response** rather than one field.

The trade-off is real and is named rather than hidden: a rename **does not fail**. `System.Text.Json` deserialises the
missing name to null and hands back the same rows of the same type. That is exactly the blind spot the
**[Live Smoke Suite](live-smoke-suite.md)** exists to cover — it records which fields carried a *value*, not merely
that a call succeeded.

---

### Why are integral-looking numbers typed `decimal`?

Because reading them as an integer throws, and a throw costs the **whole** response rather than the field.

Two measured cases:

* **Share counts are floating-point on the wire.** `floatShares` has been seen as `25595002.125` — an artifact of
  outstanding × free-float %. The fractions appear intermittently, so a clean sample proves nothing.
* **`piotroskiScore` arrives as `9.0`.** `AllowReadingFromString` rescues a quoted `"9"` and does nothing for an
  unquoted `9.0`, so an `int` would throw and cost the caller all eleven score fields.

Market cap is `decimal` for the same reason — it is fractional on `stable/profile`.

Round at the point of use, where you know what rounding means.

---

### Why are identifiers strings?

`cik` is zero-padded — `"0000320193"`. Parsing it to a number loses the padding that SEC filings actually use, and
you would have to reconstruct it to look anything up.

---

### Why does everything throw? Why is there no `TryGetProfileAsync`?

**C# forbids `out` parameters on async methods** (CS1988), so the BCL's `bool TryX(out T)` shape cannot be
expressed on an async surface at all. That is why the framework has no `TryReadAsync` either, and why
`ChannelReader<T>` pairs a *synchronous* `TryRead` with an *asynchronous* `ReadAsync` that throws.

An earlier version of this SDK imitated the pattern with a nullable return. That was worse than either option: it
put **two error channels on one signature** and gave `null` a meaning the signature could not carry, so a caller
had to read a paragraph of documentation to learn that null meant "refused" rather than "nothing there". It was
removed in a breaking change.

To degrade instead of failing, catch the exception — it is self-describing at the catch site and tells you *which*
refusal arrived. See **[Error Handling](error-handling.md)**.

---

### So what does a `null` return mean?

An answer FMP genuinely gave. `GetProfileAsync` returns null for a symbol FMP does not have; `GetScoresAsync`
returns null for an ETF, which genuinely has no scores.

An entitled call with **nothing to say** returns an empty list — not null, and not an exception. Collapsing a 402
into an empty result is what makes a paywalled endpoint indistinguishable from a real empty answer *and* from the
provider being down. This SDK's predecessor shipped that defect.

---

### Why does the SDK not know which endpoints my plan covers?

Because entitlement moves, and it varies per key. `profile-bulk` and `shares-float-all` were both recorded as 402
on Premium and **both answered 200 when re-probed**.

Anything claiming "this needs Ultimate" would be confidently wrong sooner or later, and worse, it would be wrong
*silently* — code that decides once that an endpoint is unavailable never re-checks. **Probe, catch, and
re-probe.**

---

### Why is 403 not treated as "upgrade your plan"?

Because it usually is not. FMP's own error text warns that "frequent abuse on this API Endpoint may result in
restrictions placed on this API Key" — so a 403 is a plausible outcome of hammering the bulk endpoints, and it is
just as likely to mean a revoked, mistyped or restricted key.

402 is an entitlement answer about the **endpoint**. 403 points at the **credential**. Reporting both as "your
plan does not cover this" sends someone to the billing page over a broken key. `IsPlanLimitation` and
`IsRejectedCredential` exist to keep them apart.

---

### Why is the default rate limit so low for my plan?

`PerMinuteCap` defaults to **660**, which is ~88% of **Premium's 750/min** — the lowest paid tier the SDK targets.
A default calibrated to a higher tier would trip 429s for everyone below it.

**On Ultimate, raise it to `2640`.** Leaving the default spends about a fifth of the budget you are paying for.
See **[Configuration](configuration.md)**.

---

### Why is the bulk throttle only two requests a minute?

Because the cost of getting it wrong is your **key**, not your latency. FMP warns it restricts keys for frequent
bulk abuse, and a second bulk call moments after the first was measured already refused.

Bulk data is refreshed only once every few hours, so asking more often buys nothing. See
**[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)**.

---

### Why is `FmpTransport` public?

So that an unmodelled endpoint never blocks you — and so that reaching one does not mean building a second
`HttpClient`.

The transport carries the shared throttle, the timeout, the 429 handling and the error classification. A call made
any other way has **none** of them, including the shared reservoir — so it would not even count against the budget
the rest of your calls are pacing themselves within. **[Endpoint Coverage](endpoint-coverage.md)** has the pattern.

---

### Why does `GetListAsync` want a `JsonTypeInfo` instead of just `T`?

Because the SDK is AOT-compatible and never reflects over your model. The library declares `IsAotCompatible`,
which turns `IL2026` and `IL3050` into build **errors** — so any reflection-based JSON or configuration binding
fails in this repository's CI rather than months later in a consumer's trimmed publish.

That is not theoretical: those two errors are what forced the source-generated `JsonSerializerContext` and the
by-name options binding in the first place.

---

### Why is the options binding written out by hand?

`ConfigurationBinder.Bind` is neither trim- nor AOT-safe. Eight explicit reads cost less than the alternatives — a
source generator, or an SDK that quietly breaks when a consumer publishes trimmed.

The visible cost is that a **misspelled configuration key is silently ignored** rather than throwing. Check names
against **[Configuration](configuration.md)**.

---

### Why is the endpoint denominator 243 and not something larger?

Because FMP documents the same path several times over — the Commodity, Forex, Crypto and Index sections are
largely `stable/quote` and `stable/historical-price-eod` re-documented under new headings.

243 is the **unique-path** count, enumerated and cross-checked against two independent sources. Counting
documentation pages instead would produce a larger, flattering and meaningless number. See
**[Endpoint Coverage](endpoint-coverage.md)**.

---

### Why is the README so long?

Because the measurements are the valuable part. Most of what is in it could not be read from FMP's documentation —
it was established by probing the live API, and several entries contradict what the documentation implies.

It is also kept honest: the coverage table is generated from the code, and the behavioural claims are re-checked
weekly against the live API by the **[Live Smoke Suite](live-smoke-suite.md)**.

---

### Can I use this without dependency injection?

Yes. `FmpClientFactory.Create`, in the `FmpDotNet.Extensions.DependencyInjection` package, builds a private
container through `AddFmp` and hands you a client that owns it, so the same handler chain is wired and nothing is
hand-assembled:

```csharp
using var fmp = FmpClientFactory.Create(key);

using var fmp = FmpClientFactory.Create(
    o => { o.ApiKey = key; o.PerMinuteCap = 2640; },
    loggerFactory: factory);          // optional; without it the throttle's warnings go nowhere
```

Dispose the client: that disposes the container and both `HttpClient`s, and a disposed client refuses to send.
Options validate in `Create`, not on the first request. No environment variable is read.

Constructing the pieces by hand is also possible — `FmpClient(FmpTransport, FmpBulkTransport)` and the handler types
are public — but you would be reassembling the throttle and the handler order yourself, and the handler **order** is
load-bearing (see **[Architecture](architecture.md)**). If the process also has a container registering the SDK on the
same key, hand both the same `FmpBucketRegistry` so they share one reservoir pair rather than emitting at twice the cap.

---

### Is it safe to call `AddFmp` more than once?

Yes. A second `AddFmp` for the same registration — the default, or the same name — re-configures its options and
wires nothing twice; before #65 it appended a second handler chain, which a three-attempt call measured as nine
sends. One thing it will not do silently: a later call that passes an `IFmpBuilder` callback for a registration
that is already wired throws, because the callback could no longer take effect. Reservoirs come from
`FmpBucketRegistry`, one pair per API key, so however many registrations you add on one key there is one pair.

---

### Will there be breaking changes before 1.0?

Yes, and there already have been several. **Treat a minor bump as potentially breaking until 1.0** — the surface
is still being shaped by what the live API turns out to do. Every breaking change so far *removed* public members
that measurement or use showed were the wrong shape; the latest, #65, removed the 25-argument `FmpClient`
constructor and the handler-type service registrations, and made `FmpClient` disposable. See the
**[Changelog](../changelog.md)**.
