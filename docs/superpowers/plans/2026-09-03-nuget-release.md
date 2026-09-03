# The First Release — 0.9.0 on nuget.org — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Publish `FmpDotNet` and `FmpDotNet.Extensions.DependencyInjection` to nuget.org, cut 0.9.0 as the first
tagged release, retire GitHub Packages as a publishing target, and rewrite every page that documents the old feed.

**Architecture:** One workflow, `.github/workflows/publish.yml`, is the only path to nuget.org. It is the *entry*
workflow on all three of its triggers — a published GitHub Release (stable), a passing CI run on `master`
(prerelease), and `workflow_dispatch` with a required suffix — because nuget.org's Trusted Publishing policy binds
to a workflow file by name and the documentation does not say whether a reusable workflow's caller or callee is
matched. The credential is a one-hour OIDC key, never a stored secret. Six guards stand between `dotnet pack` and
a published version, and two xunit tests pin the parts of the arrangement that live in files rather than in the run.

**Tech Stack:** GitHub Actions, `NuGet/login@v1` (OIDC Trusted Publishing), .NET 10 SDK, `dotnet pack` /
`dotnet nuget push`, xunit, DocFX 2.78.5 for the documentation site the changed pages build into.

**Spec:** `docs/superpowers/specs/2026-09-03-nuget-release-design.md`

**Issue:** #73 — every commit references it.

## Global Constraints

Copied from the spec. Every task's requirements include these.

* **nuget.org never deletes a version.** Unlisting hides it from search and from `dotnet add package`; an existing
  pin still restores it. Every version this work publishes is permanent and public.
* **nuget.org refuses to overwrite an existing version**, exactly as GitHub Packages does. The `ci.<run number>`
  suffix therefore survives the move unchanged.
* **NuGet orders every prerelease of a version below the release of that version.** `0.9.0` outranks
  `0.9.0-ci.999`.
* **A stable version can only be produced by publishing a GitHub Release.** No other trigger may pack without a
  suffix; this is enforced in the workflow, not by convention.
* **Both package ids are free on nuget.org**, verified 2026-09-03 (flat container 404 for each, zero search hits).
* **The existing GitHub Packages versions are frozen, never deleted.** `0.1.0-ci.89` is the newest; a private
  consumer has commits pinned to earlier ones.
* **Not 1.0.** The pre-1.0 caveat — a minor bump may break — stays in every page that carries it today.
* **No change to the public surface**, to `docs.yml`, or to `smoke.yml`.
* **Prose wraps at 120 columns.** Table rows and unbreakable URLs are the standing exemptions, and in YAML so
  are `::error::` annotation strings — an annotation wrapped across lines stops being an annotation — and command
  lines whose length is one unbreakable path.
* **Commit messages** are conventional, reference `(#73)`, and end with
  `Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.
* **Never paste or echo an API key**, including inside a URL.

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `.github/workflows/publish.yml` | **Create.** The only path to nuget.org: three triggers, six guards, OIDC credential. | 1 |
| `.github/workflows/ci.yml` | **Modify.** Delete the `publish` job (lines 113–191) and its `packages: write`. CI becomes build + test. | 1 |
| `src/Directory.Build.props` | **Modify.** `VersionPrefix` `0.1.0` → `0.9.0`; the versioning comment rewritten around nuget.org. | 1 |
| `tests/FmpDotNet.Tests/PublishWorkflowTests.cs` | **Create.** Pins `PACKAGES` against the projects under `src/` (task 1) and pins that nothing hands a reader the retired feed URL (task 3). | 1, 3 |
| `README.md` | **Modify.** Two NuGet badges; "Installing and versioning" rewritten. | 2 |
| `docs/index.md` | **Modify.** The Install section and the last paragraph of Status. | 2 |
| `docs/guides/getting-started.md` | **Modify.** Steps 1 and 2 collapse into one; five steps instead of six. | 2 |
| `docs/guides/troubleshooting.md` | **Modify.** The 401 entry goes; the "cannot find package" entry is rewritten. | 2 |
| `docs/guides/faq.md` | **Modify.** "Why not nuget.org?" is replaced by "Why is 0.9.0 not 1.0?"; the per-push entry is re-argued. | 2 |
| `docs/guides/releases-and-versioning.md` | **Modify.** Rewritten end to end — the largest single change. | 3 |
| `docs/changelog.md` | **Modify.** The banner, a fresh `[Unreleased]`, and a `[0.9.0]` section. | 3 |
| `docs/guides/development.md` | **Modify.** The workflow table gains Publish and loses the publish clause on CI. | 3 |
| `SECURITY.md` | **Modify.** The version example and the supported-versions table. | 3 |

---

### Task 1: The publish workflow, and the test that pins what it ships

**Files:**
- Create: `.github/workflows/publish.yml`
- Create: `tests/FmpDotNet.Tests/PublishWorkflowTests.cs`
- Modify: `.github/workflows/ci.yml` (delete lines 113–191)
- Modify: `src/Directory.Build.props` (the `VersionPrefix` property group and the comment above it)

**Interfaces:**
- Consumes: `RepositoryLayout.Root()` from `tests/FmpDotNet.Tests/RepositoryLayout.cs` — walks up from
  `[CallerFilePath]` to the directory holding `FmpDotNet.slnx` and returns its full path.
- Produces: `.github/workflows/publish.yml` with an `env:` block whose `PACKAGES:` line reads
  `FmpDotNet FmpDotNet.Extensions.DependencyInjection` — Task 3 adds a second test to the same test class.

- [ ] **Step 1: Write the failing test**

Create `tests/FmpDotNet.Tests/PublishWorkflowTests.cs`:

```csharp
using System.Text.RegularExpressions;

namespace FmpDotNet.Tests;

/// <summary>Pins the half of publishing (#73) that lives in files rather than in a run.
///
/// <para><c>publish.yml</c> pushes one file per id in its <c>PACKAGES</c> list, by name, rather than globbing
/// <c>artifacts/*.nupkg</c>. That is what makes a third package a decision somebody records — the workflow fails
/// on a packed package the list does not name. This test closes the other half of the loop: a project added under
/// <c>src/</c> and never added to the list fails here, on its own commit, rather than at the release that skips
/// it.</para>
///
/// <para>A regex over the one <c>PACKAGES:</c> line is enough for a file of that shape, and this project has no
/// YAML dependency and should not gain one for it. Neither test needs the workflow to run.</para>
/// </summary>
public class PublishWorkflowTests
{
    [Fact]
    public void EveryPackableProjectIsNamedInThePublishWorkflow()
    {
        var src = Path.Combine(RepositoryLayout.Root(), "src");
        var onDisk = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Select(x => Regex.Match(x, @"<PackageId>([^<]+)</PackageId>").Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        // An empty entry means a csproj under src/ declares no PackageId, which would pack under its assembly
        // name and could never match the list.
        Assert.All(onDisk, id => Assert.NotEmpty(id));

        var workflow = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root(), ".github", "workflows", "publish.yml"));
        var listed = Regex.Match(workflow, @"^\s*PACKAGES:\s*(.+)$", RegexOptions.Multiline).Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk, listed);
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~PublishWorkflowTests`

Expected: FAIL. `publish.yml` does not exist yet, so `File.ReadAllText` throws
`DirectoryNotFoundException`/`FileNotFoundException` on `.github/workflows/publish.yml`.

- [ ] **Step 3: Create the publish workflow**

Create `.github/workflows/publish.yml` with exactly this content:

```yaml
name: Publish

# The only path to nuget.org, for both streams, and deliberately the ENTRY workflow on every trigger.
#
# nuget.org's Trusted Publishing policy binds to a workflow FILE — repository owner, repository, and a file name
# with no path. Neither NuGet's documentation nor the NuGet/login action states whether that name is matched
# against the entry workflow (`workflow_ref`) or a called one (`job_workflow_ref`), checked 2026-09-03. So this
# file is never a reusable workflow: whatever the answer is, one policy naming publish.yml covers every run.
#
# WHAT CAN PUBLISH WHAT. A published GitHub Release packs with no suffix and is the ONLY way a stable version can
# be produced. A CI run that passed on master packs `<VersionPrefix>-ci.<that run's number>`. A manual dispatch
# must supply a suffix and is refused without one. The "Work out the version suffix" step enforces this; it is not
# a convention.
#
# WHY workflow_run RATHER THAN A JOB IN ci.yml. Publishing has to happen under this file's name for the policy
# above, and it has to happen only after the tests pass. `workflow_run` is what buys both. It does not fire for
# runs that started before this file reached the default branch, which is what workflow_dispatch is for — once.
on:
  release:
    types: [published]
  workflow_run:
    workflows: [CI]
    types: [completed]
    branches: [master]
  workflow_dispatch:
    inputs:
      suffix:
        description: 'Prerelease suffix, e.g. ci.90. Required — a manual run cannot publish a stable version.'
        required: true

# A publish that has started is never cancelled: half a push is worse than a late one, and nuget.org keeps
# whatever arrived.
concurrency:
  group: publish
  cancel-in-progress: false

permissions:
  contents: read

# Ordered, and the order is load-bearing: the core is pushed before the package that depends on it, so a refused
# push never leaves a package on the feed declaring a dependency on one that is not there. PublishWorkflowTests
# pins this list against the projects under src/.
env:
  PACKAGES: FmpDotNet FmpDotNet.Extensions.DependencyInjection

jobs:
  publish:
    name: Publish to NuGet
    # workflow_run fires on completion, success or not. A package that exists is a package someone can restore,
    # so an unverified one is worse than none.
    if: github.event_name != 'workflow_run' || github.event.workflow_run.conclusion == 'success'
    runs-on: ubuntu-latest
    timeout-minutes: 20

    permissions:
      contents: read
      id-token: write       # the OIDC token NuGet/login exchanges; the only privilege above the default

    steps:
      # For a workflow_run the default ref is master's tip, which may already be a later commit than the one CI
      # tested. head_sha is that commit. For a release it is null and the default — the tag — is right.
      - uses: actions/checkout@v7
        with:
          ref: ${{ github.event.workflow_run.head_sha || github.ref }}

      - name: Set up .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      # Values arrive through env rather than being interpolated into the script, so nothing a dispatcher types
      # is ever evaluated as shell.
      - name: Work out the version suffix
        id: suffix
        env:
          EVENT: ${{ github.event_name }}
          RUN_NUMBER: ${{ github.event.workflow_run.run_number }}
          INPUT_SUFFIX: ${{ inputs.suffix }}
        run: |
          set -euo pipefail
          case "$EVENT" in
            release)           suffix="" ;;
            workflow_run)      suffix="ci.$RUN_NUMBER" ;;
            workflow_dispatch) suffix="$INPUT_SUFFIX" ;;
            *) echo "::error::Unexpected event $EVENT."; exit 1 ;;
          esac
          if [ "$EVENT" != "release" ] && [ -z "$suffix" ]; then
            echo "::error::Only a published GitHub Release may produce a stable version. Every other trigger has to carry a prerelease suffix, and this one is empty."
            exit 1
          fi
          echo "suffix=$suffix" >> "$GITHUB_OUTPUT"
          echo "Suffix: ${suffix:-<none, this is a release>}"

      # Two explicit lines rather than `dotnet pack FmpDotNet.slnx`, so a third project starts shipping by being
      # named here rather than by being added to the solution. Packing the extensions project builds the core
      # again as a ProjectReference but emits a dependency on it at this same version rather than a second
      # FmpDotNet.nupkg. Each pack also produces a .snupkg beside its .nupkg.
      - name: Pack
        env:
          SUFFIX: ${{ steps.suffix.outputs.suffix }}
        run: |
          set -euo pipefail
          args=(-c Release -o ./artifacts)
          if [ -n "$SUFFIX" ]; then
            args+=("-p:VersionSuffix=$SUFFIX")
          fi
          dotnet pack src/FmpDotNet/FmpDotNet.csproj "${args[@]}"
          dotnet pack src/FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj "${args[@]}"

      # The version comes from what was packed, never from a literal in this file. A literal is a defect that
      # hides rather than fails: at any other version it would operate on something this run did not produce and
      # still go green. The glob cannot match the extensions package — the character after "FmpDotNet." has to be
      # a digit — nor a .snupkg.
      - name: Resolve the packed version
        id: packed
        run: |
          set -euo pipefail
          shopt -s nullglob
          files=(artifacts/FmpDotNet.[0-9]*.nupkg)
          if [ ${#files[@]} -ne 1 ]; then
            echo "::error::Expected exactly one artifacts/FmpDotNet.<version>.nupkg, found ${#files[@]}."
            ls -la artifacts/ || true
            exit 1
          fi
          version="${files[0]#artifacts/FmpDotNet.}"
          version="${version%.nupkg}"
          echo "version=$version" >> "$GITHUB_OUTPUT"
          echo "Packed version: $version"

      # Tagging v0.9.0 against a tree whose VersionPrefix still reads 0.8.0 publishes a version nobody asked for,
      # permanently. This is the cheapest place to catch it.
      - name: Assert the tag matches the packed version
        if: github.event_name == 'release'
        env:
          VERSION: ${{ steps.packed.outputs.version }}
        run: |
          set -euo pipefail
          expected="refs/tags/v$VERSION"
          if [ "$GITHUB_REF" != "$expected" ]; then
            echo "::error::This release is $GITHUB_REF but the tree packs $VERSION, so the tag should be $expected. Either VersionPrefix in src/Directory.Build.props is wrong for this release, or the tag is."
            exit 1
          fi
          echo "$GITHUB_REF matches the packed version."

      # Makes PACKAGES govern the artefacts rather than merely describe them, in both directions: nothing
      # unaccounted for survives to the push, and nothing the list names may go missing from it. The push below
      # relies on the second loop — it pushes one file per id by name, so "the file exists" has to be established
      # here.
      - name: Partition the packed output against PACKAGES
        env:
          VERSION: ${{ steps.packed.outputs.version }}
        run: |
          set -euo pipefail
          shopt -s nullglob
          for file in artifacts/*.nupkg; do
            id="${file#artifacts/}"
            id="${id%.$VERSION.nupkg}"
            if [[ " $PACKAGES " != *" $id "* ]]; then
              echo "::error::$file is not named by PACKAGES. Add $id to PACKAGES in this workflow to publish it, or set IsPackable false on the project. A file that does not end in .$VERSION.nupkg lands here too, which means it packed at a version this run did not produce."
              exit 1
            fi
          done
          for id in $PACKAGES; do
            if [ ! -f "artifacts/$id.$VERSION.nupkg" ]; then
              echo "::error::PACKAGES names $id but artifacts/$id.$VERSION.nupkg does not exist. It either failed to pack, changed its PackageId, or set IsPackable false."
              exit 1
            fi
          done
          echo "Publishing:$(for id in $PACKAGES; do printf ' %s' "$id"; done) at $VERSION."

      # Requested immediately before the push: the key it returns is valid for one hour.
      - name: NuGet login (OIDC Trusted Publishing)
        uses: NuGet/login@v1
        id: login
        with:
          user: jerbersoft

      # One invocation per id, in PACKAGES order, rather than a glob. dotnet nuget push derives each .snupkg from
      # the .nupkg path it is handed and pushes it after the primary push succeeds, so symbols need no separate
      # step — and unlike GitHub Packages, nuget.org runs a symbol server to receive them.
      - name: Push
        env:
          VERSION: ${{ steps.packed.outputs.version }}
          NUGET_API_KEY: ${{ steps.login.outputs.NUGET_API_KEY }}
        run: |
          set -euo pipefail
          pushed=""
          for id in $PACKAGES; do
            echo "::group::Pushing $id $VERSION"
            if ! dotnet nuget push "artifacts/$id.$VERSION.nupkg" \
                   --source https://api.nuget.org/v3/index.json \
                   --api-key "$NUGET_API_KEY" \
                   --skip-duplicate; then
              echo "::endgroup::"
              echo "::error::Pushing $id $VERSION failed. A 403 naming permission is the Trusted Publishing policy rather than the package: widen it for jerbersoft at https://www.nuget.org/account/trustedpublishing so that it covers $id — including the new-packages scope if $id has never been published — then re-run. --skip-duplicate makes the re-run complete rather than abort on what already went out. Published before this failure:${pushed:- nothing}."
              exit 1
            fi
            echo "::endgroup::"
            pushed="$pushed $id"
          done

      # A green push exit code is not evidence a version is live: nuget.org validates after the push returns.
      # This reads the public feed rather than trusting the exit code.
      - name: Verify both packages reached the feed
        env:
          VERSION: ${{ steps.packed.outputs.version }}
        run: |
          set -euo pipefail
          pending="$PACKAGES"
          for attempt in $(seq 1 30); do
            remaining=""
            for id in $pending; do
              lower="$(echo "$id" | tr '[:upper:]' '[:lower:]')"
              if curl -sfL "https://api.nuget.org/v3-flatcontainer/$lower/index.json" -o index.json \
                 && jq -e --arg v "$VERSION" '.versions | index($v) != null' index.json > /dev/null; then
                echo "$id $VERSION is live."
              else
                remaining="$remaining $id"
              fi
            done
            pending="$(echo "$remaining" | xargs || true)"
            if [ -z "$pending" ]; then
              break
            fi
            echo "Attempt $attempt/30 — still waiting for:$pending"
            sleep 30
          done
          if [ -n "$pending" ]; then
            echo "::error::After 15 minutes these were still not on the feed at $VERSION:$pending. nuget.org validation can lag, so check https://www.nuget.org/profiles/jerbersoft before doing anything else — once they appear, a re-run is refused as a duplicate."
            exit 1
          fi
          echo "Both packages are live at $VERSION."
```

- [ ] **Step 4: Run the test and watch it pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~PublishWorkflowTests`
Expected: PASS — one test, `EveryPackableProjectIsNamedInThePublishWorkflow`.

- [ ] **Step 5: Delete the publish job from `ci.yml`**

Delete lines **113–191** of `.github/workflows/ci.yml` — the blank line after `retention-days: 14`, the
`# Publishes the SDK to this repository's GitHub Packages NuGet feed (#10).` comment block, and the entire
`publish:` job including its `packages: write` permission and its `Upload symbol package` step. The file's last
retained line is line 112, `          retention-days: 14`.

Then, in the same file, change the header comment. Find:

```
# Deliberately NOT here: the live API. That is smoke.yml, which runs weekly against a real key (#26). It is
```

and insert this paragraph directly above it, followed by a blank comment line:

```
# Deliberately NOT here either, since #73: publishing. That is publish.yml, which packs and pushes to nuget.org
# after this workflow passes on master, and on a published GitHub Release for a stable version. It is a separate
# file because nuget.org's Trusted Publishing policy binds to a workflow file by name, so the workflow that pushes
# has to be the one the policy names.
#
```

- [ ] **Step 6: Verify `ci.yml` still parses and no longer publishes**

Run:
```bash
grep -c "packages: write\|nuget.pkg.github.com\|Publish to GitHub Packages" .github/workflows/ci.yml
tail -1 .github/workflows/ci.yml
```
Expected: `0`, and `          retention-days: 14`.

- [ ] **Step 7: Bump `VersionPrefix` and rewrite the comment above it**

In `src/Directory.Build.props`, replace the whole comment block that begins
`    Package identity shared by every package under src/.` and the `<VersionPrefix>0.1.0</VersionPrefix>` line
with this — the surrounding `<PropertyGroup>` and the `Authors`/`RepositoryUrl`/`RepositoryType` lines below stay
exactly as they are:

```xml
  <!--
    Package identity shared by every package under src/. PackageId, Description and PackageTags are each
    package's own and stay in its csproj.

    VersionPrefix, not Version. publish.yml packs a release with no suffix and a prerelease with
    `-p:VersionSuffix=ci.<CI run number>`, so every push that passes CI on master publishes a distinct prerelease
    (0.9.0-ci.91, 0.9.0-ci.92, ...) and a published GitHub Release publishes the plain VersionPrefix. nuget.org
    refuses to overwrite an existing version, exactly as GitHub Packages did, so the suffix is not optional.

    THE VALUE HERE IS THE VERSION BEING PREPARED, NOT THE ONE LAST RELEASED. NuGet orders every prerelease of a
    version BELOW the release of that version, so leaving this at 0.9.0 after 0.9.0 ships would publish
    0.9.0-ci.N builds that no consumer can ever resolve as the latest — permanently invisible, and occupying
    permanent public version slots, because nuget.org has no delete. Cutting a release is therefore two steps:
    tag it, then bump this. The procedure is written down under "Cutting a release" in
    docs/guides/releases-and-versioning.md, not only in this comment.

    One VersionPrefix for both packages is also what makes the pair consistent: `dotnet pack` of a project with
    a ProjectReference emits a dependency on the referenced package at the version it was built with, so
    FmpDotNet.Extensions.DependencyInjection 0.9.0 depends on FmpDotNet >= 0.9.0, and the two are published
    together from one run.
  -->
  <PropertyGroup>
    <VersionPrefix>0.9.0</VersionPrefix>
```

Then, further down the same file, the symbols comment claims the `.snupkg` is not published. Find:

```
    A .snupkg carries the PDBs separately, so the main package stays small and a consumer who never debugs into
    the SDK downloads nothing extra.
```

and append to that sentence's paragraph, as a new sentence: `Since #73 it goes to nuget.org's symbol server with
the package, so a consumer needs nothing from this repository to debug into the SDK.`

- [ ] **Step 8: Verify the whole suite and the version**

Run:
```bash
dotnet build FmpDotNet.slnx
dotnet test FmpDotNet.slnx
dotnet pack src/FmpDotNet/FmpDotNet.csproj -c Release -o /tmp/fmp-pack-check && ls /tmp/fmp-pack-check
```
Expected: build and tests green; the pack produces `FmpDotNet.0.9.0.nupkg` and `FmpDotNet.0.9.0.snupkg`.
Then `rm -rf /tmp/fmp-pack-check`.

- [ ] **Step 9: Commit**

```bash
git add .github/workflows/publish.yml .github/workflows/ci.yml src/Directory.Build.props \
        tests/FmpDotNet.Tests/PublishWorkflowTests.cs
git commit -m "feat(ci): publish to nuget.org from one entry workflow, and stop publishing to GitHub Packages (#73)"
```

The full message body should name: the three triggers and that only a Release can produce a stable version; the
six guards; why the workflow is never reusable (the policy binds to a file name and the claim it matches is
undocumented); and that `VersionPrefix` is now the version being prepared. End with the `Claude-Session:` trailer.

---

### Task 2: The consumer path — what someone reads before they can call anything

Every page in this task loses the same thing: the ceremony of authenticating a restore. Do not reword around it —
delete it. The replacement text below is complete; use it verbatim.

**Files:**
- Modify: `README.md` (title area, the `## Installing and versioning` section, the reference-link block at the end)
- Modify: `docs/index.md` (the `## Install` section, the last paragraph of `## Status`)
- Modify: `docs/guides/getting-started.md` (Prerequisites, steps 1 and 2, and the renumbering that follows)
- Modify: `docs/guides/troubleshooting.md` (the two entries under `## Restore and packaging`)
- Modify: `docs/guides/faq.md` (two entries)

**Interfaces:**
- Consumes: nothing from Task 1 at build time. The version `0.9.0` named in these pages is the one Task 1 set in
  `src/Directory.Build.props`.
- Produces: `README.md` keeps its `## Installing and versioning` heading, so the anchor
  `../../README.md#installing-and-versioning` that guides link to still resolves. Do not rename any heading in
  this task; DocFX validates those links and Task 3 links to this one.

- [ ] **Step 1: README — add the badges**

In `README.md`, after line 1 (`# FmpDotNet`) and the blank line under it, insert:

```markdown
[![FmpDotNet][core-badge]][core-pkg] [![DependencyInjection][di-badge]][di-pkg]

```

Then append these four lines to the very end of the file, after the existing `[inventory]: …` line:

```markdown
[core-badge]: https://img.shields.io/nuget/v/FmpDotNet?label=FmpDotNet
[core-pkg]: https://www.nuget.org/packages/FmpDotNet
[di-badge]: https://img.shields.io/nuget/v/FmpDotNet.Extensions.DependencyInjection?label=DependencyInjection
[di-pkg]: https://www.nuget.org/packages/FmpDotNet.Extensions.DependencyInjection
```

Reference-style, because the inline form puts the longest of these past 120 columns. `img.shields.io` is on
nuget.org's README image allowlist, so the badges render on the package page as well as on the site.

- [ ] **Step 2: README — rewrite "Installing and versioning"**

Replace everything between the `## Installing and versioning` heading and the `## Configuration` heading — that
is, the five paragraphs beginning "Two packages are published to **GitHub Packages**…" — with:

```markdown
Two packages are published to **nuget.org**:

```bash
dotnet add package FmpDotNet.Extensions.DependencyInjection
```

which brings `FmpDotNet` with it. There is no source to add and no token: restoring is anonymous, like any other
public package. `FmpDotNet` is the client, the models and the transports;
`FmpDotNet.Extensions.DependencyInjection` is the registration surface — `AddFmp` in every form, the
`IHostApplicationBuilder` sugar and `FmpClientFactory` — and nothing else. A consumer with a container of its own
can reference `FmpDotNet` alone. The two are versioned and published together, and everything below applies to
both.

**A release is cut from a tag.** Publishing a GitHub Release on `vX.Y.Z` is the only thing that produces a stable
version; no other trigger in the pipeline can pack without a prerelease suffix.

**Every push to `master` that passes CI publishes a prerelease** — the version being prepared, with
`-ci.<CI run number>` on the end. Run numbers never reset, so the versions are monotonic; a re-run keeps its
number and is pushed with `--skip-duplicate`, which makes re-running a green build a no-op. NuGet orders every
prerelease below the release of the same version, so `dotnet add package` ignores them unless you pass
`--prerelease` or name one exactly.

**Pin both packages to the same version** if you reference both. The extensions package depends on the core as a
floor, not an exact version, so NuGet will pair an older `AddFmp` with a newer core, and that pairing breaks the
first time the core reshapes something the older wiring constructs — the constructor change in #65 is the live
example.

Until 1.0, treat a minor bump as potentially breaking: the surface is still being shaped by what the live API
turns out to do, and two releases so far have removed public members after measurement showed they were the wrong
shape. Nothing published is ever removed, though — nuget.org unlists at most — so a pin keeps restoring.

Each package ships the XML documentation, and a matching `.snupkg` on nuget.org's symbol server carries the PDBs.
With Source Link, a debugger steps from your code into this SDK's source at the exact commit the binary was built
from.
```

- [ ] **Step 3: `docs/index.md` — the Install section**

Replace the body of `## Install` — from "Two packages, published together to **this repository's GitHub Packages
feed**…" down to and including the paragraph ending "…in the README is the full account." — with:

```markdown
Two packages, published together to **nuget.org**. `FmpDotNet.Extensions.DependencyInjection` is the registration
surface — `AddFmp` in every form, the host-builder sugar and `FmpClientFactory` — and brings `FmpDotNet`, the
client, with it. A consumer with a container of its own can reference `FmpDotNet` alone.

```sh
dotnet add package FmpDotNet.Extensions.DependencyInjection
```

No source to add, no token, no `nuget.config`. If you reference both packages directly, **pin them to the same
version**: the extensions package depends on the core as a floor rather than an exact version.
[Installing and versioning](../README.md#installing-and-versioning) in the README is the full account.
```

- [ ] **Step 4: `docs/index.md` — the last paragraph of Status**

Replace:

```markdown
No stable release has been cut yet. Every push to `master` publishes a prerelease to this repository's GitHub
Packages feed. See [Releases and Versioning](guides/releases-and-versioning.md).
```

with:

```markdown
The current release is on nuget.org; every push to `master` that passes CI publishes a prerelease of the version
being prepared. See [Releases and Versioning](guides/releases-and-versioning.md).
```

- [ ] **Step 5: `getting-started.md` — the prerequisite and the opening line**

Delete this bullet from `## Prerequisites` entirely:

```markdown
* **A GitHub personal access token** with the `read:packages` scope, to restore the package.
```

Change the opening line from `From nothing to a working call, in order. Budget ten minutes, most of it on step 1.`
to `From nothing to a working call, in order. Budget five minutes.`

In the remaining `## Prerequisites` bullet about the API key, change the link
`[the throttle note](#4-set-the-throttle-to-your-tier)` to `[the throttle note](#3-set-the-throttle-to-your-tier)`.

- [ ] **Step 6: `getting-started.md` — steps 1 and 2 become one**

Replace everything from `## 1. Add the package source` down to (but not including) `## 3. Register the client` —
the `nuget.config`, the two `export` lines, the GitHub Actions block, the "Install, pinned" section and its
Packages-page link — with:

```markdown
## 1. Install

```bash
dotnet add package FmpDotNet.Extensions.DependencyInjection
```

That brings `FmpDotNet` — the client, the models and the transports — with it. The extensions package is the
registration surface: `AddFmp` in every form, the `IHostApplicationBuilder` sugar and `FmpClientFactory`. Both are
on nuget.org, so there is no source to add, no token and no `nuget.config`; restoring is anonymous, like any other
public package.

A project that references both directly pins them to the **same** version: the extensions package depends on the
core as a floor, not an exact version, so NuGet will otherwise pair an older `AddFmp` with a newer core, and that
pairing breaks the first time the core reshapes something the older wiring constructs.

Between releases, every push to `master` that passes CI publishes a prerelease of the version being prepared.
Those are not resolved by default — `--prerelease`, or an exact `--version`, asks for one. Every version is listed
at [nuget.org/packages/FmpDotNet](https://www.nuget.org/packages/FmpDotNet), and
**[Releases and Versioning](releases-and-versioning.md)** is the full account.
```

- [ ] **Step 7: `getting-started.md` — renumber the remaining steps**

Four headings, in order down the file:

| From | To |
|---|---|
| `## 3. Register the client` | `## 2. Register the client` |
| `## 4. Set the throttle to your tier` | `## 3. Set the throttle to your tier` |
| `## 5. Make a call` | `## 4. Make a call` |
| `## 6. Handle the failures that will actually happen` | `## 5. Handle the failures that will actually happen` |

Nothing outside this file links to any of these anchors — verified 2026-09-03 — and the one in-page link is the
Prerequisites one fixed in Step 5.

- [ ] **Step 8: `troubleshooting.md` — one entry replaces two**

Under `## Restore and packaging`, replace both entries — the 401 entry beginning
`### \`Unable to load the service index for source https://nuget.pkg.github.com/...\` (401)` and the
`### \`Unable to find package FmpDotNet\` — with credentials working` entry, down to but not including the next
`###` or `##` heading — with:

```markdown
### `Unable to find package FmpDotNet`

Either the version does not exist, or you are asking for a prerelease without saying so.

`dotnet add package` resolves **stable versions only**. Everything published between releases is a prerelease —
the version being prepared, with `-ci.<CI run number>` on the end — so a build that wants one has to ask:

```bash
dotnet add package FmpDotNet --prerelease
dotnet add package FmpDotNet --version 0.9.0-ci.91
```

If even a stable version cannot be found, check the source list rather than the version: `dotnet nuget list source`
should include `https://api.nuget.org/v3/index.json`, and a `nuget.config` containing `<clear />` removes the
default source without saying so.

Every published version is listed at
[nuget.org/packages/FmpDotNet](https://www.nuget.org/packages/FmpDotNet).
```

- [ ] **Step 9: `faq.md` — replace "Why is the package not on nuget.org?"**

Replace that entire entry — heading and three paragraphs, down to but not including the `---` that follows — with:

```markdown
### Why is 0.9.0 not 1.0?

Because the surface is still being shaped by what the live API turns out to do. Two releases so far have **removed
public members** after measurement showed they were the wrong shape, and the endpoint surface is still growing.

1.0 is a promise that a minor bump cannot break you; until then a minor bump can, and the version number says so.
What 0.9.0 does promise is that the packages are on nuget.org, restore anonymously, and will not vanish —
nuget.org has no delete, only unlisting, so a pin keeps restoring whatever happens next.
```

- [ ] **Step 10: `faq.md` — re-argue "Why does every push publish a new version?"**

Replace the two paragraphs of that entry, keeping its heading, with:

```markdown
So that what has landed on `master` is installable without waiting for a release, and so that *"which SDK did this
commit build against"* is answerable from your own git history.

Every push that passes CI publishes the version being prepared with `-ci.<CI run number>` on the end. NuGet orders
every prerelease **below** the release of the same version, so those builds never overtake a release and
`dotnet add package` ignores them unless you pass `--prerelease` or name one exactly. Run numbers never reset, so
they are monotonic; a re-run keeps its number and is pushed with `--skip-duplicate`, which makes re-running a
green build a no-op rather than a failure. And nuget.org refuses to overwrite an existing version, so the suffix
is not decoration — a fixed version would fail the publish on the second push. Full detail in
**[Releases and Versioning](releases-and-versioning.md)**.
```

- [ ] **Step 11: Verify the pages build and nothing is over-long**

Run:
```bash
dotnet tool restore
dotnet docfx docs/docfx.json --warningsAsErrors
awk 'length > 120 && $0 !~ /^\| / {print FILENAME":"FNR" ("length")"}' \
  README.md docs/index.md docs/guides/getting-started.md docs/guides/troubleshooting.md docs/guides/faq.md
grep -rn "nuget.pkg.github.com\|read:packages\|GITHUB_PACKAGES_TOKEN" \
  README.md docs/index.md docs/guides/getting-started.md docs/guides/troubleshooting.md docs/guides/faq.md
```
Expected: DocFX exits 0 with `0 warning(s)`; both the `awk` and the `grep` print nothing.

- [ ] **Step 12: Commit**

```bash
git add README.md docs/index.md docs/guides/getting-started.md docs/guides/troubleshooting.md docs/guides/faq.md
git commit -m "docs: install is one command now — the consumer path drops the GitHub Packages ceremony (#73)"
```

The body should say what disappeared rather than what was reworded: the `nuget.config`, the PAT and its scope, the
two environment variables, the Actions restore block and the 401 entry that existed to explain them. End with the
`Claude-Session:` trailer.

---

### Task 3: The release record — how a version is cut, and what is supported

**Files:**
- Modify: `docs/guides/releases-and-versioning.md` (replaced end to end)
- Modify: `docs/changelog.md` (the banner, a fresh `[Unreleased]`, a new `[0.9.0]` section)
- Modify: `docs/guides/development.md` (the workflow table and the paragraph under it)
- Modify: `SECURITY.md` (the version example and the supported-versions table)
- Modify: `tests/FmpDotNet.Tests/PublishWorkflowTests.cs` (add the second test)

**Interfaces:**
- Consumes: `PublishWorkflowTests` from Task 1 — add a method to that class, do not create a second file.
  `RepositoryLayout.Root()` is already imported by being in the same namespace.
- Consumes: the README anchor `#installing-and-versioning`, unchanged by Task 2, which the last line of
  `releases-and-versioning.md` links to as `../../README.md#installing-and-versioning`.

- [ ] **Step 1: Write the failing test**

Add this method to the existing `PublishWorkflowTests` class in
`tests/FmpDotNet.Tests/PublishWorkflowTests.cs`:

```csharp
    /// <summary>The GitHub Packages feed stopped receiving publishes at 0.1.0-ci.89 (#73). Prose may name it —
    /// the versioning guide says where it stops and why — but nothing a reader follows may hand them the source
    /// URL, because adding it produces a 401 rather than a package. <c>docs/superpowers/</c> is exempt: it is a
    /// record of decisions as they were made, and rewriting history there would be a lie.</summary>
    [Fact]
    public void NothingPointsAtTheRetiredFeed()
    {
        var root = RepositoryLayout.Root();
        var files = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\', '/').Contains("/docs/superpowers/"))
            .Where(p => !p.Replace('\\', '/').Contains("/docs/_site/"))
            .Concat([
                Path.Combine(root, "README.md"),
                Path.Combine(root, "CONTRIBUTING.md"),
                Path.Combine(root, "SECURITY.md"),
            ]);

        var offenders = files
            .Where(p => File.ReadAllText(p).Contains("nuget.pkg.github.com"))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~NothingPointsAtTheRetiredFeed`
Expected: FAIL, listing `docs/guides/releases-and-versioning.md` — Task 2 cleared the other pages, and this task
clears the last one.

- [ ] **Step 3: Replace `docs/guides/releases-and-versioning.md` end to end**

Write the file with exactly this content:

````markdown
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
````

- [ ] **Step 4: Run the test and watch it pass**

Run: `dotnet test tests/FmpDotNet.Tests --filter FullyQualifiedName~PublishWorkflowTests`
Expected: PASS — two tests.

- [ ] **Step 5: `docs/changelog.md` — the banner**

Replace the whole blockquote near the top — the six lines beginning `> **No release has been cut.**` — with:

```markdown
> **History before 0.9.0 is reconstructed from the commit log**, grouped into the slices the work was actually
> built as, because the project ran for three months without a tag. Entries carry their issue numbers; dates are
> commit dates. From 0.9.0 on, changes are added under **Unreleased** as they land, and move into a version
> section when one is cut.
```

- [ ] **Step 6: `docs/changelog.md` — a fresh Unreleased and the 0.9.0 section**

Replace these three lines:

```markdown
## [Unreleased]

Everything below is in `master` and available in the latest `0.1.0-ci.N` prerelease.
```

with the following, where `<DATE>` is today's date in `YYYY-MM-DD` — the two occurrences must match. Everything
that was already under `[Unreleased]`, starting at `### The documentation site — #71`, stays exactly where it is
and is now part of 0.9.0.

```markdown
## [Unreleased]

Nothing yet. Work that lands on `master` appears here, and in the latest prerelease — the version being prepared,
with `-ci.<CI run number>` on the end.

---

## [0.9.0] — <DATE>

The first release, and the first version on nuget.org. Everything below it shipped in it.

### The first release — #73 · <DATE>

Both packages on **nuget.org**, installable with one command and no credential. The design is at
`docs/superpowers/specs/2026-09-03-nuget-release-design.md`.

**Added**
- `.github/workflows/publish.yml` — the only path to nuget.org, and the entry workflow on each of its triggers: a
  published GitHub Release packs a stable version, a passing CI run on `master` packs a prerelease, and a manual
  dispatch must supply a suffix. The credential is a one-hour OIDC key from nuget.org's Trusted Publishing, so no
  API key is stored in this repository.
- Six guards between `dotnet pack` and a published version: the version is read off what was packed, a release's
  tag must match it, the `PACKAGES` list must account for every packed file and every listed id, packages are
  pushed by name in dependency order, `--skip-duplicate` makes a re-run finish rather than abort, and the run
  polls the public feed until both versions are live.
- `PublishWorkflowTests` — every packable project under `src/` is named in `PACKAGES`, and nothing a reader
  follows still hands them the retired feed's source URL.
- The `.snupkg` symbol packages now reach nuget.org's symbol server, so stepping into the SDK needs nothing from
  this repository.
- NuGet version badges on the README.

**Changed**
- `VersionPrefix` is `0.9.0`, and it now means *the version being prepared* rather than the last one released:
  cutting a release is two steps, tag it then bump it, because NuGet orders every prerelease below the release of
  the same version.
- Installing is `dotnet add package FmpDotNet.Extensions.DependencyInjection` — no source, no token, no
  `nuget.config`. Getting Started, the FAQ, Troubleshooting, the README, the landing page, Development and
  `SECURITY.md` all say so.

**Removed**
- The `Publish to GitHub Packages` job, its `packages: write` permission and its 90-day symbol artifact. That feed
  is frozen at `0.1.0-ci.89`, not deleted: anything already pinned to it keeps restoring.
```

- [ ] **Step 7: `docs/guides/development.md` — the workflow table**

Change the heading `## The three workflows` to `## The four workflows`.

Replace the CI row:

```markdown
| **CI** (`ci.yml`) | every push to any branch, PRs to `master` | build + test, then publish to GitHub Packages on `master` |
```

with these two rows — CI loses its publishing clause, and Publish is inserted after the Docs row:

```markdown
| **CI** (`ci.yml`) | every push to any branch, PRs to `master` | build + test |
```

```markdown
| **Publish** (`publish.yml`) | a published GitHub Release; a CI run that passed on `master` | that what reaches nuget.org was packed from a tested commit, matches its tag, and is exactly the set `PACKAGES` names |
```

Then append one sentence to the paragraph below the table, after "…rather than as \"CI failed\".":

```markdown
Publish is separate because nuget.org's Trusted Publishing policy binds to a workflow file by name, so the
workflow that pushes has to be the one the policy names — which is also why it is never called as a reusable
workflow.
```

- [ ] **Step 8: `SECURITY.md`**

Replace:

```markdown
* The affected version — a package version (`0.1.0-ci.N`) or a commit SHA.
```

with:

```markdown
* The affected version — a package version (`0.9.0`, or a `-ci.N` prerelease) or a commit SHA.
```

Replace the supported-versions table and the paragraph under it:

```markdown
| Version | Supported |
|---|---|
| Latest `0.1.0-ci.N` prerelease on `master` | ✅ |
| Older `ci.N` prereleases | ❌ — fixes land on `master` and publish as a new prerelease |

**No stable release has been cut yet.** Everything published so far is a CI prerelease; see
[Releases and Versioning](https://jerbersoft.github.io/fmpdotnet/guides/releases-and-versioning.html). Until 1.0, the
supported version is simply the newest one.
```

with:

```markdown
| Version | Supported |
|---|---|
| The latest release on nuget.org | ✅ |
| The latest `-ci.N` prerelease on `master` | ✅ |
| Anything older | ❌ — fixes land on `master` and publish as a new version |

**0.9.0 is the first release**; before it, everything published was a CI prerelease on a GitHub Packages feed that
is now frozen. See
[Releases and Versioning](https://jerbersoft.github.io/fmpdotnet/guides/releases-and-versioning.html). Until 1.0,
the supported version is simply the newest one.
```

- [ ] **Step 9: Verify**

Run:
```bash
dotnet test FmpDotNet.slnx
dotnet docfx docs/docfx.json --warningsAsErrors
awk 'length > 120 && $0 !~ /^\| / {print FILENAME":"FNR" ("length")"}' \
  docs/guides/releases-and-versioning.md docs/changelog.md docs/guides/development.md SECURITY.md
git grep -n "nuget.pkg.github.com" -- ':!docs/superpowers'
```
Expected: tests green; DocFX exits 0 with `0 warning(s)`; the `awk` prints nothing; the `git grep` prints nothing.

- [ ] **Step 10: Commit**

```bash
git add docs/guides/releases-and-versioning.md docs/changelog.md docs/guides/development.md SECURITY.md \
        tests/FmpDotNet.Tests/PublishWorkflowTests.cs
git commit -m "docs: how a release is cut, and what 0.9.0 supports (#73)"
```

The body should name the two-step release procedure and why the bump is not optional, the frozen feed and that it
is frozen rather than deleted, and the test that keeps its URL out of anything a reader follows. End with the
`Claude-Session:` trailer.

---

### Task 4: Cut the release — NOT DISPATCHED TO AN IMPLEMENTER

**This task is executed by the controller, in the main session, and only after the repository owner gives an
explicit go-ahead at Step 3.** Publishing to nuget.org cannot be undone: there is no delete, only unlisting.
Approval given earlier in the session, or for any other step, does not carry to this one. Do not dispatch a
subagent for it.

**Files:**
- Modify: `src/Directory.Build.props` (`VersionPrefix` `0.9.0` → `0.10.0`), after the release is live
- Modify: `docs/changelog.md` (a fresh `[Unreleased]` body), after the release is live

**Preconditions:**
- Tasks 1–3 merged to `master` by the repository's normal route: PR, green checks, local `git merge --no-ff`,
  push, both branches deleted.
- The Trusted Publishing policy exists at nuget.org — owner `jerbersoft`, repository owner `jerbersoft`,
  repository `fmpdotnet`, workflow file `publish.yml`, environment empty, the **new packages** scope, glob
  `FmpDotNet*`. Nothing publishes without it; the first push 403s partway through.

- [ ] **Step 1: Confirm the prerelease went out**

The merge to `master` runs CI, and a green CI run triggers `publish.yml`.

```bash
gh run list --workflow=publish.yml --limit 3
gh run view <run-id> --log | tail -40
curl -s https://api.nuget.org/v3-flatcontainer/fmpdotnet/index.json | jq .
```
Expected: a successful run, and the flat container listing `0.9.0-ci.<N>` for both ids.

If no `publish.yml` run exists, `workflow_run` did not fire for a run that started before the file reached
`master` — force one prerelease by hand:

```bash
gh workflow run publish.yml -f suffix=ci.<the CI run number>
```

- [ ] **Step 2: Verify what a consumer will actually get**

```bash
cd "$(mktemp -d)" && dotnet new console -o probe && cd probe
dotnet add package FmpDotNet.Extensions.DependencyInjection --prerelease
dotnet build
```
Expected: restore succeeds with no source configuration and no credential, resolving `0.9.0-ci.<N>` for both
packages. This is the first end-to-end proof that anonymous restore works. Delete the directory afterwards.

- [ ] **Step 3: Ask the owner, and stop**

Report: the prerelease version that is live, the run that published it, and that the next step tags and publishes
`0.9.0` permanently. **Wait for an explicit go-ahead.** If it does not come, the work stops here in a good state:
the pipeline is proven and nothing stable exists.

- [ ] **Step 4: Tag and release**

```bash
git checkout master && git pull
git tag v0.9.0 && git push origin v0.9.0

# The release notes are the [0.9.0] section of the changelog, minus its own heading: everything from the line
# after "## [0.9.0]" up to the next "## [" heading.
awk '/^## \[0\.9\.0\]/{f=1; next} f && /^## \[/{exit} f' docs/changelog.md > "$SCRATCH/release-notes.md"
gh release create v0.9.0 --title "0.9.0" --notes-file "$SCRATCH/release-notes.md"
```

`$SCRATCH` is the session scratchpad directory. Publishing the Release is what triggers the publish;
`gh release create` publishes unless `--draft` is passed.

- [ ] **Step 5: Watch the run and verify the feed**

```bash
gh run watch <run-id>
curl -s https://api.nuget.org/v3-flatcontainer/fmpdotnet/index.json | jq '.versions'
curl -s https://api.nuget.org/v3-flatcontainer/fmpdotnet.extensions.dependencyinjection/index.json | jq '.versions'
```
Expected: the run is green — its own verification step already polled the feed — and `0.9.0` appears in both
listings. The run's `Assert the tag matches the packed version` step must have passed; if it failed, nothing was
pushed and the tag is wrong, not the packages.

- [ ] **Step 6: Bump `VersionPrefix` and open a fresh Unreleased**

On a branch, in one commit:

* `src/Directory.Build.props`: `<VersionPrefix>0.9.0</VersionPrefix>` → `<VersionPrefix>0.10.0</VersionPrefix>`.
* `docs/changelog.md`: leave the `[0.9.0]` section alone; the `[Unreleased]` body written in Task 3 already says
  what it should.

```bash
git commit -m "chore: prepare 0.10.0 — the next prerelease sorts above the release just cut (#73)"
```

Then PR, green, `git merge --no-ff`, push, delete branches. **Do not skip this**: until it lands, every push to
`master` publishes a `0.9.0-ci.N` that NuGet orders below the `0.9.0` now on the feed.

- [ ] **Step 7: Close the issue**

Tick every box on #73 and close it, with a comment recording the released version, the two run URLs, and anything
deferred.

---

## Known transient

Between the merge of Tasks 1–3 and the Release in Task 4, the documentation describes a stable version that does
not exist yet: `dotnet add package FmpDotNet.Extensions.DependencyInjection` with no `--prerelease` will fail for
anyone who reads Getting Started in that window. This is deliberate — the alternative is a second documentation
pass — and the window is minutes to hours, not days. Task 4 Step 3 is the only thing that can extend it, and if
the owner declines, Task 4 Step 6 still runs so `master` is not stuck publishing versions below a release that
never happened.

## Definition of done

- [ ] Trusted Publishing policy exists, with the new-packages scope and the `FmpDotNet*` glob
- [ ] `publish.yml` implements the three triggers and all six guards
- [ ] `ci.yml` publishes nothing and requests no `packages: write`
- [ ] `VersionPrefix` is `0.9.0` when the release is cut, and `0.10.0` afterwards
- [ ] `PublishWorkflowTests` passes both tests; `dotnet test FmpDotNet.slnx` green
- [ ] All eleven files updated; `dotnet docfx docs/docfx.json --warningsAsErrors` at zero warnings
- [ ] `0.9.0-ci.<N>` live on nuget.org for both ids, and an anonymous restore of it succeeds
- [ ] With the owner's explicit go-ahead: `v0.9.0` tagged, the Release published, `0.9.0` and both `.snupkg` live
- [ ] `[Unreleased]` is empty and `VersionPrefix` is `0.10.0`
- [ ] #73 closed
