# Host registration — design, 2026-09-01

No issue yet; one should be opened before implementation, per CONTRIBUTING.

**Revised 2026-09-01, after #44 merged** (`f505748`). The first draft was written against a three-link
chain and assumed #44 was still open. It is not, and the retry handler it added takes the outermost slot
this design wanted to hand to consumers. Every section below that touched handler order has been
rewritten rather than patched; see "Handler order" and "Risks". Line citations were against `f505748`.

**Revised 2026-09-02, after #61 merged** (`e33ed85`). `AddFmp` no longer lives in the core: #61 moved
`FmpServiceCollectionExtensions.cs` into a second package, `FmpDotNet.Extensions.DependencyInjection`,
and cut the core down to `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions` and
NodaTime, with `PackageBoundaryTests` pinning that cut. Everything this design adds to the DI layer now
lands in that package, the container-free factory moves off `FmpClient` because the core can no longer
call `AddFmp`, and the one dependency this design was worried about adding to the core is added to the
extensions package instead. The sections that changed are "Global constraints", "The pivot", "Reservoirs",
"The container-free factory", "Host-builder sugar", "Public surface added", "File layout", "Testing" and
"Risks". Line citations are now against `e33ed85`. Two files moved without their line numbers changing —
`FmpServiceCollectionExtensions.cs` is now `src/FmpDotNet.Extensions.DependencyInjection/` and
`AddFmpTests.cs` is now `tests/FmpDotNet.Extensions.DependencyInjection.Tests/` — so a citation into
either names the same line at the new path.

`AddFmp` already registers the SDK into anything holding an `IServiceCollection` — ASP.NET Core, a Worker
Service, a console app built on `Host.CreateApplicationBuilder`. `FmpDotNet.SmokeTests/LiveApi.cs:41`
registers it against a bare `ServiceCollection` today. So this slice is not "make the SDK registrable". It
is four specific things that the one registration path cannot express:

1. a **container-free** construction path, for a host with no `IServiceCollection` at all;
2. **`IHostApplicationBuilder` sugar**, so `Program.cs` is one line;
3. a **customization surface**, so a host can put its own handlers on the two FMP clients;
4. **named registrations**, so one process can hold more than one FMP configuration.

Unlike every other spec in this directory, nothing here rests on a measurement of FMP. It rests on the
shape of this codebase, and the claims about that shape were checked against the code rather than
remembered. Each is cited to the file and line that proves it.

## Goal

One core registration routine, parameterised by name, that all four entry points call. No duplicated
pipeline, because the handler order is contractual and a divergence in it fails silently rather than
loudly.

## What this is not

- **Not a retry policy.** #44 shipped one — `FmpRetryHandler`/`FmpBulkRetryHandler` and the four
  `FmpOptions` knobs `MaxAttempts`, `BulkMaxAttempts`, `RetryBaseDelay`, `MaxRetryDelay` — and this
  design does not revisit any of it. What this slice must do is fit alongside it: carry the two retry
  links through the name-parameterised core, and decide where a consumer's handlers sit relative to a
  retry the SDK now performs itself. That second question is materially harder than it was when retry
  was hypothetical — see "Handler order".
- **Not a change to any handler, transport, endpoint group or model.** All seven handler classes and both
  transports are untouched; see "No handler changes" below for why that falls out rather than being an
  aspiration.
- **Not a tier map.** Unchanged: entitlement moves and varies per key.
- **No environment-variable convention.** `FmpClientFactory.Create()` will not read `FMP_API_KEY`. The smoke
  suite reads it, a host can pass it in one line, and a library that silently picks up ambient
  credentials is worse than one that does not.

## Global constraints

- Target `net10.0`. No reflection: both packages declare `IsAotCompatible` through
  `src/Directory.Build.props`, and `IL2026`/`IL3050` are build errors in each. This design *reduces*
  reflective activation — explicit factory lambdas replace reflective `TryAddTransient<T>()` for the
  per-name registrations.
- NodaTime only in public signatures.
- Every new public member carries an XML doc comment. `CS1591` is not suppressed outside the eight
  documented model files, so an undocumented member fails the build.
- `TreatWarningsAsErrors` is on solution-wide.
- Every existing test in `AddFmpTests` must pass **unmodified**. That is the compatibility proof, and it
  is achievable — see "Compatibility". The file now lives in the extensions package's own test project.
- **The core gains no package reference.** Its three dependencies after #61 are the whole list, and
  `tests/FmpDotNet.Tests/PackageBoundaryTests.cs` fails the build that adds a fourth. This design adds
  `Microsoft.Extensions.Hosting.Abstractions` to that test's negative list, so "never to the core" is
  pinned rather than promised. The extensions package is where new references go.

## The pivot: an `FmpClient` is a composition of two transports

Everything else in this design is cheap because of one fact, verified across all 25 files in
`src/FmpDotNet/Endpoints/`: **every endpoint group takes exactly one constructor dependency**, and it is
a transport.

- 24 groups take `FmpTransport` — e.g. `CompanyEndpoints.cs:8`, `QuoteEndpoints.cs:26`.
- `BulkEndpoints.cs:25` takes `FmpBulkTransport`, which is itself `FmpTransport` (`FmpBulkTransport.cs:10`).

They hold no other state. So an `FmpClient` is fully determined by the pair `(FmpTransport,
FmpBulkTransport)`, and today's 25-parameter constructor is an elaboration of a two-parameter one.

```csharp
public sealed class FmpClient : IDisposable
{
    private readonly IDisposable? _owned;

    public FmpClient(FmpTransport standard, FmpBulkTransport bulk) : this(standard, bulk, null) { }

    public FmpClient(FmpTransport standard, FmpBulkTransport bulk, IDisposable? owned)
    {
        _owned = owned;
        Company = new CompanyEndpoints(standard);
        // …23 more…
        Bulk = new BulkEndpoints(bulk);
    }

    public CompanyEndpoints Company { get; }
    // …
}
```

**Why the ownership constructor is public.** The first draft made the three-argument constructor private,
because `Create` sat on `FmpClient` itself. After #61 the factory lives in the extensions package, another
assembly, and the core cannot see `AddFmp` to build the provider it would hand over. The choices were a
public constructor or an `InternalsVisibleTo` from the core to the extensions package. The constructor
wins: "a client may own one disposable that goes with it" is a legitimate thing for the core to say about
itself, while `InternalsVisibleTo` would open, across the exact boundary #61 drew, a door that
`PackageBoundaryTests` exists to keep shut. The parameter is an `IDisposable?`, not a `ServiceProvider`,
so the core still knows nothing about containers.

**Why this is safe to change.** `new FmpClient(` appears nowhere — not in `src/`, not in `tests/`, not in
`README.md`. The 25-argument constructor is public but has no caller, in this repository or in its
documentation. Reshaping it is a source break only for a consumer who hand-constructed the client rather
than resolving it, which the type's own XML doc has always told them not to do.

**What it buys, beyond the four features.** It deletes the failure mode `AddFmpTests.cs:21` exists to
catch — a group added to `FmpClient` but never registered, which today fails at a consumer's first
resolve rather than in this build. After this change the client cannot be missing a group it declares.

**The 25 individual `TryAddTransient<XEndpoints>()` registrations stay** for the default registration.
Nothing in the repository or README resolves a group directly, so they could go; removing them would be a
silent break for a consumer who does, in exchange for 25 fewer lines. Not worth it. Named registrations
do **not** get them — 25 × N keyed registrations to save `client.Company` is a bad trade, and the
alternative is one property access.

Consequence worth stating plainly: for the default registration, `GetRequiredService<CompanyEndpoints>()`
and `GetRequiredService<FmpClient>().Company` are now *different instances*. They are stateless wrappers
over the same transport, so this is invisible in behaviour.

## Reservoirs: one per API key, not one per process

Today `FmpBuckets` is a single `TryAddSingleton` (`FmpServiceCollectionExtensions.cs:122`), and
`FmpBuckets.cs:3` records why: handlers are transient and `HttpClientFactory` rebuilds them, so a
per-handler bucket would mean several independent reservoirs and an aggregate rate above the cap.
`AddFmpTests.cs:158` pins it.

Named registrations contradict that invariant, so it has to be replaced rather than dropped. FMP meters
**per API key**, so the reservoir is scoped to the key:

```csharp
public sealed class FmpBucketRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _byKeyHash = new(StringComparer.Ordinal);

    public FmpBuckets For(string registrationName, FmpOptions options);
}
```

| registrations | reservoirs |
|---|---|
| `AddFmp("a", ApiKey=K1)` + `AddFmp("b", ApiKey=K1)` | one pair, shared — the emitted rate stays at the cap |
| `AddFmp("a", ApiKey=K1)` + `AddFmp("c", ApiKey=K2)` | two pairs — an Ultimate key is not dragged to a Premium key's cap |

**It lives in the core, in `Http/` beside `FmpBuckets`.** Not because the handlers take it — they take
`FmpBuckets`, which the registry hands out — but because it is a rate-limiting concept with no DI in it:
a dictionary, a hash, and an `ILogger` for the cap-conflict warning, all of which the core already has
(`Microsoft.Extensions.Logging.Abstractions` stayed). The extensions package's `UseBucketRegistry` and
`FmpClientFactory.Create(registry:)` take it as a plain parameter. The #61 spec's one-line reason for this
placement ("because the handlers take it") was wrong; the placement was right.

Registered `TryAddSingleton`, so **the registry is per container**. Within one host, registrations
sharing a key share a reservoir, which is the case that matters. A separate container or a
factory-built client gets its own registry unless explicitly handed one — see "Explicit sharing".

**Why not a process-wide static.** It would make "one reservoir per API key" hold literally everywhere,
at the cost of a static outliving containers and of coupling every test in `AddFmpTests` — which builds
a fresh `ServiceCollection` per test, all with `ApiKey` `"k"` — to a single reservoir that one test could
drain for the others. The per-container registry keeps tests independent by construction.

**Keying on a hash of the key.** The dictionary key is the lowercase hex of `SHA256.HashData(UTF8 bytes)`.
This is defence in depth, not a security boundary, and the spec should not pretend otherwise: the key is
already in `FmpOptions`, and it is in every request URI because that is how FMP authenticates
(`FmpOptions.cs`, `ApiKey`). What it buys is that the registry's own dictionary — the thing a diagnostic
dump or a debugger view of the registry would show — is not a second legible copy of the secret. No
crypto dependency: `SHA256.HashData` and `Convert.ToHexStringLower` are both in-box and AOT-safe.

**The unset key is a real case, not an edge case.** `ApiKey` defaults to `""` and is deliberately never
validated (`FmpServiceCollectionExtensions.cs:114`: an SDK cannot know whether its caller intends to make
a request). So every unconfigured registration shares the `""` reservoir. That is correct — they are all
going to fail the same way — and it is what keeps `AddFmpTests`'s configuration-free cases working.

**First writer wins on caps, and says so.** Two registrations sharing a key but declaring different
`PerMinuteCap`s cannot both be honoured; the first to resolve sizes the bucket. The registry keeps the
caps that created each entry and logs a warning naming both registrations when a later one disagrees.
A warning rather than a throw: the condition is recoverable, the behaviour is defined, and throwing on
first resolve is a hostile response to a misconfiguration this specific. This is the one place the
registry needs an `ILogger`.

## Named registrations

### The blocker

`services.AddHttpClient<FmpTransport>(StandardClient)` (`FmpServiceCollectionExtensions.cs:136`) is a
**typed** client. A type can be registered that way once; two named registrations cannot both resolve as
`FmpTransport`. So registration moves to **named** `HttpClient`s plus explicit transport construction —
which is what typed-client registration does internally, so this is a re-spelling rather than a change in
behaviour.

```csharp
// name is Options.DefaultName ("") for the default registration
services.AddHttpClient(StandardClientName(name))              // "fmp", or "fmp:research"
    .ConfigureHttpClient((sp, c) => { c.BaseAddress = …; c.Timeout = Timeout.InfiniteTimeSpan; })
    // consumer handlers are applied here — see "Handler order"
    .AddHttpMessageHandler(sp => new FmpRetryHandler(
        sp.GetRequiredService<IClock>(), Options.Create(o),
        sp.GetRequiredService<ILogger<FmpRetryHandler>>()))
    .AddHttpMessageHandler(sp => new FmpRateLimitHandler(
        sp.GetRequiredService<IClock>(), registry.For(name, o), Options.Create(o),
        sp.GetRequiredService<ILogger<FmpRateLimitHandler>>()))
    .AddHttpMessageHandler(sp => new FmpTimeoutHandler(Options.Create(o)));
```

The bulk client adds the same four links #44 gave it, in the same order: developer cache, then
`FmpBulkRetryHandler`, then `FmpBulkRateLimitHandler`, then `FmpBulkTimeoutHandler`.

### No handler changes

All seven handler classes keep their `IOptions<FmpOptions>` constructors exactly as they are. The closure
hands them `Options.Create(monitor.Get(name))`, and `FmpRateLimitHandler` gets its `FmpBuckets` from the
registry directly — which its base already takes as a constructor parameter
(`FmpRateLimitHandler.cs:26`). The whole feature is additive in the DI layer. No existing file in `Http/` is
touched; the registry is a new file beside `FmpBuckets`.

**#44's two handlers cost this design nothing**, which is worth checking rather than assuming:
`FmpRetryHandler` and `FmpBulkRetryHandler` take `(IClock, IOptions<FmpOptions>, ILogger<T>)`
(`FmpRetryHandler.cs:138`, `:150`) — the same shape the rate-limit handlers take. They thread through the
name-parameterised core by the same closure, with no special case.

The `TryAddTransient<FmpRateLimitHandler>()` style registrations
(`FmpServiceCollectionExtensions.cs:123-129`) go away, replaced by the explicit lambdas above. That is
the AOT improvement noted in the constraints: reflective activation becomes explicit construction.

### Options

`services.AddOptions<FmpOptions>(name)` carries the same **eleven** `Validate` calls and `ValidateOnStart`
as today, per name — seven before #44, plus the four it added for `MaxAttempts`, `BulkMaxAttempts`,
`RetryBaseDelay` and `MaxRetryDelay`. Named options validate independently, so a bad `"research"`
registration fails at startup naming `"research"`.

For the default registration `name` is `Options.DefaultName`, and `IOptions<FmpOptions>.Value` is by
definition `monitor.Get("")` — so the default path keeps resolving `IOptions<FmpOptions>` exactly as it
does now, and `FmpTransport`'s constructor never learns that names exist.

### Resolution

```csharp
services.AddFmp("research", o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; });

class Report([FromKeyedServices("research")] FmpClient fmp) { … }
```

Keyed `FmpTransport`, `FmpBulkTransport` and `FmpClient` per name. The default registration additionally
registers **unkeyed** `FmpTransport` and `FmpBulkTransport`, because the README's "Reaching an endpoint that is not modelled" section documents
those as the supported way to reach one of FMP's endpoints the SDK has not modelled — that escape hatch
must not become keyed-only.

### Naming

`StandardClient` (`"fmp"`) and `BulkClient` (`"fmp-bulk"`) stay as public constants; `AddFmpTests.cs:189`
uses them. Two helpers are added for the named forms:

```csharp
public static string StandardClientName(string? name);   // "" → "fmp",      "research" → "fmp:research"
public static string BulkClientName(string? name);       // "" → "fmp-bulk", "research" → "fmp-bulk:research"
```

A null or empty name means the default registration. Registering the same name twice is the caller
re-configuring one registration, not creating two — the `TryAdd*` semantics that already make
`AddFmp` idempotent (`AddFmpTests.cs:158`) extend per name.

## `IFmpBuilder` and handler order

```csharp
services.AddFmp(configuration, fmp => fmp
    .ConfigureStandardClient(b => b.ConfigurePrimaryHttpMessageHandler(() => corporateProxyHandler))
    .ConfigureBulkClient(b => b.ConfigurePrimaryHttpMessageHandler(() => stub))
    .UseBucketRegistry(shared));
```

The examples are deliberately a proxy and a test stub rather than `AddStandardResilienceHandler`, which
the first draft used and which `FmpRetryHandler`'s doc comment records as having measurably harmed a
consumer of this SDK. See "Consumer handlers go outermost".

`AddFmp` keeps returning `IServiceCollection`, so every existing call site compiles untouched — including
`AddFmpTests.cs:18` (`…AddFmp(configuration).BuildServiceProvider()`) and `:162`
(`…AddFmp(c).AddFmp(c).AddFmp(c)…`). The customization surface arrives as an optional
`Action<IFmpBuilder>` parameter instead of as a changed return type. A builder return would be the more
mainstream idiom, but it is a source break for a cosmetic gain and this is not the slice to spend that
on.

```csharp
public interface IFmpBuilder
{
    IServiceCollection Services { get; }
    string Name { get; }                                              // "" for the default registration
    IFmpBuilder ConfigureStandardClient(Action<IHttpClientBuilder> configure);
    IFmpBuilder ConfigureBulkClient(Action<IHttpClientBuilder> configure);
    IFmpBuilder ConfigureAllClients(Action<IHttpClientBuilder> configure);
    IFmpBuilder UseBucketRegistry(FmpBucketRegistry registry);
}
```

### The builder collects; it does not proxy

The implementation records the callbacks and `AddFmp` applies them at one defined point. It is not a live
wrapper around `IHttpClientBuilder`. This matters for two reasons, and both are correctness rather than
style: `UseBucketRegistry` has to be known *before* the rate-limit handlers are constructed, and consumer
handlers have to be added *before* ours to land outermost. A live proxy would make both depend on the
order of statements inside the caller's lambda.

### Consumer handlers go outermost

`AddFmp` invokes the collected client callbacks **before** adding its own handlers, so the chains are:

```
standard:  consumer handlers → retry → throttle → timeout → network
bulk:      consumer handlers → developer bulk cache → retry → throttle → timeout → network
```

Outermost is still the right default, but **the reason has changed completely since #44**, and the
first draft's reason is now obsolete. That draft argued outermost was right so a consumer's retry handler
would take a token and a deadline per attempt. The SDK now performs that retry itself, and
`FmpRetryHandler` already occupies the outermost slot on the standard client for exactly that reasoning
(`FmpServiceCollectionExtensions.cs:131`). So the question is no longer "where should a retry go" — it is
"what does a consumer handler see, given a retry it does not control".

Outermost means a consumer handler sees **one entry per logical call**, not one per attempt. Retries,
throttle waits and timeouts all happen beneath it. That is the correct default for the three things
consumers actually add here — a proxy, a tracing span, a stubbed primary handler in an integration test —
because all three want the logical call, not its internal attempts. The alternative placement, between
the SDK's retry and its throttle, would show a handler each attempt but would sit inside a retry loop it
cannot see, which is a worse default and a stranger thing to explain.

The cost is real and has to be documented rather than designed away: **a consumer who adds a handler to
observe retries will not see them**, and a consumer who adds their own retry handler gets
`MaxAttempts × their attempts` sends — nine, at both defaults of three. The XML doc on
`ConfigureStandardClient` says so in those words.

**This is the design's sharpest hazard, and it is not hypothetical.** `FmpRetryHandler`'s own doc comment
records a measured case: a consumer of this SDK "had to strip `AddStandardResilienceHandler` off both
clients because its retry did exactly this and its circuit breaker then cascaded a handful of 429s into
thousands of skipped symbols". The customization surface being added here is precisely the thing that
makes `AddStandardResilienceHandler` a one-liner again. The first draft of this spec used it as the
worked example. It no longer does — the examples are a proxy and a test stub, and the doc comment carries
an explicit warning against stacking a second retry policy. See "Risks".

The existing order comments (`FmpServiceCollectionExtensions.cs:131` and `:141`) already record that
handler order is contractual; this adds the consumer-handler rule to both, with the reasoning above.

Two smaller consequences worth stating:

- Consumer handlers sit *outside* the developer bulk cache, so a tracing handler observes cache hits.
  That is the right way round — a hit is an event the host may want to see — and it does not disturb the
  property the cache was placed outermost for, which is that a hit consumes neither a bulk token nor a
  timeout budget (`FmpServiceCollectionExtensions.cs:141`).
- On the bulk client the SDK's retry sits *inside* the cache, so a replay is never retried. Consumer
  handlers being outside both preserves that.

**A stale comment to fix while here.** `FmpServiceCollectionExtensions.cs:192` still describes the chain
as "throttle → timeout → network". #44 made that untrue on both clients and the comment was not updated;
this slice rewrites it rather than leaving a contractual statement about order that no longer matches the
order.

### Explicit sharing

`UseBucketRegistry` is how a container and a factory-built client join reservoirs:

```csharp
var shared = new FmpBucketRegistry();
services.AddFmp(o => o.ApiKey = "K", fmp => fmp.UseBucketRegistry(shared));
using var side = FmpClientFactory.Create(o => o.ApiKey = "K", registry: shared);
```

Without it, a console tool that both registers the SDK and spins up a side client on the same key emits
at twice its cap. With it, they share one reservoir.

### Section name

A `sectionName` parameter on the configuration-binding overloads. Default `"Fmp"` for the default
registration and `"Fmp:{name}"` for a named one — so `Fmp:research:ApiKey` binds `"research"`. The
convention is regular and the override exists for hosts whose configuration is shaped otherwise.

## The container-free factory

```csharp
using var fmp = FmpClientFactory.Create("apikey");

using var fmp = FmpClientFactory.Create(
    o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; },
    loggerFactory: factory,          // optional
    registry: shared,                // optional
    configure: b => b.ConfigureStandardClient(…));   // optional
```

`FmpClientFactory` is a static class in the extensions package, namespace
`FmpDotNet.Extensions.DependencyInjection`. The first draft put `Create` on `FmpClient`; after #61 the core
cannot reference `AddFmp`, and a factory that did not go through `AddFmp` would be the second wiring path
this design exists to avoid. So the method moves, and keeps its one-wiring-path property. A consumer who
references only the core does not get it, and does not need it: a core-only consumer has a container of
their own, by the README's own definition of who references the core alone.

`Create` builds a private `ServiceProvider` through `AddFmp` and holds it:

```csharp
var sp = new ServiceCollection()
    .AddLogging(b => …)              // supplied ILoggerFactory, or none
    .AddFmp(configure, fmpBuilder)
    .BuildServiceProvider();

return new FmpClient(sp.GetRequiredService<FmpTransport>(),
                     sp.GetRequiredService<FmpBulkTransport>(),
                     sp);            // the public 3-arg ctor: sp is what Dispose disposes
```

**Why a private container rather than hand-wiring.** One wiring path. The handler order is contractual
and getting it wrong fails silently — a throttle inside a timeout still works, it just stops obeying the
cap under back-pressure. A hand-wired second copy would have to be kept in sync with `AddFmp` by
inspection, forever, including for every handler added later. It costs no new dependency: the concrete
`Microsoft.Extensions.DependencyInjection` container already ships to every consumer of the extensions
package, pulled in by `Microsoft.Extensions.Logging` via `Microsoft.Extensions.Http` — which, after #61,
is exactly the package the factory lives in. The core does not carry it and does not need to. The cost
is a container the caller did not ask for and a few milliseconds at construction.

**Disposal.** `Dispose` disposes `_owned` and nothing else, so it is a no-op on a DI-resolved client.
`ServiceProvider.Dispose` is idempotent, so double disposal is safe.

**On making `FmpClient` disposable at all.** A transient `IDisposable` is tracked by the scope it is resolved from,
which for a client resolved from the *root* provider means it is retained for the provider's life. That is new:
before this design nothing the container resolved for the SDK was disposable — `FmpTransport` is not, and the typed
client's `HttpClient` was created by the factory inside its registration lambda rather than resolved as a service.
The cost is bounded and documented rather than designed away: resolve the client inside a scope (an ASP.NET Core
request already is one) or hold one instance, rather than resolving per call from the root; the doc on `Dispose`
and the README's registration section both say so. The default registration constructs its unkeyed client directly
from the transports rather than forwarding to the keyed one, so a root-resolved client is tracked once, not twice.

**Logging defaults to none.** With no `ILoggerFactory` the clamped-`Retry-After` warning
(`FmpRateLimitHandler.cs:61`) and the cap-conflict warning go nowhere. Documented on the overload,
because a silent throttle is exactly the thing someone debugging a slow run needs to see.

The `configure` hook is on the factory partly for consumers — a proxy, a corporate handler — and partly
because it is how this path gets tested without a network.

## Host-builder sugar

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddFmp();                                  // binds "Fmp" off builder.Configuration
builder.AddFmp("research");                        // binds "Fmp:research"
```

```csharp
public static IHostApplicationBuilder AddFmp(
    this IHostApplicationBuilder builder,
    string? name = null, string? sectionName = null, Action<IFmpBuilder>? configureBuilder = null);

public static IHostApplicationBuilder AddFmp(
    this IHostApplicationBuilder builder,
    Action<FmpOptions> configure, string? name = null, Action<IFmpBuilder>? configureBuilder = null);
```

Delegates to `builder.Services.AddFmp(builder.Configuration, …)`. This is the **only new package
dependency in the design**: `Microsoft.Extensions.Hosting.Abstractions`, added to
`FmpDotNet.Extensions.DependencyInjection` and never to the core. Before #61 that reference would have
landed on the core, which this design flagged as its first thing to cut; #61 retired the concern rather
than the feature. A third package for the hosting sugar alone was considered and rejected: it would carry
two extension methods, and a consumer using `IHostApplicationBuilder` already has every abstraction the
DI package pulls in. It is still the lowest-value item of the four per unit of public surface — see
"Risks".

## Public surface added

### New overloads, never new optional parameters

The two existing `AddFmp` signatures are left **byte-identical**. Adding an optional parameter to a
shipped method is a binary break — a consumer compiled against `0.1.0-ci.N` would have to recompile — and
new overloads cost nothing to avoid it. The README already tells consumers to pin an exact prerelease,
which makes this cheap to get right and annoying to get wrong.

```csharp
// unchanged
IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration);
IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure);

// new — default registration, with customization
IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration,
                          Action<IFmpBuilder> configureBuilder, string? sectionName = null);
IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure,
                          Action<IFmpBuilder> configureBuilder);

// new — named registration
IServiceCollection AddFmp(this IServiceCollection services, string name, IConfiguration configuration,
                          Action<IFmpBuilder>? configureBuilder = null, string? sectionName = null);
IServiceCollection AddFmp(this IServiceCollection services, string name, Action<FmpOptions> configure,
                          Action<IFmpBuilder>? configureBuilder = null);
```

Overload resolution is unambiguous throughout: the first parameter is `IConfiguration`, `Action<FmpOptions>`
or `string`, and the two callback types are distinct. `AddFmp(configuration, fmp => …)` binds the third
overload; `AddFmp(o => o.ApiKey = "…", fmp => …)` binds the fourth.

```csharp
// FmpDotNet.Extensions.DependencyInjection.FmpClientFactory
public static FmpClient Create(string apiKey);
public static FmpClient Create(Action<FmpOptions> configure, ILoggerFactory? loggerFactory = null,
                               FmpBucketRegistry? registry = null,
                               Action<IFmpBuilder>? configureBuilder = null);
```

### Everything added

| type / member | package | where |
|---|---|---|
| `FmpClient(FmpTransport, FmpBulkTransport)` | `FmpDotNet` | replaces the 25-arg constructor |
| `FmpClient(FmpTransport, FmpBulkTransport, IDisposable?)` | `FmpDotNet` | new — the ownership constructor the factory uses |
| `FmpClient : IDisposable` | `FmpDotNet` | new |
| `FmpBucketRegistry` | `FmpDotNet` | `Http/` |
| `FmpClientFactory.Create(…)` ×2 overloads | `FmpDotNet.Extensions.DependencyInjection` | new static class |
| `IFmpBuilder` | `FmpDotNet.Extensions.DependencyInjection` | new |
| four `AddFmp` overloads, above | `FmpDotNet.Extensions.DependencyInjection` | `FmpServiceCollectionExtensions` |
| `StandardClientName(string?)`, `BulkClientName(string?)` | `FmpDotNet.Extensions.DependencyInjection` | `FmpServiceCollectionExtensions` |
| `IHostApplicationBuilder.AddFmp(…)` ×2 | `FmpDotNet.Extensions.DependencyInjection` | `FmpHostApplicationBuilderExtensions` |

The core's public surface grows by one constructor pair, one interface implementation and one class in
`Http/`. Everything that knows what a container is sits in the extensions package.

## Compatibility

**Every existing test in `AddFmpTests` passes unmodified** — re-checked against the file as #44 left it,
which added three cases the first draft of this spec never saw. The ones that constrain this design:

| test | what it resolves | why it still passes |
|---|---|---|
| `Registers_exactly_one_reservoir_pair…` (`:158`) | `FmpBuckets` (`:167`) | compat registration below |
| `Gives_the_two_clients_separate_reservoirs` (`:171`) | `FmpBuckets` (`:174`) | compat registration below |
| `Every_retry_attempt_draws_its_own_token…` (`:316`) | `FmpBuckets` (`:336`) | compat registration below |
| `The_ordinary_client_retries_a_5xx_and_the_bulk_client_does_not` (`:268`) | named clients by constant | `StandardClient`/`BulkClient` preserved |
| `Holding_the_bucket_for_nothing_on_a_429…` (`:290`) | named client by constant | same |

The compatibility registration, on the default registration only:

```csharp
services.TryAddSingleton(sp => sp.GetRequiredService<FmpBucketRegistry>()
                                 .For("", sp.GetRequiredService<IOptions<FmpOptions>>().Value));
```

Since the registry caches per key, this resolves to the *same* instance the default registration's
handlers use — so `GetRequiredService<FmpBuckets>()` keeps working and keeps meaning what it meant.
`Registers_exactly_one_reservoir_pair_however_many_times_AddFmp_is_called` then passes as written: three
`AddFmp` calls with the same (empty) key reach one registry and one pair.

`Every_retry_attempt_draws_its_own_token_because_the_retry_sits_outside_the_throttle` is the one to watch
hardest, because it asserts a *cross-handler* property — that the reservoir it resolves is the same one
the retried attempts drew from. The compat registration is what keeps that true. If it were ever dropped,
that test would not fail loudly; it would resolve a second, full reservoir and silently assert nothing.
Worth a comment on the registration saying so.

Dropping the `TryAddTransient<FmpRateLimitHandler>()`-style registrations is safe for the same reason,
checked rather than assumed: `FmpBuckets` is the *only* pipeline service any test resolves from the
container. The handler tests construct their subjects directly — e.g.
`FmpDeveloperBulkCacheHandlerTests.cs:40` — so no test depends on a handler being registered.

The one break is `new FmpClient(25 args)`, which has no caller anywhere in the repository or its
documentation.

## File layout

`FmpServiceCollectionExtensions.cs` is ~200 lines after #44 and would reach ~400. Splitting it is part of
this work, not a follow-up. Two projects now, and the split falls along the package boundary #61 drew:

**`src/FmpDotNet.Extensions.DependencyInjection/`** — everything that knows what a container is

| file | contents |
|---|---|
| `FmpServiceCollectionExtensions.cs` | public entry points only |
| `FmpRegistration.cs` | internal, name-parameterised core — the one wiring path |
| `FmpOptionsBinder.cs` | the existing `Bind` method, already self-contained |
| `IFmpBuilder.cs`, `FmpBuilder.cs` | new |
| `FmpClientFactory.cs` | new — the container-free path, built on `AddFmp` |
| `FmpHostApplicationBuilderExtensions.cs` | new |
| `FmpDotNet.Extensions.DependencyInjection.csproj` | add `Microsoft.Extensions.Hosting.Abstractions` 10.0.9 |

**`src/FmpDotNet/`** — the core, which gains no reference

| file | contents |
|---|---|
| `Http/FmpBucketRegistry.cs` | new |
| `FmpClient.cs` | 2-arg and 3-arg ctors, `IDisposable` |
| `FmpDotNet.csproj` | unchanged |

**Elsewhere**

| file | contents |
|---|---|
| `tests/FmpDotNet.Tests/PackageBoundaryTests.cs` | one more `InlineData`: `Microsoft.Extensions.Hosting.Abstractions` |
| `README.md` | a "Registering the SDK" section covering all four paths, and the Installing section saying the factory and the host sugar are in the extensions package |

The binder moves unchanged. Its long comment about `TimeSpan.TryParse("45")` meaning forty-five days is
the reason it is worth having its own file rather than being buried mid-registration. Two small
corrections belong with the move, both left behind by #44: the binder's own doc comment still says
"Seven explicit reads" when there are now eleven, and `FmpServiceCollectionExtensions.cs:192` still
describes the chain as "throttle → timeout → network". Neither is this slice's doing; both are in files
this slice rewrites, and leaving a stale contractual comment in a file being restructured is worse than
fixing it in passing. #63 tracks the second of these among the follow-ups from #61, which kept the moved
file byte-identical on purpose; if #63 lands first, this slice inherits the fix, and if this slice lands
first, it closes that item.

## Testing

New coverage, one test per claim this design makes. Tests follow their subject's package:
`FmpBucketRegistryTests` and the `PackageBoundaryTests` addition go in `tests/FmpDotNet.Tests/`; every
other file below goes in `tests/FmpDotNet.Extensions.DependencyInjection.Tests/`, which gains the
concrete `Microsoft.Extensions.Hosting` 10.0.9 so `FmpHostBuilderTests` can call
`Host.CreateApplicationBuilder`. Nothing new needs `InternalsVisibleTo`: `FmpRegistration` is internal
to the extensions package and is exercised only through `AddFmp`.

**`PackageBoundaryTests`** (existing, core)
- `Microsoft.Extensions.Hosting.Abstractions` joins the four assemblies the core must not reference. Green
  before this slice and after it; its job is the commit that puts the hosting sugar in the wrong project.

**`FmpBucketRegistryTests`**
- same key, two registration names → `Assert.Same` on both `Standard` and `Bulk`
- different keys → `Assert.NotSame`
- unset key → the `""` entry, shared, not an exception
- same key with conflicting `PerMinuteCap` → first wins, and a warning naming both registrations is
  logged (asserted through a capturing `ILoggerProvider`)

**`AddFmpTests` additions** — the existing file, existing cases untouched
- `AddFmp("a", …)` + `AddFmp("b", …)` → both `FmpClient`s resolve keyed, and are distinct
- same key across two names → one reservoir pair; different keys → two
- a named registration's options validate under its own name
- the unkeyed `FmpTransport`/`FmpBulkTransport` still resolve when a default registration exists
  (the README's "Reaching an endpoint that is not modelled" escape hatch)

**`FmpBuilderTests`**
- a consumer handler added via `ConfigureStandardClient` sits **outside the retry handler**. #44 makes
  this assertable by counting rather than by timing, which is strictly better than what the first draft
  of this spec proposed: point the client at a 5xx upstream with `MaxAttempts = 3` and
  `RetryBaseDelay = 1ms`, and assert the consumer handler was entered **once** while the upstream saw
  **three** sends. Inside the retry it would have been entered three times. No clock, no wall-time
  assertion, no reflection over the chain — the numbers differ by construction. This is modelled on
  `AddFmpTests.cs:316`, which uses the same 5xx-upstream setup for the reservoir claim.
- the same shape proves the ordering claim's cost: the consumer handler observes one entry for a call
  that took three attempts, which is the documented behaviour rather than a defect
- `ConfigurePrimaryHttpMessageHandler` on the bulk client reaches the bulk client only
- `UseBucketRegistry` makes a container and a factory-built client share a pair

**`FmpClientFactoryTests`**
- `Create` yields a client that answers from a stubbed primary handler
- `Dispose` disposes the private provider; a call after `Dispose` throws
- `Dispose` on a DI-resolved client is a no-op and leaves it usable
- `Create` with no `ILoggerFactory` does not throw

**`FmpHostBuilderTests`**
- `Host.CreateApplicationBuilder` + `builder.AddFmp()` binds `Fmp` from configuration
- `builder.AddFmp("research")` binds `Fmp:research`

## Risks

**The surface is wide for a pre-1.0 SDK.** Four features, each adding public types, on a package whose
README already warns that a minor bump may break. Items 1 (the transport-pair pivot) and 3 (named
registrations) are load-bearing — the first because everything else rests on it, the second because it is
the only one that cannot be approximated by a few lines in a consumer's own code. The
`IHostApplicationBuilder` sugar is the weakest per unit of surface. Before #61 it also carried the
design's only new package dependency, onto the core; that reference now lands on the extensions package,
where a hosting abstraction is at home, so the cost is public surface alone. It is still the first thing
to cut if the surface starts feeling wide. This is recorded as
a reservation, not an objection: all four are in scope as agreed.

**Per-key reservoirs make a misconfiguration quieter than a crash.** Two registrations sharing a key with
different caps produce a warning and defined first-writer-wins behaviour. Someone who does not read logs
gets the other registration's cap. The alternative — throwing at first resolve — was judged worse for a
condition this recoverable, but the trade is real and belongs in the doc comment as well as here.

**The customization surface re-enables a failure this SDK has already been burned by, and this is the
risk I would most want a second opinion on.** `FmpRetryHandler`'s doc comment records that a consumer had
to strip `AddStandardResilienceHandler` off both clients because its retry amplified load and its circuit
breaker "cascaded a handful of 429s into thousands of skipped symbols". `ConfigureStandardClient` makes
adding it a one-liner again, now on top of an SDK that retries on its own — so the stacked worst case is
`MaxAttempts × their attempts` sends, nine at both defaults.

The design's answer is documentation: an explicit warning on `ConfigureStandardClient`, and examples that
show a proxy and a test stub rather than a resilience handler. That is a weaker guarantee than the
alternatives, and the alternatives were considered and rejected as worse:

- *refusing to expose the standard client* would also block the legitimate cases (proxy, tracing, test
  stubbing) that motivated item 3 in the first place;
- *detecting a consumer-added retry handler* means inspecting the chain, which needs reflection, which
  the repository forbids;
- *making 429 handling immune to a nested retry* is a change to #44's design, out of scope here.

If that trade is judged wrong, the honest response is to cut item 3 rather than to weaken the warning —
the surface is the hazard, and documentation only mitigates it.
