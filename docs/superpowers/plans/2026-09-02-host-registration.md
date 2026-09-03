# Host Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the SDK the four registration paths the design names — a container-free factory, `IHostApplicationBuilder` sugar, a customization surface for consumer handlers, and named registrations — through one name-parameterised wiring routine in `FmpDotNet.Extensions.DependencyInjection`, with the core gaining a two-transport `FmpClient` constructor and a per-API-key reservoir registry and no new package reference.

**Architecture:** `FmpClient` becomes a composition of `(FmpTransport, FmpBulkTransport)`, so a client is fully determined by two transports and the 25-argument constructor goes away. `FmpBucketRegistry` in the core's `Http/` hands out one reservoir pair per API key. In the extensions package, `FmpRegistration.Register(services, name, configure, configureBuilder)` is the only place the handler chain is spelled out; the two existing `AddFmp` overloads, four new ones, the host-builder sugar and `FmpClientFactory.Create` all call it. Consumer handlers collected through `IFmpBuilder` are applied before the SDK's own, so they sit outermost.

**Tech Stack:** .NET 10 SDK 10.0.102, C#, Microsoft.Extensions.DependencyInjection keyed services, Microsoft.Extensions.Http named clients, Microsoft.Extensions.Options named options, Microsoft.Extensions.Hosting.Abstractions (extensions package only), xUnit 2.9.3.

**Spec:** [`docs/superpowers/specs/2026-09-01-host-registration-design.md`](../specs/2026-09-01-host-registration-design.md), on master at `ffdfa2b`, revised for #44 and #61. Read it before Task 1. The plan argues from it; the Self-Review at the bottom lists every place the plan goes beyond it. GitHub issue: #65. This plan also closes the items of #63 that live in files this slice rewrites; the Self-Review says which.

## Global Constraints

Copied from the spec's "Global constraints", the #61 spec, and `CONTRIBUTING.md`. Every task's requirements implicitly include this section.

- **Branch is `feat/host-registration-65`**, created from `master` at `ffdfa2b`. Commit in conventional-commit form referencing `#65`. End every commit message with `Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.
- **Target `net10.0`. No reflection in either library.** Both packages declare `IsAotCompatible` through `src/Directory.Build.props`; `IL2026`/`IL3050` are build errors. New registrations use explicit factory lambdas. The 25 `TryAddTransient<XEndpoints>()` calls stay exactly as they are today — they already compile under this rule.
- **NodaTime only in public signatures.** No `TimeSpan` on any new public member.
- **Every new public member carries an XML doc comment.** `CS1591` is an error outside the eight documented model files.
- **`TreatWarningsAsErrors` is on solution-wide.** Run `dotnet build FmpDotNet.slnx -warnaserror` before every commit.
- **The two existing `AddFmp` signatures are byte-identical** after this work: `AddFmp(this IServiceCollection services, IConfiguration configuration)` and `AddFmp(this IServiceCollection services, Action<FmpOptions> configure)`. New capability arrives as new overloads, never as an optional parameter on a shipped method.
- **Every existing test in `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs` passes unmodified.** New cases are appended to that file; no existing line in it changes. The compatibility registration in Task 3 is what makes this true.
- **The core gains no package reference.** `src/FmpDotNet/FmpDotNet.csproj`'s three references are the whole list. `Microsoft.Extensions.Hosting.Abstractions` goes on the extensions csproj and joins `PackageBoundaryTests`' negative list.
- **Package versions:** every `Microsoft.Extensions.*` reference at `10.0.9`, `NodaTime` at `3.2.2`, `VersionPrefix` at `0.1.0`.
- **No existing file in `src/FmpDotNet/Http/` changes.** Handlers keep their `IOptions<FmpOptions>` constructors. The only core source files that change are `FmpClient.cs` and the new `Http/FmpBucketRegistry.cs`.
- **Handler order is contractual.** Standard client: consumer handlers → retry → throttle → timeout → network. Bulk client: consumer handlers → developer cache → retry → throttle → timeout → network. Task 4's counting test is the proof.
- **The constants `StandardClient` (`"fmp"`) and `BulkClient` (`"fmp-bulk"`) keep their names and values.** `AddFmpTests.cs:189` and `:268` resolve clients by them.
- **`FmpClientFactory.Create()` does not read `FMP_API_KEY`** or any other environment variable.
- **Baseline counts on this branch before Task 1:** `FmpDotNet.Tests` 1,463 passed; `FmpDotNet.Extensions.DependencyInjection.Tests` 23 passed; `FmpDotNet.SmokeTests` 22 passed, 5 skipped. Each task states the counts it expects.
- **Never paste an API key**, and never echo one. Every key in this plan is the placeholder `"k"` or a made-up `"K1"`/`"K2"`.

## File Structure

| file | responsibility | task |
|---|---|---|
| `src/FmpDotNet/FmpClient.cs` | **Modify.** Two-transport constructor pair, `IDisposable`, every group built from the pair. Property docs untouched. | 1 |
| `tests/FmpDotNet.Tests/FmpClientTests.cs` | **Create.** The core's own tests for the constructor pivot and disposal. | 1 |
| `src/FmpDotNet/Http/FmpBucketRegistry.cs` | **Create.** One reservoir pair per API key; first writer wins on caps, with a warning. | 2 |
| `tests/FmpDotNet.Tests/FmpBucketRegistryTests.cs` | **Create.** | 2 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpOptionsBinder.cs` | **Create.** The by-name `Bind`, moved unchanged apart from its doc comment. | 3 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpRegistration.cs` | **Create.** The one wiring path, name-parameterised. Named `HttpClient`s, explicit handler lambdas, keyed transports and client, the default-only registrations, the compatibility `FmpBuckets`. | 3, 4 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` | **Rewrite.** Public entry points only: the two existing overloads, four new ones, the two constants, `StandardClientName`/`BulkClientName`. | 3, 4, 5 |
| `src/FmpDotNet.Extensions.DependencyInjection/IFmpBuilder.cs` | **Create.** The customization surface. | 4 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpBuilder.cs` | **Create.** Internal; collects callbacks and the registry, applied by `FmpRegistration`. | 4 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpBuilderTests.cs` | **Create.** | 4 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs` | **Append.** Named-registration cases. Existing lines untouched. | 5 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpClientFactory.cs` | **Create.** The container-free path, built on `AddFmp`. | 6 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpClientFactoryTests.cs` | **Create.** | 6 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpHostApplicationBuilderExtensions.cs` | **Create.** Two `IHostApplicationBuilder.AddFmp` overloads. | 7 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj` | **Modify.** Add `Microsoft.Extensions.Hosting.Abstractions`; rewrite the dependency comment (#63). | 7 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj` | **Modify.** Add `Microsoft.Extensions.Hosting` and a direct `Microsoft.Extensions.DependencyInjection` (#63). | 7 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpHostBuilderTests.cs` | **Create.** | 7 |
| `tests/FmpDotNet.Tests/PackageBoundaryTests.cs` | **Modify.** One more `InlineData`. | 7 |
| `src/FmpDotNet/FmpDotNet.csproj` | **Modify.** One comment (#63). No reference change. | 8 |
| `README.md` | **Modify.** A "Registering the SDK" section; Usage, Configuration and Installing touch-ups (#63). | 8 |

---

### Task 1: `FmpClient` is a composition of two transports

Every endpoint group takes exactly one constructor dependency, a transport — 24 take `FmpTransport`, `BulkEndpoints` takes `FmpBulkTransport` — so the 25-argument primary constructor is an elaboration of a two-argument one. This task replaces it. `new FmpClient(` has no caller in `src/`, `tests/` or `README.md`, and the container resolves the client today through `TryAddTransient<FmpClient>()`, which keeps working: with the two public constructors below, the container picks the two-argument one because `IDisposable` is not a registered service. Task 3 replaces that registration with an explicit factory anyway.

**Files:**
- Modify: `src/FmpDotNet/FmpClient.cs`
- Create: `tests/FmpDotNet.Tests/FmpClientTests.cs`

**Interfaces:**
- Consumes: `FmpTransport(HttpClient http, IOptions<FmpOptions> options)` (`src/FmpDotNet/FmpTransport.cs:25`), `FmpBulkTransport(HttpClient http, IOptions<FmpOptions> options)` (`src/FmpDotNet/FmpBulkTransport.cs:10`); every `XEndpoints(FmpTransport transport)` primary constructor under `src/FmpDotNet/Endpoints/`, and `BulkEndpoints(FmpBulkTransport transport)`.
- Produces: `public FmpClient(FmpTransport standard, FmpBulkTransport bulk)`; `public FmpClient(FmpTransport standard, FmpBulkTransport bulk, IDisposable? owned)`; `FmpClient : IDisposable` whose `Dispose()` disposes `owned` exactly once and nothing else. Task 6's factory uses the three-argument constructor.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/FmpClientTests.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace FmpDotNet.Tests;

/// <summary>The core's own proof of the transport-pair pivot: a client is fully determined by two transports,
/// and disposing it disposes what it owns and nothing else.</summary>
public class FmpClientTests
{
    private static (FmpTransport Standard, FmpBulkTransport Bulk) Transports()
    {
        // No request is ever sent, so a bare HttpClient and unvalidated options are enough.
        var options = Options.Create(new FmpOptions { ApiKey = "k" });
        return (new FmpTransport(new HttpClient(), options), new FmpBulkTransport(new HttpClient(), options));
    }

    [Fact]
    public void Composes_every_group_from_the_transport_pair()
    {
        var (standard, bulk) = Transports();

        using var client = new FmpClient(standard, bulk);

        Assert.NotNull(client.Company);
        Assert.NotNull(client.Directory);
        Assert.NotNull(client.Statements);
        Assert.NotNull(client.Calendar);
        Assert.NotNull(client.Analyst);
        Assert.NotNull(client.Economics);
        Assert.NotNull(client.Search);
        Assert.NotNull(client.SecFilings);
        Assert.NotNull(client.InstitutionalOwnership);
        Assert.NotNull(client.InsiderTrades);
        Assert.NotNull(client.Congress);
        Assert.NotNull(client.Transcripts);
        Assert.NotNull(client.Esg);
        Assert.NotNull(client.Cot);
        Assert.NotNull(client.Quote);
        Assert.NotNull(client.Chart);
        Assert.NotNull(client.Bulk);
        Assert.NotNull(client.TechnicalIndicators);
        Assert.NotNull(client.MarketPerformance);
        Assert.NotNull(client.EtfAndFunds);
        Assert.NotNull(client.Indexes);
        Assert.NotNull(client.MarketHours);
        Assert.NotNull(client.News);
        Assert.NotNull(client.Fundraisers);
        Assert.NotNull(client.DiscountedCashFlow);
    }

    private sealed class Sentinel : IDisposable
    {
        public int Disposals;
        public void Dispose() => Disposals++;
    }

    [Fact]
    public void Dispose_disposes_what_it_owns_exactly_once()
    {
        var (standard, bulk) = Transports();
        var owned = new Sentinel();
        var client = new FmpClient(standard, bulk, owned);

        client.Dispose();
        client.Dispose();

        // The owner is a ServiceProvider in practice, whose Dispose is idempotent — but the client should not
        // rely on that, so it hands the owner over once and forgets it.
        Assert.Equal(1, owned.Disposals);
    }

    [Fact]
    public void Dispose_without_an_owner_is_a_no_op_and_the_client_stays_usable()
    {
        var (standard, bulk) = Transports();
        var client = new FmpClient(standard, bulk);

        client.Dispose();

        Assert.NotNull(client.Company);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`

Expected: compile errors in `FmpClientTests.cs` — `FmpClient` has no constructor taking two or three arguments, and no `Dispose`. That is the RED state for this task; record the first two error lines in your report.

- [ ] **Step 3: Rewrite `FmpClient`'s constructor**

In `src/FmpDotNet/FmpClient.cs`, replace lines 6-21 (the summary and the primary-constructor declaration) with:

```csharp
/// <summary>Entry point to the FMP API, grouped the way FMP's own documentation groups it.
///
/// <para>Resolve this from dependency injection after calling <c>AddFmp</c> from the
/// <c>FmpDotNet.Extensions.DependencyInjection</c> package, or build one without a container through that
/// package's <c>FmpClientFactory.Create</c>. Both go through the same wiring.</para>
///
/// <para>A client is a composition of two transports. Every endpoint group takes exactly one — the ordinary
/// transport, or the bulk transport for <see cref="Bulk"/> — and holds no other state, so the pair determines
/// the whole client and a group can never be declared here yet left out of the wiring.</para></summary>
public sealed class FmpClient : IDisposable
{
    private IDisposable? _owned;

    /// <summary>Composes the client from the two transports.</summary>
    public FmpClient(FmpTransport standard, FmpBulkTransport bulk) : this(standard, bulk, null) { }

    /// <summary>Composes the client from the two transports and takes ownership of <paramref name="owned"/>,
    /// which <see cref="Dispose"/> disposes.
    ///
    /// <para><c>FmpClientFactory.Create</c> hands over the private container it built the transports from. A
    /// client resolved from a host's own container owns nothing, and its <see cref="Dispose"/> does
    /// nothing.</para></summary>
    public FmpClient(FmpTransport standard, FmpBulkTransport bulk, IDisposable? owned)
    {
        ArgumentNullException.ThrowIfNull(standard);
        ArgumentNullException.ThrowIfNull(bulk);
        _owned = owned;

        Company = new CompanyEndpoints(standard);
        Directory = new DirectoryEndpoints(standard);
        Statements = new StatementEndpoints(standard);
        Calendar = new CalendarEndpoints(standard);
        Analyst = new AnalystEndpoints(standard);
        Economics = new EconomicsEndpoints(standard);
        Search = new SearchEndpoints(standard);
        SecFilings = new SecFilingsEndpoints(standard);
        InstitutionalOwnership = new InstitutionalOwnershipEndpoints(standard);
        InsiderTrades = new InsiderTradesEndpoints(standard);
        Congress = new CongressEndpoints(standard);
        Transcripts = new TranscriptsEndpoints(standard);
        Esg = new EsgEndpoints(standard);
        Cot = new CotEndpoints(standard);
        Quote = new QuoteEndpoints(standard);
        Chart = new ChartEndpoints(standard);
        Bulk = new BulkEndpoints(bulk);
        TechnicalIndicators = new TechnicalIndicatorsEndpoints(standard);
        MarketPerformance = new MarketPerformanceEndpoints(standard);
        EtfAndFunds = new EtfAndFundsEndpoints(standard);
        Indexes = new IndexesEndpoints(standard);
        MarketHours = new MarketHoursEndpoints(standard);
        News = new NewsEndpoints(standard);
        Fundraisers = new FundraisersEndpoints(standard);
        DiscountedCashFlow = new DiscountedCashFlowEndpoints(standard);
    }
```

Then strip the 25 property initialisers, leaving every property and its doc comment byte-for-byte as it is:

```bash
sed -i '' -E 's/ \{ get; \} = [A-Za-z]+;$/ { get; }/' src/FmpDotNet/FmpClient.cs
grep -c '{ get; }$' src/FmpDotNet/FmpClient.cs     # expect 25
grep -c '{ get; } =' src/FmpDotNet/FmpClient.cs    # expect 0
```

Finally add `Dispose` before the class's closing brace, after the `DiscountedCashFlow` property:

```csharp

    /// <summary>Disposes whatever this client owns — the private container behind a factory-built client — and
    /// nothing else. A no-op on a client resolved from dependency injection, and safe to call twice.</summary>
    public void Dispose() => Interlocked.Exchange(ref _owned, null)?.Dispose();
```

- [ ] **Step 4: Build and run the new tests**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test tests/FmpDotNet.Tests --no-build --filter "FullyQualifiedName~FmpClientTests"`
Expected: 3 passed.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test FmpDotNet.slnx --no-build -- RunConfiguration.TreatNoTestsAsError=true`
Expected: `FmpDotNet.Tests` 1,466 passed (1,463 + 3); `FmpDotNet.Extensions.DependencyInjection.Tests` 23 passed — in particular `Resolves_the_client_and_every_endpoint_group`, which proves the container still resolves `FmpClient` through the two-argument constructor; `FmpDotNet.SmokeTests` 22 passed, 5 skipped.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet/FmpClient.cs tests/FmpDotNet.Tests/FmpClientTests.cs
git commit -F - <<'EOF'
feat(core): FmpClient is a composition of two transports, and disposable (#65)

Every endpoint group takes exactly one transport, so the 25-argument
constructor was an elaboration of a two-argument one. The pair now
determines the whole client, which deletes the failure mode where a group
declared on the client is never registered. A second constructor takes an
owner that Dispose disposes once; a container-resolved client owns
nothing. No caller of the 25-argument form existed in this repository or
its README.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 2: `FmpBucketRegistry` — one reservoir pair per API key

FMP meters per API key. Today `FmpBuckets` is one singleton per container (`FmpServiceCollectionExtensions.cs:122`), which named registrations contradict. The registry replaces that invariant with "one pair per key within a container": registrations sharing a key share a pair, different keys get their own. Nothing uses it yet; Task 3 wires it in.

**Files:**
- Create: `src/FmpDotNet/Http/FmpBucketRegistry.cs`
- Create: `tests/FmpDotNet.Tests/FmpBucketRegistryTests.cs`

**Interfaces:**
- Consumes: `FmpBuckets(FmpOptions options, double nowSeconds = 0.0)` (`src/FmpDotNet/Http/FmpBuckets.cs:11`); `FmpOptions.ApiKey`, `.PerMinuteCap`, `.BulkPerMinuteCap`.
- Produces: `public sealed class FmpBucketRegistry(ILogger<FmpBucketRegistry>? logger = null)` with `public FmpBuckets For(string registrationName, FmpOptions options)`. Task 3 registers one per container and calls `For(name, options)` from the rate-limit handler lambdas; Task 4's `UseBucketRegistry` and Task 6's `Create(registry:)` take a consumer-made instance.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Tests/FmpBucketRegistryTests.cs`:

```csharp
using FmpDotNet.Http;
using Microsoft.Extensions.Logging;

namespace FmpDotNet.Tests;

public class FmpBucketRegistryTests
{
    private static FmpOptions With(string apiKey, int cap = 660, int bulkCap = 2) =>
        new() { ApiKey = apiKey, PerMinuteCap = cap, BulkPerMinuteCap = bulkCap };

    [Fact]
    public void Registrations_sharing_a_key_share_one_pair()
    {
        var registry = new FmpBucketRegistry();

        var a = registry.For("a", With("K1"));
        var b = registry.For("b", With("K1"));

        // The emitted rate stays at the cap because both registrations draw from the same reservoirs.
        Assert.Same(a, b);
        Assert.Same(a.Standard, b.Standard);
        Assert.Same(a.Bulk, b.Bulk);
    }

    [Fact]
    public void Registrations_on_different_keys_get_their_own_pairs()
    {
        var registry = new FmpBucketRegistry();

        // An Ultimate key is not dragged down to a Premium key's cap.
        Assert.NotSame(registry.For("a", With("K1")), registry.For("c", With("K2")));
    }

    [Fact]
    public void The_unset_key_is_a_shared_pair_rather_than_an_error()
    {
        var registry = new FmpBucketRegistry();

        // ApiKey defaults to "" and is never validated; every unconfigured registration lands here, and they
        // are all going to fail the same way, so sharing is right.
        Assert.Same(registry.For("", With("")), registry.For("other", With("")));
    }

    [Fact]
    public void First_writer_wins_on_caps_and_the_conflict_is_logged_naming_both_registrations()
    {
        var log = new CapturingLogger();
        var registry = new FmpBucketRegistry(log);

        var first = registry.For("", With("K1", cap: 300));
        var second = registry.For("research", With("K1", cap: 3000));

        Assert.Same(first, second);
        var warning = Assert.Single(log.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("(default)", warning.Message);
        Assert.Contains("research", warning.Message);
        Assert.Contains("300", warning.Message);
        Assert.Contains("3000", warning.Message);
    }

    [Fact]
    public void Agreeing_caps_do_not_warn()
    {
        var log = new CapturingLogger();
        var registry = new FmpBucketRegistry(log);

        registry.For("a", With("K1"));
        registry.For("b", With("K1"));

        Assert.Empty(log.Entries);
    }

    private sealed class CapturingLogger : ILogger<FmpBucketRegistry>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error" | head -3`
Expected: `FmpBucketRegistry` does not exist. Record the first error line.

- [ ] **Step 3: Write the registry**

Create `src/FmpDotNet/Http/FmpBucketRegistry.cs`:

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FmpDotNet.Http;

/// <summary>One reservoir pair per API key, because FMP meters per key.
///
/// <para>Registrations that share a key share a pair, so their aggregate rate stays at the cap; registrations on
/// different keys get their own, so an Ultimate key is not dragged down to a Premium key's cap. One registry per
/// container: the container wiring registers it as a singleton, and a factory-built client gets its own unless
/// it is handed one — which is how a host and a side client on the same key join reservoirs.</para>
///
/// <para>Keyed on a SHA-256 of the key rather than the key itself, so a debugger view or a diagnostic dump of
/// this dictionary is not a second legible copy of the secret. Defence in depth, not a security boundary: the key
/// is in every request URI, because that is how FMP authenticates.</para>
///
/// <para>The unset key is a real case. <see cref="FmpOptions.ApiKey"/> defaults to <c>""</c> and is never
/// validated, so every unconfigured registration shares the <c>""</c> pair. They are all going to fail the same
/// way, and sharing is what keeps a configuration-free test container working.</para>
///
/// <para>First writer wins on caps. Two registrations sharing a key but declaring different caps cannot both be
/// honoured; the first to resolve sizes the pair, and a later one that disagrees is logged as a warning naming
/// both, once per disagreeing registration. A warning rather than a throw, because the condition is recoverable
/// and the behaviour is defined. An instance created without a logger — as a consumer does to share one across
/// containers — warns nowhere.</para></summary>
public sealed class FmpBucketRegistry(ILogger<FmpBucketRegistry>? logger = null)
{
    private sealed record Entry(FmpBuckets Buckets, int PerMinuteCap, int BulkPerMinuteCap, string Registration)
    {
        public ConcurrentDictionary<string, byte> Warned { get; } = new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, Entry> _byKeyHash = new(StringComparer.Ordinal);
    private readonly ILogger _logger = logger ?? NullLogger<FmpBucketRegistry>.Instance;

    /// <summary>The pair for the API key in <paramref name="options"/>, created from these options the first
    /// time the key is seen. <paramref name="registrationName"/> is <c>""</c> for the default registration and
    /// is used only to name the parties in the cap-conflict warning.</summary>
    public FmpBuckets For(string registrationName, FmpOptions options)
    {
        ArgumentNullException.ThrowIfNull(registrationName);
        ArgumentNullException.ThrowIfNull(options);

        var entry = _byKeyHash.GetOrAdd(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.ApiKey))),
            _ => new Entry(new FmpBuckets(options), options.PerMinuteCap, options.BulkPerMinuteCap, registrationName));

        if ((entry.PerMinuteCap != options.PerMinuteCap || entry.BulkPerMinuteCap != options.BulkPerMinuteCap)
            && entry.Warned.TryAdd(registrationName, 0))
        {
            _logger.LogWarning(
                "FMP registrations {First} and {Second} share an API key but declare different caps "
                + "({FirstCap}/min, bulk {FirstBulkCap}/min against {SecondCap}/min, bulk {SecondBulkCap}/min). "
                + "The first to resolve sized the shared reservoir; the second's caps are ignored.",
                Display(entry.Registration), Display(registrationName),
                entry.PerMinuteCap, entry.BulkPerMinuteCap, options.PerMinuteCap, options.BulkPerMinuteCap);
        }

        return entry.Buckets;

        static string Display(string name) => name.Length == 0 ? "(default)" : name;
    }
}
```

- [ ] **Step 4: Build and run the new tests**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`. `NullLogger<T>` and `LogWarning` both come from `Microsoft.Extensions.Logging.Abstractions`, which the core already references; `Convert.ToHexStringLower` and `SHA256.HashData` are in-box. No new reference.

Run: `dotnet test tests/FmpDotNet.Tests --no-build --filter "FullyQualifiedName~FmpBucketRegistryTests"`
Expected: 5 passed.

- [ ] **Step 5: Confirm the boundary held, then commit**

Run: `dotnet test tests/FmpDotNet.Tests --no-build --filter "FullyQualifiedName~PackageBoundaryTests"`
Expected: 7 passed — the registry added nothing the core did not already reference.

```bash
git add src/FmpDotNet/Http/FmpBucketRegistry.cs tests/FmpDotNet.Tests/FmpBucketRegistryTests.cs
git commit -F - <<'EOF'
feat(core): FmpBucketRegistry hands out one reservoir pair per API key (#65)

FMP meters per key, so the reservoir is scoped to the key rather than to
the process: registrations sharing a key share a pair, different keys get
their own. Keyed on a SHA-256 of the key so the dictionary is not a second
legible copy of the secret. First writer wins on caps, with a warning that
names both registrations. Nothing wires it yet; the container wiring does
in the next commit.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 3: One wiring path — `FmpRegistration`, name-parameterised

The design's goal: one core registration routine, parameterised by name, that every entry point calls. This task creates it and re-points the two existing `AddFmp` overloads at it with the name fixed to `""`, so the public surface is unchanged while the wiring underneath becomes the shape the later tasks extend. Three things change in behaviour, all of them in the design: `FmpBuckets` is drawn from the per-key `FmpBucketRegistry` rather than registered as its own singleton (a compatibility registration keeps `GetRequiredService<FmpBuckets>()` resolving to the same pair the handlers use); the transports are resolved through named `HttpClient`s and explicit factory lambdas instead of typed-client registration; and a second `AddFmp` for the same registration re-configures its options and wires nothing twice — today it appends a second copy of the handler chain, which the RED test below measures as nine sends.

**Files:**
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpOptionsBinder.cs`
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpRegistration.cs`
- Rewrite: `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs`
- Append: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs`

**Interfaces:**
- Consumes: `FmpBucketRegistry.For(string, FmpOptions)` (Task 2); `FmpClient(FmpTransport, FmpBulkTransport)` (Task 1); the seven handler constructors — `FmpRetryHandler(IClock, IOptions<FmpOptions>, ILogger<FmpRetryHandler>)`, `FmpBulkRetryHandler(IClock, IOptions<FmpOptions>, ILogger<FmpBulkRetryHandler>)`, `FmpRateLimitHandler(IClock, FmpBuckets, IOptions<FmpOptions>, ILogger<FmpRateLimitHandler>)`, `FmpBulkRateLimitHandler(IClock, FmpBuckets, IOptions<FmpOptions>, ILogger<FmpBulkRateLimitHandler>)`, `FmpTimeoutHandler(IOptions<FmpOptions>)`, `FmpBulkTimeoutHandler(IOptions<FmpOptions>)`, `FmpDeveloperBulkCacheHandler(IOptions<FmpOptions>, ILogger<FmpDeveloperBulkCacheHandler>)`.
- Produces: `internal static IServiceCollection FmpRegistration.Register(IServiceCollection services, string name, Action<FmpOptions> configure)` — Task 4 adds a fourth parameter; `public static string StandardClientName(string? name)` and `BulkClientName(string? name)` on `FmpServiceCollectionExtensions`; `internal static void FmpOptionsBinder.Bind(IConfiguration section, FmpOptions o)`; keyed `FmpTransport`, `FmpBulkTransport` and `FmpClient` registered under the registration name (`""` for the default), plus unkeyed ones and `FmpBuckets` on the default registration only.

- [ ] **Step 1: Write the failing test**

Append to `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs`, immediately before the class's closing brace (the file's last line). `FailingHandler` is the private class already defined in this file.

```csharp

    [Fact]
    public async Task Calling_AddFmp_twice_for_one_registration_wires_the_handler_chain_once()
    {
        // Registering the same name twice is the caller re-configuring one registration, not creating two. A
        // second copy of the chain would be a retry inside a retry: 3 × 3 = 9 sends per call.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        Action<FmpOptions> configure = o => { o.ApiKey = "k"; o.RetryBaseDelay = Duration.FromMilliseconds(1); };
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(configure).AddFmp(configure);
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        using var provider = services.BuildServiceProvider();

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();

        Assert.Equal(3, upstream.Sends);
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)" && dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build --filter "FullyQualifiedName~Calling_AddFmp_twice"`

Expected: FAIL — `Assert.Equal() Failure: Expected: 3, Actual: 9`. Today each `AddFmp` appends the whole chain to the named client's configuration. Record the actual number in your report; if it is not 9, say so and stop.

- [ ] **Step 3: Move the binder into its own file**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpOptionsBinder.cs`. The body of `Bind` is the current `FmpServiceCollectionExtensions.cs:33-69`, unchanged; only the doc comment changes — the old one said "Seven explicit reads", which stopped being true at #44 (#63).

```csharp
using Microsoft.Extensions.Configuration;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Binds <see cref="FmpOptions"/> from a configuration section by name rather than by reflection.
///
/// <para><c>ConfigurationBinder.Bind</c> is neither trim- nor AOT-safe, and this assembly declares itself
/// AOT-compatible. An explicit read per option costs less than the alternatives — a source generator, or an SDK
/// that quietly breaks when a consumer publishes trimmed.</para></summary>
internal static class FmpOptionsBinder
{
    internal static void Bind(IConfiguration section, FmpOptions o)
    {
        if (section[nameof(FmpOptions.ApiKey)] is { } apiKey) o.ApiKey = apiKey;
        if (section[nameof(FmpOptions.BaseUrl)] is { } baseUrl) o.BaseUrl = baseUrl;
        if (Int32(section[nameof(FmpOptions.PerMinuteCap)]) is { } cap) o.PerMinuteCap = cap;
        if (Int32(section[nameof(FmpOptions.BulkPerMinuteCap)]) is { } bulkCap) o.BulkPerMinuteCap = bulkCap;
        if (Span(section[nameof(FmpOptions.RequestTimeout)]) is { } timeout) o.RequestTimeout = timeout;
        if (Span(section[nameof(FmpOptions.BulkRequestTimeout)]) is { } bulkTimeout) o.BulkRequestTimeout = bulkTimeout;
        if (Span(section[nameof(FmpOptions.MaxRetryAfter)]) is { } retry) o.MaxRetryAfter = retry;
        if (Int32(section[nameof(FmpOptions.MaxAttempts)]) is { } attempts) o.MaxAttempts = attempts;
        if (Int32(section[nameof(FmpOptions.BulkMaxAttempts)]) is { } bulkAttempts) o.BulkMaxAttempts = bulkAttempts;
        if (Span(section[nameof(FmpOptions.RetryBaseDelay)]) is { } backoff) o.RetryBaseDelay = backoff;
        if (Span(section[nameof(FmpOptions.MaxRetryDelay)]) is { } maxBackoff) o.MaxRetryDelay = maxBackoff;
        if (section[nameof(FmpOptions.DeveloperBulkCacheDirectory)] is { } cache)
            o.DeveloperBulkCacheDirectory = cache;

        static int? Int32(string? raw) =>
            int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        // Accepts the "00:00:30" form configuration usually carries and a bare number of seconds, which is what
        // anyone setting this from an environment variable reaches for first.
        //
        // The bare-number case is tested FIRST and deliberately: TimeSpan.TryParse("45") succeeds and yields
        // FORTY-FIVE DAYS. Trying the clock form first therefore turns "RequestTimeout=45" — the most natural
        // thing anyone would write — into a timeout that never fires, silently, with no parse error to notice.
        // The TimeSpan hop is confined to this parse; the option itself is a NodaTime Duration.
        static Duration? Span(string? raw) => raw switch
        {
            null or "" => null,
            var s when !s.Contains(':') && double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds) => Duration.FromSeconds(seconds),
            var s when TimeSpan.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v)
                => Duration.FromTimeSpan(v),
            _ => null,
        };
    }
}
```

- [ ] **Step 4: Write the wiring path**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpRegistration.cs`. The eleven `Validate` calls and their messages are the current `FmpServiceCollectionExtensions.cs:81-111`, verbatim; the handler-order comments are the current `:131-135` and `:141-147`, with the consumer-handler rule added at the top of each.

```csharp
using FmpDotNet.Endpoints;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>The one wiring path. Every public entry point — <c>AddFmp</c> in each of its forms, the host-builder
/// sugar and <c>FmpClientFactory</c> — ends here, parameterised by registration name, so the handler order that
/// is contractual exists in exactly one place.
///
/// <para>A registration is a named pair of <c>HttpClient</c>s (see
/// <see cref="FmpServiceCollectionExtensions.StandardClientName"/>), named options validated under the same
/// name, and keyed <see cref="FmpTransport"/>, <see cref="FmpBulkTransport"/> and <see cref="FmpClient"/>
/// registrations under that name. The default registration — name <c>""</c> — additionally registers the
/// unkeyed transports and client, the endpoint groups, and <see cref="FmpBuckets"/> for compatibility.</para>
/// </summary>
internal static class FmpRegistration
{
    /// <summary>Keyed by registration name; present once the name's chain has been wired, so a second
    /// <c>AddFmp</c> for the same name re-configures its options and adds nothing else.</summary>
    private sealed class Wired;

    internal static IServiceCollection Register(IServiceCollection services, string name, Action<FmpOptions> configure)
    {
        services.AddOptions<FmpOptions>(name)
            .Configure(configure)
            // BaseUrl reaches `new Uri(...)` inside HttpClientFactory on first resolve, which throws a
            // UriFormatException with no mention of configuration. Rejecting it by name at startup is the point.
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl)
                           && Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                "Fmp:BaseUrl must be an absolute URI (e.g. https://financialmodelingprep.com).")
            // At 0 the reservoir never refills and the first Acquire blocks forever: calls hang rather than fail,
            // which is the worst of both.
            .Validate(o => o.PerMinuteCap > 0,
                "Fmp:PerMinuteCap must be > 0 — it is the shared token bucket's refill rate.")
            .Validate(o => o.BulkPerMinuteCap > 0,
                "Fmp:BulkPerMinuteCap must be > 0 — it is the bulk token bucket's refill rate.")
            .Validate(o => o.RequestTimeout > Duration.Zero,
                "Fmp:RequestTimeout must be > 0 — it bounds a single FMP HTTP attempt.")
            .Validate(o => o.BulkRequestTimeout > Duration.Zero,
                "Fmp:BulkRequestTimeout must be > 0 — it bounds a single bulk FMP HTTP attempt.")
            .Validate(o => o.MaxRetryAfter >= Duration.Zero,
                "Fmp:MaxRetryAfter must be >= 0 — it caps how long one 429 may hold the shared request budget.")
            // At 0 there is no attempt at all: the handler's loop would return nothing and the caller would meet a
            // failure the SDK never actually tried to produce. 1 is the "no retry" setting, and it is legal.
            .Validate(o => o.MaxAttempts >= 1,
                "Fmp:MaxAttempts must be >= 1 — it counts SENDS, not retries, so 1 means send once and do not retry.")
            .Validate(o => o.BulkMaxAttempts >= 1,
                "Fmp:BulkMaxAttempts must be >= 1 — it counts SENDS, not retries, so 1 means send once and do not "
                + "retry. 1 is the default for bulk.")
            // At 0 every backoff step is 0 and the jitter with it, so a retry sequence becomes an unpaced burst
            // against an upstream that is already failing.
            .Validate(o => o.RetryBaseDelay > Duration.Zero,
                "Fmp:RetryBaseDelay must be > 0 — it is the first step of the retry backoff, doubling per attempt.")
            // Unlike Fmp:MaxRetryAfter, which may be zero: that one holds the SHARED bucket and "hold it for
            // nothing" is a coherent choice, while a zero ceiling here would flatten every backoff to an
            // unpaced burst.
            .Validate(o => o.MaxRetryDelay > Duration.Zero,
                "Fmp:MaxRetryDelay must be > 0 — it caps one retry's wait, and at 0 every attempt fires immediately.")
            .ValidateOnStart();

        // The API key is deliberately NOT validated. An SDK cannot know whether its caller intends to make a
        // request; the host that does know should assert it.

        // Everything below this line is wired once per name. A second AddFmp for the same name has re-configured
        // its options above and is done: appending the chain again would put a retry inside a retry.
        if (services.Any(d => d.IsKeyedService && d.ServiceType == typeof(Wired) && Equals(d.ServiceKey, name)))
            return services;
        services.AddKeyedSingleton(name, new Wired());

        // NodaTime's clock, not TimeProvider — the SDK's time surface is NodaTime throughout, and a test
        // substitutes NodaTime.Testing.FakeClock here.
        services.TryAddSingleton<IClock>(SystemClock.Instance);
        // One registry per container. Registrations sharing an API key share a reservoir pair through it.
        services.TryAddSingleton(sp => new FmpBucketRegistry(sp.GetRequiredService<ILogger<FmpBucketRegistry>>()));

        // The retry is added FIRST, which makes it the OUTERMOST handler, and that is the point rather than a
        // detail. FmpRateLimitHandlerBase acquires its token BEFORE delegating, so a retry placed inside it would
        // be reached after the single token had already been drawn and every attempt after the first would bypass
        // the reservoir entirely. Outside, each attempt re-acquires — and it is still outside the timeout, so
        // each attempt gets a fresh RequestTimeout rather than sharing one budget.
        // Explicit construction rather than AddHttpMessageHandler<T>: each link gets THIS registration's options,
        // and the throttle gets this registration's reservoir from the registry. Nothing is activated by reflection.
        Configure(services.AddHttpClient(FmpServiceCollectionExtensions.StandardClientName(name)), name)
            .AddHttpMessageHandler(sp => new FmpRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpTimeoutHandler(Options.Create(OptionsFor(sp, name))));

        // The developer cache is added FIRST, which makes it the OUTERMOST handler, and that placement is the
        // point rather than a detail: a replay must not consume a bulk token or start a timeout. A cache hit
        // therefore never reaches the rate limiter at all. It is inert unless
        // FmpOptions.DeveloperBulkCacheDirectory is set, so it costs a null check when it is off.
        // The retry sits INSIDE the cache here, unlike the ordinary client where it is outermost: a replay must
        // never be retried, because a cache hit cannot fail transiently and re-serving it would only multiply the
        // work. FmpOptions.BulkMaxAttempts defaults to 1, so this link is inert unless a caller opts in.
        Configure(services.AddHttpClient(FmpServiceCollectionExtensions.BulkClientName(name)), name)
            .AddHttpMessageHandler(sp => new FmpDeveloperBulkCacheHandler(
                Options.Create(OptionsFor(sp, name)), sp.GetRequiredService<ILogger<FmpDeveloperBulkCacheHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkTimeoutHandler(Options.Create(OptionsFor(sp, name))));

        // The transports and the client, keyed by registration name. The transports' constructors never learn
        // that names exist: each is handed IOptions carrying its own registration's values.
        services.TryAddKeyedTransient(name, (sp, _) => new FmpTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(FmpServiceCollectionExtensions.StandardClientName(name)),
            Options.Create(OptionsFor(sp, name))));
        services.TryAddKeyedTransient(name, (sp, _) => new FmpBulkTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(FmpServiceCollectionExtensions.BulkClientName(name)),
            Options.Create(OptionsFor(sp, name))));
        services.TryAddKeyedTransient(name, (sp, key) => new FmpClient(
            sp.GetRequiredKeyedService<FmpTransport>(key), sp.GetRequiredKeyedService<FmpBulkTransport>(key)));

        if (name.Length == 0) RegisterDefaultOnly(services);
        return services;
    }

    /// <summary>What only the default registration gets: the unkeyed transports and client, the endpoint groups,
    /// and <see cref="FmpBuckets"/> for compatibility.</summary>
    private static void RegisterDefaultOnly(IServiceCollection services)
    {
        var name = Options.DefaultName;

        // The README's "Reaching an endpoint that is not modelled" section documents
        // GetRequiredService<FmpTransport>() and <FmpBulkTransport>() as the way to reach an endpoint the SDK has
        // not modelled. That escape hatch stays unkeyed.
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpTransport>(name));
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpBulkTransport>(name));
        services.TryAddTransient(sp => sp.GetRequiredKeyedService<FmpClient>(name));

        // COMPATIBILITY, and load-bearing for a test that cannot fail loudly without it. GetRequiredService<FmpBuckets>()
        // resolves to the SAME pair the default registration's handlers draw from, because the registry caches
        // per key. Every_retry_attempt_draws_its_own_token_because_the_retry_sits_outside_the_throttle asserts a
        // cross-handler property through this instance: that the reservoir it resolves is the one the retried
        // attempts drained. Drop this registration and that test would resolve a second, full reservoir and
        // silently assert nothing.
        services.TryAddSingleton(sp => BucketsFor(sp, name));

        // The endpoint groups, resolvable on their own for the default registration. Nothing in the repository or
        // the README resolves one directly, but removing these would be a silent break for a consumer who does.
        // Named registrations do not get them: 25 × N keyed registrations to save `client.Company` is a bad trade.
        services.TryAddTransient<CompanyEndpoints>();
        services.TryAddTransient<DirectoryEndpoints>();
        services.TryAddTransient<StatementEndpoints>();
        services.TryAddTransient<CalendarEndpoints>();
        services.TryAddTransient<AnalystEndpoints>();
        services.TryAddTransient<EconomicsEndpoints>();
        services.TryAddTransient<SearchEndpoints>();
        services.TryAddTransient<SecFilingsEndpoints>();
        services.TryAddTransient<InstitutionalOwnershipEndpoints>();
        services.TryAddTransient<InsiderTradesEndpoints>();
        services.TryAddTransient<CongressEndpoints>();
        services.TryAddTransient<TranscriptsEndpoints>();
        services.TryAddTransient<EsgEndpoints>();
        services.TryAddTransient<CotEndpoints>();
        services.TryAddTransient<QuoteEndpoints>();
        services.TryAddTransient<ChartEndpoints>();
        services.TryAddTransient<BulkEndpoints>();
        services.TryAddTransient<TechnicalIndicatorsEndpoints>();
        services.TryAddTransient<MarketPerformanceEndpoints>();
        services.TryAddTransient<EtfAndFundsEndpoints>();
        services.TryAddTransient<IndexesEndpoints>();
        services.TryAddTransient<MarketHoursEndpoints>();
        services.TryAddTransient<NewsEndpoints>();
        services.TryAddTransient<FundraisersEndpoints>();
        services.TryAddTransient<DiscountedCashFlowEndpoints>();
    }

    /// <summary>This registration's options. For the default registration this is exactly what
    /// <c>IOptions&lt;FmpOptions&gt;.Value</c> returns, validated the same way.</summary>
    private static FmpOptions OptionsFor(IServiceProvider sp, string name) =>
        sp.GetRequiredService<IOptionsMonitor<FmpOptions>>().Get(name);

    /// <summary>This registration's reservoir pair — shared with every other registration on the same API key.</summary>
    private static FmpBuckets BucketsFor(IServiceProvider sp, string name) =>
        sp.GetRequiredService<FmpBucketRegistry>().For(name, OptionsFor(sp, name));

    /// <summary>Everything both clients share.
    ///
    /// <para><c>Timeout.InfiniteTimeSpan</c> is a decision, not an omission. Timeouts belong to
    /// <see cref="FmpTimeoutHandlerBase"/> for two reasons the client-level knob cannot serve: it sits INSIDE the
    /// rate-limit handler, so a wait on the shared token bucket is not charged against the attempt; and it reports
    /// expiry as a <see cref="TimeoutException"/> rather than the <see cref="TaskCanceledException"/> HttpClient
    /// raises, which callers routinely mistake for a shutdown.</para>
    ///
    /// <para>Handler ORDER is contractual: the first added is outermost. The ordinary chain is retry → throttle →
    /// timeout → network and the bulk chain is developer cache → retry → throttle → timeout → network; the
    /// reasons are on each chain above. Swapping throttle and timeout puts the throttle wait back inside the
    /// deadline, which is the coupling the timeout exists to avoid.</para></summary>
    private static IHttpClientBuilder Configure(IHttpClientBuilder builder, string name) =>
        builder.ConfigureHttpClient((sp, client) =>
        {
            var o = OptionsFor(sp, name);
            client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
}
```

- [ ] **Step 5: Reduce the public file to entry points**

Replace the whole of `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Registers the FMP clients. Every overload ends in the same wiring, so the handler order that is
/// contractual exists in exactly one place.</summary>
public static class FmpServiceCollectionExtensions
{
    /// <summary>Name of the <c>HttpClient</c> for ordinary endpoints on the default registration.</summary>
    public const string StandardClient = "fmp";

    /// <summary>Name of the <c>HttpClient</c> for <c>*-bulk</c> endpoints on the default registration, which
    /// carries its own throttle and its own much longer timeout.</summary>
    public const string BulkClient = "fmp-bulk";

    /// <summary>The name of the <c>HttpClient</c> behind a registration's ordinary endpoints:
    /// <see cref="StandardClient"/> for the default registration — a null or empty <paramref name="name"/> — and
    /// <c>"fmp:{name}"</c> for a named one.</summary>
    public static string StandardClientName(string? name) =>
        string.IsNullOrEmpty(name) ? StandardClient : $"{StandardClient}:{name}";

    /// <summary>The name of the <c>HttpClient</c> behind a registration's <c>*-bulk</c> endpoints:
    /// <see cref="BulkClient"/> for the default registration — a null or empty <paramref name="name"/> — and
    /// <c>"fmp-bulk:{name}"</c> for a named one.</summary>
    public static string BulkClientName(string? name) =>
        string.IsNullOrEmpty(name) ? BulkClient : $"{BulkClient}:{name}";

    /// <summary>Binds the <c>Fmp</c> configuration section and registers both clients.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddFmp(o => FmpOptionsBinder.Bind(configuration.GetSection(FmpOptions.SectionName), o));
    }

    /// <summary>Registers both clients against options configured in code. Calling it again for the same
    /// registration re-configures the options and wires nothing twice.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return FmpRegistration.Register(services, Options.DefaultName, configure);
    }
}
```

- [ ] **Step 6: Build, then run the RED test and the whole existing file**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build`
Expected: 24 passed — the 23 existing cases unmodified, and `Calling_AddFmp_twice…` now green with 3 sends. If `Registers_exactly_one_reservoir_pair…`, `Every_retry_attempt_draws_its_own_token…` or `Resolves_the_client_and_every_endpoint_group` fails, the compatibility registration or the unkeyed default registrations in `RegisterDefaultOnly` are wrong; do not touch the test.

- [ ] **Step 7: Add the name-helper theory**

Append to `AddFmpTests.cs`, before the class's closing brace:

```csharp

    [Theory]
    [InlineData(null, "fmp", "fmp-bulk")]
    [InlineData("", "fmp", "fmp-bulk")]
    [InlineData("research", "fmp:research", "fmp-bulk:research")]
    public void Client_names_are_the_constants_for_the_default_registration_and_suffixed_for_a_named_one(
        string? name, string standard, string bulk)
    {
        Assert.Equal(standard, FmpServiceCollectionExtensions.StandardClientName(name));
        Assert.Equal(bulk, FmpServiceCollectionExtensions.BulkClientName(name));
    }
```

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)" && dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build`
Expected: 27 passed.

- [ ] **Step 8: Run the full suite and check the diff shape**

Run: `dotnet test FmpDotNet.slnx --no-build -- RunConfiguration.TreatNoTestsAsError=true`
Expected: `FmpDotNet.Tests` 1,471 passed (the two core tests that build through `AddFmp`, `DirectoryEndpointsTests` and `CompanyScreenerTests`, included); `FmpDotNet.Extensions.DependencyInjection.Tests` 27 passed; `FmpDotNet.SmokeTests` 22 passed, 5 skipped.

Run: `git diff --stat; git diff -- tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs | grep -c '^-[^-]'`
Expected: the last number is `0` — nothing was removed from `AddFmpTests.cs`, only appended.

- [ ] **Step 9: Commit**

```bash
git add src/FmpDotNet.Extensions.DependencyInjection tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs
git commit -F - <<'EOF'
refactor(di): one name-parameterised wiring path behind AddFmp (#65)

FmpRegistration.Register is where the handler chain is spelled out, once,
for a registration name; the two AddFmp overloads call it with the default
name and keep their signatures byte-identical. Named HttpClients with
explicit handler construction replace the typed clients, so each link is
handed its own registration's options and its own registration's reservoir
pair from FmpBucketRegistry; GetRequiredService<FmpBuckets>() keeps
resolving, to the same pair. A second AddFmp for a name re-configures its
options and wires nothing twice — it used to append a second chain, which
a new test measured as nine sends. The binder moves to its own file with
its stale "seven reads" comment corrected (#63).

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 4: `IFmpBuilder` — consumer handlers, outermost

The customization surface: a host puts its own handlers on the two clients, or hands the registration a shared `FmpBucketRegistry`. The builder collects and `FmpRegistration` applies, before adding the SDK's handlers, so consumer handlers land outermost. The counting test is the design's own proof: against a 5xx upstream with three attempts, a consumer handler is entered once while the upstream sees three sends.

**Files:**
- Create: `src/FmpDotNet.Extensions.DependencyInjection/IFmpBuilder.cs`
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpBuilder.cs`
- Modify: `src/FmpDotNet.Extensions.DependencyInjection/FmpRegistration.cs` (a fourth parameter; the builder's callbacks applied; the registry override)
- Modify: `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` (two new overloads, `SectionNameFor`)
- Create: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpBuilderTests.cs`

**Interfaces:**
- Consumes: `FmpRegistration.Register(services, name, configure)` (Task 3); `FmpBucketRegistry` (Task 2).
- Produces: `public interface IFmpBuilder`; `internal static IServiceCollection FmpRegistration.Register(IServiceCollection services, string name, Action<FmpOptions> configure, Action<IFmpBuilder>? configureBuilder)` — the signature Tasks 5, 6 and 7 call; `AddFmp(this IServiceCollection, IConfiguration, Action<IFmpBuilder>, string? sectionName = null)`; `AddFmp(this IServiceCollection, Action<FmpOptions>, Action<IFmpBuilder>)`; `internal static string FmpServiceCollectionExtensions.SectionNameFor(string name, string? sectionName)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpBuilderTests.cs`:

```csharp
using System.Net;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpBuilderTests
{
    /// <summary>Answers every request with the same status, and counts.</summary>
    private sealed class FailingUpstream(HttpStatusCode status) : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain"),
                RequestMessage = req,
            });
        }
    }

    /// <summary>Answers 200 with an empty JSON array, and counts.</summary>
    private sealed class CountingUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = req,
            });
        }
    }

    /// <summary>A consumer's own link: counts how many times it is entered.</summary>
    private sealed class EntryCounter : DelegatingHandler
    {
        public int Entries;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Entries);
            return base.SendAsync(req, ct);
        }
    }

    [Fact]
    public async Task A_consumer_handler_on_the_standard_client_sits_outside_the_retry()
    {
        var upstream = new FailingUpstream(HttpStatusCode.ServiceUnavailable);
        var entries = new EntryCounter();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(
            o => { o.ApiKey = "k"; o.MaxAttempts = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); },
            fmp => fmp.ConfigureStandardClient(b => b
                .AddHttpMessageHandler(() => entries)
                .ConfigurePrimaryHttpMessageHandler(() => upstream)));
        using var provider = services.BuildServiceProvider();

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();

        // Outermost means one entry per logical call with the three attempts beneath it. Inside the retry the
        // counter would read three. No clock, no timing: the numbers differ by construction.
        Assert.Equal(3, upstream.Sends);
        Assert.Equal(1, entries.Entries);
    }

    [Fact]
    public async Task ConfigureBulkClient_reaches_the_bulk_client_only()
    {
        var everywhere = new CountingUpstream();
        var bulkOnly = new CountingUpstream();
        var services = new ServiceCollection().AddLogging();
        // Defaults first: IConfigureOptions run in registration order and the last PrimaryHandler assignment
        // wins, so the per-client override has to be registered after the default to be the one that applies.
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => everywhere));
        services.AddFmp(o => o.ApiKey = "k",
            fmp => fmp.ConfigureBulkClient(b => b.ConfigurePrimaryHttpMessageHandler(() => bulkOnly)));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        (await factory.CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        Assert.Equal(1, everywhere.Sends);
        Assert.Equal(0, bulkOnly.Sends);

        (await factory.CreateClient(FmpServiceCollectionExtensions.BulkClient)
            .GetAsync("stable/profile-bulk?part=0")).Dispose();
        Assert.Equal(1, everywhere.Sends);
        Assert.Equal(1, bulkOnly.Sends);
    }

    [Fact]
    public async Task ConfigureAllClients_reaches_both()
    {
        var upstream = new CountingUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k",
            fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        (await factory.CreateClient(FmpServiceCollectionExtensions.StandardClient)
            .GetAsync("stable/profile")).Dispose();
        (await factory.CreateClient(FmpServiceCollectionExtensions.BulkClient)
            .GetAsync("stable/profile-bulk?part=0")).Dispose();

        Assert.Equal(2, upstream.Sends);
    }

    [Fact]
    public void The_builder_exposes_the_services_and_the_registration_name()
    {
        var services = new ServiceCollection().AddLogging();
        IServiceCollection? seen = null;
        string? name = null;

        services.AddFmp(o => o.ApiKey = "k", fmp => { seen = fmp.Services; name = fmp.Name; });

        Assert.Same(services, seen);
        Assert.Equal("", name);
    }

    [Fact]
    public void A_second_AddFmp_for_the_same_registration_with_a_builder_throws()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k");

        // The SDK's handlers are already in place; nothing added now could land outermost, so silently dropping
        // the callback would be worse than refusing it.
        Assert.Throws<InvalidOperationException>(() => services.AddFmp(o => { }, fmp => { }));
    }

    [Fact]
    public void A_second_AddFmp_for_the_same_registration_reconfigures_its_options()
    {
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(o => o.ApiKey = "k")
            .AddFmp(o => o.PerMinuteCap = 5)
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<FmpOptions>>().Value;

        Assert.Equal("k", options.ApiKey);
        Assert.Equal(5, options.PerMinuteCap);
    }

    [Fact]
    public void UseBucketRegistry_makes_the_container_draw_from_the_given_registry()
    {
        var shared = new FmpBucketRegistry();
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(o => o.ApiKey = "K1", fmp => fmp.UseBucketRegistry(shared))
            .BuildServiceProvider();

        // The compatibility FmpBuckets is the shared registry's pair for this key, and the registry the container
        // resolves is the shared one — so anything else handed the same instance joins the same reservoirs.
        Assert.Same(shared.For("", new FmpOptions { ApiKey = "K1" }), provider.GetRequiredService<FmpBuckets>());
        Assert.Same(shared, provider.GetRequiredService<FmpBucketRegistry>());
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error" | head -3`
Expected: `IFmpBuilder` does not exist; no `AddFmp` overload takes two callbacks. Record the first error line.

- [ ] **Step 3: Write the interface**

Create `src/FmpDotNet.Extensions.DependencyInjection/IFmpBuilder.cs`:

```csharp
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>The customization surface for one FMP registration, handed to the <c>configureBuilder</c> callback
/// of <c>AddFmp</c>.
///
/// <para>The builder collects; it does not proxy. Every callback given here is applied by <c>AddFmp</c> at one
/// defined point, before the SDK adds its own handlers, so nothing depends on the order of statements inside
/// the caller's lambda. Two things follow. <see cref="UseBucketRegistry"/> is known before any rate-limit handler
/// is built. And consumer handlers land <b>outermost</b>: a handler added through
/// <see cref="ConfigureStandardClient"/> sees one entry per logical call, with the SDK's retry, throttle wait and
/// timeout all happening beneath it. That is the right default for the things hosts actually add here — a
/// proxy, a tracing span, a stubbed primary handler in a test — and it means a handler added to observe retries
/// will not see them.</para>
///
/// <para>A registration's callbacks are given on its first <c>AddFmp</c>. A later <c>AddFmp</c> for the same
/// name may re-configure its options; one that passes a builder callback throws, because the SDK's handlers are
/// already in place and nothing added afterwards could land outermost.</para></summary>
public interface IFmpBuilder
{
    /// <summary>The service collection the registration is being added to.</summary>
    IServiceCollection Services { get; }

    /// <summary>The registration's name — <c>""</c> for the default registration.</summary>
    string Name { get; }

    /// <summary>Configures the <c>HttpClient</c> behind the ordinary endpoints. Handlers added here are outermost.
    ///
    /// <para><b>Do not add a second retry policy here.</b> The SDK already retries transient failures on this
    /// client — <see cref="FmpOptions.MaxAttempts"/>, three by default — and a retry added here multiplies with
    /// it: two policies of three attempts each make nine sends per call. A consumer of this SDK measured exactly
    /// that with <c>AddStandardResilienceHandler</c>, whose circuit breaker then cascaded a handful of 429s into
    /// thousands of skipped symbols. Tune the SDK's retry through <see cref="FmpOptions"/> instead.</para></summary>
    IFmpBuilder ConfigureStandardClient(Action<IHttpClientBuilder> configure);

    /// <summary>Configures the <c>HttpClient</c> behind the <c>*-bulk</c> endpoints. Handlers added here are
    /// outermost — outside the developer cache too, so a handler here observes cache hits. The warning on
    /// <see cref="ConfigureStandardClient"/> about stacking a second retry applies here as well.</summary>
    IFmpBuilder ConfigureBulkClient(Action<IHttpClientBuilder> configure);

    /// <summary>Configures both clients: the same as <see cref="ConfigureStandardClient"/> and
    /// <see cref="ConfigureBulkClient"/> with the same callback.</summary>
    IFmpBuilder ConfigureAllClients(Action<IHttpClientBuilder> configure);

    /// <summary>Draws this registration's reservoirs from <paramref name="registry"/> rather than from the
    /// container's own, which is how a container and a factory-built client on the same API key share a pair
    /// instead of emitting at twice the cap. The registry also becomes the container's, unless the container
    /// already has one.</summary>
    IFmpBuilder UseBucketRegistry(FmpBucketRegistry registry);
}
```

- [ ] **Step 4: Write the collecting implementation**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpBuilder.cs`:

```csharp
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Collects a registration's customizations for <see cref="FmpRegistration"/> to apply at one defined
/// point. Nothing here touches the service collection; that is the point of collecting rather than proxying.</summary>
internal sealed class FmpBuilder(IServiceCollection services, string name) : IFmpBuilder
{
    public IServiceCollection Services { get; } = services;
    public string Name { get; } = name;

    internal List<Action<IHttpClientBuilder>> Standard { get; } = [];
    internal List<Action<IHttpClientBuilder>> Bulk { get; } = [];
    internal FmpBucketRegistry? Registry { get; private set; }

    public IFmpBuilder ConfigureStandardClient(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Standard.Add(configure);
        return this;
    }

    public IFmpBuilder ConfigureBulkClient(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Bulk.Add(configure);
        return this;
    }

    public IFmpBuilder ConfigureAllClients(Action<IHttpClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Standard.Add(configure);
        Bulk.Add(configure);
        return this;
    }

    public IFmpBuilder UseBucketRegistry(FmpBucketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        return this;
    }
}
```

- [ ] **Step 5: Apply the builder in `FmpRegistration`**

In `src/FmpDotNet.Extensions.DependencyInjection/FmpRegistration.cs`, make these changes:

1. The signature becomes
   `internal static IServiceCollection Register(IServiceCollection services, string name, Action<FmpOptions> configure, Action<IFmpBuilder>? configureBuilder)`.

2. Replace the two lines that check `Wired` and return with:

```csharp
        if (services.Any(d => d.IsKeyedService && d.ServiceType == typeof(Wired) && Equals(d.ServiceKey, name)))
        {
            if (configureBuilder is not null)
                throw new InvalidOperationException(
                    $"AddFmp: the {(name.Length == 0 ? "default" : $"\"{name}\"")} FMP registration is already wired. "
                    + "Builder callbacks apply on the first AddFmp for a name; a later call can only re-configure its options.");
            return services;
        }
        services.AddKeyedSingleton(name, new Wired());

        var builder = new FmpBuilder(services, name);
        configureBuilder?.Invoke(builder);
        // A shared registry, if one was given, is captured by the handler lambdas directly — it must be known
        // before any rate-limit handler is built, and a service lookup could find a different one.
        var registry = builder.Registry;
        if (registry is not null) services.TryAddSingleton(registry);
```

3. Replace the standard-chain statement (`Configure(services.AddHttpClient(FmpServiceCollectionExtensions.StandardClientName(name)), name).AddHttpMessageHandler(…)…`) so the consumer callbacks run first — the leading comment gains its first sentence:

```csharp
        // Consumer handlers are applied BEFORE the SDK's, so they are outermost: they see one entry per logical
        // call, and the retry, the throttle wait and the timeout all happen beneath them. IFmpBuilder says why.
        // The retry is added FIRST among the SDK's own, which makes it the OUTERMOST of those, and that is the
        // point rather than a detail. FmpRateLimitHandlerBase acquires its token BEFORE delegating, so a retry
        // placed inside it would be reached after the single token had already been drawn and every attempt
        // after the first would bypass the reservoir entirely. Outside, each attempt re-acquires — and it is
        // still outside the timeout, so each attempt gets a fresh RequestTimeout rather than sharing one budget.
        // Explicit construction rather than AddHttpMessageHandler<T>: each link gets THIS registration's options,
        // and the throttle gets this registration's reservoir from the registry. Nothing is activated by reflection.
        var standard = Configure(services.AddHttpClient(FmpServiceCollectionExtensions.StandardClientName(name)), name);
        foreach (var customize in builder.Standard) customize(standard);
        standard
            .AddHttpMessageHandler(sp => new FmpRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name, registry), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpTimeoutHandler(Options.Create(OptionsFor(sp, name))));
```

4. The same for the bulk chain — its comment gains the sentence "Consumer handlers sit outside the developer cache too, so a tracing handler observes cache hits, and outside the retry, so a replay is still never retried." and the statement becomes:

```csharp
        var bulk = Configure(services.AddHttpClient(FmpServiceCollectionExtensions.BulkClientName(name)), name);
        foreach (var customize in builder.Bulk) customize(bulk);
        bulk
            .AddHttpMessageHandler(sp => new FmpDeveloperBulkCacheHandler(
                Options.Create(OptionsFor(sp, name)), sp.GetRequiredService<ILogger<FmpDeveloperBulkCacheHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRetryHandler(
                sp.GetRequiredService<IClock>(), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRetryHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkRateLimitHandler(
                sp.GetRequiredService<IClock>(), BucketsFor(sp, name, registry), Options.Create(OptionsFor(sp, name)),
                sp.GetRequiredService<ILogger<FmpBulkRateLimitHandler>>()))
            .AddHttpMessageHandler(sp => new FmpBulkTimeoutHandler(Options.Create(OptionsFor(sp, name))));
```

5. `if (name.Length == 0) RegisterDefaultOnly(services);` becomes `if (name.Length == 0) RegisterDefaultOnly(services, registry);`, the method's signature becomes `private static void RegisterDefaultOnly(IServiceCollection services, FmpBucketRegistry? registry)`, and its compatibility line becomes `services.TryAddSingleton(sp => BucketsFor(sp, name, registry));`.

6. `BucketsFor` takes the override:

```csharp
    /// <summary>This registration's reservoir pair — shared with every other registration on the same API key,
    /// from the registry the registration was given or, failing that, the container's.</summary>
    private static FmpBuckets BucketsFor(IServiceProvider sp, string name, FmpBucketRegistry? registry) =>
        (registry ?? sp.GetRequiredService<FmpBucketRegistry>()).For(name, OptionsFor(sp, name));
```

- [ ] **Step 6: Add the two overloads**

In `FmpServiceCollectionExtensions.cs`, change the existing `Action<FmpOptions>` overload's last line to `return FmpRegistration.Register(services, Options.DefaultName, configure, null);` and add, after it:

```csharp

    /// <summary>Binds a configuration section and registers both clients, with a customization callback — see
    /// <see cref="IFmpBuilder"/>. <paramref name="sectionName"/> defaults to <c>"Fmp"</c>.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, IConfiguration configuration,
        Action<IFmpBuilder> configureBuilder, string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        var section = SectionNameFor(Options.DefaultName, sectionName);
        return FmpRegistration.Register(services, Options.DefaultName,
            o => FmpOptionsBinder.Bind(configuration.GetSection(section), o), configureBuilder);
    }

    /// <summary>Registers both clients against options configured in code, with a customization callback — see
    /// <see cref="IFmpBuilder"/>.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, Action<FmpOptions> configure,
        Action<IFmpBuilder> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        return FmpRegistration.Register(services, Options.DefaultName, configure, configureBuilder);
    }

    /// <summary>The configuration section a registration binds by default: <c>"Fmp"</c> for the default
    /// registration and <c>"Fmp:{name}"</c> for a named one, unless <paramref name="sectionName"/> overrides it.</summary>
    internal static string SectionNameFor(string name, string? sectionName) =>
        sectionName ?? (name.Length == 0 ? FmpOptions.SectionName : $"{FmpOptions.SectionName}:{name}");
```

- [ ] **Step 7: Build and run the new tests, then the whole extensions project**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build --filter "FullyQualifiedName~FmpBuilderTests"`
Expected: 7 passed. If `A_consumer_handler_on_the_standard_client_sits_outside_the_retry` reports `entries.Entries` of 3, the callbacks were applied after the SDK's handlers — the `foreach` must precede the first `AddHttpMessageHandler`.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build`
Expected: 34 passed.

- [ ] **Step 8: Commit**

```bash
git add src/FmpDotNet.Extensions.DependencyInjection tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpBuilderTests.cs
git commit -F - <<'EOF'
feat(di): IFmpBuilder puts consumer handlers outermost and shares reservoirs (#65)

AddFmp gains an optional customization callback, as new overloads rather
than a changed return type. The builder collects and the registration
applies, before the SDK's own handlers, so a consumer handler sees one
entry per logical call with retry, throttle and timeout beneath it — a
counting test proves it against a 5xx upstream. UseBucketRegistry hands a
registration a shared FmpBucketRegistry, which is how a container and a
side client on one key stop emitting at twice the cap. A second AddFmp for
a wired name that passes a builder throws rather than dropping it.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 5: Named registrations

One process, more than one FMP configuration. A named registration is the same wiring under a name: named options validated under it, `HttpClient`s `"fmp:{name}"` and `"fmp-bulk:{name}"`, and keyed `FmpTransport`, `FmpBulkTransport` and `FmpClient` resolved with `[FromKeyedServices(name)]`. Task 3 already wires by name; this task adds the two overloads that pass one in, the section-name convention, and the tests.

**Files:**
- Modify: `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` (two overloads)
- Append: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs`

**Interfaces:**
- Consumes: `FmpRegistration.Register(services, name, configure, configureBuilder)` (Task 4); `SectionNameFor(name, sectionName)` (Task 4).
- Produces: `AddFmp(this IServiceCollection, string name, IConfiguration configuration, Action<IFmpBuilder>? configureBuilder = null, string? sectionName = null)` and `AddFmp(this IServiceCollection, string name, Action<FmpOptions> configure, Action<IFmpBuilder>? configureBuilder = null)`. Task 7's host sugar calls both. An empty `name` is the default registration.

- [ ] **Step 1: Write the failing tests**

Append to `AddFmpTests.cs`, before the class's closing brace. `FailingHandler` is the private class already in the file; `FmpDotNet.Http`, `Microsoft.Extensions.Options` and `NodaTime` are already imported there.

```csharp

    [Fact]
    public void Named_registrations_resolve_keyed_and_distinct_and_create_no_default()
    {
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp("a", o => o.ApiKey = "K1")
            .AddFmp("b", o => o.ApiKey = "K2")
            .BuildServiceProvider();

        Assert.NotSame(provider.GetRequiredKeyedService<FmpClient>("a"), provider.GetRequiredKeyedService<FmpClient>("b"));
        Assert.NotNull(provider.GetRequiredKeyedService<FmpTransport>("a"));
        Assert.NotNull(provider.GetRequiredKeyedService<FmpBulkTransport>("b"));

        // Named registrations alone do not conjure a default one.
        Assert.Null(provider.GetService<FmpClient>());
        Assert.Null(provider.GetService<FmpTransport>());
    }

    [Fact]
    public async Task Named_registrations_sharing_a_key_draw_from_one_reservoir_pair()
    {
        // Modelled on Every_retry_attempt_draws_its_own_token…: a capacity-3 reservoir emptied through "a" is
        // empty for "b" too, because FMP meters per key and both hold the same one. "c" holds another key and
        // is untouched.
        var upstream = new FailingHandler(System.Net.HttpStatusCode.ServiceUnavailable);
        var services = new ServiceCollection().AddLogging();
        services.AddFmp("a", o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); });
        services.AddFmp("b", o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); });
        services.AddFmp("c", o => { o.ApiKey = "K2"; o.PerMinuteCap = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); });
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        using var provider = services.BuildServiceProvider();

        (await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(FmpServiceCollectionExtensions.StandardClientName("a"))
            .GetAsync("stable/profile")).Dispose();
        Assert.Equal(3, upstream.Sends);

        var registry = provider.GetRequiredService<FmpBucketRegistry>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<FmpOptions>>();
        var now = SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
        Assert.True(registry.For("b", monitor.Get("b")).Standard.Acquire(now) > Duration.Zero,
            "\"b\" shares \"a\"'s key and should have found its reservoir drained");
        Assert.Equal(Duration.Zero, registry.For("c", monitor.Get("c")).Standard.Acquire(now));
    }

    [Fact]
    public void A_named_registrations_options_validate_under_its_own_name()
    {
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp("research", o => { o.ApiKey = "k"; o.PerMinuteCap = 0; })
            .BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<FmpOptions>>();

        var failure = Assert.Throws<OptionsValidationException>(() => monitor.Get("research"));
        Assert.Equal("research", failure.OptionsName);

        // Nothing was registered under the default name, so its options are the defaults, and valid.
        Assert.Equal(660, provider.GetRequiredService<IOptions<FmpOptions>>().Value.PerMinuteCap);
    }

    [Fact]
    public void The_unkeyed_transports_still_resolve_beside_a_named_registration()
    {
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp(o => o.ApiKey = "k")
            .AddFmp("research", o => o.ApiKey = "r")
            .BuildServiceProvider();

        // The README's "Reaching an endpoint that is not modelled" section shows a consumer resolving the default
        // transports by type, and a named registration beside them must not make that keyed-only.
        Assert.NotNull(provider.GetRequiredService<FmpTransport>());
        Assert.NotNull(provider.GetRequiredService<FmpBulkTransport>());
        Assert.NotNull(provider.GetRequiredKeyedService<FmpTransport>("research"));
    }

    [Fact]
    public void A_named_registration_binds_Fmp_colon_name_unless_a_section_is_given()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fmp:ApiKey"] = "default-key",
            ["Fmp:research:ApiKey"] = "research-key",
            ["Vendors:Fmp:ApiKey"] = "vendor-key",
        }).Build();
        using var provider = new ServiceCollection().AddLogging()
            .AddFmp("research", configuration)
            .AddFmp("vendor", configuration, sectionName: "Vendors:Fmp")
            .BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<FmpOptions>>();

        Assert.Equal("research-key", monitor.Get("research").ApiKey);
        Assert.Equal("vendor-key", monitor.Get("vendor").ApiKey);
    }
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error" | head -3`
Expected: no `AddFmp` overload takes a `string` first. Record the first error line.

- [ ] **Step 3: Add the overloads**

In `FmpServiceCollectionExtensions.cs`, after the `AddFmp(…, Action<FmpOptions> configure, Action<IFmpBuilder> configureBuilder)` overload and before `SectionNameFor`, add:

```csharp

    /// <summary>Registers a <b>named</b> FMP configuration bound from configuration, so one process can hold more
    /// than one — a second API key, a second tier. Resolve it with <c>[FromKeyedServices(name)]</c>: the
    /// <see cref="FmpClient"/>, <see cref="FmpTransport"/> and <see cref="FmpBulkTransport"/> are keyed by
    /// <paramref name="name"/>, its options validate under that name, and its <c>HttpClient</c>s are
    /// <see cref="StandardClientName"/> and <see cref="BulkClientName"/> of it. Binds <c>"Fmp:{name}"</c> unless
    /// <paramref name="sectionName"/> says otherwise. Registrations sharing an API key share a reservoir pair —
    /// see <see cref="FmpDotNet.Http.FmpBucketRegistry"/>. An empty name is the default registration.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, string name, IConfiguration configuration,
        Action<IFmpBuilder>? configureBuilder = null, string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = SectionNameFor(name, sectionName);
        return FmpRegistration.Register(services, name,
            o => FmpOptionsBinder.Bind(configuration.GetSection(section), o), configureBuilder);
    }

    /// <summary>Registers a <b>named</b> FMP configuration from code. Everything on the configuration-bound
    /// overload applies.</summary>
    public static IServiceCollection AddFmp(this IServiceCollection services, string name, Action<FmpOptions> configure,
        Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);
        return FmpRegistration.Register(services, name, configure, configureBuilder);
    }
```

- [ ] **Step 4: Build and test**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`. Overload resolution is unambiguous: the first parameter is `IConfiguration`, `Action<FmpOptions>` or `string`, and the two callback types are distinct.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build`
Expected: 39 passed.

Run: `git diff -- tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs | grep -c '^-[^-]'`
Expected: `0`.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs
git commit -F - <<'EOF'
feat(di): named registrations — one process, more than one FMP configuration (#65)

AddFmp(name, …) registers keyed FmpTransport, FmpBulkTransport and
FmpClient under the name, validates its options under the name, and binds
"Fmp:{name}" unless a section is given. Registrations sharing an API key
share a reservoir pair through the registry; the default registration
keeps the unkeyed transports the README's escape hatch resolves.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 6: `FmpClientFactory` — the container-free path

For a host with no `IServiceCollection` at all. `Create` builds a private container through `AddFmp` and the client owns it, so the contractual handler order is never hand-wired a second time. It lives in the extensions package because the core cannot call `AddFmp`; it uses the public three-argument `FmpClient` constructor from Task 1.

**Files:**
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpClientFactory.cs`
- Create: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpClientFactoryTests.cs`

**Interfaces:**
- Consumes: `FmpClient(FmpTransport, FmpBulkTransport, IDisposable?)` (Task 1); `AddFmp(Action<FmpOptions>, Action<IFmpBuilder>)` (Task 4); `IFmpBuilder.UseBucketRegistry` (Task 4); `FmpServiceCollectionExtensions.StandardClient`/`BulkClient`.
- Produces: `public static class FmpClientFactory` with `Create(string apiKey)` and `Create(Action<FmpOptions> configure, ILoggerFactory? loggerFactory = null, FmpBucketRegistry? registry = null, Action<IFmpBuilder>? configureBuilder = null)`. The README (Task 8) documents both.

- [ ] **Step 1: Write the failing tests**

Create `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpClientFactoryTests.cs`:

```csharp
using System.Net;
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpClientFactoryTests
{
    /// <summary>Answers stable/available-sectors the way FMP does — one-property objects under "sector" — and
    /// counts.</summary>
    private sealed class SectorsUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"sector\":\"Technology\"}]", System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = req,
            });
        }
    }

    /// <summary>Answers 503 every time, and counts.</summary>
    private sealed class FailingUpstream : HttpMessageHandler
    {
        public int Sends;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain"),
                RequestMessage = req,
            });
        }
    }

    [Fact]
    public async Task Create_yields_a_client_that_answers_through_the_chain_AddFmp_wires()
    {
        var upstream = new SectorsUpstream();
        using var client = FmpClientFactory.Create(o => o.ApiKey = "k",
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));

        var sectors = await client.Directory.GetSectorsAsync();

        Assert.Equal(new[] { "Technology" }, sectors);
        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task Dispose_disposes_what_the_client_owns_and_it_refuses_to_send_afterwards()
    {
        var upstream = new SectorsUpstream();
        var client = FmpClientFactory.Create(o => o.ApiKey = "k",
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));
        Assert.NotEmpty(await client.Directory.GetSectorsAsync());

        client.Dispose();
        client.Dispose();                                                   // safe to call twice

        // The private container and the two HttpClients are gone. A disposed HttpClient throws
        // ObjectDisposedException; if the transport wraps it, the cause is still that exception.
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.Directory.GetSectorsAsync());
        Assert.True(failure is ObjectDisposedException || failure.InnerException is ObjectDisposedException,
            $"expected ObjectDisposedException, got {failure.GetType().Name}: {failure.Message}");
        Assert.Equal(1, upstream.Sends);
    }

    [Fact]
    public async Task Dispose_on_a_container_resolved_client_is_a_no_op()
    {
        var upstream = new SectorsUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => o.ApiKey = "k");
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream));
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<FmpClient>();

        client.Dispose();

        // The container owns the transports, not the client: it stays usable, and so does the next resolve.
        Assert.NotEmpty(await client.Directory.GetSectorsAsync());
        Assert.NotEmpty(await provider.GetRequiredService<FmpClient>().Directory.GetSectorsAsync());
    }

    [Fact]
    public void Create_with_only_an_api_key_and_no_logger_factory_does_not_throw()
    {
        using var client = FmpClientFactory.Create("k");

        Assert.NotNull(client.Company);
    }

    [Fact]
    public void Create_validates_the_options_before_returning()
    {
        // The host path validates on start. The factory validates on Create, so a bad BaseUrl is an exception
        // here rather than a UriFormatException on the first request.
        Assert.Throws<OptionsValidationException>(() =>
            FmpClientFactory.Create(o => { o.ApiKey = "k"; o.BaseUrl = "not a uri"; }));
    }

    [Fact]
    public async Task A_container_and_a_factory_built_client_handed_one_registry_share_a_reservoir_pair()
    {
        var shared = new FmpBucketRegistry();
        var upstream = new FailingUpstream();
        var services = new ServiceCollection().AddLogging();
        services.AddFmp(o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; }, fmp => fmp.UseBucketRegistry(shared));
        using var provider = services.BuildServiceProvider();
        using var side = FmpClientFactory.Create(
            o => { o.ApiKey = "K1"; o.PerMinuteCap = 3; o.RetryBaseDelay = Duration.FromMilliseconds(1); },
            registry: shared,
            configureBuilder: fmp => fmp.ConfigureAllClients(b => b.ConfigurePrimaryHttpMessageHandler(() => upstream)));

        // Three failing attempts through the side client empty the capacity-3 reservoir…
        await Assert.ThrowsAnyAsync<Exception>(() => side.Directory.GetSectorsAsync());
        Assert.Equal(3, upstream.Sends);

        // …and the container, which never sent anything, finds its reservoir empty too. Without the shared
        // registry the two would emit at twice the cap.
        var now = SystemClock.Instance.GetCurrentInstant().ToUnixTimeTicks() / (double)NodaConstants.TicksPerSecond;
        Assert.True(provider.GetRequiredService<FmpBuckets>().Standard.Acquire(now) > Duration.Zero,
            "the container's reservoir still had tokens — the side client drew from a different pair");
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error" | head -3`
Expected: `FmpClientFactory` does not exist. Record the first error line.

- [ ] **Step 3: Write the factory**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpClientFactory.cs`:

```csharp
using FmpDotNet.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>Builds an <see cref="FmpClient"/> for a host that has no <c>IServiceCollection</c> at all.
///
/// <para>One wiring path. <see cref="Create(Action{FmpOptions}, ILoggerFactory, FmpBucketRegistry, Action{IFmpBuilder})"/>
/// builds a private container through <c>AddFmp</c> and the client owns it, so the handler chain — whose order is
/// contractual and whose mistakes fail silently — is never hand-wired a second time. It costs no new dependency:
/// the concrete container ships with this package through <c>Microsoft.Extensions.Http</c>. The cost is a container
/// the caller did not ask for and a few milliseconds at construction.</para>
///
/// <para><b>Logging defaults to none.</b> Without an <see cref="ILoggerFactory"/> the clamped-<c>Retry-After</c>
/// warning and the cap-conflict warning go nowhere, and a silent throttle is exactly the thing someone debugging
/// a slow run needs to see. Pass the host's factory.</para>
///
/// <para>Reads no environment variable. A host can pass its key in one line; a library that silently picks up
/// ambient credentials is worse than one that does not.</para></summary>
public static class FmpClientFactory
{
    /// <summary>A client for one API key, every other option at its default, and no logging.</summary>
    public static FmpClient Create(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        return Create(o => o.ApiKey = apiKey);
    }

    /// <summary>A client built from <paramref name="configure"/>, validated now rather than on its first call.
    /// Dispose it: it owns the container behind it.</summary>
    /// <param name="configure">Configures the options, exactly as <c>AddFmp</c> would.</param>
    /// <param name="loggerFactory">Where the SDK's warnings go. None by default.</param>
    /// <param name="registry">A registry to share reservoirs through — a container's, via
    /// <see cref="IFmpBuilder.UseBucketRegistry"/> — so a host and this client on the same key emit at the cap
    /// rather than at twice it.</param>
    /// <param name="configureBuilder">The customization surface <c>AddFmp</c> offers: a proxy, a tracing handler,
    /// a stubbed primary handler in a test.</param>
    public static FmpClient Create(Action<FmpOptions> configure, ILoggerFactory? loggerFactory = null,
        FmpBucketRegistry? registry = null, Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var services = new ServiceCollection();
        // Registered before AddLogging, whose TryAdd then keeps the caller's factory. An instance the container
        // did not create is not disposed by it, which is right: the factory is the host's.
        if (loggerFactory is not null) services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddFmp(configure, fmp =>
        {
            if (registry is not null) fmp.UseBucketRegistry(registry);
            configureBuilder?.Invoke(fmp);
        });

        var provider = services.BuildServiceProvider();
        try
        {
            // The host path validates on start. A factory-built client that threw a configuration error on its
            // first request instead would be the worse of the two, so validate here, before anything is built.
            var options = provider.GetRequiredService<IOptions<FmpOptions>>();
            _ = options.Value;

            // The HttpClients are created here rather than left inside resolved transports so that Dispose can
            // dispose them. A disposed client should refuse to send, and disposing the container alone would
            // leave the factory's pooled handlers — and with them the transports — working.
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var standard = factory.CreateClient(FmpServiceCollectionExtensions.StandardClient);
            var bulk = factory.CreateClient(FmpServiceCollectionExtensions.BulkClient);
            return new FmpClient(new FmpTransport(standard, options), new FmpBulkTransport(bulk, options),
                new Owned(provider, standard, bulk));
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>What a factory-built client owns: its two <c>HttpClient</c>s and the container behind them.</summary>
    private sealed class Owned(ServiceProvider provider, HttpClient standard, HttpClient bulk) : IDisposable
    {
        public void Dispose()
        {
            standard.Dispose();
            bulk.Dispose();
            provider.Dispose();
        }
    }
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`. If the build reports `IL2026`/`IL3050`, something reflective crept in — every registration in this file is a factory lambda or an instance.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build --filter "FullyQualifiedName~FmpClientFactoryTests"`
Expected: 6 passed. If `Dispose_disposes_what_the_client_owns…` fails because the failure is neither an `ObjectDisposedException` nor wraps one, report the exception type and message; do not loosen the assertion.

Run: `dotnet test tests/FmpDotNet.Extensions.DependencyInjection.Tests --no-build`
Expected: 45 passed.

- [ ] **Step 5: Commit**

```bash
git add src/FmpDotNet.Extensions.DependencyInjection/FmpClientFactory.cs tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpClientFactoryTests.cs
git commit -F - <<'EOF'
feat(di): FmpClientFactory.Create builds a client with no host container (#65)

A private container through AddFmp, owned by the client, so the one wiring
path stays the only one. Validates on Create rather than on the first
request. Dispose disposes the container and the two HttpClients, so a
disposed client refuses to send; on a container-resolved client it is a
no-op. Takes a shared FmpBucketRegistry so a host and a side client on one
key emit at the cap rather than twice it. Logging is none unless a factory
is passed, and no environment variable is read.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 7: Host-builder sugar, and the hosting abstraction on the right package

`builder.AddFmp()` on an `IHostApplicationBuilder`, delegating to the `IServiceCollection` overloads with the builder's configuration. This is the design's only new package dependency, `Microsoft.Extensions.Hosting.Abstractions`, and it goes on the extensions csproj; the same commit adds it to the core's boundary test so it can never go on the core. The csproj rewrite also closes the #63 items about its comments.

**Files:**
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpHostApplicationBuilderExtensions.cs`
- Rewrite: `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj`
- Modify: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj`
- Modify: `tests/FmpDotNet.Tests/PackageBoundaryTests.cs:18-24`
- Create: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpHostBuilderTests.cs`

**Interfaces:**
- Consumes: `AddFmp(string name, IConfiguration, Action<IFmpBuilder>?, string?)` and `AddFmp(string name, Action<FmpOptions>, Action<IFmpBuilder>?)` (Task 5).
- Produces: `AddFmp(this IHostApplicationBuilder builder, string? name = null, string? sectionName = null, Action<IFmpBuilder>? configure = null)` and `AddFmp(this IHostApplicationBuilder builder, Action<FmpOptions> configure, string? name = null, Action<IFmpBuilder>? configureBuilder = null)`, both returning the builder.

- [ ] **Step 1: Extend the boundary test and write the failing host tests**

In `tests/FmpDotNet.Tests/PackageBoundaryTests.cs`, add one row to the negative theory so lines 18-24 read:

```csharp
    [Theory]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
    [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
    [InlineData("Microsoft.Extensions.Options.ConfigurationExtensions")]
    [InlineData("Microsoft.Extensions.Hosting.Abstractions")]
    public void The_core_does_not_reference(string assembly) =>
        Assert.DoesNotContain(assembly, CoreReferences);
```

and extend the class's summary with one sentence at the end of the first paragraph: `Hosting.Abstractions is on the list since #65, which put the host-builder sugar in the extensions package; this is what keeps it there.`

Create `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpHostBuilderTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Extensions.DependencyInjection.Tests;

public class FmpHostBuilderTests
{
    private static HostApplicationBuilder Builder(params (string Key, string Value)[] settings)
    {
        // The full builder rather than the empty one, so logging and the rest of the defaults are present the way
        // they are in a real host. The in-memory values are added last and win.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        return builder;
    }

    [Fact]
    public void AddFmp_binds_the_Fmp_section_from_the_hosts_configuration()
    {
        var builder = Builder(("Fmp:ApiKey", "host-key"));
        builder.AddFmp();
        using var host = builder.Build();

        Assert.Equal("host-key", host.Services.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
        Assert.NotNull(host.Services.GetRequiredService<FmpClient>());
    }

    [Fact]
    public void AddFmp_with_a_name_binds_Fmp_colon_name()
    {
        var builder = Builder(("Fmp:research:ApiKey", "research-key"));
        builder.AddFmp("research");
        using var host = builder.Build();

        Assert.Equal("research-key",
            host.Services.GetRequiredService<IOptionsMonitor<FmpOptions>>().Get("research").ApiKey);
        Assert.NotNull(host.Services.GetRequiredKeyedService<FmpClient>("research"));
    }

    [Fact]
    public void AddFmp_with_a_configure_delegate_takes_the_options_from_code()
    {
        var builder = Builder();
        string? seen = null;
        builder.AddFmp(o => o.ApiKey = "code-key", configureBuilder: fmp => seen = fmp.Name);
        using var host = builder.Build();

        Assert.Equal("code-key", host.Services.GetRequiredService<IOptions<FmpOptions>>().Value.ApiKey);
        Assert.Equal("", seen);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet build FmpDotNet.slnx 2>&1 | grep -E "error" | head -3`
Expected: `Host`/`IHostApplicationBuilder` unknown in the test project (no hosting package yet) and no `AddFmp` on a host builder. The boundary row is green already — the core has never referenced hosting — and its job is the commit that would change that.

- [ ] **Step 3: Put the hosting abstraction on the extensions csproj, and fix its comments (#63)**

Replace the whole of `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    The container wiring for FmpDotNet, as its own package (#61).

    The core's transports and handlers take IOptions<FmpOptions> and ILogger<T> and nothing more, so the core
    depends on the options package, the logging abstractions and NodaTime. Everything that touches
    IServiceCollection, IConfiguration, AddHttpClient or IHostApplicationBuilder is here, so that a consumer
    with a container of their own can reference FmpDotNet alone — and so that the hosting abstraction lands here
    rather than on the core (#65).

    Version, authors, repository, licence, README, symbols, Source Link and the documentation and AOT switches
    come from ../Directory.Build.props, shared with FmpDotNet so the pair cannot drift apart.
  -->
  <PropertyGroup>
    <RootNamespace>FmpDotNet.Extensions.DependencyInjection</RootNamespace>
    <AssemblyName>FmpDotNet.Extensions.DependencyInjection</AssemblyName>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>FmpDotNet.Extensions.DependencyInjection</PackageId>
    <Description>Registers FmpDotNet into Microsoft.Extensions.DependencyInjection: AddFmp with named registrations and a customization surface, IHostApplicationBuilder sugar, a container-free FmpClientFactory, options binding and validation, and the two clients with their throttle, retry and timeout chains.</Description>
    <PackageTags>fmp;financialmodelingprep;dependency-injection;hosting;market-data</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <!-- Packs as a dependency on FmpDotNet at the version this project was built with — the same VersionPrefix
         and VersionSuffix, so a consumer who adds this package gets the matching core. -->
    <ProjectReference Include="..\FmpDotNet\FmpDotNet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- The rule for this list: name what the code compiles against and Http does not already imply.
         Configuration.Abstractions is IConfiguration, GetSection and the indexer, for the by-name binding.
         Hosting.Abstractions is IHostApplicationBuilder, for the host sugar; a hosting abstraction belongs in
         the integration package and never on the core, and PackageBoundaryTests holds the core to that.
         Http brings the rest: DependencyInjection.Abstractions (IServiceCollection, keyed services, TryAdd*),
         Options (AddOptions/Configure/Validate/ValidateOnStart — the concrete Options package; there is no
         Options.Abstractions), Logging, and the concrete container FmpClientFactory builds on.
         NOT Options.ConfigurationExtensions: ConfigurationBinder is neither trim- nor AOT-safe and is never called. -->
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
  </ItemGroup>

</Project>
```

In `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj`, replace the first `<ItemGroup>` (lines 11-19) with:

```xml
  <ItemGroup>
    <!-- The concrete configuration package: ConfigurationBuilder and AddInMemoryCollection, which every test here
         uses to drive the by-name binding. Nothing under src/ references it, by design. -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.9" />
    <!-- The concrete container and the concrete host, referenced directly because the tests use them directly:
         ServiceCollection and BuildServiceProvider throughout, Host.CreateApplicationBuilder in the host-builder
         tests. Both would arrive transitively; naming them says what the tests are about. -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NodaTime" Version="3.2.2" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
```

Also update the test csproj's leading comment (lines 3-6) so its first sentence reads: `Tests for the DI package (#61, #65): registration, options binding and validation, handler order and reservoir sharing, the builder, the factory and the host sugar — every one a property of this package.`

- [ ] **Step 4: Write the host sugar**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpHostApplicationBuilderExtensions.cs`:

```csharp
using Microsoft.Extensions.Hosting;

namespace FmpDotNet.Extensions.DependencyInjection;

/// <summary>One-line registration on an <see cref="IHostApplicationBuilder"/> — ASP.NET Core, a Worker Service,
/// or a console app built on <c>Host.CreateApplicationBuilder</c>. Delegates to the <c>IServiceCollection</c>
/// overloads with the builder's configuration; nothing is wired here.</summary>
public static class FmpHostApplicationBuilderExtensions
{
    /// <summary>Registers FMP from the builder's configuration: the <c>"Fmp"</c> section for the default
    /// registration, <c>"Fmp:{name}"</c> for a named one, or <paramref name="sectionName"/> if given. See
    /// <see cref="FmpServiceCollectionExtensions.AddFmp(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Microsoft.Extensions.Configuration.IConfiguration, Action{IFmpBuilder}, string)"/>.</summary>
    public static IHostApplicationBuilder AddFmp(this IHostApplicationBuilder builder, string? name = null,
        string? sectionName = null, Action<IFmpBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddFmp(name ?? "", builder.Configuration, configure, sectionName);
        return builder;
    }

    /// <summary>Registers FMP against options configured in code, under <paramref name="name"/> if given.</summary>
    public static IHostApplicationBuilder AddFmp(this IHostApplicationBuilder builder, Action<FmpOptions> configure,
        string? name = null, Action<IFmpBuilder>? configureBuilder = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.Services.AddFmp(name ?? "", configure, configureBuilder);
        return builder;
    }
}
```

- [ ] **Step 5: Build and test**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)"`
Expected: `0 Warning(s)`, `0 Error(s)`. A `CS1574` on the long `cref` means the parameter list does not match Task 5's overload exactly; fix the cref, not the overload.

Run: `dotnet test FmpDotNet.slnx --no-build -- RunConfiguration.TreatNoTestsAsError=true`
Expected: `FmpDotNet.Tests` 1,472 passed (boundary theory now 8 cases); `FmpDotNet.Extensions.DependencyInjection.Tests` 48 passed; `FmpDotNet.SmokeTests` 22 passed, 5 skipped.

- [ ] **Step 6: Commit**

```bash
git add src/FmpDotNet.Extensions.DependencyInjection tests/FmpDotNet.Extensions.DependencyInjection.Tests tests/FmpDotNet.Tests/PackageBoundaryTests.cs
git commit -F - <<'EOF'
feat(di): IHostApplicationBuilder.AddFmp, with Hosting.Abstractions on the extensions package (#65)

builder.AddFmp() binds "Fmp" off the host's configuration and
builder.AddFmp("research") binds "Fmp:research"; both delegate to the
IServiceCollection overloads. Microsoft.Extensions.Hosting.Abstractions
lands on FmpDotNet.Extensions.DependencyInjection and never on the core:
PackageBoundaryTests now says so. The extensions csproj's dependency
comment states the actual rule for its list and stops calling
Microsoft.Extensions.Options an abstractions package (#63); the test
project names the concrete container and host it uses directly.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 8: Say so — README and one csproj comment

The README gains a "Registering the SDK" section covering all four paths, and three touch-ups from #63: the Usage block's missing `using`, the Installing paragraph naming what the extensions package now holds and why both packages are pinned together, and the Configuration section's named-section rule. The core csproj's dependency comment stops calling `Microsoft.Extensions.Options` an abstractions package (#63). No code changes.

**Files:**
- Modify: `README.md` (Usage block; a new section before `## Endpoint coverage`; Installing; Configuration)
- Modify: `src/FmpDotNet/FmpDotNet.csproj:21-23` (one comment)

**Interfaces:** none. Everything documented here exists after Task 7: `FmpClientFactory.Create`, `IHostApplicationBuilder.AddFmp`, `AddFmp(name, …)`, `IFmpBuilder`, `FmpBucketRegistry`.

- [ ] **Step 1: The Usage block's missing using**

In `README.md`, the Usage code block (it starts at line 30 with `using FmpDotNet;`) lists four usings. Insert `using Microsoft.Extensions.DependencyInjection;` between `using FmpDotNet.Models;` and `using NodaTime;`, so the block opens:

```csharp
using FmpDotNet;
using FmpDotNet.Extensions.DependencyInjection;
using FmpDotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
```

`GetRequiredService` on the line below needs it, and a reader copies the block whole.

- [ ] **Step 2: The new section**

Insert the following immediately before the line `## Endpoint coverage`, separated from the Usage section above by one blank line:

````markdown
## Registering the SDK

Four ways in, one wiring path. Every one of them ends in the same registration routine, so the handler chain —
whose order is contractual — exists in one place, and the differences between them are only where the options
come from and how the client is reached.

**A host with a container.** The default: `AddFmp` on the `IServiceCollection`, from configuration or from code,
then resolve `FmpClient`. That is the Usage block above.

**A host built on `IHostApplicationBuilder`** — ASP.NET Core, a Worker Service, `Host.CreateApplicationBuilder`:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddFmp();                                  // binds "Fmp" off builder.Configuration
builder.AddFmp("research");                        // binds "Fmp:research" — a named registration, below
```

**No container at all.** `FmpClientFactory.Create` builds a private container through `AddFmp` and the client
owns it, so the same chain is wired and nothing is hand-assembled. Dispose the client. Logging is none unless you
pass a factory, and no environment variable is read:

```csharp
using var fmp = FmpClientFactory.Create("apikey");

using var fmp = FmpClientFactory.Create(
    o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; },
    loggerFactory: factory);                       // optional; without it the throttle's warnings go nowhere
```

**More than one FMP configuration in one process.** A named registration is the same wiring under a name. Its
options bind from `Fmp:{name}` and validate under the name; its client, its transports and its `HttpClient`s are
keyed by it:

```csharp
services.AddFmp("research", configuration);        // binds "Fmp:research"
services.AddFmp("research", o => { o.ApiKey = "…"; o.PerMinuteCap = 2640; });

sealed class Report([FromKeyedServices("research")] FmpClient fmp) { … }
```

Registrations that share an API key share a reservoir pair, because FMP meters per key; registrations on
different keys get their own. Two registrations sharing a key but declaring different caps cannot both be
honoured: the first to resolve sizes the pair, and the second is logged as a warning naming both.

**Putting your own handlers on the clients.** Every `AddFmp` overload takes an optional callback over
`IFmpBuilder`, which configures the `HttpClient` behind the ordinary endpoints, the one behind the bulk
endpoints, or both:

```csharp
services.AddFmp(configuration, fmp => fmp
    .ConfigureStandardClient(b => b.ConfigurePrimaryHttpMessageHandler(() => corporateProxyHandler))
    .ConfigureBulkClient(b => b.ConfigurePrimaryHttpMessageHandler(() => stub)));
```

Your handlers sit **outermost**: they see one entry per logical call, and the SDK's retry, throttle wait and
timeout all happen beneath them. That is the right default for a proxy, a tracing span or a stubbed primary
handler in a test, and it means a handler added to observe retries will not see them. **Do not add a second retry
policy.** The SDK already retries transient failures (`MaxAttempts`, three by default); a retry stacked on top
multiplies with it — two policies of three attempts each are nine sends per call — and a consumer of this SDK
has already been burned by `AddStandardResilienceHandler` doing exactly that. Tune the SDK's retry through
`FmpOptions` instead.

**Sharing reservoirs across containers.** A host that registers the SDK and also spins up a side client on the
same key would emit at twice its cap. Hand both the same `FmpBucketRegistry`:

```csharp
var shared = new FmpBucketRegistry();
services.AddFmp(o => o.ApiKey = "K", fmp => fmp.UseBucketRegistry(shared));
using var side = FmpClientFactory.Create(o => o.ApiKey = "K", registry: shared);
```

````

- [ ] **Step 3: Installing**

In the "Installing and versioning" section, the first paragraph currently reads:

```
`FmpDotNet` is the client, the models and the transports; `FmpDotNet.Extensions.DependencyInjection` is `AddFmp` —
the container wiring, options binding and validation — and nothing else. A consumer with a container of its own
can reference `FmpDotNet` alone. The two are versioned and published together, and everything below applies to
both.
```

Replace those four lines with:

```
`FmpDotNet` is the client, the models and the transports; `FmpDotNet.Extensions.DependencyInjection` is the
registration surface — `AddFmp` in every form, the `IHostApplicationBuilder` sugar and `FmpClientFactory` — and
nothing else. A consumer with a container of its own can reference `FmpDotNet` alone. The two are versioned and
published together, and everything below applies to both.
```

Then, at the end of the paragraph that begins `**Pin an exact prerelease.**`, after `— which is how \`trader\` consumes it.`, add:

```
 A project that references both packages directly pins them to the same version: the extensions package
depends on the core as a floor, not an exact version, so NuGet will pair an older `AddFmp` with a newer core, and
that pairing can fail at resolve time once a later core adds something the older wiring does not know about.
```

- [ ] **Step 4: Configuration**

In the "Configuration" section, after the paragraph that ends `turn \`RequestTimeout=45\` into a timeout that never fires.`, add a new paragraph:

```
A named registration binds the same keys under `Fmp:{name}` — `Fmp:research:ApiKey` configures
`AddFmp("research", configuration)` — unless the call names another section. Named options validate
independently, so a bad `research` registration fails at startup naming `research`.
```

- [ ] **Step 5: The core csproj comment (#63)**

In `src/FmpDotNet/FmpDotNet.csproj`, replace lines 21-23:

```xml
    <!-- Two abstractions packages and NodaTime, and nothing else. The transports and handlers take
         IOptions<FmpOptions> and ILogger<T>; everything that touches a container is in
         FmpDotNet.Extensions.DependencyInjection. PackageBoundaryTests pins this list. -->
```

with:

```xml
    <!-- The options package, the logging abstractions and NodaTime, and nothing else. The transports and handlers
         take IOptions<FmpOptions> and ILogger<T>; everything that touches a container or a host is in
         FmpDotNet.Extensions.DependencyInjection. PackageBoundaryTests pins this list. -->
```

- [ ] **Step 6: Prove the generated table is untouched, and the build is clean**

Run: `FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EndpointCoverageTests" 2>&1 | grep -E "^Passed!|^Failed!"; git diff --stat`
Expected: the tests pass and `git diff --stat` still lists only `README.md` and `src/FmpDotNet/FmpDotNet.csproj`, with the README line count consistent with your edits and nothing changed inside the coverage table. If the regeneration rewrote table lines, the section was inserted inside the table's markers; move it.

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)"; grep -rn "FmpClient\.Create\b" README.md src tests; grep -c "FmpClientFactory" README.md`
Expected: `0 Warning(s)`, `0 Error(s)`; the first grep prints nothing (the old name from the first draft of the design appears nowhere in code or docs); the second prints a positive count.

- [ ] **Step 7: Commit**

```bash
git add README.md src/FmpDotNet/FmpDotNet.csproj
git commit -F - <<'EOF'
docs: register the SDK four ways, and say which package each lives in (#65)

A "Registering the SDK" section: the container, the host builder, the
container-free factory, named registrations, consumer handlers outermost
with the warning against stacking a retry, and sharing reservoirs across
containers. The Usage block gains the using it needed, Installing names
what the extensions package holds and why both packages are pinned
together, Configuration states the Fmp:{name} rule, and the core csproj's
comment stops calling Microsoft.Extensions.Options an abstractions
package (#63).

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 9: Prove the pair packs, then open the PR

The whole-branch checks: the full suite at its final counts, both packages packed the way CI does with the extensions nuspec carrying exactly one new dependency, and a PR against master.

**Files:** none in the repository. Pack output goes to `dist/host`, which is gitignored.

- [ ] **Step 1: Final green**

Run: `dotnet build FmpDotNet.slnx -warnaserror 2>&1 | grep -E "Warning\(s\)|Error\(s\)" && dotnet test FmpDotNet.slnx --no-build -- RunConfiguration.TreatNoTestsAsError=true 2>&1 | grep -E "^Passed!|^Failed!"; git status --short; git log --oneline master..HEAD | wc -l`
Expected: `0 Warning(s)`, `0 Error(s)`; `FmpDotNet.Tests` 1,472 passed, `FmpDotNet.Extensions.DependencyInjection.Tests` 48 passed, `FmpDotNet.SmokeTests` 22 passed and 5 skipped; an empty status; nine commits above master (the plan, one each for Tasks 1-8).

- [ ] **Step 2: Pack both and read the nuspecs**

```bash
rm -rf dist/host
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o dist/host -p:VersionSuffix=ci.0
dotnet pack src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj -c Release -o dist/host -p:VersionSuffix=ci.0
ls dist/host
unzip -p dist/host/FmpDotNet.0.1.0-ci.0.nupkg FmpDotNet.nuspec | grep -E "<dependency "
unzip -p dist/host/FmpDotNet.Extensions.DependencyInjection.0.1.0-ci.0.nupkg FmpDotNet.Extensions.DependencyInjection.nuspec | grep -E "<dependency "
```

Expected: four files. The core's dependencies are exactly `Microsoft.Extensions.Logging.Abstractions` 10.0.9, `Microsoft.Extensions.Options` 10.0.9 and `NodaTime` 3.2.2 — unchanged by this slice. The extensions package's are exactly `FmpDotNet` 0.1.0-ci.0, `Microsoft.Extensions.Configuration.Abstractions` 10.0.9, `Microsoft.Extensions.Hosting.Abstractions` 10.0.9 and `Microsoft.Extensions.Http` 10.0.9. Paste both blocks into your report.

- [ ] **Step 3: Push and open the PR**

Run: `git push -u origin feat/host-registration-65`

Open the PR with `gh pr create --base master --title "Host registration: factory, host sugar, customization surface, named registrations (#65)"`. The body covers, in this order:

1. The pivot: `FmpClient` is a composition of two transports; the 25-argument constructor is gone and had no caller; the new ownership constructor and why it is public.
2. The one wiring path: `FmpRegistration` name-parameterised, explicit handler lambdas, keyed transports and client, the compatibility `FmpBuckets`; the idempotency fix, with the nine-sends measurement from Task 3's RED run.
3. Consumer handlers outermost, proven by counting: one entry, three sends. The stacked-retry warning and where it is documented.
4. Named registrations and per-key reservoirs, with the drained-reservoir test.
5. `FmpClientFactory.Create`: validates on Create; Dispose disposes the container and both HttpClients; the container-and-side-client sharing test.
6. The host sugar and the dependency: `Hosting.Abstractions` on the extensions package, `PackageBoundaryTests` pinning it off the core; both nuspec dependency blocks from Step 2, verbatim.
7. Which #63 items this closes (the Self-Review below lists them) and which it leaves.
8. Counts: 1,472 / 48 / 22+5.

Reference `#65` with "Closes #65" and end the body with `https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.

Wait for **`.NET — build + test`** to go green before merging.

---

## Self-Review

**Spec coverage.** The design's sections, in its order. "The pivot" → Task 1, including the public ownership constructor the #61 revision called for. "Reservoirs: one per API key" → Task 2 (the registry, the SHA-256 key, the unset-key case, first-writer-wins with a warning), wired in Task 3 and shared in Tasks 4 and 6. "Named registrations" — the blocker, no handler changes, options, resolution, naming → Tasks 3 and 5 (`StandardClientName`/`BulkClientName` in Task 3, the overloads and keyed resolution in Task 5, per-name validation tested in Task 5). "`IFmpBuilder` and handler order" — collects not proxies, consumer handlers outermost, explicit sharing, section name → Task 4 (`SectionNameFor` lands there; named use in Task 5). "The container-free factory" → Task 6, as `FmpClientFactory` in the extensions package. "Host-builder sugar" → Task 7. "Public surface added" → every row of the design's table has a task: the constructor pair and `IDisposable` (1), `FmpBucketRegistry` (2), the name helpers (3), `IFmpBuilder` and the two default-registration overloads (4), the two named overloads (5), `FmpClientFactory.Create` ×2 (6), `IHostApplicationBuilder.AddFmp` ×2 (7). "Compatibility" → the compatibility registration in Task 3 with the design's own warning comment; the unmodified-`AddFmpTests` check is the `grep -c '^-[^-]'` in Tasks 3 and 5. "File layout" → matches the File Structure table above, file for file. "Testing" → `FmpBucketRegistryTests` (2), the `AddFmpTests` additions (5), `FmpBuilderTests` (4, plus the container-sharing test in 6), `FmpClientFactoryTests` (6), `FmpHostBuilderTests` (7), the `PackageBoundaryTests` row (7). "Risks" → documented where the design says: the warning on `ConfigureStandardClient` (Task 4) and in the README (Task 8).

**Where the plan goes beyond the spec, deliberately.**
- *Idempotent wiring, and a throw.* The design says registering a name twice is re-configuration, not a second registration, and relies on `TryAdd*` for it. `TryAdd*` does not cover `AddHttpMessageHandler`, which appends; Task 3's RED run measures the second chain as nine sends. The `Wired` marker makes the design's claim true. A second call that passes a builder callback throws rather than dropping it silently, because the SDK's handlers are already in place and nothing added later could land outermost.
- *`Create` validates eagerly.* The host path validates on start; a factory-built client that threw a configuration error on its first request would be the worse of the two. One line and one test.
- *`Dispose` disposes the HttpClients too.* The design says "a call after Dispose throws". Disposing the container alone does not make that true — the factory's pooled handlers outlive it — so the factory creates the two `HttpClient`s itself and the owned disposable closes them. The transports are constructed with the same one-line expression the registration uses.
- *The cap-conflict warning fires once per disagreeing registration*, not on every handler rebuild, which happens every two minutes under `HttpClientFactory`.
- *`FmpBucketRegistry` takes an optional logger*, because a consumer creating one to share across containers has no container to resolve a logger from.
- *A theory over the name helpers*, three rows, so `StandardClientName` and `BulkClientName` have a test of their own.

**#63 items this plan closes, because the files are rewritten anyway:** the stale "throttle → timeout → network" comment (Task 3, in `FmpRegistration.Configure`'s doc); "Seven explicit reads" (Task 3, `FmpOptionsBinder`, the number removed rather than updated); the "abstractions package" wording in both csprojs (Tasks 7 and 8); the extensions csproj's rationale for its list (Task 7, which states the actual rule); the `ProjectReference` slash style (Task 7, backslashes like the test projects); the test project's direct `Microsoft.Extensions.DependencyInjection` reference (Task 7); the README Usage `using` (Task 8); the pin-both note (Task 8). **Left open on #63:** the post-merge feed grant, which is manual, and the optional csproj-reading test, which the whole-branch review of #61 judged unnecessary.

**Placeholder scan.** No `TBD`, no `TODO`, no "similar to Task N". Every code step carries the full content. Every file in the File Structure table appears in a task.

**Type and name consistency.** `FmpRegistration.Register` is three-argument in Task 3 and four-argument from Task 4 on; Tasks 5, 6 and 7 call the four-argument form through the public overloads. `BucketsFor(sp, name)` in Task 3 becomes `BucketsFor(sp, name, registry)` in Task 4, and `RegisterDefaultOnly` gains the same parameter. `StandardClientName`/`BulkClientName` are defined in Task 3 and used in Tasks 3, 5 and 6 with the same spelling. `IFmpBuilder`'s four methods are named the same in Task 4's interface, Task 4's tests, Task 6's factory and Task 8's README. `FmpClientFactory.Create`'s parameter names — `configure`, `loggerFactory`, `registry`, `configureBuilder` — match between Task 6's code, Task 6's tests (which use them as named arguments) and Task 8's README. The host overloads' parameter names — `name`, `sectionName`, `configure` / `configure`, `name`, `configureBuilder` — match between Task 7's code and tests. Test counts chain: 1,463 → 1,466 (Task 1, +3) → 1,471 (Task 2, +5) → 1,472 (Task 7, +1); 23 → 27 (Task 3, +4) → 34 (Task 4, +7) → 39 (Task 5, +5) → 45 (Task 6, +6) → 48 (Task 7, +3).

**Facts verified against the tree rather than assumed.** Every endpoint group's primary constructor takes exactly one transport (`grep` across `src/FmpDotNet/Endpoints/`, 25 hits, one `FmpBulkTransport`). `new FmpClient(` has no caller in `src/`, `tests/` or `README.md`. The seven handler constructors' shapes are as Task 3 cites them. `FmpTransport` reads `options.Value` in a field initialiser, so `Options.Create` of an unvalidated `FmpOptions` is enough for Task 1's tests. `stable/available-sectors` answers one-property objects under `sector` (`DirectoryEndpoints.cs:70-83`), which is why Task 6's stub returns `[{"sector":"Technology"}]`. `Microsoft.Extensions.Hosting` and `.Hosting.Abstractions` at 10.0.9 are in the local package cache. `TryAddKeyedTransient` with a `(IServiceProvider, object?)` factory is in `DependencyInjection.Abstractions` 10.0.9. The README's escape-hatch code is its "Reaching an endpoint that is not modelled" section (lines 528 and 541 after #61; Task 8's README additions move it, so the code cites the section by name).
