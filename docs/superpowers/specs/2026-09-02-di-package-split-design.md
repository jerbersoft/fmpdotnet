# `FmpDotNet.Extensions.DependencyInjection` — design, 2026-09-02

What issue [#61](https://github.com/jerbersoft/fmpdotnet/issues/61) does: moves `AddFmp` out of the core
package into a second package, so the core carries only the two abstractions its transports and handlers
actually use. No endpoint, model, handler or transport changes. No change to the coverage count.

Unlike most specs in this directory, nothing here rests on a measurement of FMP. It rests on the shape of
this codebase and of the Microsoft.Extensions dependency graph, and every claim about either was checked
against the code or against `obj/project.assets.json` rather than remembered. Line citations are against
`1c2530c`.

## The decision

**Move the one file. Leave the transports and handlers alone.**

`src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` is the only file in the core that
uses `IServiceCollection`, `IConfiguration` or `AddHttpClient`. Every other Microsoft.Extensions use in the
core is `IOptions<FmpOptions>` and `ILogger<T>` — two abstractions packages any library keeps, on the seven
handler classes and the two transports (`FmpTransport.cs:25`, `FmpBulkTransport.cs:10`,
`Http/FmpRetryHandler.cs:139`, and so on). That one file costs the core three package references:
`Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions` and
`Microsoft.Extensions.Options.ConfigurationExtensions`. After the move it costs the core nothing.

The pending host-registration design (branch `docs/host-registration-design`, unmerged) would add a fourth,
`Microsoft.Extensions.Hosting.Abstractions`, to the core, and its own Risks section names that as the first
thing to cut. A separate package is where that surface belongs, and this split is the precondition for it.

### Two decisions were the user's and are settled

- **How deep the cut goes.** Only the DI wiring moves. The alternative — a core with NodaTime as its only
  dependency, transports and handlers taking `FmpOptions` directly, logging abstracted or dropped — was
  offered and declined. It changes every handler constructor and every test that builds one, and the DI
  package would then have to adapt `IOptions` back to plain constructors. `Microsoft.Extensions.Options`
  and `Microsoft.Extensions.Logging.Abstractions` are the two packages a library is expected to keep, and
  keeping them costs no consumer anything they do not already have.
- **The namespace is `FmpDotNet.Extensions.DependencyInjection`**, matching the package id, so the using
  line names the package that provides it. `Microsoft.Extensions.DependencyInjection` — the .NET library
  guideline for `IServiceCollection` extensions, and what Microsoft's own packages do — was recommended and
  declined; keeping `FmpDotNet.DependencyInjection` was declined for leaving the namespace and the package id
  disagreeing forever. The class name `FmpServiceCollectionExtensions` and both `AddFmp` signatures are
  unchanged.

## What moves, what stays

| | before | after |
|---|---|---|
| `FmpServiceCollectionExtensions` | `src/FmpDotNet/DependencyInjection/`, namespace `FmpDotNet.DependencyInjection` | `src/FmpDotNet.Extensions.DependencyInjection/`, namespace `FmpDotNet.Extensions.DependencyInjection` |
| `StandardClient`, `BulkClient` constants | on that class | on that class, unchanged |
| the by-name `Bind` method and its `TimeSpan.TryParse("45")` comment | inside that file | inside that file, unchanged |
| the eleven `Validate` calls and `ValidateOnStart` | inside that file | inside that file, unchanged |
| `FmpOptions` | core | core — it is what the transports read |
| seven handlers, `FmpBuckets`, `TokenBucket`, both transports | core, `Http/` | core, untouched |
| `FmpClient` and 25 endpoint groups | core | core, untouched |

Every type `AddFmp` wires is `public` — checked across `Http/*.cs`, both transports and all 25 endpoint
groups — so the new package reaches them without an `InternalsVisibleTo`.

**One doc comment changes in the core.** `FmpClient.cs:9` crefs
`DependencyInjection.FmpServiceCollectionExtensions.AddFmp(…)`. A `<see cref>` cannot point at an assembly
the core does not reference, and an unresolved cref is `CS1574`, a build error here. It becomes a `<c>`
mention naming the package: "Resolve this from dependency injection after calling `AddFmp` from the
`FmpDotNet.Extensions.DependencyInjection` package."

## Dependencies

| package | keeps | drops | adds |
|---|---|---|---|
| `FmpDotNet` | `Microsoft.Extensions.Logging.Abstractions`, `NodaTime` | `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions` | `Microsoft.Extensions.Options` — used directly by every handler and transport, reached transitively today |
| `FmpDotNet.Extensions.DependencyInjection` | — | — | `ProjectReference` to `FmpDotNet`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Configuration.Abstractions` |

**`Options.ConfigurationExtensions` is not carried over.** The binding reads the section by name through
the `IConfiguration` indexer (`FmpServiceCollectionExtensions.cs:35-47`), which is
`Configuration.Abstractions`; `AddOptions<T>().Configure().Validate().ValidateOnStart()` is
`Microsoft.Extensions.Options`, which `Microsoft.Extensions.Http` already brings. `ConfigurationBinder` was
never called — the file's own comment records why, and that reason is the one that keeps this package
AOT-compatible.

**What `Microsoft.Extensions.Http` brings**, verified against the test project's assets file:
`Configuration.Abstractions`, `DependencyInjection.Abstractions`, `Diagnostics`, `Logging`,
`Logging.Abstractions`, `Options` — and through `Logging`, the concrete `DependencyInjection` container.
So the new package's graph is the core's graph today, less `Options.ConfigurationExtensions` and the
`Configuration.Binder` and concrete `Configuration` packages it carried. `Configuration.Abstractions` is
listed explicitly rather than left to arrive transitively, because it is the package the new code
compiles against.

The new package declares `IsAotCompatible`, as the core does, so `IL2026` and `IL3050` stay build errors on
the by-name binding. That analyser is what forced the by-name binding in the first place, and moving the
file must not lose the guard that keeps it that way.

## Shared packaging metadata: `src/Directory.Build.props`

Today `FmpDotNet.csproj` carries the package identity, the Source Link and symbol settings, the
documentation-file switch, the AOT switch and the README and LICENSE pack items, each with a comment
explaining it. A second package needs all of them, and duplicating them means the two packages can drift
— a version bumped in one csproj and not the other publishes two packages that disagree about which SDK
they are.

They move to a new `src/Directory.Build.props`, which imports the root one:

```xml
<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
```

Under `src/` only, so the test projects — which set `IsPackable=false` and want none of this — are
unaffected. The comments move with the properties they explain; the CS1591 comment stays true of both
packages, since the zero-warning bar is the same.

| moves to `src/Directory.Build.props` | stays in each csproj |
|---|---|
| `VersionPrefix`, `Authors`, `RepositoryUrl`, `RepositoryType`, `PackageProjectUrl`, `PackageLicenseExpression`, `PackageReadmeFile` | `PackageId`, `RootNamespace`, `AssemblyName`, `Description`, `PackageTags` |
| `IncludeSymbols`, `SymbolPackageFormat`, `PublishRepositoryUrl`, `EmbedUntrackedSources`, `ContinuousIntegrationBuild` | `InternalsVisibleTo` |
| `GenerateDocumentationFile`, `IsAotCompatible` | `PackageReference`s |
| the README and LICENSE `None` items, addressed as `$(MSBuildThisFileDirectory)../README.md` rather than `../../README.md`, so the path does not depend on project depth | |

Both packages pack the repository README. It is the endpoint table plus the measured-behaviour notes, and
it is also where `AddFmp` and the `Fmp` configuration section are documented — so it is the right package
page for the DI package as well, not only for the core.

**Version coupling.** `dotnet pack` of a project with a `ProjectReference` emits a dependency on the
referenced project's package at the version it was built with. Both projects share one `VersionPrefix`
and one `VersionSuffix` per CI run, so `FmpDotNet.Extensions.DependencyInjection 0.1.0-ci.N` depends on
`FmpDotNet >= 0.1.0-ci.N` — the pair published together, and a consumer who adds the extensions package
gets the matching core.

## Tests

**`AddFmpTests.cs` moves** to a new `tests/FmpDotNet.Extensions.DependencyInjection.Tests` project. It is
350 lines testing registration, validation, handler order and reservoir sharing — all of them properties
of the file that moves — and a test project per package matches the package boundary. Every test in it
must pass with only its using line changed; that is the compatibility proof for the move.

**`FmpDotNet.Tests` keeps two DI-shaped tests where they are.** `DirectoryEndpointsTests.cs:146` and
`CompanyScreenerTests.cs:311` each build a provider through `AddFmp` to prove a group is registered. They
sit inside files about their endpoint groups and are not worth splitting out. `FmpDotNet.Tests` gains a
`ProjectReference` to the new package for them, and keeps its `InternalsVisibleTo` grant from the core.

**`FmpDotNet.SmokeTests`** builds its harness through `AddFmp` (`LiveApi.cs:42`) and gains the same
`ProjectReference`. It already references the concrete `Microsoft.Extensions.DependencyInjection` directly.

**Both `ConfigurationBuilder`-using test projects add `Microsoft.Extensions.Configuration` directly.** The
concrete configuration package — `ConfigurationBuilder`, `AddInMemoryCollection` — reaches the tests today
only through `Options.ConfigurationExtensions → Configuration.Binder → Configuration`, verified in the
assets file. The split drops the first link, so the tests would lose it silently. Referencing what a
project uses is correct anyway; this just makes it necessary.

**One new test pins the cut.** In `FmpDotNet.Tests`, a test reads
`typeof(FmpClient).Assembly.GetReferencedAssemblies()` and asserts that none is `Microsoft.Extensions.Http`,
`Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Configuration.Abstractions`
or `Microsoft.Extensions.Options.ConfigurationExtensions`. `GetReferencedAssemblies` lists what the
compiled IL actually references, so a `using Microsoft.Extensions.DependencyInjection` creeping back into
the core fails this test on the commit that adds it, not at the next consumer's restore. The test project
is not AOT-compiled, so the metadata read is fine there.

**CI needs no change to test.** `dotnet test FmpDotNet.slnx` runs every project in the solution, and the
zero-tests-is-an-error setting applies per project, so the new test project is gated the same way the
existing two are.

## CI

The Pack step gains one line:

```yaml
- name: Pack
  run: |
    dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o ./artifacts -p:VersionSuffix=ci.${{ github.run_number }}
    dotnet pack src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj -c Release -o ./artifacts -p:VersionSuffix=ci.${{ github.run_number }}
```

Two explicit lines rather than `dotnet pack FmpDotNet.slnx`, so the workflow names what ships and a third
project does not start publishing by being added to the solution. The Push step already globs
`./artifacts/*.nupkg` and the symbol upload globs `*.snupkg`, so both packages and both symbol packages go
where the one does today, with `--skip-duplicate` covering re-runs of both.

The NuGet cache key hashes `**/*.csproj`; `Directory.Build.props` is not in it. That only affects cache
reuse, never correctness, and the new csproj changes the hash on this change anyway.

## Documentation

- **README Usage** (`README.md:27-37`): the using line becomes `using FmpDotNet.Extensions.DependencyInjection;`.
- **README Installing** (`README.md:825-828`): `dotnet add package FmpDotNet.Extensions.DependencyInjection`,
  with one sentence saying it brings `FmpDotNet` with it, and that a consumer with their own container
  can reference `FmpDotNet` alone. The versioning paragraphs apply to both packages and say so.
- **README Status** (`README.md:21`): the sentence naming `AddFmp` names the package.
- **`FmpClient` doc comment**: the cref change above.
- **The new csproj's `Description`**: one sentence — registers FmpDotNet into
  Microsoft.Extensions.DependencyInjection: `AddFmp`, options binding and validation, and the two typed
  clients with their throttle, retry and timeout chains.

## Compatibility

**This is a breaking change for every consumer, and it is made on purpose.** A consumer on `FmpDotNet`
alone loses `AddFmp` at their next bump and gets a compile error at the call site — loud, and fixed by one
package reference and one using line. The README already says to treat a minor bump before 1.0 as
potentially breaking, and two releases have removed public members after measurement; this is the same
policy applied to packaging.

`trader` is that consumer today. It pins an exact prerelease, so nothing changes for it until it chooses
to bump; when it does, it adds `FmpDotNet.Extensions.DependencyInjection` and changes the using line. That
is recorded on #61 rather than avoided here.

No binary-compatibility shim — no type-forwarding `FmpDotNet.DependencyInjection` namespace left in the
core — because the core cannot reference the package it would forward to, and a shim that only works one
way is a trap rather than a courtesy.

## Relation to the host-registration design

`docs/host-registration-design` is revised against the new package in a follow-up, not here. What that
revision has to change, so it is not rediscovered:

- `FmpClient.Create` cannot stay on the core's `FmpClient`. It builds a private container by calling
  `AddFmp`, and after this split the core cannot reference `AddFmp`. It becomes a static on a type in
  the extensions package — `FmpClientFactory.Create`, or similar — and keeps its one-wiring-path
  property, since it still goes through `AddFmp`.
- `IFmpBuilder`, `FmpBuilder`, the named-registration core and the host-builder sugar all land in the
  extensions package. `Microsoft.Extensions.Hosting.Abstractions` is added there, never to the core,
  which retires that design's flagged risk.
- `FmpBucketRegistry` stays in the core's `Http/`, beside `FmpBuckets`, because the handlers take it.
- Its "File layout" table and its `FmpDotNet.csproj` row are rewritten for two projects.

## Testing

One test per claim this design makes, beyond the moved file passing unchanged:

- **`FmpDotNet.Tests/PackageBoundaryTests.cs`** — the core assembly references none of the four dropped
  packages. Fails on the commit that reintroduces DI code into the core.
- **The moved `AddFmpTests`** — every existing case, unmodified apart from the using line. The reservoir
  and handler-order tests in it (`AddFmpTests.cs:158`, `:268`, `:290`, `:316`) are the ones that prove the
  wiring survived the move byte-for-byte, because they assert on what the chain does rather than on what
  it is called.
- **Manual, in the plan, not automated:** `dotnet pack` both projects locally and read the extensions
  package's `.nuspec` to confirm it depends on `FmpDotNet` at the same version and on
  `Microsoft.Extensions.Http` and `Microsoft.Extensions.Configuration.Abstractions`, and nothing else.
  Read the core's `.nuspec` to confirm it depends on `Microsoft.Extensions.Options`,
  `Microsoft.Extensions.Logging.Abstractions` and `NodaTime`, and nothing else.

## Files

| file | change |
|---|---|
| `src/Directory.Build.props` | new — shared package metadata, imports the root props |
| `src/FmpDotNet/FmpDotNet.csproj` | package metadata out; three references dropped, `Microsoft.Extensions.Options` added |
| `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` | deleted |
| `src/FmpDotNet/FmpClient.cs` | cref → `<c>` |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj` | new |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` | the moved file, namespace changed |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/*.csproj` | new |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs` | moved, using line changed |
| `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj` | `ProjectReference` to the new package; `Microsoft.Extensions.Configuration` |
| `tests/FmpDotNet.Tests/PackageBoundaryTests.cs` | new |
| `tests/FmpDotNet.Tests/DirectoryEndpointsTests.cs`, `CompanyScreenerTests.cs` | using line |
| `tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj` | `ProjectReference` to the new package |
| `tests/FmpDotNet.SmokeTests/LiveApi.cs` | using line |
| `FmpDotNet.slnx` | two projects added |
| `.github/workflows/ci.yml` | second pack line |
| `README.md` | Usage, Installing, Status |

## What this design does not do

- **No non-DI construction path in the core.** `FmpClient` still takes all 25 endpoint groups, and a
  consumer without a container wires it exactly as they would today. The host-registration design owns
  that question.
- **No handler, transport, options or model changes.** `Http/` is untouched, `FmpOptions` is untouched.
- **No version bump.** `0.1.0` and the `-ci.N` scheme continue; the break is covered by the README's
  existing pre-1.0 policy.
- **No second README.** Both packages ship the repository README.
