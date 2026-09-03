# Releases and Versioning

## Where the packages live

Two packages — `FmpDotNet`, the client, and `FmpDotNet.Extensions.DependencyInjection`, the registration surface,
which depends on it — are published together to **nuget.org**. Everything on this page applies to both.

* [nuget.org/packages/FmpDotNet](https://www.nuget.org/packages/FmpDotNet)
* [nuget.org/packages/FmpDotNet.Extensions.DependencyInjection](https://www.nuget.org/packages/FmpDotNet.Extensions.DependencyInjection)

Restore is anonymous: no source to add, no token, no `nuget.config`. Setup is in
**[Getting Started](getting-started.md)**.

### The feed that used to be here

Before 0.9.0 the packages went to this repository's GitHub Packages feed, which required authentication for every
restore — public packages included. That feed is **frozen, not deleted**: `0.1.0-ci.89` is the newest version on
it, everything already there stays restorable for anyone pinned to it, and nothing new will be published there.
The versions are on the repository's [Packages page](https://github.com/jerbersoft/fmpdotnet/packages). Move a pin
across at your convenience; a `0.1.0-ci.N` pin does not stop working.

## Two streams

| Stream | Looks like | Cut by |
|---|---|---|
| **Release** | `0.9.0` | publishing a GitHub Release on the matching `v0.9.0` tag |
| **Prerelease** | `0.9.0-ci.91` | every CI run that passes on `master` |

**Publishing a GitHub Release is the only way a stable version can be produced.** The workflow refuses to pack
without a suffix on any other trigger, so a stable version cannot arrive by accident.

## Every passing push to `master` publishes a prerelease

The suffix is the **CI run number** of the run whose tests passed, so a version maps back to the run page that
produced it, and to the commit on that page.

* **Run numbers never reset**, so versions are monotonic.
* **A re-run keeps its number**, which is why the push uses `--skip-duplicate` — re-running a green build should
  be a no-op, not a failure.
* **NuGet orders a prerelease below the release of the same version.** `0.9.0` outranks `0.9.0-ci.999`, so a
  prerelease never overtakes a release, and `dotnet add package` ignores prereleases unless you pass
  `--prerelease` or name one exactly.
* **nuget.org refuses to overwrite an existing version**, so the suffix is not decoration: a fixed version would
  publish once and fail every push after it.

## Pin, and pin both to the same version

```xml
<PackageReference Include="FmpDotNet.Extensions.DependencyInjection" Version="0.9.0" />
<!-- only if the core is referenced directly as well — and then at the same version -->
<PackageReference Include="FmpDotNet" Version="0.9.0" />
```

**Pin both** if you reference both. The extensions package depends on the core as a floor — `FmpDotNet >= 0.9.0` —
not an exact version, so NuGet will pair an older `AddFmp` with a newer core, and that pairing breaks the first
time the core reshapes something the older wiring constructs. The `FmpClient` constructor change in #65 is the
live example.

Nothing published is ever removed. nuget.org has **no delete** — unlisting hides a version from search and from
`dotnet add package`, and an existing pin still restores it.

## Cutting a release

1. **Land everything the release contains**, this page's sibling [Changelog](../changelog.md) included.
2. **Tag it and publish a Release.**

   ```bash
   git tag v0.9.0 && git push origin v0.9.0
   ```

   Then publish a GitHub Release on that tag — that event is what triggers the publish. The run asserts the tag
   matches what the tree packs, so `v0.9.0` against a tree still reading `0.8.0` fails before anything is pushed.
3. **Bump `VersionPrefix`** in `src/Directory.Build.props` to the next version, on `master`, straight afterwards.

**Step 3 is not optional.** `VersionPrefix` is what CI packs, so leaving it at `0.9.0` after 0.9.0 has shipped
publishes `0.9.0-ci.N` builds that NuGet orders *below* the release already out — permanently invisible, and
occupying permanent public version slots, because nothing can be deleted. Bumping to `0.10.0` puts the next
prerelease above `0.9.0` and below `0.10.0`, which is where the work belongs.

## Stability policy

**Until 1.0, treat a minor bump as potentially breaking.**

The surface is still being shaped by what the live API turns out to do, and **two releases have already removed
public members** after measurement showed they were the wrong shape:

* one error channel — every failure throws, nothing returns null for it;
* the deletion of members that existed only to serve other members.

Both are in the **[Changelog](../changelog.md)**, marked breaking. Breaking commits carry `!` — `refactor!:`.

Once 1.0 lands, ordinary semver applies. It has not landed because the endpoint surface is still growing and the
measurement that would justify freezing a shape has not been done for all of it.

## What ships in a package

* The **assembly**, targeting `net10.0`.
* **XML documentation** — the type and member docs carry a lot of the measured behaviour, so IntelliSense tells
  you about the 4000-row truncation at the call site rather than in a document you have to find.
* A matching **`.snupkg`** carrying the PDBs, with **Source Link**. A debugger steps from your code into this
  SDK's source **at the exact commit the binary was built from**.

The `.snupkg` is pushed to **nuget.org's symbol server** with the package, so debugging into the SDK needs nothing
from this repository. That is a change of feed rather than of intent: GitHub Packages runs no symbol server — its
service index advertises `PackagePublish/2.0.0` and no `SymbolPackagePublish` resource of any version — so the old
push passed `--no-symbols` and uploaded the `.snupkg` as a 90-day workflow artifact instead. Both of those are
gone.

## Deterministic builds

The project sets `ContinuousIntegrationBuild` when `GITHUB_ACTIONS` is true, which normalises source paths — so
**the same commit packs to the same bytes on any runner**. Local packs are deliberately not deterministic; only
the CI-produced package is.

## How publishing is authorised

**No API key, and no secret in this repository.** The publish job asks GitHub for a short-lived OIDC token,
`NuGet/login` exchanges it with nuget.org for an API key valid for **one hour**, and that key exists nowhere else.
nuget.org matches the token against a **Trusted Publishing** policy naming the repository owner, the repository
and the workflow file, scoped by a package-id glob.

Two consequences worth knowing:

* **The policy names `publish.yml` specifically.** Renaming that file breaks publishing until the policy is
  updated, and the failure is a 403 at push time rather than anything at build time. It is also why publishing is
  its own workflow and never a reusable one called from CI: NuGet documents the field without saying whether a
  caller or a callee is matched, so the workflow that pushes is always the one the policy names.
* **A package id the policy does not cover is refused**, even for the account that owns every other id. That is
  why the policy carries the *new packages* scope and a `FmpDotNet*` glob rather than a list of the ids that
  happened to exist when it was written.

## The publish job's guards

* **The version comes from what was packed**, read off the `.nupkg` filename, never a literal in the workflow.
* **A release's tag must match it**, or the run fails before pushing anything.
* **`PACKAGES` governs the output in both directions.** A packed `.nupkg` the list does not name fails the run, and
  so does a listed id with no file — so a third package is a decision somebody records rather than one that
  silently ships or silently does not. `PublishWorkflowTests` pins that list against the projects under `src/`.
* **Pushed by name, in dependency order** — the core first, then the package that depends on it — so a refused
  push never leaves a package on the feed declaring a dependency on one that is not there.
* **`--skip-duplicate`**, so re-running a partially failed release completes rather than aborting on what already
  went out.
* **Verified against the public feed** afterwards: the run polls for fifteen minutes until both ids list the
  version. A green push exit code is not evidence that a version is live — nuget.org validates after the push
  returns.
* **Prereleases run the same steps as releases**, so the release path is exercised on every push to `master`
  rather than for the first time at a release.

## Finding a version

Every version is listed on the two package pages at the top of this page. A prerelease's suffix is the CI run
number, so `0.9.0-ci.91` is CI run 91 and its commit is on that run's page.

## Reference

* [Installing and versioning](../../README.md#installing-and-versioning)
