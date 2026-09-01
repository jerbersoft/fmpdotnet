# Host registration — design, 2026-09-01

No issue yet; one should be opened before implementation, per CONTRIBUTING.

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

- **Not a retry policy.** Issue #44 (retry transient 5xx and connection faults) stays open and separate.
  This slice only decides *where a consumer's own retry handler sits* in the chain — see "Handler order"
  — because that placement is a property of the customization surface being added here.
- **Not a change to any handler, transport, endpoint group or model.** The five handler classes and both
  transports are untouched; see "No handler changes" below for why that falls out rather than being an
  aspiration.
- **Not a tier map.** Unchanged: entitlement moves and varies per key.
- **No environment-variable convention.** `FmpClient.Create()` will not read `FMP_API_KEY`. The smoke
  suite reads it, a host can pass it in one line, and a library that silently picks up ambient
  credentials is worse than one that does not.

## Global constraints

- Target `net10.0`. No reflection: the library declares `IsAotCompatible` and `IL2026`/`IL3050` are build
  errors. This design *reduces* reflective activation — explicit factory lambdas replace reflective
  `TryAddTransient<T>()` for the per-name registrations.
- NodaTime only in public signatures.
- Every new public member carries an XML doc comment. `CS1591` is not suppressed outside the eight
  documented model files, so an undocumented member fails the build.
- `TreatWarningsAsErrors` is on solution-wide.
- Every existing test in `AddFmpTests` must pass **unmodified**. That is the compatibility proof, and it
  is achievable — see "Compatibility".

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

    private FmpClient(FmpTransport standard, FmpBulkTransport bulk, IDisposable? owned)
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

Today `FmpBuckets` is a single `TryAddSingleton` (`FmpServiceCollectionExtensions.cs:100`), and
`FmpBuckets.cs:3` records why: handlers are transient and `HttpClientFactory` rebuilds them, so a
per-handler bucket would mean several independent reservoirs and an aggregate rate above the cap.
`AddFmpTests.cs:149` pins it.

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
validated (`FmpServiceCollectionExtensions.cs:96`: an SDK cannot know whether its caller intends to make
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

`services.AddHttpClient<FmpTransport>(StandardClient)` (`FmpServiceCollectionExtensions.cs:105`) is a
**typed** client. A type can be registered that way once; two named registrations cannot both resolve as
`FmpTransport`. So registration moves to **named** `HttpClient`s plus explicit transport construction —
which is what typed-client registration does internally, so this is a re-spelling rather than a change in
behaviour.

```csharp
// name is Options.DefaultName ("") for the default registration
services.AddHttpClient(StandardClientName(name))              // "fmp", or "fmp:research"
    .ConfigureHttpClient((sp, c) => { c.BaseAddress = …; c.Timeout = Timeout.InfiniteTimeSpan; })
    // consumer handlers are applied here — see "Handler order"
    .AddHttpMessageHandler(sp => new FmpRateLimitHandler(
        sp.GetRequiredService<IClock>(), registry.For(name, o), Options.Create(o),
        sp.GetRequiredService<ILogger<FmpRateLimitHandler>>()))
    .AddHttpMessageHandler(sp => new FmpTimeoutHandler(Options.Create(o)));
```

### No handler changes

The five handler classes keep their `IOptions<FmpOptions>` constructors exactly as they are. The closure
hands them `Options.Create(monitor.Get(name))`, and `FmpRateLimitHandler` gets its `FmpBuckets` from the
registry directly — which its base already takes as a constructor parameter
(`FmpRateLimitHandler.cs:29`). The whole feature is additive in the DI layer. Nothing in `Http/` is
touched.

The `TryAddTransient<FmpRateLimitHandler>()` style registrations
(`FmpServiceCollectionExtensions.cs:101-105`) go away, replaced by the explicit lambdas above. That is
the AOT improvement noted in the constraints: reflective activation becomes explicit construction.

### Options

`services.AddOptions<FmpOptions>(name)` carries the same seven `Validate` calls and `ValidateOnStart` as
today, per name. Named options validate independently, so a bad `"research"` registration fails at
startup naming `"research"`.

For the default registration `name` is `Options.DefaultName`, and `IOptions<FmpOptions>.Value` is by
definition `monitor.Get("")` — so the default path keeps resolving `IOptions<FmpOptions>` exactly as it
does now, and `FmpTransport`'s constructor never learns that names exist.

### Resolution

```csharp
services.AddFmp("research", o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; });

class Report([FromKeyedServices("research")] FmpClient fmp) { … }
```

Keyed `FmpTransport`, `FmpBulkTransport` and `FmpClient` per name. The default registration additionally
registers **unkeyed** `FmpTransport` and `FmpBulkTransport`, because README:526 and README:539 document
those as the supported way to reach one of FMP's endpoints the SDK has not modelled — that escape hatch
must not become keyed-only.

### Naming

`StandardClient` (`"fmp"`) and `BulkClient` (`"fmp-bulk"`) stay as public constants; `AddFmpTests.cs:180`
uses them. Two helpers are added for the named forms:

```csharp
public static string StandardClientName(string? name);   // "" → "fmp",      "research" → "fmp:research"
public static string BulkClientName(string? name);       // "" → "fmp-bulk", "research" → "fmp-bulk:research"
```

A null or empty name means the default registration. Registering the same name twice is the caller
re-configuring one registration, not creating two — the `TryAdd*` semantics that already make
`AddFmp` idempotent (`AddFmpTests.cs:149`) extend per name.

## `IFmpBuilder` and handler order

```csharp
services.AddFmp(configuration, fmp => fmp
    .ConfigureStandardClient(b => b.AddStandardResilienceHandler())
    .ConfigureBulkClient(b => b.ConfigurePrimaryHttpMessageHandler(() => stub))
    .UseBucketRegistry(shared));
```

`AddFmp` keeps returning `IServiceCollection`, so every existing call site compiles untouched — including
`AddFmpTests.cs:18` (`…AddFmp(configuration).BuildServiceProvider()`) and `:152`
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

`AddFmp` invokes the collected client callbacks **before** adding its own handlers, so the chain is:

```
consumer handlers → developer bulk cache (bulk only) → throttle → timeout → network
```

This is a decision, not a consequence of how `AddHttpMessageHandler` appends. Innermost would put a
retry handler *inside* the timeout and *inside* the throttle, which breaks both:

- the whole retry sequence would share one `RequestTimeout` budget, so the deadline that is documented as
  bounding "one FMP HTTP attempt" (`FmpOptions.cs`, `RequestTimeout`) would silently bound N of them;
- retries would re-fire without taking a token, which is exactly the above-cap emitted rate the bucket
  exists to prevent — and they would do it while the upstream is already refusing us.

Outermost, each retry attempt takes its own token and gets its own deadline. This is directly relevant to
issue #44, and it is the reason that issue can be implemented later without revisiting this one.

The existing order comment (`FmpServiceCollectionExtensions.cs:151`) already records that handler order
is contractual; this adds the consumer-handler rule to the same comment, with the retry reasoning.

Note the bulk consequence: consumer handlers sit *outside* the developer bulk cache, so a tracing handler
observes cache hits. That is the right way round — a hit is an event the host may want to see — and it
does not disturb the property the cache was placed outermost for, which is that a hit consumes neither a
bulk token nor a timeout budget (`FmpServiceCollectionExtensions.cs:113`).

### Explicit sharing

`UseBucketRegistry` is how a container and a factory-built client join reservoirs:

```csharp
var shared = new FmpBucketRegistry();
services.AddFmp(o => o.ApiKey = "K", fmp => fmp.UseBucketRegistry(shared));
using var side = FmpClient.Create(o => o.ApiKey = "K", registry: shared);
```

Without it, a console tool that both registers the SDK and spins up a side client on the same key emits
at twice its cap. With it, they share one reservoir.

### Section name

A `sectionName` parameter on the configuration-binding overloads. Default `"Fmp"` for the default
registration and `"Fmp:{name}"` for a named one — so `Fmp:research:ApiKey` binds `"research"`. The
convention is regular and the override exists for hosts whose configuration is shaped otherwise.

## The container-free factory

```csharp
using var fmp = FmpClient.Create("apikey");

using var fmp = FmpClient.Create(
    o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; },
    loggerFactory: factory,          // optional
    registry: shared,                // optional
    configure: b => b.ConfigureStandardClient(…));   // optional
```

`Create` builds a private `ServiceProvider` through `AddFmp` and holds it:

```csharp
var sp = new ServiceCollection()
    .AddLogging(b => …)              // supplied ILoggerFactory, or none
    .AddFmp(configure, fmpBuilder)
    .BuildServiceProvider();

return new FmpClient(sp.GetRequiredService<FmpTransport>(),
                     sp.GetRequiredService<FmpBulkTransport>(),
                     sp);            // the private 3-arg ctor: sp is what Dispose disposes
```

**Why a private container rather than hand-wiring.** One wiring path. The handler order is contractual
and getting it wrong fails silently — a throttle inside a timeout still works, it just stops obeying the
cap under back-pressure. A hand-wired second copy would have to be kept in sync with `AddFmp` by
inspection, forever, including for every handler added later. It costs no new dependency: the concrete
`Microsoft.Extensions.DependencyInjection` container already ships to every consumer today, pulled in by
`Microsoft.Extensions.Logging` via `Microsoft.Extensions.Http` (verified against
`src/FmpDotNet/obj/project.assets.json`). The cost is a container the caller did not ask for and a few
milliseconds at construction.

**Disposal.** `Dispose` disposes `_owned` and nothing else, so it is a no-op on a DI-resolved client.
`ServiceProvider.Dispose` is idempotent, so double disposal is safe.

**On making `FmpClient` disposable at all.** A transient `IDisposable` is tracked by the container until
its scope ends, which for one resolved from the *root* provider means it is retained for the provider's
life. That is worth stating and then putting in proportion: it already happens today, because
`AddHttpClient<FmpTransport>` registers a transient wrapping a disposable `HttpClient`. This adds one
tracked object beside ones already tracked; it does not introduce a new class of leak. In an ASP.NET Core
request scope — the normal case — it is disposed with the request.

**Logging defaults to none.** With no `ILoggerFactory` the clamped-`Retry-After` warning
(`FmpRateLimitHandler.cs:59`) and the cap-conflict warning go nowhere. Documented on the overload,
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
    string? name = null, string? sectionName = null, Action<IFmpBuilder>? configure = null);

public static IHostApplicationBuilder AddFmp(
    this IHostApplicationBuilder builder,
    Action<FmpOptions> configure, string? name = null, Action<IFmpBuilder>? configureBuilder = null);
```

Delegates to `builder.Services.AddFmp(builder.Configuration, …)`. This is the **only new package
dependency in the design**: `Microsoft.Extensions.Hosting.Abstractions`, which is not currently in the
graph. It is also the lowest-value item of the four per unit of public surface — see "Risks".

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
public static FmpClient Create(string apiKey);
public static FmpClient Create(Action<FmpOptions> configure, ILoggerFactory? loggerFactory = null,
                               FmpBucketRegistry? registry = null,
                               Action<IFmpBuilder>? configureBuilder = null);
```

### Everything added

| type / member | where |
|---|---|
| `FmpClient(FmpTransport, FmpBulkTransport)` | replaces the 25-arg constructor |
| `FmpClient : IDisposable` | new |
| `FmpClient.Create(…)` ×2 overloads | new |
| `FmpBucketRegistry` | `Http/` |
| `IFmpBuilder` | `DependencyInjection/` |
| four `AddFmp` overloads, above | `FmpServiceCollectionExtensions` |
| `StandardClientName(string?)`, `BulkClientName(string?)` | `FmpServiceCollectionExtensions` |
| `IHostApplicationBuilder.AddFmp(…)` ×2 | `FmpHostApplicationBuilderExtensions` |

## Compatibility

**Every existing test in `AddFmpTests` passes unmodified**, including the two that resolve `FmpBuckets`
directly (`:158`, `:167`). That holds because the default registration keeps a compatibility
registration:

```csharp
services.TryAddSingleton(sp => sp.GetRequiredService<FmpBucketRegistry>()
                                 .For("", sp.GetRequiredService<IOptions<FmpOptions>>().Value));
```

Since the registry caches per key, this resolves to the *same* instance the default registration's
handlers use — so `GetRequiredService<FmpBuckets>()` keeps working and keeps meaning what it meant.
`Registers_exactly_one_reservoir_pair_however_many_times_AddFmp_is_called` then passes as written: three
`AddFmp` calls with the same (empty) key reach one registry and one pair.

Dropping the `TryAddTransient<FmpRateLimitHandler>()`-style registrations is safe for the same reason,
checked rather than assumed: `FmpBuckets` at `:158` and `:167` is the *only* pipeline service any test
resolves from the container. The handler tests construct their subjects directly — e.g.
`FmpDeveloperBulkCacheHandlerTests.cs:40` — so no test depends on a handler being registered.

The one break is `new FmpClient(25 args)`, which has no caller anywhere in the repository or its
documentation.

## File layout

`FmpServiceCollectionExtensions.cs` is ~160 lines and would reach ~350. Splitting it is part of this
work, not a follow-up:

| file | contents |
|---|---|
| `DependencyInjection/FmpServiceCollectionExtensions.cs` | public entry points only |
| `DependencyInjection/FmpRegistration.cs` | internal, name-parameterised core — the one wiring path |
| `DependencyInjection/FmpOptionsBinder.cs` | the existing `Bind` method, already self-contained |
| `DependencyInjection/IFmpBuilder.cs`, `FmpBuilder.cs` | new |
| `DependencyInjection/FmpHostApplicationBuilderExtensions.cs` | new |
| `Http/FmpBucketRegistry.cs` | new |
| `FmpClient.cs` | 2-arg ctor, `IDisposable`, `Create` |
| `FmpDotNet.csproj` | add `Microsoft.Extensions.Hosting.Abstractions` |
| `README.md` | a "Registering the SDK" section covering all four paths |

The binder moves unchanged. Its long comment about `TimeSpan.TryParse("45")` meaning forty-five days is
the reason it is worth having its own file rather than being buried mid-registration.

## Testing

New coverage, one test per claim this design makes:

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
  (the README:526 escape hatch)

**`FmpBuilderTests`**
- a consumer handler added via `ConfigureStandardClient` runs **before** the throttle. Asserted by
  draining the shared registry's bucket with a short hold, then timing entry: the consumer handler
  records its entry instant, the stubbed primary handler records its own, and the gap must cover the
  hold. This is the one test with a real-time dependency; the hold is kept small and the assertion is a
  lower bound, not an equality.
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
`IHostApplicationBuilder` sugar is the weakest per unit of surface and carries the design's only new
package dependency; it is the first thing to cut if the surface starts feeling wide. This is recorded as
a reservation, not an objection: all four are in scope as agreed.

**Per-key reservoirs make a misconfiguration quieter than a crash.** Two registrations sharing a key with
different caps produce a warning and defined first-writer-wins behaviour. Someone who does not read logs
gets the other registration's cap. The alternative — throwing at first resolve — was judged worse for a
condition this recoverable, but the trade is real and belongs in the doc comment as well as here.

**The handler-order test is time-dependent.** It is the only one, it asserts a lower bound rather than an
equality, and the alternative — reflecting over the handler chain — is barred by the repository's own
no-reflection rule.
