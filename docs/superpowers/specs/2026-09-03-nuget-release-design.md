# The first release: 0.9.0 on nuget.org

**Date:** 2026-09-03
**Status:** approved in chat, 2026-09-03 (four sections, four scoping answers)

## Goal

Publish `FmpDotNet` and `FmpDotNet.Extensions.DependencyInjection` to **nuget.org**, cut **0.9.0** as the first
release this project has ever tagged, retire the GitHub Packages feed as a publishing target, and rewrite the
eleven files whose documentation currently argues the opposite position.

## What this is not

* **Not 1.0.** The pre-1.0 caveat stays: until 1.0, a minor bump may break. What changes is the *feed*, not the
  stability claim.
* **Not a change to the public surface.** No type, member or option moves in this work. If the surface needs a
  change, it belongs to a different issue and a different version.
* **Not a deletion of anything already published.** The `0.1.0-ci.N` versions on GitHub Packages stay
  restorable — a private consumer has commits pinned to them, and a deleted package version breaks those builds
  with no recovery.
* **Not a package icon, and not an id prefix reservation.** Both are available later and neither gates shipping.
* **Not a rewrite of guide prose** beyond the passages the feed change makes false.
* **No change to `docs.yml`, `smoke.yml`, or the shape of the documentation site.**

## Global constraints

Copied verbatim into the plan; every task's requirements include them.

* **Both ids are free on nuget.org.** Verified 2026-09-03: `api.nuget.org/v3-flatcontainer/fmpdotnet/index.json`
  and the `.extensions.dependencyinjection` id both return 404, and a search for `fmpdotnet` returns zero hits.
* **nuget.org never deletes a version.** Unlisting hides it from search and from `dotnet add package`; an existing
  pin still restores it. Every version this design publishes is permanent and public.
* **nuget.org refuses to overwrite an existing version** — the same constraint GitHub Packages imposes. The
  `ci.<run number>` suffix therefore survives the move unchanged; it was never a GitHub Packages quirk.
* **NuGet orders every prerelease of a version below the release of that version.** `0.9.0` outranks
  `0.9.0-ci.999`.
* **A stable version can only be produced by publishing a GitHub Release.** No other trigger in this design is
  able to pack without a suffix. This is a property of the workflow, not a convention.
* **Prose lines wrap at 120 columns.** Table rows and unbreakable URLs are the standing exemptions.
* **Repository workflow:** issue → branch → conventional commits referencing the issue → PR → local
  `git merge --no-ff` → push → delete both branches.

## 1. The release model

### The feed

nuget.org becomes the only publishing target. `ci.yml`'s `Publish to GitHub Packages` job is deleted, along with
its `packages: write` permission and the comments arguing for it.

The existing GitHub Packages versions are **frozen, not removed**. `0.1.0-ci.89` — the version the last master
push produced — remains the newest on that feed, every version already there stays restorable by anyone pinned to
it, and the documentation says so once, in `releases-and-versioning.md`, rather than dropping the feed silently.

### Two version streams

| Stream | Version | Produced by |
| --- | --- | --- |
| Prerelease | `<VersionPrefix>-ci.<CI run number>` | every successful **CI** run on `master` |
| Release | `<VersionPrefix>` | publishing a GitHub Release on a `vX.Y.Z` tag |

The run number comes from the *CI* run, not from the publishing workflow's own, so the numbering continues
unbroken from `0.1.0-ci.89` and a version still maps to the CI run page that produced it.

### The rule this scheme has never needed before

**Cutting a release is two steps: tag it, then bump `VersionPrefix`.**

Once `0.9.0` exists, CI must not keep publishing `0.9.0-ci.N` — NuGet orders those below `0.9.0`, so they would be
permanently invisible to anyone asking for the latest version, while still consuming permanent public version
slots. So the release is followed by a commit moving `VersionPrefix` to `0.10.0`, and CI's next prerelease is
`0.10.0-ci.N`: above `0.9.0`, below `0.10.0`. This is what the sibling `databentodotnet` does, and why its props
read `0.10.0` while its newest tag is `v0.10.0`.

### Symbols

nuget.org runs a symbol server. `--no-symbols` comes off, `dotnet nuget push` sends each `.snupkg` after its
`.nupkg` succeeds, and the 90-day `Upload symbol package` artifact step — which existed only because GitHub
Packages had nowhere to put symbols — is deleted.

## 2. The pipeline

### One entry workflow, three triggers

`.github/workflows/publish.yml` holds every step. **It is never called as a reusable workflow.**

NuGet's Trusted Publishing policy binds to a *workflow file*, and neither the NuGet documentation nor the
`NuGet/login` action states whether that is matched against the entry workflow (`workflow_ref`) or a called one
(`job_workflow_ref`). Checked 2026-09-03; the documentation specifies the field and the `.github/workflows/`-less
file-name form, and says nothing about reusable workflows. Rather than bet the release path on an undocumented
claim, this design keeps `publish.yml` as the entry workflow in every case, so one policy covers everything and
the question never arises.

```yaml
on:
  release:
    types: [published]          # the only path that can publish a stable version
  workflow_run:
    workflows: [CI]
    types: [completed]
    branches: [master]          # prereleases, gated on the tests that just passed
  workflow_dispatch:
    inputs:
      suffix:                   # required, so a manual run cannot produce a stable version
        required: true
```

* The job runs when `github.event_name != 'workflow_run' || github.event.workflow_run.conclusion == 'success'`.
  An unverified package is worse than no package.
* Checkout takes `github.event.workflow_run.head_sha` when present and the default ref otherwise, so a prerelease
  is built from the commit CI actually tested and a release from the tag.
* `concurrency: { group: publish, cancel-in-progress: false }` — a publish that has started is never cancelled.
* Job permissions are `contents: read` and `id-token: write`; `timeout-minutes: 20`, above the verification's
  15-minute ceiling.
* `workflow_dispatch` exists because a `workflow_run` trigger does not fire for runs that started before the
  workflow file reached the default branch — so the first prerelease after the merge may need forcing once.

### The guards

`PACKAGES` is an ordered environment variable — `FmpDotNet FmpDotNet.Extensions.DependencyInjection` — and the
order is load-bearing.

1. **Pack both projects by name**, `-c Release`, into `./artifacts`, never `dotnet pack` over the solution. A
   third project starts shipping by being named here, not by being added to `FmpDotNet.slnx`.
2. **Resolve the version from what was packed** — the single `artifacts/FmpDotNet.*.nupkg` — never from a literal
   in the workflow. Exactly one match is required; zero or many fails the run.
3. **On a release, assert the tag matches**: `github.ref` must equal `refs/tags/v<resolved version>`. Tagging
   `v0.9.0` against a tree whose `VersionPrefix` still reads `0.8.0` fails here rather than publishing a version
   nobody asked for.
4. **Partition the output against `PACKAGES`.** A `.nupkg` the list does not name fails the run; an id the list
   names with no file fails it too. The failure message says to add the id to `PACKAGES` or set `IsPackable`
   false, so a new package arrives as a recorded decision.
5. **Push by name, in `PACKAGES` order** — the core first, then the package that depends on it. If the core's push
   is refused, nothing has gone out declaring a dependency on a package that is not there. `--skip-duplicate`, so
   re-running a partially failed release completes rather than aborting on what already went out. A 403 gets an
   error message naming the Trusted Publishing policy page, because that is what a 403 here almost always means.
6. **Verify against the public feed.** Poll `v3-flatcontainer/<id>/index.json` for the packed version, every 30
   seconds for 15 minutes, and fail if either id has not appeared. A green push exit code is not evidence that a
   version is live; nuget.org validation runs after the push returns.

The sibling's `FIRST_PUBLISH` list and its preflight script are deliberately **not** ported. Both exist to work
out a push order across five packages; with two in a fixed dependency order, the order is a constant.

### The credential

`NuGet/login@v1` with `user: jerbersoft`, exchanging the job's OIDC token for a one-hour API key. No stored
secret, nothing to rotate.

**Precondition, outside the working tree and outside this repository.** One Trusted Publishing policy at
<https://www.nuget.org/account/trustedpublishing>, owned by the `jerbersoft` user:

| Field | Value |
| --- | --- |
| Repository owner | `jerbersoft` |
| Repository | `fmpdotnet` |
| Workflow file | `publish.yml` |
| Environment | empty |
| Scopes | publish **new packages** and new versions |
| Glob | `FmpDotNet*` |

The **new packages** scope and the glob are what let one policy cover two ids that do not exist yet. Without
them, the first push returns 403 partway through — the sibling found this out at #103 by publishing four packages
and being refused the fifth.

## 3. The documentation

Eleven files carry the retired position. The character of the change is that a class of explanation *disappears*
rather than being reworded: authenticating a restore stops being a thing a consumer has to understand.

| File | What changes |
| --- | --- |
| `docs/guides/getting-started.md` | Step 1 collapses from a `nuget.config` with `packageSourceCredentials`, a PAT scoped `read:packages` and two environment variables to a single `dotnet add package` line. |
| `docs/guides/releases-and-versioning.md` | Rewritten. The feed, the two streams, the release procedure end to end, the post-release `VersionPrefix` bump and what skipping it costs, permanence and unlisting, trusted publishing, the guards, symbols now shipped, and where the frozen GitHub Packages versions live. |
| `docs/guides/faq.md` | "Why is the package not on nuget.org?" is **deleted**, and "Why is 0.9.0 not 1.0?" takes its place. "Why does every push publish a new version?" keeps its answer, re-argued from nuget.org's immutability. |
| `docs/guides/troubleshooting.md` | The 401/403-on-restore entries go; an entry on prereleases needing `--prerelease` replaces them. |
| `README.md` | "Installing and versioning" rewritten to match; two NuGet version badges under the title, as reference-style links so no line passes 120 columns. |
| `docs/changelog.md` | The "No release has been cut" banner goes. `[Unreleased]` becomes a `## [0.9.0]` section dated the day the Release is published, with the move to nuget.org as its own **Changed** entry, and a fresh empty `[Unreleased]` opens above it. |
| `docs/index.md` | The install line and the feed sentence. |
| `docs/guides/development.md` | The one feed mention. |
| `SECURITY.md` | Two mentions: where packages come from, and what a consumer restores. |
| `src/Directory.Build.props` | `VersionPrefix` to `0.9.0`; the versioning comment rewritten around nuget.org's immutability and the post-release bump rule. |
| `.github/workflows/ci.yml` | The `publish` job, its `packages: write` permission and its header paragraphs deleted. |

## 4. Testing

Two tests in `tests/FmpDotNet.Tests/PublishWorkflowTests.cs`, both reaching the repository through the existing
`RepositoryLayout.Root()`, both mirroring how `DocsSiteTests` guards the documentation site — set equality against
what is on disk, no YAML dependency, nothing that needs the workflow to run.

* **`EveryPackableProjectIsNamedInThePublishWorkflow`** — the `PackageId` of every `src/*/*.csproj` equals the set
  in `publish.yml`'s `PACKAGES` line. A package added to `src/` fails on its own commit rather than at the
  release that skips it, and a `PACKAGES` entry for a project that no longer exists fails too.
* **`NothingPointsAtTheRetiredFeed`** — no tracked file under `docs/` (excluding `docs/superpowers/`, which is a
  record of decisions and keeps its history), nor `README.md`, `CONTRIBUTING.md` or `SECURITY.md`, contains
  `nuget.pkg.github.com`. Prose may name GitHub Packages; what it may not do is hand a reader a source to add.

The full suite — `dotnet test FmpDotNet.slnx` — stays green, and `dotnet docfx docs/docfx.json --warningsAsErrors`
stays at zero warnings, since this work rewrites pages the site builds.

## 5. The order it lands in

Two steps are irreversible and one is not mine to take.

1. **The owner creates the Trusted Publishing policy** described above. Nothing publishes before it exists.
2. **One branch, one PR**: `publish.yml`, the `ci.yml` deletions, `VersionPrefix`, the two tests, and all eleven
   documentation files. Green, then merged.
3. **The merge publishes `0.9.0-ci.<N>` to nuget.org.** The first public artefact is deliberately a prerelease: it
   proves the credential, both new ids, the push order and the feed verification while no stable version yet
   exists to be stuck with. If it fails, nothing stable is out and the fix is a normal commit.
4. **The owner gives an explicit go-ahead**, and only then: tag `v0.9.0`, publish the GitHub Release, and
   `publish.yml` pushes `0.9.0` with its symbols. nuget.org has no delete, so this step is never automatic and
   never inferred from an earlier approval.
5. **A short follow-up PR** bumps `VersionPrefix` to `0.10.0` and opens a fresh `[Unreleased]`.

## 6. Risks

| Risk | Mitigation |
| --- | --- |
| The policy is missing or mis-scoped — a 403 partway through the push. | The prerelease at step 3 hits it first, with nothing stable published; the push step's error message names the policy page and the ids already pushed. |
| `workflow_run` does not fire for the merge that introduces `publish.yml`. | `workflow_dispatch` with a required `suffix` input forces the first prerelease, and cannot produce a stable version. |
| nuget.org validation lags and the verification times out on a push that actually worked. | The step names the profile page and says a re-run will be refused as a duplicate once the versions appear — a timeout is a report, not a reason to push again. |
| A consumer restoring from GitHub Packages sees no new versions. | The feed is frozen, not deleted; `releases-and-versioning.md` says where it stops and what replaces it. |
| `VersionPrefix` is not bumped after the release, so CI publishes invisible `0.9.0-ci.N` builds. | Step 5 is part of this issue's checklist, not a follow-up someone might not open. |

## Definition of done

- [ ] Trusted Publishing policy exists at nuget.org for `jerbersoft/fmpdotnet`, workflow `publish.yml`, glob
      `FmpDotNet*`, new-packages scope
- [ ] `.github/workflows/publish.yml` implements the three triggers and all six guards
- [ ] `ci.yml` no longer publishes anything and no longer requests `packages: write`
- [ ] `src/Directory.Build.props` reads `VersionPrefix` `0.9.0`, with the comment rewritten
- [ ] `PublishWorkflowTests` passes both tests; `dotnet test FmpDotNet.slnx` green
- [ ] All eleven documentation files updated; `dotnet docfx docs/docfx.json --warningsAsErrors` at zero warnings
- [ ] Merged, and `0.9.0-ci.<N>` is live on nuget.org for both ids
- [ ] With the owner's explicit go-ahead: `v0.9.0` tagged, the Release published, `0.9.0` and both `.snupkg`
      live on nuget.org
- [ ] `VersionPrefix` bumped to `0.10.0` and a fresh `[Unreleased]` opened
