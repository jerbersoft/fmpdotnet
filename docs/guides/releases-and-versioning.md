# Releases and Versioning

## Where the packages live

Two packages — `FmpDotNet`, the client, and `FmpDotNet.Extensions.DependencyInjection`, the registration surface,
which depends on it — are published together to **this repository's GitHub Packages NuGet feed**, not nuget.org.
Everything on this page applies to both.

```
https://nuget.pkg.github.com/jerbersoft/index.json
```

GitHub Packages requires authentication for every restore, including for public packages — there is no anonymous read.
Setup is in **[Getting Started](getting-started.md)**; the reasoning for not being on nuget.org is in the
**[FAQ](faq.md)**.

## Every push to `master` publishes a prerelease

```
0.1.0-ci.7
0.1.0-ci.8
0.1.0-ci.9
…
```

The suffix is the **CI run number**.

**That shape is forced by the feed, not chosen.** GitHub Packages *refuses to overwrite* an existing NuGet
version, so a fixed `0.1.0` would publish once and then fail the publish job on every subsequent push — a red CI
that means nothing.

Two consequences worth knowing:

* **Run numbers never reset**, so versions are monotonic.
* **A re-run keeps its number**, which is why the push uses `--skip-duplicate` — re-running a green build should
  be a no-op, not a failure.

## Pin the exact prerelease

```xml
<PackageReference Include="FmpDotNet.Extensions.DependencyInjection" Version="0.1.0-ci.79" />
<!-- only if the core is referenced directly as well — and then at the same version -->
<PackageReference Include="FmpDotNet" Version="0.1.0-ci.79" />
```

**Do not float.** A floating reference to a feed that gains a version on every push is a build that changes
underneath you with no commit of yours.

Pinning also makes *"which SDK did this commit build against"* answerable from your own git history.

**Pin both to the same version** if you reference both. The extensions package depends on the core as a floor —
`FmpDotNet >= 0.1.0-ci.N` — not an exact version, so NuGet will pair an older `AddFmp` with a newer core, and that
pairing breaks the first time the core reshapes something the older wiring constructs. The `FmpClient`
constructor change in #65 is the live example.

## Cutting a release

A release is cut by packing **without a suffix**:

```bash
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o ./artifacts
```

giving a plain `0.1.0`. **NuGet orders a release above every prerelease of the same version**, so a hand-cut build
always supersedes the CI ones it follows — `0.1.0` outranks `0.1.0-ci.999`.

**No release has been cut yet.** Everything published so far is a `ci.N` prerelease. See the
**[Changelog](../changelog.md)**.

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

The `.snupkg` is **not** pushed to the feed. That is not a preference — GitHub Packages runs no symbol server, and
its service index advertises `PackagePublish/2.0.0` with no `SymbolPackagePublish` resource of any version. Left
implicit, `dotnet nuget push` looks for that resource, fails to find it, and warns on every publish about
something that cannot be fixed. So the push passes `--no-symbols` and the `.snupkg` is uploaded as a **workflow
artifact** instead, retained 90 days.

Loading the PDBs from that artifact gives a debugger the exact source for the binary it is stepping through. If
the package ever goes to nuget.org, which does run a symbol server, that flag comes off.

## Deterministic builds

The project sets `ContinuousIntegrationBuild` when `GITHUB_ACTIONS` is true, which normalises source paths — so
**the same commit packs to the same bytes on any runner**. Local packs are deliberately not deterministic; only
the CI-produced package is.

## How publishing is authorised

**No PAT.** The publish job's `GITHUB_TOKEN` can write to its own repository's package registry given
`packages: write`, which is granted **only on that job** — the workflow default is `contents: read`.

Consumers in GitHub Actions read the feed with **their own** `GITHUB_TOKEN`. That works because the package grants
read access to the consuming repository under its *Manage Actions access* setting — a one-off grant in the package
settings, not a secret in either repository. It is the only manual step in the chain, and it is **per package**:
the grant on `FmpDotNet` does not carry over to `FmpDotNet.Extensions.DependencyInjection`, which needs its own.

## The publish job's guards

* **`master` only**, and never on a `pull_request` event.
* **After `.NET — build + test` is green.** A package that exists is a package someone can restore, so publishing
  an unverified one is worse than publishing nothing.
* **`--skip-duplicate`**, so a re-run is a no-op.
* `dotnet pack` builds Release from scratch rather than reusing the test job's output — jobs run on separate
  runners, and shipping an artifact between them to save one compile would cost more in moving parts than it
  saves.

## Finding a version

The repository's [Packages page](https://github.com/jerbersoft/fmpdotnet/packages) lists every published version.
The run number in a version maps directly to a CI run, so `0.1.0-ci.42` is run 42 — and its commit is on that
run's page.

## Reference

* [Installing and versioning](../../README.md#installing-and-versioning)
