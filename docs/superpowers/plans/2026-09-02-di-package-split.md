# FmpDotNet.Extensions.DependencyInjection Package Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `AddFmp` out of the `FmpDotNet` package into a new `FmpDotNet.Extensions.DependencyInjection` package, so the core compiles against `Microsoft.Extensions.Options` and `Microsoft.Extensions.Logging.Abstractions` only, and pin that cut with a test.

**Architecture:** One file moves — `FmpServiceCollectionExtensions.cs` — with its namespace changed to `FmpDotNet.Extensions.DependencyInjection` and nothing else touched. The package metadata both projects share moves into a `src/Directory.Build.props` so the two packages cannot drift apart. The moved file's 23 tests move with it into a test project of their own. A new `PackageBoundaryTests` in the core's test project reads the core assembly's compiled references and fails on the commit that lets DI code back in.

**Tech Stack:** .NET 10 SDK 10.0.102 (`global.json` pins the 10.0.100 feature band), C#, MSBuild `Directory.Build.props`, `dotnet pack`, xUnit 2.9.3, GitHub Actions.

**Spec:** [`docs/superpowers/specs/2026-09-02-di-package-split-design.md`](../specs/2026-09-02-di-package-split-design.md), committed on this branch as `121eb47`. Read it before Task 1. The plan argues from it; where the plan departs from it, the Self-Review at the bottom says so.

## Global Constraints

Copied from the spec and `CONTRIBUTING.md`. Every task's requirements implicitly include this section.

- **Branch is `feat/di-package-61`**, already created from `master`, already carrying the spec commit. Commit in conventional-commit form referencing `#61`. End every commit message with `Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.
- **The moved file changes on exactly one line: its namespace.** `FmpServiceCollectionExtensions`, both `AddFmp` signatures, the `StandardClient`/`BulkClient` constants, the by-name `Bind`, the eleven `Validate` calls, the handler order and every comment — including one stale comment noted in the Self-Review — stay byte-identical. `git diff -M --stat` must show a rename with `2 +-`.
- **The namespace is `FmpDotNet.Extensions.DependencyInjection`.** That was the user's decision over `Microsoft.Extensions.DependencyInjection`; do not revisit it.
- **No reflection in either library.** Both packages declare `IsAotCompatible`; `IL2026` and `IL3050` are build errors. The test projects may reflect.
- **Package versions stay as they are:** every `Microsoft.Extensions.*` reference at `10.0.9`, `NodaTime` at `3.2.2`, `VersionPrefix` at `0.1.0`. No version bump: the README's pre-1.0 policy covers the break.
- **`Http/`, `FmpOptions`, the transports, the endpoints and the models are untouched.** The only core source change outside the csproj is one doc comment in `FmpClient.cs`.
- **The CI job name `.NET — build + test` is load-bearing** (a repository ruleset matches it by name). Do not touch it.
- **Build must be clean under `-warnaserror`.** Run `dotnet build FmpDotNet.slnx -warnaserror` before every commit.
- **Full suite must be green.** On this branch before Task 1: `FmpDotNet.Tests` 1,479 passed; `FmpDotNet.SmokeTests` 22 passed, 5 skipped (no key, by design). The counts each task expects are written in that task.
- **Never paste an API key**, and never echo one. Nothing in this plan needs one.
- **`dist/` is gitignored** and is where local `dotnet pack` output goes. Use subdirectories of it for the before/after comparisons below; do not commit anything from it.

## File Structure

| file | responsibility | task |
|---|---|---|
| `src/Directory.Build.props` | **Create.** Package identity, symbols, Source Link, documentation and AOT switches shared by every project under `src/`. Imports the root props explicitly, because MSBuild only auto-imports the nearest one. | 1 |
| `src/FmpDotNet/FmpDotNet.csproj` | **Modify.** Task 1 strips it to what is the core's own (`PackageId`, `Description`, `PackageTags`, `InternalsVisibleTo`, references). Task 2 drops three references and adds `Microsoft.Extensions.Options`. | 1, 2 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj` | **Create.** The new package: `ProjectReference` to the core, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Configuration.Abstractions`. | 2 |
| `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` | **Move** from `src/FmpDotNet/DependencyInjection/`, via `git mv`; namespace line changed. | 2 |
| `src/FmpDotNet/FmpClient.cs` | **Modify.** The `<see cref>` to `AddFmp` becomes a `<c>` mention, because an unresolved cref is `CS1574`, a build error here. | 2 |
| `FmpDotNet.slnx` | **Modify.** One project under `/src/` in Task 2, one under `/tests/` in Task 3. | 2, 3 |
| `tests/FmpDotNet.Tests/PackageBoundaryTests.cs` | **Create.** Seven cases: four assemblies the core must not reference, three it must (the positive control). | 2 |
| `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj` | **Modify.** `ProjectReference` to the new package; direct `Microsoft.Extensions.Configuration`, which reached it only through a dependency the split removes. | 2 |
| `tests/FmpDotNet.Tests/AddFmpTests.cs`, `CompanyScreenerTests.cs`, `DirectoryEndpointsTests.cs` | **Modify.** One using line each. | 2 |
| `tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj`, `LiveApi.cs` | **Modify.** `ProjectReference`; one using line. | 2 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj` | **Create.** Test project for the new package. | 3 |
| `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs` | **Move** from `tests/FmpDotNet.Tests/`, via `git mv`; namespace line changed. | 3 |
| `.github/workflows/ci.yml` | **Modify.** A second explicit `dotnet pack` line. | 4 |
| `README.md` | **Modify.** Status, Usage, Installing. | 4 |

---

### Task 1: Shared packaging metadata in `src/Directory.Build.props`

Pure refactor of the core csproj. The test is a before/after comparison of the packed `.nuspec`, which must be byte-identical.

**Files:**
- Create: `src/Directory.Build.props`
- Modify: `src/FmpDotNet/FmpDotNet.csproj`

**Interfaces:**
- Consumes: the root `Directory.Build.props` (target framework, nullable, implicit usings, `TreatWarningsAsErrors`).
- Produces: every property and item in the table under "Shared packaging metadata" in the spec, applied to every project under `src/`. Task 2's new csproj relies on getting `VersionPrefix`, `IsAotCompatible`, `GenerateDocumentationFile`, the symbol settings and the README/LICENSE items from here without declaring them.

- [ ] **Step 1: Record the baseline package**

```bash
rm -rf dist/before dist/after
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o dist/before
unzip -p dist/before/FmpDotNet.0.1.0.nupkg FmpDotNet.nuspec
unzip -Z1 dist/before/FmpDotNet.0.1.0.nupkg | sort
```

Expected: the nuspec shows `<id>FmpDotNet</id>`, `<version>0.1.0</version>`, `<authors>Herbert Sabanal</authors>`, `<license type="expression">MIT</license>`, `<readme>README.md</readme>`, the repository element with the current branch and commit, and five dependencies. The listing contains `README.md`, `LICENSE`, `lib/net10.0/FmpDotNet.dll` and `lib/net10.0/FmpDotNet.xml`. A `FmpDotNet.0.1.0.snupkg` sits beside the nupkg.

- [ ] **Step 2: Write `src/Directory.Build.props`**

The comments are the ones from `FmpDotNet.csproj`, moved with the properties they explain. Two phrases are adjusted so they stay true of both packages: "this assembly" becomes the package's name where the sentence is about the core specifically.

```xml
<Project>

  <!--
    Everything a package under src/ ships with, shared so that the two packages cannot disagree about which SDK
    they are — a version bumped in one csproj and not the other would publish a pair that do not match.

    MSBuild auto-imports only the NEAREST Directory.Build.props, so this file REPLACES the root one for every
    project under src/ unless it imports it. The root carries the build policy shared with the test projects
    (target framework, nullable, implicit usings, warnings as errors); this file carries what only a shipped
    package needs. The tests set IsPackable=false and want none of this, which is why it sits here and not at the
    root.
  -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>

    <!--
      XML documentation ships with the package, so a consumer gets the measured upstream quirks in IntelliSense
      rather than having to open this repo to find them. That is most of the value the docs carry.

      CS1591 ("missing XML comment for publicly visible type or member") is NOT suppressed here, and that is the
      #21 change worth noting. It used to be, project-wide, because turning the doc file on reported 262 of them
      — all in the seven period-shaped fundamentals models from #4, whose properties are flat transcriptions of
      FMP's wire fields. Documenting those individually would bury each type's real documentation, which is the
      type-level note recording what the endpoint actually does.

      The cost of the project-wide suppression was that a NEW undocumented public member, anywhere in the SDK,
      compiled silently. Each of those seven models now carries a file-scoped `#pragma warning disable CS1591`
      instead, with the count and the reasoning at the top of the file, and #40 added an eighth — CotReport, 128
      properties of CFTC column names. The exemption is visible where it applies and the zero-warning bar holds
      everywhere else — so an undocumented endpoint, option or converter added later fails the build. The same
      bar applies to every package under src/.

      Turning this on for the first time also caught a real defect it is worth recording: KeyMetrics carried
      `R&D` unescaped in a doc comment, which is malformed XML (CS1570). That one is fixed, not deferred.
    -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <!--
    Package identity shared by every package under src/. PackageId, Description and PackageTags are each
    package's own and stay in its csproj.

    VersionPrefix, not Version. CI packs with `-p:VersionSuffix=ci.<run number>` so every push to master
    publishes a distinct prerelease (0.1.0-ci.7, 0.1.0-ci.8, ...). That matters because GitHub Packages
    REFUSES to overwrite an existing NuGet version: a fixed version would make the second push of any given
    version fail the build. A local `dotnet pack` with no suffix yields a plain 0.1.0, which outranks every
    prerelease, so a hand-cut release is always newer than the CI builds it supersedes. The scheme is written
    down for consumers under "Installing and versioning" in the README, not only in this comment.

    One VersionPrefix for both packages is also what makes the pair consistent: `dotnet pack` of a project with
    a ProjectReference emits a dependency on the referenced package at the version it was built with, so
    FmpDotNet.Extensions.DependencyInjection 0.1.0-ci.N depends on FmpDotNet >= 0.1.0-ci.N, and the two are
    published together from one CI run.
  -->
  <PropertyGroup>
    <VersionPrefix>0.1.0</VersionPrefix>
    <Authors>Herbert Sabanal</Authors>
    <RepositoryUrl>https://github.com/jerbersoft/fmpdotnet</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageProjectUrl>https://github.com/jerbersoft/fmpdotnet</PackageProjectUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <!-- An SPDX expression rather than PackageLicenseFile: NuGet renders it as a link to the canonical licence
         text and clients can read the terms without unpacking anything. The LICENSE file is packed as well, so
         the package is self-contained either way. No PackageIcon — nothing is branded yet, and an invented mark
         would only have to be replaced. -->
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <!--
    Debuggability of a shipped build.

    A .snupkg carries the PDBs separately, so the main package stays small and a consumer who never debugs into
    the SDK downloads nothing extra. Source Link is what makes those PDBs useful: it stamps the commit into the
    PDB so a debugger can fetch the exact source for the binary being stepped through. It needs no PackageReference
    — the .NET SDK has carried Microsoft.SourceLink.GitHub in-box since .NET 8 and resolves the provider from the
    git remote.

    EmbedUntrackedSources covers the generated files. Every model in FmpDotNet is deserialised through code the
    JSON source generator emits from FmpJsonContext, and none of that is in git — so without this, stepping into
    deserialisation reaches a file Source Link cannot resolve.

    ContinuousIntegrationBuild is set only under GitHub Actions. It normalises source paths so the same commit
    packs to the same bytes anywhere — which is the property that makes a build reproducible, and also the one
    that would replace a local developer's real paths with placeholders and break stepping into a local `dotnet
    pack`. CI wants it, a laptop does not.
  -->
  <PropertyGroup>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <!-- The repository README is the package README, for both packages. It is the endpoint table plus the
         measured upstream-behaviour notes, and it is also where AddFmp and the "Fmp" configuration section are
         documented — so it is the right package page for the DI package as well as for the core. Addressed from
         this file's directory rather than as ../../README.md so the path does not depend on project depth. -->
    <None Include="$(MSBuildThisFileDirectory)../README.md" Pack="true" PackagePath="\" Visible="false" />
    <None Include="$(MSBuildThisFileDirectory)../LICENSE" Pack="true" PackagePath="\" Visible="false" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Strip `src/FmpDotNet/FmpDotNet.csproj` to what is the core's own**

Replace the whole file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>FmpDotNet</RootNamespace>
    <AssemblyName>FmpDotNet</AssemblyName>
  </PropertyGroup>

  <!-- Identity that is this package's own. Everything shared with the other package under src/ — version,
       authors, repository, licence, README, symbols, Source Link, the documentation and AOT switches — is in
       ../Directory.Build.props, so the two cannot drift apart. -->
  <PropertyGroup>
    <PackageId>FmpDotNet</PackageId>
    <Description>A .NET client for the Financial Modeling Prep API. NodaTime throughout, AOT-compatible, with the upstream's measured quirks documented on the members they affect.</Description>
    <PackageTags>fmp;financialmodelingprep;market-data;fundamentals;finance</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="FmpDotNet.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.9" />
    <PackageReference Include="NodaTime" Version="3.2.2" />
  </ItemGroup>

</Project>
```

The references are unchanged in this task; Task 2 changes them.

- [ ] **Step 4: Pack again and compare**

```bash
dotnet build FmpDotNet.slnx -warnaserror
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o dist/after
diff <(unzip -p dist/before/FmpDotNet.0.1.0.nupkg FmpDotNet.nuspec) <(unzip -p dist/after/FmpDotNet.0.1.0.nupkg FmpDotNet.nuspec) && echo NUSPEC IDENTICAL
diff <(unzip -Z1 dist/before/FmpDotNet.0.1.0.nupkg | grep -v psmdcp | sort) <(unzip -Z1 dist/after/FmpDotNet.0.1.0.nupkg | grep -v psmdcp | sort) && echo CONTENTS IDENTICAL
ls dist/after
```

Expected: the build is clean; both `diff`s print nothing and the two echo lines appear; `dist/after` holds `FmpDotNet.0.1.0.nupkg` and `FmpDotNet.0.1.0.snupkg`. The nuspec comparison includes the `commit` attribute, which is the same because both packs ran at the same `HEAD` — do not commit between Step 1 and Step 4. The `grep -v psmdcp` drops the one entry that legitimately differs: NuGet names the Open Packaging metadata part under `package/services/metadata/core-properties/` with a fresh GUID on every pack.

If the nuspec differs, the props file is not being imported, or a property moved with a different value. Fix it before continuing; do not accept a diff here.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test FmpDotNet.slnx`

Expected: `FmpDotNet.Tests` 1,479 passed; `FmpDotNet.SmokeTests` 22 passed, 5 skipped. Nothing about the tests changed; this confirms the test projects, which live outside `src/`, did not pick up the new props.

- [ ] **Step 6: Commit**

```bash
git add src/Directory.Build.props src/FmpDotNet/FmpDotNet.csproj
git commit -m "build: share package metadata across src/ in a Directory.Build.props (#61)

Version, authors, repository, licence, README and LICENSE items, symbol and Source Link settings, and the
documentation and AOT switches move out of FmpDotNet.csproj into src/Directory.Build.props, which imports the
root props explicitly because MSBuild auto-imports only the nearest one. The csproj keeps PackageId,
Description, PackageTags, InternalsVisibleTo and its references.

Packed before and after at the same HEAD: the .nuspec and the package contents are byte-identical. This is the
precondition for a second package under src/ that cannot drift from the first.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE"
```

---

### Task 2: The new package, and the core without it

The one atomic move. It starts with the boundary test, which is red against the current tree, and ends with it green. Nothing in between builds on its own, so the steps are edits and the verification is at the end.

**Files:**
- Create: `tests/FmpDotNet.Tests/PackageBoundaryTests.cs`
- Create: `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj`
- Move: `src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs` → `src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs` (line 9 changes)
- Modify: `src/FmpDotNet/FmpDotNet.csproj` (the `PackageReference` group)
- Modify: `src/FmpDotNet/FmpClient.cs:8-9`
- Modify: `FmpDotNet.slnx`
- Modify: `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj`
- Modify: `tests/FmpDotNet.Tests/AddFmpTests.cs:4`, `tests/FmpDotNet.Tests/CompanyScreenerTests.cs:6`, `tests/FmpDotNet.Tests/DirectoryEndpointsTests.cs:4`
- Modify: `tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj`, `tests/FmpDotNet.SmokeTests/LiveApi.cs:1`

**Interfaces:**
- Consumes: the shared props from Task 1.
- Produces: package `FmpDotNet.Extensions.DependencyInjection`, assembly and root namespace of the same name, containing `public static class FmpServiceCollectionExtensions` with `AddFmp(this IServiceCollection, IConfiguration)`, `AddFmp(this IServiceCollection, Action<FmpOptions>)`, `const string StandardClient = "fmp"`, `const string BulkClient = "fmp-bulk"` — every signature exactly as it is today. Task 3's test project references this project. Task 4's CI change packs it.

- [ ] **Step 1: Write the failing boundary test**

Create `tests/FmpDotNet.Tests/PackageBoundaryTests.cs`:

```csharp
using System.Reflection;

namespace FmpDotNet.Tests;

/// <summary>Pins the dependency cut #61 made: the core assembly compiles against the options and logging
/// abstractions only, and the container wiring lives in <c>FmpDotNet.Extensions.DependencyInjection</c>.
///
/// <para><see cref="Assembly.GetReferencedAssemblies"/> lists what the compiled IL actually references, not what
/// the package graph carries — so a <c>using Microsoft.Extensions.DependencyInjection</c> that creeps back into
/// the core fails here, on the commit that adds it, rather than at the next consumer's restore. This project is
/// not AOT-compiled, so reading assembly metadata is fine here even though the library itself may not.</para>
/// </summary>
public class PackageBoundaryTests
{
    private static IReadOnlyList<string> CoreReferences { get; } =
        typeof(FmpClient).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    [Theory]
    [InlineData("Microsoft.Extensions.Http")]
    [InlineData("Microsoft.Extensions.DependencyInjection.Abstractions")]
    [InlineData("Microsoft.Extensions.Configuration.Abstractions")]
    [InlineData("Microsoft.Extensions.Options.ConfigurationExtensions")]
    public void The_core_does_not_reference(string assembly) =>
        Assert.DoesNotContain(assembly, CoreReferences);

    /// <summary>The positive control. Without it, the theory above would pass against an empty list — for
    /// instance if <c>typeof(FmpClient)</c> ever resolved to some other assembly.</summary>
    [Theory]
    [InlineData("Microsoft.Extensions.Options")]
    [InlineData("Microsoft.Extensions.Logging.Abstractions")]
    [InlineData("NodaTime")]
    public void The_core_still_references(string assembly) =>
        Assert.Contains(assembly, CoreReferences);
}
```

- [ ] **Step 2: Run it and watch three cases fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~PackageBoundaryTests"`

Expected: 7 total, **3 failed, 4 passed**. The failures are `The_core_does_not_reference` for `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Configuration.Abstractions` — the three assemblies `AddFmp` compiles against today. The `Options.ConfigurationExtensions` case passes already: the core never called anything in that package (the by-name `Bind` exists precisely to avoid `ConfigurationBinder`), so its IL never referenced it; the package reference was dead weight. All three positive-control cases pass.

If a different set fails, stop and find out why before moving anything.

- [ ] **Step 3: Create the new package project**

Create `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    The container wiring for FmpDotNet, as its own package (#61).

    The core's transports and handlers take IOptions<FmpOptions> and ILogger<T> and nothing more, so the core
    depends on two abstractions packages any library keeps. Everything that touches IServiceCollection,
    IConfiguration or AddHttpClient is in this one file, and it lives here so that a consumer with their own
    container can reference FmpDotNet alone — and so that IHostApplicationBuilder sugar, when it comes, lands
    here rather than putting Microsoft.Extensions.Hosting.Abstractions on the core.

    Version, authors, repository, licence, README, symbols, Source Link and the documentation and AOT switches
    come from ../Directory.Build.props, shared with FmpDotNet so the pair cannot drift apart.
  -->
  <PropertyGroup>
    <RootNamespace>FmpDotNet.Extensions.DependencyInjection</RootNamespace>
    <AssemblyName>FmpDotNet.Extensions.DependencyInjection</AssemblyName>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>FmpDotNet.Extensions.DependencyInjection</PackageId>
    <Description>Registers FmpDotNet into Microsoft.Extensions.DependencyInjection: AddFmp, options binding and validation, and the two typed clients with their throttle, retry and timeout chains.</Description>
    <PackageTags>fmp;financialmodelingprep;dependency-injection;market-data</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <!-- Packs as a dependency on FmpDotNet at the version this project was built with — the same VersionPrefix
         and VersionSuffix, so a consumer who adds this package gets the matching core. -->
    <ProjectReference Include="../FmpDotNet/FmpDotNet.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Configuration.Abstractions is what the by-name binding compiles against (IConfiguration, GetSection and
         the indexer) — listed explicitly rather than left to arrive through Http, because it is what the code
         uses. NOT Options.ConfigurationExtensions: ConfigurationBinder is neither trim- nor AOT-safe and is never
         called; AddOptions/Configure/Validate/ValidateOnStart are Microsoft.Extensions.Options, which Http brings.
         Http also brings DependencyInjection.Abstractions (IServiceCollection, TryAdd*) and Logging. -->
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Move the file and change its namespace**

```bash
git mv src/FmpDotNet/DependencyInjection/FmpServiceCollectionExtensions.cs src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs
rmdir src/FmpDotNet/DependencyInjection 2>/dev/null || true
```

Then change line 9 of the moved file, and only line 9:

```csharp
namespace FmpDotNet.DependencyInjection;
```

becomes

```csharp
namespace FmpDotNet.Extensions.DependencyInjection;
```

The using lines at the top (`Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Extensions`, `Microsoft.Extensions.Options`, `FmpDotNet.Endpoints`, `FmpDotNet.Http`, `NodaTime`) all still resolve and stay as they are. `FmpOptions` and `FmpClient` resolve because `FmpDotNet` is an enclosing namespace of the new one.

- [ ] **Step 5: Cut the core's references**

In `src/FmpDotNet/FmpDotNet.csproj`, replace the `PackageReference` group with:

```xml
  <ItemGroup>
    <!-- Two abstractions packages and NodaTime, and nothing else. The transports and handlers take
         IOptions<FmpOptions> and ILogger<T>; everything that touches a container is in
         FmpDotNet.Extensions.DependencyInjection. PackageBoundaryTests pins this list. -->
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.9" />
    <PackageReference Include="NodaTime" Version="3.2.2" />
  </ItemGroup>
```

`Microsoft.Extensions.Options` is used directly by every handler and both transports and was reached transitively through `Microsoft.Extensions.Http` until now. (It, and `Logging.Abstractions`, still pull `DependencyInjection.Abstractions` into the core's package graph — that is Microsoft's dependency, not ours, and the boundary test reads the IL rather than the graph, so it is unaffected.)

- [ ] **Step 6: Fix the doc comment in `FmpClient.cs`**

Lines 8-9 of `src/FmpDotNet/FmpClient.cs` currently read:

```csharp
/// <para>Resolve this from dependency injection after calling
/// <see cref="DependencyInjection.FmpServiceCollectionExtensions.AddFmp(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{FmpOptions})"/>.</para></summary>
```

Replace them with:

```csharp
/// <para>Resolve this from dependency injection after calling <c>AddFmp</c> from the
/// <c>FmpDotNet.Extensions.DependencyInjection</c> package.</para></summary>
```

A `<see cref>` cannot point at an assembly the core does not reference; left as it was, this is `CS1574` and fails the build.

- [ ] **Step 7: Add the project to the solution**

`FmpDotNet.slnx` becomes:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/FmpDotNet/FmpDotNet.csproj" />
    <Project Path="src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj" />
    <Project Path="tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 8: Point the test projects at the new package**

In `tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj`, add one `PackageReference` to the existing group and one `ProjectReference` to the existing group, so they read:

```xml
  <ItemGroup>
    <!-- Configuration (ConfigurationBuilder, AddInMemoryCollection) reached this project only through
         Options.ConfigurationExtensions on the core, which #61 removed. Referenced directly, because it is used
         directly, by the tests that build an in-memory configuration to drive AddFmp. -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NodaTime" Version="3.2.2" />
    <PackageReference Include="NodaTime.Testing" Version="3.2.2" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
```

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\FmpDotNet\FmpDotNet.csproj" />
    <!-- For the tests that build a provider through AddFmp to prove a group is registered
         (DirectoryEndpointsTests, CompanyScreenerTests). The concrete container and AddLogging arrive through
         this reference's Microsoft.Extensions.Http, as they did through the core's until #61. -->
    <ProjectReference Include="..\..\src\FmpDotNet.Extensions.DependencyInjection\FmpDotNet.Extensions.DependencyInjection.csproj" />
  </ItemGroup>
```

In `tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj`, the `ProjectReference` group becomes:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\FmpDotNet\FmpDotNet.csproj" />
    <ProjectReference Include="..\..\src\FmpDotNet.Extensions.DependencyInjection\FmpDotNet.Extensions.DependencyInjection.csproj" />
  </ItemGroup>
```

Then change the using line in each of these four files from `using FmpDotNet.DependencyInjection;` to `using FmpDotNet.Extensions.DependencyInjection;`:

- `tests/FmpDotNet.Tests/AddFmpTests.cs` line 4
- `tests/FmpDotNet.Tests/CompanyScreenerTests.cs` line 6
- `tests/FmpDotNet.Tests/DirectoryEndpointsTests.cs` line 4
- `tests/FmpDotNet.SmokeTests/LiveApi.cs` line 1

Check nothing was missed:

```bash
grep -rn "FmpDotNet.DependencyInjection" src tests README.md --include='*.cs' --include='*.csproj' --include='*.md'
```

Expected: no output.

- [ ] **Step 9: Build, and confirm the move is a rename**

```bash
dotnet build FmpDotNet.slnx -warnaserror
git add -A src tests FmpDotNet.slnx
git diff --cached -M --name-status -- src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs src/FmpDotNet/DependencyInjection
git diff --cached -M --stat -- src/FmpDotNet.Extensions.DependencyInjection/FmpServiceCollectionExtensions.cs src/FmpDotNet/DependencyInjection
```

Expected: a clean build of four projects; a `--name-status` line beginning `R` with a score in the high nineties (`R099`, a rename at 99% similarity) naming the old path then the new; and a `--stat` line ending in `| 2 +-`. Git abbreviates the long path in the stat line with `...` or braces, which is why `--name-status` is there to show the rename in full. `2 +-` is one line removed and one added — the namespace. Anything larger means the file was edited beyond that, which the Global Constraints forbid.

- [ ] **Step 10: Run the full suite, boundary test now green**

```bash
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~PackageBoundaryTests"
dotnet test FmpDotNet.slnx
```

Expected, first command: 7 total, 7 passed. Second: `FmpDotNet.Tests` **1,486** passed (1,479 + 7); `FmpDotNet.SmokeTests` 22 passed, 5 skipped. Every one of the 23 `AddFmpTests` cases passes with only its using line changed — that is the compatibility proof for the move.

- [ ] **Step 11: Commit**

```bash
git add -A src tests FmpDotNet.slnx
git commit -m "feat: move AddFmp into FmpDotNet.Extensions.DependencyInjection (#61)

FmpServiceCollectionExtensions.cs was the only file in the core that used IServiceCollection, IConfiguration
or AddHttpClient, and it cost the core Microsoft.Extensions.Http, DependencyInjection.Abstractions and
Options.ConfigurationExtensions. It moves, byte-identical apart from its namespace, into a second package
that takes a ProjectReference to the core plus Http and Configuration.Abstractions. The core keeps
Logging.Abstractions and NodaTime and takes Microsoft.Extensions.Options directly, which every handler and
transport uses and which arrived transitively until now.

PackageBoundaryTests reads the core assembly's compiled references and fails on the commit that lets any of
the four dropped assemblies back in. It was red on three of them before the move.

BREAKING: AddFmp is no longer in the FmpDotNet package. Consumers add FmpDotNet.Extensions.DependencyInjection
and change the using line to FmpDotNet.Extensions.DependencyInjection. No type-forwarding shim: the core
cannot reference the package it would forward to.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE"
```

---

### Task 3: A test project for the new package

`AddFmpTests` tests registration, validation, handler order and reservoir sharing — all properties of the file that moved — and a test project per package matches the package boundary.

**Files:**
- Create: `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj`
- Move: `tests/FmpDotNet.Tests/AddFmpTests.cs` → `tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs` (namespace line changes)
- Modify: `FmpDotNet.slnx`

**Interfaces:**
- Consumes: the `FmpDotNet.Extensions.DependencyInjection` project from Task 2.
- Produces: a third test project the solution-wide `dotnet test` runs, holding the 23 `AddFmp` cases.

- [ ] **Step 1: Create the test project**

Create `tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Tests for the DI package (#61): registration, options binding and validation, handler order and reservoir
       sharing — every one a property of FmpServiceCollectionExtensions, which is the whole package. The core's
       own tests stay in FmpDotNet.Tests, including the two that build a provider through AddFmp to prove an
       endpoint group is registered; those sit inside files about their groups. -->
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- The concrete configuration package: ConfigurationBuilder and AddInMemoryCollection, which every test here
         uses to drive the by-name binding. Nothing under src/ references it, by design. -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NodaTime" Version="3.2.2" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <!-- The core arrives through this reference. No InternalsVisibleTo is needed: everything AddFmp wires, and
         everything these tests inspect (FmpBuckets, TokenBucket.Acquire, IOptions<FmpOptions>), is public. -->
    <ProjectReference Include="..\..\src\FmpDotNet.Extensions.DependencyInjection\FmpDotNet.Extensions.DependencyInjection.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Move the test file and change its namespace**

```bash
git mv tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs
```

Then change line 9 of the moved file from

```csharp
namespace FmpDotNet.Tests;
```

to

```csharp
namespace FmpDotNet.Extensions.DependencyInjection.Tests;
```

Leave line 4's `using FmpDotNet.Extensions.DependencyInjection;` in place. It is now redundant — the enclosing namespace is in scope — but it is harmless, nothing here turns unnecessary-using diagnostics into errors, and keeping it makes the file's dependence on the package legible at the top. The two private handlers (`CountingHandler`, `FailingHandler`) and the `Build`/`BuildWithUpstream` helpers are defined inside the file, so nothing else from `FmpDotNet.Tests` is needed.

- [ ] **Step 3: Add the project to the solution**

`FmpDotNet.slnx` becomes:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/FmpDotNet/FmpDotNet.csproj" />
    <Project Path="src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/FmpDotNet.Tests/FmpDotNet.Tests.csproj" />
    <Project Path="tests/FmpDotNet.SmokeTests/FmpDotNet.SmokeTests.csproj" />
    <Project Path="tests/FmpDotNet.Extensions.DependencyInjection.Tests/FmpDotNet.Extensions.DependencyInjection.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 4: Build and run the suite the way CI runs it**

```bash
dotnet build FmpDotNet.slnx -warnaserror
dotnet test FmpDotNet.slnx --no-build --logger trx -- RunConfiguration.TreatNoTestsAsError=true
```

Expected, three result lines: `FmpDotNet.Extensions.DependencyInjection.Tests` **23** passed; `FmpDotNet.Tests` **1,463** passed (1,486 − 23); `FmpDotNet.SmokeTests` 22 passed, 5 skipped. The `TreatNoTestsAsError` form is the one in `ci.yml`; running it here proves the new project is discovered and gated the same way as the other two. The `.trx` files land under each project's `TestResults/`, which is gitignored.

Confirm the move is a rename:

```bash
git add -A tests FmpDotNet.slnx
git diff --cached -M --name-status -- tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs
git diff --cached -M --stat -- tests/FmpDotNet.Tests/AddFmpTests.cs tests/FmpDotNet.Extensions.DependencyInjection.Tests/AddFmpTests.cs
```

Expected: a `--name-status` line beginning `R` with a score in the high nineties from the old path to the new, and a `--stat` line ending in `| 2 +-` — the namespace line, and nothing else.

- [ ] **Step 5: Commit**

```bash
git add -A tests FmpDotNet.slnx
git commit -m "test: give FmpDotNet.Extensions.DependencyInjection its own test project (#61)

AddFmpTests moves with the file it tests, namespace changed and nothing else: 23 cases covering registration,
validation, handler order and reservoir sharing. FmpDotNet.Tests keeps the two endpoint-group tests that go
through AddFmp, because they sit inside files about their groups. The solution-wide dotnet test picks the new
project up, and TreatNoTestsAsError gates it per project like the other two.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE"
```

---

### Task 4: Publish it, and say so

CI packs the second package; the README tells a consumer which package to add.

**Files:**
- Modify: `.github/workflows/ci.yml` (the `Pack` step, lines 158-159)
- Modify: `README.md:19-22` (Status), `README.md:31` (Usage), `README.md:827-828` (Installing)

**Interfaces:**
- Consumes: the project path `src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj` from Task 2 and the using line `using FmpDotNet.Extensions.DependencyInjection;`.
- Produces: nothing later tasks consume.

- [ ] **Step 1: Add the second pack line**

In `.github/workflows/ci.yml`, the `Pack` step currently reads:

```yaml
      - name: Pack
        run: dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o ./artifacts -p:VersionSuffix=ci.${{ github.run_number }}
```

Replace it with:

```yaml
      # Two explicit lines rather than `dotnet pack FmpDotNet.slnx`, so the workflow names what ships and a third
      # project does not start publishing by being added to the solution. Packing the extensions project builds
      # the core again as its ProjectReference but does not produce a second FmpDotNet.nupkg — it emits a
      # dependency on it, at this same version. The Push step's glob and --skip-duplicate cover both packages,
      # and the symbol upload's glob covers both .snupkg files.
      - name: Pack
        run: |
          dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o ./artifacts -p:VersionSuffix=ci.${{ github.run_number }}
          dotnet pack src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj -c Release -o ./artifacts -p:VersionSuffix=ci.${{ github.run_number }}
```

Do not touch the `name: .NET — build + test` line of the other job.

- [ ] **Step 2: Check the YAML still parses**

Run: `ruby -ryaml -e 'YAML.load_file(".github/workflows/ci.yml"); puts "ok"'`

Expected: `ok`. (Ruby ships with macOS; on a machine without it, `python3 -c 'import yaml,sys; yaml.safe_load(open(".github/workflows/ci.yml")); print("ok")'` does the same if PyYAML is installed.) The workflow itself runs on push in Task 5.

- [ ] **Step 3: Update the README's Status paragraph**

`README.md` lines 19-22 currently read:

```markdown
Every endpoint `Trader.Adapters.MarketData.Fmp` calls is modelled, which is what that adapter's removal was
waiting on — along with the whole `*-bulk` surface and the universe and directory lists. The supporting
machinery is in place too: options and validation, `AddFmp`, the two throttle reservoirs, per-attempt timeouts,
the JSON and CSV pipelines, and a developer disk cache for bulk responses.
```

Replace them with:

```markdown
Every endpoint `Trader.Adapters.MarketData.Fmp` calls is modelled, which is what that adapter's removal was
waiting on — along with the whole `*-bulk` surface and the universe and directory lists. The supporting
machinery is in place too: options and validation, `AddFmp` in the `FmpDotNet.Extensions.DependencyInjection`
package, the two throttle reservoirs, per-attempt timeouts, the JSON and CSV pipelines, and a developer disk
cache for bulk responses.
```

- [ ] **Step 4: Update the Usage block's using line**

`README.md` line 31, inside the `csharp` block under `## Usage`:

```csharp
using FmpDotNet.DependencyInjection;
```

becomes

```csharp
using FmpDotNet.Extensions.DependencyInjection;
```

The rest of the block — `services.AddFmp(configuration);`, `services.AddFmp(o => o.ApiKey = "…");`, `provider.GetRequiredService<FmpClient>()` — is unchanged, because the method and its signatures are.

- [ ] **Step 5: Update Installing and versioning**

`README.md` lines 827-828 currently read:

```markdown
The package is published to this repository's **GitHub Packages** NuGet feed, not to nuget.org. Add the source,
then `dotnet add package FmpDotNet`.
```

Replace them with:

```markdown
Two packages are published to this repository's **GitHub Packages** NuGet feed, not to nuget.org. Add the
source, then `dotnet add package FmpDotNet.Extensions.DependencyInjection`, which brings `FmpDotNet` with it.
`FmpDotNet` is the client, the models and the transports; `FmpDotNet.Extensions.DependencyInjection` is `AddFmp` —
the container wiring, options binding and validation — and nothing else. A consumer with a container of its own
can reference `FmpDotNet` alone. The two are versioned and published together, and everything below applies to
both.
```

The paragraphs that follow (prerelease per push, pin an exact prerelease, how a release is cut, XML docs and `.snupkg` per package) stay as they are; the last sentence above is what makes them read as applying to both.

- [ ] **Step 6: Confirm the generated table is untouched**

Run: `FMPDOTNET_UPDATE_README=1 dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EndpointCoverageTests"`

Then: `git diff --stat README.md`

Expected: the regenerator makes no further change — the diff is the three hand edits from Steps 3-5 and nothing in the endpoint table. This task adds no endpoint, so any table change is a finding to understand before continuing.

- [ ] **Step 7: Commit in two pieces**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: pack and publish FmpDotNet.Extensions.DependencyInjection beside the core (#61)

A second explicit pack line rather than packing the solution, so the workflow names what ships. The push
step's glob, --skip-duplicate and the symbol upload already cover both packages.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE"

git add README.md
git commit -m "docs: name the package AddFmp lives in (#61)

Usage shows the new using line; Installing says to add FmpDotNet.Extensions.DependencyInjection, that it
brings FmpDotNet with it, and that a consumer with its own container can take the core alone; Status names
the package.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE"
```

---

### Task 5: Prove the pair packs and restores, then open the PR

The spec's manual check, plus one consumer-shaped restore that the nuspec alone cannot prove: that adding the extensions package to a fresh project pulls in the core at the same version and `AddFmp` compiles against the packed assemblies.

**Files:** none in the repository. A scratch console app is created outside it and deleted.

**Interfaces:** none.

- [ ] **Step 1: Pack both projects the way CI does**

```bash
rm -rf dist/split
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o dist/split -p:VersionSuffix=ci.0
dotnet pack src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj -c Release -o dist/split -p:VersionSuffix=ci.0
ls dist/split
```

Expected: exactly four files — `FmpDotNet.0.1.0-ci.0.nupkg`, `FmpDotNet.0.1.0-ci.0.snupkg`, `FmpDotNet.Extensions.DependencyInjection.0.1.0-ci.0.nupkg`, `FmpDotNet.Extensions.DependencyInjection.0.1.0-ci.0.snupkg`. The second pack rebuilds the core as a `ProjectReference` but does not write a second core package.

- [ ] **Step 2: Read both nuspecs**

```bash
unzip -p dist/split/FmpDotNet.0.1.0-ci.0.nupkg FmpDotNet.nuspec
unzip -p dist/split/FmpDotNet.Extensions.DependencyInjection.0.1.0-ci.0.nupkg FmpDotNet.Extensions.DependencyInjection.nuspec
unzip -Z1 dist/split/FmpDotNet.Extensions.DependencyInjection.0.1.0-ci.0.nupkg | sort
```

Expected, core dependencies — these three and nothing else:

```xml
<dependency id="Microsoft.Extensions.Logging.Abstractions" version="10.0.9" exclude="Build,Analyzers" />
<dependency id="Microsoft.Extensions.Options" version="10.0.9" exclude="Build,Analyzers" />
<dependency id="NodaTime" version="3.2.2" exclude="Build,Analyzers" />
```

Expected, extensions dependencies — these three and nothing else:

```xml
<dependency id="FmpDotNet" version="0.1.0-ci.0" exclude="Build,Analyzers" />
<dependency id="Microsoft.Extensions.Configuration.Abstractions" version="10.0.9" exclude="Build,Analyzers" />
<dependency id="Microsoft.Extensions.Http" version="10.0.9" exclude="Build,Analyzers" />
```

Both nuspecs carry `<version>0.1.0-ci.0</version>`, `<authors>Herbert Sabanal</authors>`, `<license type="expression">MIT</license>`, `<readme>README.md</readme>` and the same `<repository>` element. The extensions listing contains `README.md`, `LICENSE`, `lib/net10.0/FmpDotNet.Extensions.DependencyInjection.dll` and `lib/net10.0/FmpDotNet.Extensions.DependencyInjection.xml`. Any extra dependency on either side is a defect in Task 2's csproj edits; fix it there, in a fix-up commit, before continuing.

- [ ] **Step 3: Restore the pair into a fresh consumer**

```bash
PKG="$(pwd)/dist/split"
APP="$(mktemp -d)"
dotnet new console -o "$APP" --name SplitCheck
dotnet add "$APP" package FmpDotNet.Extensions.DependencyInjection --version 0.1.0-ci.0 --source "$PKG" --source https://api.nuget.org/v3/index.json
cat > "$APP/Program.cs" <<'EOF'
using FmpDotNet;
using FmpDotNet.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var provider = new ServiceCollection().AddLogging().AddFmp(o => o.ApiKey = "k").BuildServiceProvider();
Console.WriteLine(provider.GetRequiredService<FmpClient>().GetType().Assembly.GetName().Name);
EOF
dotnet run --project "$APP"
grep -A3 '"FmpDotNet/' "$APP/obj/project.assets.json" | head -8
rm -rf "$APP"
```

Expected: `dotnet run` prints `FmpDotNet`. The `grep` shows `FmpDotNet/0.1.0-ci.0` in the consumer's resolved graph, reached through the extensions package, which is the version-coupling claim in the spec made observable. The `"k"` is a placeholder that passes the non-empty check; no request is sent. The two `--source` flags are both needed: the local directory holds the pair, nuget.org holds the Microsoft packages. Delete the scratch app afterwards, as the last line does.

- [ ] **Step 4: Final green, push, open the PR**

```bash
dotnet build FmpDotNet.slnx -warnaserror
dotnet test FmpDotNet.slnx
git status --short
git log --oneline master..HEAD
git push -u origin feat/di-package-61
```

Expected: clean build; 23 + 1,463 unit tests passed and 22 smoke passed with 5 skipped; an empty status; seven commits above master (the spec, the plan, one each for Tasks 1-3, and two for Task 4).

Open the PR with `gh pr create --base master --title "Split AddFmp into FmpDotNet.Extensions.DependencyInjection (#61)"`. The body covers, in this order:

1. What moved and what did not — one file, one changed line, verified by `git diff -M --stat` showing `2 +-` twice (Task 2 Step 9, Task 3 Step 4).
2. The dependency cut, as the two nuspec dependency blocks from Step 2, pasted verbatim.
3. The boundary test: red on three assemblies before the move, green after, with the positive control.
4. The breaking change and what a consumer does about it: add the package, change the using line. Note that `trader` pins an exact prerelease and is unaffected until it bumps.
5. The consumer restore from Step 3: the printed assembly name and the resolved core version.
6. That `docs/host-registration-design` is revised against the new package in a follow-up, per the spec's "Relation to the host-registration design" section.

Reference `#61` with "Closes #61" and end the body with `https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.

Wait for **`.NET — build + test`** to go green before merging. On the first master run after merge, the Publish job's Pack step should log two `Successfully created package` lines per package (nupkg and snupkg) and the Push step should push two nupkgs.

---

## Self-Review

**Spec coverage.** Every section of the design maps to a task. The decision and "What moves, what stays" → Task 2 Steps 3-6, with the rename check in Step 9 enforcing "unchanged apart from the namespace". The Dependencies table → Task 2 Steps 3 and 5, verified by the nuspec read in Task 5 Step 2. "Shared packaging metadata" → Task 1, all of the table's rows, with the before/after nuspec diff as the test. Tests → Task 2 Step 1 (`PackageBoundaryTests`), Task 2 Step 8 (the two test projects' references and the direct `Microsoft.Extensions.Configuration`), Task 3 (the moved `AddFmpTests`). CI → Task 4 Step 1. Documentation → Task 4 Steps 3-5 and Task 2 Step 6 (the cref) and Step 3 (the csproj `Description`). Compatibility → the `BREAKING` paragraph in Task 2's commit and item 4 of the PR body. The spec's manual pack check → Task 5 Steps 1-2. The "Relation to the host-registration design" section is a follow-up by the spec's own words and appears in the plan only as PR-body item 6, correctly.

**Two places the plan goes beyond the spec, deliberately.** First, `PackageBoundaryTests` carries a three-case positive control the spec did not ask for: a `DoesNotContain` over an empty list passes, so without the control a wrong `typeof` would make the guard vacuous. Second, Task 5 Step 3 restores the packed pair into a scratch consumer, which is the only way to observe the spec's version-coupling claim rather than read it off a nuspec. Both cost minutes and neither touches the shipped code.

**One place the plan changes a second line in a moved file.** The spec says `AddFmpTests` passes "with only its using line changed". The plan also changes its namespace line, from `FmpDotNet.Tests` to `FmpDotNet.Extensions.DependencyInjection.Tests`, so the assembly's namespace matches its project. The tests' behaviour is unaffected and the rename check in Task 3 Step 4 still shows `2 +-`, because the using line was already changed in Task 2 and is kept as it is.

**One thing noticed and left alone.** The moved file's comment on `Configure(IHttpClientBuilder)` says the chain is "throttle → timeout → network", which has been stale since #44 made it retry → throttle → timeout. Fixing it here would break the byte-identical rename the Global Constraints require and that makes the move reviewable as a move. It is a one-line follow-up and should be filed as such after this lands.

**Placeholder scan.** No `TBD`, no `TODO`, no "similar to Task N". Every code step carries the full content. Every file path in the File Structure table appears in a task.

**Type and name consistency.** `FmpDotNet.Extensions.DependencyInjection` is the package id, assembly name, root namespace, source directory, test-project prefix and the using line in Tasks 2, 3, 4 and 5 — checked by reading each occurrence. `PackageBoundaryTests` is named the same in Task 2 Steps 1, 2 and 10 and in the Task 2 commit. The `-p:VersionSuffix=ci.0` in Task 5 produces `0.1.0-ci.0`, which is the version named in the expected nuspecs, the `dotnet add package --version` and the `grep`. The test counts chain: 1,479 → 1,486 (+7 boundary cases: four negative, three positive) → 1,463 (−23 moved) and 23 in the new project, 1,486 in total, unchanged from the end of Task 2.

**Facts verified against the tree rather than assumed.** `AddFmpTests` defines its own `CountingHandler`, `FailingHandler`, `Build` and `BuildWithUpstream` and uses nothing from `StubHandler.cs` or `Binding.cs`, so it moves alone. `FmpBuckets.Standard` and `TokenBucket.Acquire` are public, so the new test project needs no `InternalsVisibleTo`. The three test files that use `ConfigurationBuilder` are `AddFmpTests`, `DirectoryEndpointsTests` and `CompanyScreenerTests`; nothing in `tests/` calls `ConfigurationBinder` (`Get<T>`, `Bind(`, `Configure<T>(IConfiguration)`), so `Microsoft.Extensions.Configuration` is the only direct addition the tests need. The core's only Microsoft.Extensions namespaces outside the moved file are `Options` and `Logging`. The 23-case count came from running the filter, not from counting attributes (14 facts plus one theory with nine rows).
