# The documentation site — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A DocFX site at `https://jerbersoft.github.io/fmpdotnet/` holding the wiki's guides, the README rendered
from the same file, and an API reference generated from both packages' doc comments — built with
`--warningsAsErrors` on every push, deployed from `master`, with the wiki disabled once it is live.

**Architecture:** `docs/` gains `docfx.json`, the navigation, a landing page and fourteen pages moved from the wiki
with their links rewritten to relative forms the build validates. The README is listed as content from `..`, so
guides link to `../../README.md#section` and a stale anchor is a red build. A separate `docs.yml` workflow builds
on every push and deploys only from `master`; two unit tests pin what DocFX does not check.

**Tech Stack:** DocFX 2.78.5 (`.config/dotnet-tools.json`, `rollForward: false`), .NET 10 SDK from `global.json`,
GitHub Pages with `build_type: workflow`, `actions/upload-pages-artifact@v5` + `actions/deploy-pages@v5`, xunit.

**Spec:** `docs/superpowers/specs/2026-09-03-docfx-site-design.md`

**Issue:** #71. **Branch:** `feat/docs-site-71`, created from `docs/docfx-site-design` (which carries the spec and
this plan), so the one pull request holds spec, plan and implementation.

## Global Constraints

- DocFX **2.78.5**, pinned in `.config/dotnet-tools.json` with `rollForward: false`.
- `--warningsAsErrors` on every build, local and CI. Verified 2026-09-03: with warnings present docfx exits 255 under
  the flag and 0 without it; `InvalidFileLink` and `InvalidBookmark` both fire on the mistakes this design must catch.
- Content is **enumerated, never globbed** from `docs/`. `docs/superpowers/` is never listed.
- The two shipping projects are **listed by name** in `docfx.json`; `DocsSiteTests` pins the list to `src/*/*.csproj`.
- **One canonical copy of each fact.** Guides link to the README; the README is not restated in a guide.
- The job name **`Docs — build`** is load-bearing once it is a required check.
- .NET SDK from `global.json` (`global-json-file:` in the workflow, never `dotnet-version:`).
- Wiki pages are **moved, not rewritten**: the only edits are link forms, the two "this wiki" sentences, and the
  additions Task 5 names.
- Commit messages are conventional commits referencing `#71` and end with the trailer
  `Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE`.
- Prose wraps at 120 columns, as every markdown file in this repository does. Tables do not wrap.
- **Never paste an API key** anywhere, including a URL. The wiki pages carry none; keep it so.
- The source of the wiki pages is the clone at
  `/private/tmp/claude-501/-Users-herbertsabanal-Projects-fmpdotnet/a9dbed2a-4169-446b-8d6f-dbd380761ca6/scratchpad/wiki`
  at commit `662a4ac`. Nothing in that clone is edited; the scripts read from it.

---

## File structure

```
.config/dotnet-tools.json                 Task 2   new — docfx 2.78.5
.gitignore                                Task 2   docs/_site/, docs/api/*.yml, docs/api/.manifest
docs/docfx.json                           Task 2   new — metadata (both projects), content (enumerated), README from ..
docs/toc.yml                              Task 2   new — the top navigation
docs/index.md                             Task 2   new — the landing page (replaces the wiki Home)
docs/api/index.md                         Task 2   new — hand-written front of the generated reference
docs/changelog.md                         Task 1   moved from the wiki; Task 5 adds this issue's entry
docs/guides/toc.yml                       Task 1   new — the sidebar, the wiki's five groups
docs/guides/*.md                          Task 1   thirteen pages moved from the wiki; Task 5 extends two
README.md                                 Task 2   ## Documentation; two spec links made absolute
CONTRIBUTING.md                           Task 4   seven links to the site
SECURITY.md                               Task 4   three links to the site
src/Directory.Build.props                 Task 4   PackageProjectUrl → the site
.github/workflows/ci.yml                  Task 4   two comment lines: the repository is public
.github/workflows/docs.yml                Task 6   new — Docs — build, Docs — deploy
tests/FmpDotNet.Tests/DocsSiteTests.cs    Task 3   new — two tests
docs/superpowers/specs/2026-09-03-docfx-site-design.md   Task 5   line 3: the issue number
```

Each task ends with a commit. Every command runs from the repository root, `/Users/herbertsabanal/Projects/fmpdotnet`.
The scratchpad for throwaway files is
`/private/tmp/claude-501/-Users-herbertsabanal-Projects-fmpdotnet/a9dbed2a-4169-446b-8d6f-dbd380761ca6/scratchpad`;
call it `$SCRATCH` below.

---

### Task 1: Move the fourteen wiki pages into `docs/` with their links rewritten

**Files:**
- Create: `docs/guides/getting-started.md`, `docs/guides/configuration.md`, `docs/guides/endpoint-coverage.md`,
  `docs/guides/recipes.md`, `docs/guides/error-handling.md`, `docs/guides/rate-limits-and-bulk-data.md`,
  `docs/guides/troubleshooting.md`, `docs/guides/faq.md`, `docs/guides/architecture.md`,
  `docs/guides/contributing.md`, `docs/guides/development.md`, `docs/guides/live-smoke-suite.md`,
  `docs/guides/releases-and-versioning.md`, `docs/changelog.md`, `docs/guides/toc.yml`
- Throwaway: `$SCRATCH/migrate-wiki.py` (not committed)

**Interfaces:**
- Produces: the fourteen files at the paths above, which Tasks 2, 3 and 5 rely on by name; `docs/guides/toc.yml`
  with `href:` lines naming every guide, which `DocsSiteTests.EveryGuideIsInTheSidebar` (Task 3) reads.

- [ ] **Step 1: Confirm the source**

Run: `git -C "$SCRATCH/wiki" rev-parse --short HEAD && ls "$SCRATCH/wiki"/*.md | wc -l`
Expected: `662a4ac` and `17`.

- [ ] **Step 2: Write the migration script**

Write `$SCRATCH/migrate-wiki.py`:

```python
#!/usr/bin/env python3
"""Copies the fourteen wiki pages into docs/ with their links rewritten (#71).

One-shot and throwaway: it lives in the scratchpad, not the repository. The rules it applies are the three in
the design's "Link rewrites" table, and nothing else about a page changes.

Usage: migrate-wiki.py <wiki clone> <repository>/docs
"""
import os
import pathlib
import re
import sys

WIKI = pathlib.Path(sys.argv[1])
DOCS = pathlib.Path(sys.argv[2])
REPO = DOCS.parent

# wiki file stem -> path under docs/. Home, _Sidebar and _Footer are superseded, not moved.
PAGES = {
    "Getting-Started": "guides/getting-started.md",
    "Configuration": "guides/configuration.md",
    "Endpoint-Coverage": "guides/endpoint-coverage.md",
    "Recipes": "guides/recipes.md",
    "Error-Handling": "guides/error-handling.md",
    "Rate-Limits-and-Bulk-Data": "guides/rate-limits-and-bulk-data.md",
    "Troubleshooting": "guides/troubleshooting.md",
    "FAQ": "guides/faq.md",
    "Architecture": "guides/architecture.md",
    "Contributing": "guides/contributing.md",
    "Development": "guides/development.md",
    "Live-Smoke-Suite": "guides/live-smoke-suite.md",
    "Releases-and-Versioning": "guides/releases-and-versioning.md",
    "Changelog": "changelog.md",
}

README_URL = "https://github.com/jerbersoft/fmpdotnet/blob/master/README.md"
WIKI_LINK = re.compile(r"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")  # [[Page]] or [[label|Page]]

for stem, dest in PAGES.items():
    out = DOCS / dest
    here = out.parent

    def rewrite(match: re.Match) -> str:
        label = match.group(1)
        page = (match.group(2) or match.group(1)).replace(" ", "-")
        target = DOCS / PAGES[page]  # KeyError here means a link to a page that is not migrating: stop and look
        return f"[{label}]({os.path.relpath(target, here)})"

    text = (WIKI / f"{stem}.md").read_text(encoding="utf-8")
    text = WIKI_LINK.sub(rewrite, text)
    text = text.replace(README_URL, os.path.relpath(REPO / "README.md", here))
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8")
    print(f"{stem}.md -> {dest}")
```

- [ ] **Step 3: Run it**

Run: `python3 "$SCRATCH/migrate-wiki.py" "$SCRATCH/wiki" docs`
Expected: fourteen lines of the form `Getting-Started.md -> guides/getting-started.md`, no traceback.

- [ ] **Step 4: Verify the rewrite by count**

Run each; the expected values were counted on the wiki clone on 2026-09-03:

```bash
ls docs/guides/*.md | wc -l                                                          # 13
grep -rho '\[\[' docs/guides docs/changelog.md | wc -l                               # 0  — no wiki-style link survives
grep -rho '\](\(\.\./\)\?\(guides/\)\?[a-z-]*\.md)' docs/guides docs/changelog.md | wc -l  # 70 — one per wiki link
grep -rho 'README\.md#[a-z0-9-]*' docs/guides docs/changelog.md | wc -l              # 17 — one per README link
grep -rho 'README\.md#[a-z0-9-]*' docs/guides docs/changelog.md | sort -u | wc -l    # 10 — distinct anchors
grep -rho 'github\.com/jerbersoft/fmpdotnet/blob/master/README' docs/guides docs/changelog.md | wc -l   # 0
grep -rho 'blob/master/docs/superpowers/specs/[^)]*' docs/guides | wc -l             # 2  — unchanged, absolute
grep -rho '\.\./\.\./README\.md' docs/guides | wc -l                                 # 17 — every README link from a guide
grep -rho '](guides/[a-z-]*\.md)' docs/changelog.md | wc -l                          # 4  — the changelog's links point into guides/
```

If a count is off, read the offending file before touching the script: the rule is right for every form the
wiki uses (counted on 2026-09-03: no `[[Page#anchor]]`, no bare relative links, one `[[FAQ|FAQ]]`).

- [ ] **Step 5: Reword the one sentence that describes "this wiki"**

In `docs/guides/contributing.md`, under `## Documentation`, replace exactly:

```
* This wiki holds guides and process. It deliberately **links into the README** for measured numbers rather than
  restating them, so there is only ever one copy to keep true. Please keep it that way.
```

with:

```
* The guides on this site hold the how-to and the process. They deliberately **link into the README** for measured
  numbers rather than restating them, so there is only ever one copy to keep true. Please keep it that way.
```

Run: `grep -rn -i "wiki" docs/guides docs/changelog.md`
Expected: no line that describes the guides as a wiki. (Mentions of GitHub wikis in general, if any, are fine —
on 2026-09-03 there were none.)

- [ ] **Step 6: Write the sidebar**

Write `docs/guides/toc.yml`:

```yaml
# The guides sidebar. Grouping and order are the wiki's _Sidebar.md, carried over rather than reinvented: it is
# the navigation readers already knew, and #71 moved these pages without rewriting them, so reordering them here
# would have been an undiscussed second change. Changelog left the Releases group for the top navigation, where a
# reader arriving from a package version looks for it.
#
# DocsSiteTests checks that every guides/*.md is listed here and that every href exists — DocFX builds an orphan
# page without a word.

- name: Using it
  items:
    - name: Getting Started
      href: getting-started.md
    - name: Configuration
      href: configuration.md
    - name: Endpoint Coverage
      href: endpoint-coverage.md
    - name: Recipes
      href: recipes.md
    - name: Error Handling
      href: error-handling.md

- name: Operating it
  items:
    - name: Rate Limits and Bulk Data
      href: rate-limits-and-bulk-data.md
    - name: Troubleshooting
      href: troubleshooting.md

- name: Understanding it
  items:
    - name: FAQ
      href: faq.md
    - name: Architecture
      href: architecture.md

- name: Contributing
  items:
    - name: Contributing
      href: contributing.md
    - name: Development
      href: development.md
    - name: Live Smoke Suite
      href: live-smoke-suite.md

- name: Releases
  items:
    - name: Releases and Versioning
      href: releases-and-versioning.md
```

Run: `grep -c 'href:' docs/guides/toc.yml && for f in $(grep -o 'href: .*' docs/guides/toc.yml | cut -d' ' -f2); do test -f "docs/guides/$f" || echo "missing $f"; done`
Expected: `13` and no `missing` line.

- [ ] **Step 7: Commit**

```bash
git add docs/guides docs/changelog.md
git commit -F - <<'EOF'
docs: move the wiki's fourteen pages into docs/, links rewritten to forms the build can check (#71)

Thirteen guides and the changelog, from the wiki at 662a4ac. Prose is untouched; the 70 wiki-style links
become relative markdown links, the 17 README links become ../../README.md#section so DocFX validates the
anchor, and the two links to the endpoint-inventory spec stay absolute because specs are not on the site.
One sentence that called the guides "this wiki" now calls them this site.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 2: The site builds — tooling, `docfx.json`, navigation, landing page, API index, README edits

**Files:**
- Create: `.config/dotnet-tools.json`, `docs/docfx.json`, `docs/toc.yml`, `docs/index.md`, `docs/api/index.md`
- Modify: `.gitignore` (append), `README.md:13`, `README.md:569`, `README.md` (new section before `## Status`,
  currently line 17)

**Interfaces:**
- Consumes: the files Task 1 created, at the paths its `PAGES` table names.
- Produces: `docs/docfx.json` with `metadata[0].src[0].files` listing the two csproj paths relative to `../src`,
  which `DocsSiteTests.EveryShippingProjectIsInTheApiReference` (Task 3) reads with `System.Text.Json`; the build
  command `dotnet docfx docs/docfx.json --warningsAsErrors`, which Tasks 5 and 6 run.

- [ ] **Step 1: Pin DocFX**

Write `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "docfx": {
      "version": "2.78.5",
      "commands": [
        "docfx"
      ],
      "rollForward": false
    }
  }
}
```

Run: `dotnet tool restore && dotnet docfx --version`
Expected: a line beginning `2.78.5`.

- [ ] **Step 2: Ignore the generated output**

Append to `.gitignore`:

```
# The documentation site. docs/_site/ is the built site and docs/api/*.yml the generated API reference; both are
# rebuilt by `dotnet docfx docs/docfx.json` and by docs.yml, and a generated directory in version control is a
# merge conflict waiting for the next doc-comment fix. docs/api/index.md is hand-written and stays tracked.
docs/_site/
docs/api/*.yml
docs/api/.manifest
```

- [ ] **Step 3: Write `docs/docfx.json`**

```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/docfx/main/schemas/docfx.schema.json",

  "metadata": [
    {
      "//": [
        "The two shipping projects, named one by one rather than globbed as ../src/**/*.csproj. A glob would",
        "silently pick up a third project the day one is added, and whether it belongs in the published reference",
        "is a decision somebody should make in a diff. DocsSiteTests pins this list to the projects under src/, so",
        "the decision cannot be skipped by omission in either direction.",
        "",
        "TargetFramework is pinned because DocFX loads these through MSBuildWorkspace and a multi-targeted project",
        "would otherwise be ambiguous. There is only net10.0 today; the property keeps this honest if that changes."
      ],
      "src": [
        {
          "src": "../src",
          "files": [
            "FmpDotNet/FmpDotNet.csproj",
            "FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj"
          ]
        }
      ],
      "dest": "api",
      "properties": {
        "TargetFramework": "net10.0",
        "Configuration": "Release"
      }
    }
  ],

  "build": {
    "//": [
      "Content is enumerated rather than globbed as **/*.md for one concrete reason: docs/superpowers/ holds the",
      "designs, plans and measurements, which are working material. A broad glob would publish the next plan",
      "somebody writes. guides/ is listed, superpowers/ is not, and that is the whole rule.",
      "",
      "README.md is the repository README, rendered from the same file: one copy of the reference, on GitHub,",
      "inside both packages, and here. DocFX accepts the relative parent path (and rejects an absolute one).",
      "Guides link to it as ../../README.md#section and the build validates every anchor (#71)."
    ],
    "content": [
      { "files": ["index.md", "toc.yml", "changelog.md"] },
      { "files": ["guides/**.md", "guides/**/toc.yml"] },
      { "files": ["api/**.yml", "api/index.md"] },
      { "files": ["README.md"], "src": ".." }
    ],
    "output": "_site",
    "template": ["default", "modern"],

    "//sitemap": [
      "The one place in this repository that spells out the public URL. DocFX emits relative links everywhere",
      "else, which is why the site works unchanged under a /fmpdotnet/ path — but a sitemap has to carry absolute",
      "ones. If this ever needs to change, the reason will be a Pages setting outside this repository."
    ],
    "sitemap": {
      "baseUrl": "https://jerbersoft.github.io/fmpdotnet/",
      "changefreq": "weekly"
    },
    "globalMetadata": {
      "_appName": "FmpDotNet",
      "_appTitle": "FmpDotNet",
      "_appFooter": "FmpDotNet — an independent .NET client for the Financial Modeling Prep API. MIT. The README is the reference; where a guide and the README disagree, the README wins.",
      "_enableSearch": true,
      "_gitContribute": {
        "repo": "https://github.com/jerbersoft/fmpdotnet",
        "branch": "master"
      }
    }
  }
}
```

- [ ] **Step 4: Write the top navigation, `docs/toc.yml`**

```yaml
# The top navigation bar.
#
# Guides first, the README second, the member list third: a reader who lands here needs to be taught the
# library, then to look a measured fact up, and only then a member list. Reference is the repository README
# rendered from the same file (see docfx.json), so it has no page of its own under docs/.
#
# href on a folder entry makes the folder's toc.yml the sidebar; topicHref is the page the nav link opens. Without
# topicHref an entry lands on whichever page sorts first, and the hand-written index is built but unreachable.

- name: Guides
  href: guides/
  topicHref: guides/getting-started.md
- name: Reference
  href: ../README.md
- name: API reference
  href: api/
  topicHref: api/index.md
- name: Changelog
  href: changelog.md
- name: Packages
  href: https://github.com/jerbersoft/fmpdotnet/packages
- name: GitHub
  href: https://github.com/jerbersoft/fmpdotnet
```

- [ ] **Step 5: Write the landing page, `docs/index.md`**

````markdown
---
_layout: landing
---

<div class="text-center my-5">
  <h1 class="display-4 fw-bold">FmpDotNet</h1>
  <p class="lead">
    A .NET 10 SDK for the <a href="https://site.financialmodelingprep.com/developer/docs">Financial Modeling Prep</a>
    <code>stable</code> API — NodaTime throughout, AOT-compatible, with the upstream's measured quirks documented
    on the members they affect.
  </p>
  <p>
    <a class="btn btn-primary btn-lg" href="guides/getting-started.md">Get started</a>
    <a class="btn btn-outline-secondary btn-lg" href="../README.md">Reference</a>
    <a class="btn btn-outline-secondary btn-lg" href="api/index.md">API reference</a>
  </p>
</div>

```csharp
services.AddFmp(configuration);                       // binds the "Fmp" section

var fmp     = provider.GetRequiredService<FmpClient>();
var profile = await fmp.Company.GetProfileAsync("AAPL");
var income  = await fmp.Statements.GetIncomeStatementAsync("AAPL", FiscalPeriod.Annual, limit: 5);
```

That is the whole shape of it. Twenty-five endpoint groups hang off `FmpClient`; every one of them speaks NodaTime,
throws on every failure, and paces itself against a throttle shared by every registration on the same API key.

## Install

Two packages, published together to **this repository's GitHub Packages feed** rather than to nuget.org.
`FmpDotNet.Extensions.DependencyInjection` is the registration surface — `AddFmp` in every form, the host-builder
sugar and `FmpClientFactory` — and brings `FmpDotNet`, the client, with it. A consumer with a container of its own
can reference `FmpDotNet` alone.

```sh
dotnet add package FmpDotNet.Extensions.DependencyInjection
dotnet add package FmpDotNet
```

Every push to `master` publishes a prerelease, `0.1.0-ci.<run number>`, so **pin an exact version** — a floating
reference to a feed that gains a version on every push is a build that changes under you — and pin both packages to
the same one. GitHub Packages needs a token with `read:packages` for every restore, public packages included;
[Getting Started](guides/getting-started.md) shows the `nuget.config` that keeps it out of your tree, and
[Installing and versioning](../README.md#installing-and-versioning) in the README is the full account.

## The three things worth knowing up front

**Everything throws.** There is no `Try`-prefixed method and no method that signals a failure by returning. A
`null` return always means an answer FMP genuinely gave — "no such symbol", "an ETF has no scores" — never a
refusal. See [Error Handling](guides/error-handling.md).

**Bulk is a different animal.** The `*-bulk` endpoints are CSV rather than JSON, stream rather than list, run on
their own far tighter throttle, and can return errors under HTTP 200. They have their own transport, their own
timeout and their own reservoir. See [Rate Limits and Bulk Data](guides/rate-limits-and-bulk-data.md).

**Time is NodaTime, all the way through.** No `DateTime`, `DateOnly`, `DateTimeOffset` or `TimeSpan` appears in
any public signature. FMP sends two different timezone conventions under one identical wire format, which is
exactly the class of bug NodaTime exists to make unrepresentable. See
[Dates and times](../README.md#dates-and-times-are-nodatime).

## Status

Coverage is tracked by a table **generated from the code** — see
[endpoint coverage](../README.md#endpoint-coverage) for the current count and the per-group breakdown. Adding an
endpoint without a table entry fails the build, so that page cannot quietly go stale.

No stable release has been cut yet. Every push to `master` publishes a prerelease to this repository's GitHub
Packages feed. See [Releases and Versioning](guides/releases-and-versioning.md).
````

- [ ] **Step 6: Write the front of the API reference, `docs/api/index.md`**

```markdown
# API reference

Every public member of both packages, generated from the XML documentation comments in the source. The same
comments ship inside the packages, so what is here is what IntelliSense shows at the call site.

`GenerateDocumentationFile` and `TreatWarningsAsErrors` are both on for every project under `src/`, so a public
member without a documentation comment does not compile in this repository — with one deliberate exception, below.

## Namespaces

| Namespace | Package | Contents |
|---|---|---|
| <xref:FmpDotNet> | `FmpDotNet` | <xref:FmpDotNet.FmpClient>, <xref:FmpDotNet.FmpOptions>, the <xref:FmpDotNet.FmpException> family, the two transports, <xref:FmpDotNet.FmpRequest>, the shared enums and the criteria records |
| <xref:FmpDotNet.Endpoints> | `FmpDotNet` | The 25 endpoint groups, one class each, reached through the properties of `FmpClient` |
| <xref:FmpDotNet.Models> | `FmpDotNet` | The response models — what each endpoint returns |
| <xref:FmpDotNet.Http> | `FmpDotNet` | The handlers and their bases, <xref:FmpDotNet.Http.TokenBucket>, <xref:FmpDotNet.Http.FmpBuckets> and <xref:FmpDotNet.Http.FmpBucketRegistry> |
| <xref:FmpDotNet.Serialization> | `FmpDotNet` | The `JsonConverter<T>` implementations and the CSV reader |
| <xref:FmpDotNet.Extensions.DependencyInjection> | `FmpDotNet.Extensions.DependencyInjection` | `AddFmp` in every form, the host-builder extensions, <xref:FmpDotNet.Extensions.DependencyInjection.IFmpBuilder>, <xref:FmpDotNet.Extensions.DependencyInjection.FmpClientFactory> |

## Two things no single member's page can say

**The converters are documented because they are reachable, not because they are an entry point.** The
`JsonSerializerContext` behind every model is `internal` and does not appear here; what is public in
<xref:FmpDotNet.Serialization> is the converters it registers. Read one when you want to know exactly which wire
spelling a value round-trips as — each says, and says what an unrecognised value does.

**Eight model types render their properties without summaries, by decision.** The seven period-shaped
fundamentals — <xref:FmpDotNet.Models.IncomeStatement>, <xref:FmpDotNet.Models.BalanceSheetStatement>,
<xref:FmpDotNet.Models.CashFlowStatement>, <xref:FmpDotNet.Models.KeyMetrics>,
<xref:FmpDotNet.Models.FinancialRatios>, <xref:FmpDotNet.Models.FinancialGrowth>,
<xref:FmpDotNet.Models.EnterpriseValues> — and <xref:FmpDotNet.Models.CotReport> are flat transcriptions of FMP's
wire fields, several hundred properties between them, and each file carries a `#pragma warning disable CS1591`
with the count and the reasoning at its top. Documenting each property individually would bury the type-level
remarks, which are where the real documentation is: what the endpoint actually does, and how it was measured to
do it.

The measured behaviour behind these types — plan gating, the two timezone conventions, what a `null` means — is in
the [README](../README.md#upstream-behaviour-the-sdk-handles-for-you), rendered on this site as Reference, and the
guides link there rather than restating it.
```

- [ ] **Step 7: The README's two spec links become absolute**

Both occurrences of `(docs/superpowers/specs/2026-08-27-endpoint-inventory.md)` — lines 13 and 569 — become
`(https://github.com/jerbersoft/fmpdotnet/blob/master/docs/superpowers/specs/2026-08-27-endpoint-inventory.md)`:

```bash
sed -i '' 's|](docs/superpowers/specs/2026-08-27-endpoint-inventory.md)|](https://github.com/jerbersoft/fmpdotnet/blob/master/docs/superpowers/specs/2026-08-27-endpoint-inventory.md)|g' README.md
grep -c 'blob/master/docs/superpowers/specs/2026-08-27-endpoint-inventory.md' README.md
```

Expected: `2`. Line 890 mentions a spec path in backticks, not a link; leave it.

- [ ] **Step 8: The README gains `## Documentation`**

Insert before the line `## Status` (currently line 17), leaving one blank line on each side:

```markdown
## Documentation

**[jerbersoft.github.io/fmpdotnet](https://jerbersoft.github.io/fmpdotnet/) is the documentation**: the guides, this
README rendered as the reference, the generated API reference and the changelog, on one searchable site. Start at
[Getting Started](https://jerbersoft.github.io/fmpdotnet/guides/getting-started.html). The
[API reference](https://jerbersoft.github.io/fmpdotnet/api/) is generated from the XML documentation comments, which
also ship inside the packages and reach IntelliSense at the call site. The guides live in
[`docs/`](https://github.com/jerbersoft/fmpdotnet/tree/master/docs), so a change in behaviour and the change to the
page describing it land in the same pull request — and the site is built with `--warningsAsErrors`, so a link that
stops resolving fails the build instead of becoming a dead link nobody reports.
```

Run: `grep -n '^## ' README.md | head -4`
Expected: `## Documentation` on a line before `## Status`.

- [ ] **Step 9: Build the site**

Run: `dotnet restore FmpDotNet.slnx && dotnet docfx docs/docfx.json --warningsAsErrors; echo "exit $?"`
Expected: the metadata stage over both projects, then `Build succeeded.` with `0 warning(s)`, `0 error(s)`, and
`exit 0`. Roughly a minute.

If it reports `InvalidBookmark` for a README anchor, the guide's link is wrong, not the README's heading: open the
rendered `docs/_site/README.html`, find the `id=` DocFX gave that heading, and correct the link in the guide. On
2026-09-03 every anchor the guides use, including `plan-gating--402-and-403` and the in-page
`#4-set-the-throttle-to-your-tier` in Getting Started, rendered identically to GitHub's.

- [ ] **Step 10: Verify the rendered links and the ignore rules**

```bash
ls docs/_site/README.html docs/_site/index.html docs/_site/changelog.html docs/_site/api/index.html >/dev/null && echo pages-ok
grep -oE 'href="[^"]*README[^"]*"' docs/_site/index.html | sort -u          # README.html, README.html#... — never README.md
grep -oE 'href="\.\./README\.html#[a-z0-9-]+"' docs/_site/guides/architecture.html | wc -l   # 4 — its four README links
grep -oE 'href="[^"]*"' docs/_site/toc.html                                  # guides/getting-started.html, README.html, api/index.html, changelog.html, the two GitHub URLs
ls docs/_site/api/*.html | wc -l                                             # over 200
git status --porcelain docs | grep -E '_site|api/.*\.yml|\.manifest' ; echo "ignored-ok"
```

Expected: `pages-ok`; the index links end in `.html`; the toc carries all six targets; over 200 API pages; and the
last command prints nothing but `ignored-ok` (the generated output is not visible to git).

- [ ] **Step 11: Commit**

```bash
git add .config/dotnet-tools.json .gitignore docs/docfx.json docs/toc.yml docs/index.md docs/api/index.md README.md
git commit -F - <<'EOF'
feat(docs): the site builds — DocFX 2.78.5, both projects in the reference, the README as a page (#71)

docfx.json names the two shipping projects and enumerates content, so docs/superpowers/ is never published.
README.md is content from the parent directory: one file, rendered on GitHub, inside both packages and at
the site root, with every guide link into it validated by the build. Its two relative spec links become
absolute — the build rejects them otherwise, and the package page needed that anyway — and it gains a
Documentation section pointing at the site. The landing page replaces the wiki's Home; api/index.md fronts
the generated reference with the six namespaces and the CS1591 exemption.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 3: `DocsSiteTests` — the two facts DocFX does not check

**Files:**
- Create: `tests/FmpDotNet.Tests/DocsSiteTests.cs`

**Interfaces:**
- Consumes: `docs/docfx.json` (Task 2) — `metadata[].src[].files[]` are csproj paths relative to `../src`;
  `docs/guides/toc.yml` (Task 1) — `href: <file>.md` lines, one per guide.
- Produces: two `[Fact]`s in `FmpDotNet.Tests`, run by `.NET — build + test` without DocFX installed.

- [ ] **Step 1: Write the tests**

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FmpDotNet.Tests;

/// <summary>Pins two facts about the documentation site (#71) that DocFX itself does not check.
///
/// <para><c>docs/docfx.json</c> names the projects whose doc comments become the API reference, one by one rather
/// than by glob, so that a third package is a decision in a diff. This is what stops the decision being skipped by
/// omission: a project under <c>src/</c> that is not listed fails here, and so does a listing for a project that no
/// longer exists.</para>
///
/// <para>DocFX builds a guide nobody has linked to without a word. The sidebar is <c>docs/guides/toc.yml</c>, and
/// every page under <c>docs/guides/</c> has to be in it — and every entry in it has to exist. A regex over the
/// <c>href:</c> lines is enough for a file of that shape; this project has no YAML dependency and should not gain
/// one for it.</para>
///
/// <para>Neither test needs DocFX installed. Both find the repository root the way <see cref="EndpointCoverageTests"/>
/// does, from this file's compile-time path.</para>
/// </summary>
public class DocsSiteTests
{
    [Fact]
    public void EveryShippingProjectIsInTheApiReference()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var onDisk = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(src, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        using var docfx = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "docfx.json")));
        var listed = docfx.RootElement.GetProperty("metadata").EnumerateArray()
            .SelectMany(m => m.GetProperty("src").EnumerateArray())
            .SelectMany(s => s.GetProperty("files").EnumerateArray())
            .Select(f => f.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk, listed);
    }

    [Fact]
    public void EveryGuideIsInTheSidebar()
    {
        var guides = Path.Combine(RepositoryRoot(), "docs", "guides");
        var pages = Directory.GetFiles(guides, "*.md")
            .Select(p => Path.GetFileName(p))
            .Order(StringComparer.Ordinal)
            .ToList();

        var sidebar = File.ReadAllText(Path.Combine(guides, "toc.yml"));
        var hrefs = Regex.Matches(sidebar, @"^\s*href:\s*(\S+\.md)\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(pages, hrefs);
    }

    /// <summary>Locates the repository from this file's compile-time path, so the tests do not depend on the working
    /// directory a runner happens to choose.</summary>
    private static string RepositoryRoot([CallerFilePath] string here = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FmpDotNet.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
```

- [ ] **Step 2: Run them**

Run: `dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~DocsSiteTests" -- RunConfiguration.TreatNoTestsAsError=true`
Expected: `Passed! - Failed: 0, Passed: 2`. (They pass on first run because Tasks 1 and 2 already made both facts
true; the next two steps prove they can fail.)

- [ ] **Step 3: Prove the sidebar test has teeth**

```bash
cp docs/guides/toc.yml "$SCRATCH/toc.yml.bak"
grep -v 'href: recipes.md' docs/guides/toc.yml > "$SCRATCH/toc.yml.mutated" && cp "$SCRATCH/toc.yml.mutated" docs/guides/toc.yml
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EveryGuideIsInTheSidebar" -- RunConfiguration.TreatNoTestsAsError=true; echo "exit $?"
cp "$SCRATCH/toc.yml.bak" docs/guides/toc.yml
git diff --stat docs/guides/toc.yml   # prints nothing: restored byte for byte
```

Expected: the test run reports `Failed: 1` and a non-zero exit while `recipes.md` is missing from the sidebar, and
the file is restored afterwards.

- [ ] **Step 4: Prove the project test has teeth**

```bash
cp docs/docfx.json "$SCRATCH/docfx.json.bak"
grep -v 'FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj' docs/docfx.json | sed 's|"FmpDotNet/FmpDotNet.csproj",|"FmpDotNet/FmpDotNet.csproj"|' > "$SCRATCH/docfx.json.mutated" && cp "$SCRATCH/docfx.json.mutated" docs/docfx.json
dotnet test tests/FmpDotNet.Tests --filter "FullyQualifiedName~EveryShippingProjectIsInTheApiReference" -- RunConfiguration.TreatNoTestsAsError=true; echo "exit $?"
cp "$SCRATCH/docfx.json.bak" docs/docfx.json
git diff --stat docs/docfx.json   # prints nothing
```

Expected: `Failed: 1` while the extensions project is unlisted; restored afterwards.

- [ ] **Step 5: The whole suite, then commit**

Run: `dotnet test FmpDotNet.slnx -- RunConfiguration.TreatNoTestsAsError=true`
Expected: every project green.

```bash
git add tests/FmpDotNet.Tests/DocsSiteTests.cs
git commit -F - <<'EOF'
test(docs): pin the reference's project list to src/ and every guide to the sidebar (#71)

DocFX checks neither. A project under src/ that docfx.json does not name would ship with no reference and
no warning; a guide that toc.yml does not list builds as an orphan. Both fail on the commit that causes
them, in the ordinary test run, with no DocFX installed.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 4: Inbound references, the package URL and the CI comments

**Files:**
- Modify: `CONTRIBUTING.md:3-4`, `CONTRIBUTING.md:57`, `CONTRIBUTING.md:83`, `CONTRIBUTING.md:94-97`
- Modify: `SECURITY.md:42`, `SECURITY.md:70`, `SECURITY.md:86`
- Modify: `src/Directory.Build.props:62`
- Modify: `.github/workflows/ci.yml:46-48`, `.github/workflows/ci.yml:65-66`

**Interfaces:**
- Produces: no tracked file contains `fmpdotnet/wiki`, which Task 7's definition of done checks with `git grep`.

- [ ] **Step 1: Point `CONTRIBUTING.md` and `SECURITY.md` at the site**

Every `https://github.com/jerbersoft/fmpdotnet/wiki/<Page>` becomes
`https://jerbersoft.github.io/fmpdotnet/guides/<page>.html`, lower-cased. The labels `wiki/<Page>` in the
CONTRIBUTING table lose their `wiki/` prefix, and its opening sentence stops saying "wiki":

```bash
python3 - <<'EOF'
import pathlib, re
site = "https://jerbersoft.github.io/fmpdotnet/guides/"
for name in ("CONTRIBUTING.md", "SECURITY.md"):
    p = pathlib.Path(name)
    t = p.read_text(encoding="utf-8")
    t = re.sub(r"https://github\.com/jerbersoft/fmpdotnet/wiki/([A-Za-z-]+)", lambda m: site + m.group(1).lower() + ".html", t)
    t = re.sub(r"\[wiki/([A-Za-z-]+)\]", lambda m: "[" + m.group(1).replace("-", " ") + "]", t)
    p.write_text(t, encoding="utf-8")
EOF
```

Then in `CONTRIBUTING.md` replace exactly:

```
Thanks for looking. This is the short version; the full guide lives in the
**[wiki](https://jerbersoft.github.io/fmpdotnet/guides/contributing.html)**.
```

with:

```
Thanks for looking. This is the short version; the full guide is
**[Contributing](https://jerbersoft.github.io/fmpdotnet/guides/contributing.html)** on the documentation site.
```

Run: `git grep -n "fmpdotnet/wiki" -- . ; echo "---"; grep -c "jerbersoft.github.io/fmpdotnet/guides/" CONTRIBUTING.md SECURITY.md`
Expected: nothing before `---`; then `CONTRIBUTING.md:7` and `SECURITY.md:3`.

- [ ] **Step 2: `PackageProjectUrl` becomes the site**

In `src/Directory.Build.props`, replace the line

```xml
    <PackageProjectUrl>https://github.com/jerbersoft/fmpdotnet</PackageProjectUrl>
```

with:

```xml
    <!-- NuGet renders this as "Project website", and it is the documentation site rather than the repository (#71).
         The repository is rendered separately as "Source repository" from RepositoryUrl above, which is unchanged
         and is what Source Link uses. A github.io URL is weaker than the repository URL — renaming the repository
         breaks it without a redirect — but the README this build freezes into every package page already carries
         site URLs, so this adds no new class of permanence risk; it only stops "Project website" going somewhere
         less useful than the README rendered beneath it. -->
    <PackageProjectUrl>https://jerbersoft.github.io/fmpdotnet/</PackageProjectUrl>
```

Run: `dotnet build src/FmpDotNet/FmpDotNet.csproj --no-restore 2>&1 | tail -3`
Expected: `Build succeeded.` with 0 warnings.

- [ ] **Step 3: `ci.yml` stops calling the repository private**

The repository has been public since before this change (`gh repo view --json visibility` → `PUBLIC` on
2026-09-03); two comments still argue from billed minutes. Replace lines 46-48:

```
# This repository is PRIVATE, so runner minutes are billed rather than free. Superseded runs on a branch are
# cancelled; master is exempt because its runs are the record for a merged commit and should complete even if
# another merge lands behind them.
```

with:

```
# Superseded runs on a branch are cancelled: a run of a commit that has already been replaced answers a question
# nobody is still asking, whatever the minutes cost (nothing — the repository is public). master is exempt because
# its runs are the record for a merged commit and should complete even if another merge lands behind them.
```

and lines 65-66:

```
    # Explicit, because the GitHub default is 360 minutes. On a billed private repo a single hung step would
    # otherwise burn six hours of minutes before anyone noticed.
```

with:

```
    # Explicit, because the GitHub default is 360 minutes. A single hung step would otherwise sit for six hours
    # before anyone noticed.
```

Run: `grep -n -i "private\|billed" .github/workflows/ci.yml; echo "exit $?"`
Expected: no matches, `exit 1`.

- [ ] **Step 4: Commit**

```bash
git add CONTRIBUTING.md SECURITY.md src/Directory.Build.props .github/workflows/ci.yml
git commit -F - <<'EOF'
docs: point CONTRIBUTING, SECURITY and the package page at the site; ci.yml stops calling the repo private (#71)

Ten wiki links become site links. PackageProjectUrl follows databentodotnet's #85 reasoning: the README
frozen into each package page already carries site URLs, so pointing "Project website" at the site adds no
new permanence risk. ci.yml's two comments argued from billed minutes on a private repository, which this
one is not, and docs.yml's comments would otherwise have contradicted them.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 5: The guides that describe the site — Contributing, Development, the Changelog, and the spec's issue line

**Files:**
- Modify: `docs/guides/contributing.md` (the `## Documentation` section)
- Modify: `docs/guides/development.md` (`## Setup`, `## Layout`, `## The two workflows`)
- Modify: `docs/changelog.md` (under `## [Unreleased]`)
- Modify: `docs/superpowers/specs/2026-09-03-docfx-site-design.md:3`

**Interfaces:**
- Consumes: the build command from Task 2; the workflow name and job names Task 6 will create (`Docs`,
  `Docs — build`, `Docs — deploy`) — Development describes them ahead of the file, and Task 6 must match.

- [ ] **Step 1: Contributing gains "where a fact lives"**

In `docs/guides/contributing.md`, immediately after the third bullet of `## Documentation` (the one Task 1
reworded, ending `Please keep it that way.`) and before `## Reporting a problem`, insert:

```markdown

**Where a fact lives.** One canonical copy of each, and a change to behaviour lands in the same pull request as the
change to the page describing it:

| Content | Home |
|---|---|
| Guides, runbooks, troubleshooting, FAQ | `docs/guides/`, on this site |
| Measured upstream behaviour, the endpoint table, the registration paths, versioning | `README.md`, rendered on this site as [Reference](../../README.md) |
| API | The XML doc comments; the [API reference](../api/index.md) renders them and does not restate them |
| Changelog | [`docs/changelog.md`](../changelog.md) |
| Designs, plans and measurements | `docs/superpowers/`, never published |

The site is built with `--warningsAsErrors`, so a link to a README section that no longer exists fails the build
rather than becoming a dead link nobody reports. [Development](development.md) has the build command.
```

- [ ] **Step 2: Development gains the build, the layout entries and the third workflow**

In `docs/guides/development.md`:

(a) After the paragraph ending `it would let the two disagree silently.` in `## Setup`, insert:

````markdown

**The documentation site** builds from `docs/` with DocFX, pinned at 2.78.5 in `.config/dotnet-tools.json`:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --warningsAsErrors    # what docs.yml runs: metadata, then build; a warning fails it
dotnet docfx docs/docfx.json --serve               # the same, then serves the site locally
```

A DocFX warning is nearly always a link that will not resolve — a README section that was renamed, a guide that
moved — which on a published site is a dead link nobody reports. `docs/api/*.yml` and `docs/_site/` are generated
and gitignored; `docs/api/index.md` is hand-written.
````

(b) In the `## Layout` code block, after the line beginning `tests/FmpDotNet.SmokeTests/`, append:

```

docs/                                   the documentation site — jerbersoft.github.io/fmpdotnet
  docfx.json, toc.yml, index.md         configuration, top navigation, landing page
  guides/                               these pages, and their sidebar toc.yml
  api/index.md                          hand-written front of the generated API reference
  changelog.md
  superpowers/                          designs, plans and measurements; never published
```

(c) Replace the heading `## The two workflows` with `## The three workflows`, and add a row to its table after
the `**CI**` row:

```
| **Docs** (`docs.yml`) | every push to any branch, PRs to `master` | that the site builds with zero warnings; deploys it from `master` |
```

(d) Replace the sub-heading `### One name you must not change casually` and its first paragraph:

```
### One name you must not change casually

The CI job is called **`.NET — build + test`**, and `master`'s ruleset requires that check **by name**.
```

with:

```
### Two names you must not change casually

The CI job is called **`.NET — build + test`** and the docs build job **`Docs — build`**, and `master`'s ruleset
requires both checks **by name**.
```

Then, in the paragraph that follows, replace `**If you rename it, update the ruleset in the same change:**` with
`**If you rename either, update the ruleset in the same change:**`.

- [ ] **Step 3: The Changelog gains this issue's entry**

In `docs/changelog.md`, under `## [Unreleased]`, immediately before `### Host registration — #65 · 2026-09-02 →
2026-09-03`, insert:

```markdown
### The documentation site — #71 · 2026-09-03

The wiki's pages, the README and the API reference on one site:
[jerbersoft.github.io/fmpdotnet](https://jerbersoft.github.io/fmpdotnet/). The design is at
`docs/superpowers/specs/2026-09-03-docfx-site-design.md`.

**Added**
- A DocFX site built from `docs/` on every push by `docs.yml` with `--warningsAsErrors`, and deployed from `master`:
  the guides, the README rendered as Reference from the same file, an API reference generated from the doc
  comments of both packages, and this changelog. A guide's link to a README section is validated by the build.
- `DocsSiteTests` — every project under `src/` is in the API reference, and every guide is in the sidebar.
- `PackageProjectUrl` is the site, so a package page's "Project website" lands on the documentation.

**Changed**
- The fourteen wiki pages moved into `docs/guides/` and `docs/changelog.md`, prose unchanged; the wiki is disabled,
  and its URLs redirect to the repository. `CONTRIBUTING.md` and `SECURITY.md` link to the site.

```

- [ ] **Step 4: The spec names its issue**

In `docs/superpowers/specs/2026-09-03-docfx-site-design.md`, replace line 3:

```
No issue yet; one should be opened before implementation, per CONTRIBUTING.
```

with:

```
Issue: #71. Plan: `docs/superpowers/plans/2026-09-03-docfx-site.md`.
```

- [ ] **Step 5: Build, wrap check, commit**

Run: `dotnet docfx docs/docfx.json --warningsAsErrors >/dev/null; echo "exit $?"; awk 'length > 120 && !/^\|/ && !/^ *[a-z-]+\.[a-z]+ +[a-z]/ {print FILENAME": "FNR": "length}' docs/guides/contributing.md docs/guides/development.md docs/changelog.md`
Expected: `exit 0` and no over-long prose line (table rows and layout-tree lines are exempt from the check).

```bash
git add docs/guides/contributing.md docs/guides/development.md docs/changelog.md docs/superpowers/specs/2026-09-03-docfx-site-design.md
git commit -F - <<'EOF'
docs: say where a fact lives, how the site builds, and what #71 changed (#71)

Contributing carries the one-canonical-copy table the design rests on; there is no CLAUDE.md here to hold
it. Development gains the build and serve commands, the docs/ layout entries and the third workflow, whose
build job joins the CI job as a name the ruleset matches. The changelog gets this issue's entry.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
```

---

### Task 6: The Docs workflow

**Files:**
- Create: `.github/workflows/docs.yml`

**Interfaces:**
- Consumes: `.config/dotnet-tools.json` and `docs/docfx.json` (Task 2); the NuGet cache step from `ci.yml`.
- Produces: workflow `Docs` with jobs named `Docs — build` and `Docs — deploy` — the names Task 5 documented and
  Task 7 adds to the ruleset.

- [ ] **Step 1: Write the workflow**

```yaml
name: Docs

# Builds the documentation site from docs/ on every push, and publishes it to
# https://jerbersoft.github.io/fmpdotnet/ from master.
#
# Separate from CI on the argument that it answers a different question and should fail with its own name.
# "Docs — build failed" says a doc comment or a guide stopped compiling into a page; a red step inside CI would
# say "CI failed" and send somebody to the test output. (#71)
#
# Pages is configured with `build_type: workflow`, so this workflow IS the source. There is no gh-pages branch and
# no committed _site — the site is a build artifact, and a generated directory in version control is a merge
# conflict waiting for the next doc-comment fix.
#
# The same triggers as ci.yml, for the same reason: work here sits on feature branches before a PR exists, and a
# broken cross-reference is cheapest to fix at the push that broke it. Deploying is another matter — see the
# deploy job.

on:
  push:
    branches: ['**']
  pull_request:
    branches: [master]
  workflow_dispatch:

permissions:
  contents: read

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true

jobs:
  # THE JOB NAME IS LOAD-BEARING. master's ruleset requires the check `Docs — build` by name, beside
  # `.NET — build + test` from ci.yml. Rename it and the rule reports a check that is "expected" and never arrives.
  # `gh api repos/jerbersoft/fmpdotnet/rulesets` lists the ruleset; update it in the same change.
  build:
    name: Docs — build
    runs-on: ubuntu-latest
    timeout-minutes: 15

    # Superseded branch runs are cancelled; master is exempt, as in ci.yml — its runs are the record for a merged
    # commit and feed a deployment that must not be cut off half-way.
    concurrency:
      group: docs-${{ github.ref }}
      cancel-in-progress: ${{ github.ref != 'refs/heads/master' }}

    steps:
      - uses: actions/checkout@v7

      # global.json pins the SDK feature band, so the runner and a developer's machine resolve the same compiler —
      # which matters here: DocFX reads the doc comments through the compiler it is given.
      - name: Set up .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      - name: Cache NuGet packages
        uses: actions/cache@v6
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: ${{ runner.os }}-nuget-

      # DocFX is pinned at 2.78.5 in .config/dotnet-tools.json with rollForward: false, so this restores that exact
      # version rather than whatever is newest. The site's layout and the generated YAML schema are both
      # version-dependent; an unpinned tool would redesign the published site on somebody else's release schedule.
      - name: Restore DocFX
        run: dotnet tool restore

      # DocFX loads the two shipping projects through MSBuildWorkspace to read their doc comments, so their
      # dependencies have to be on disk first. docfx would restore them itself, but doing it here means a restore
      # failure is reported as a restore failure.
      - name: Restore projects
        run: dotnet restore FmpDotNet.slnx

      # `docfx docs/docfx.json` — not `docfx build` — because that form runs the metadata stage as well. It has to:
      # docs/api/*.yml is gitignored, so on a fresh checkout the reference YAML does not exist and `build` alone
      # would publish a site with an empty API section.
      #
      # --warningsAsErrors matches TreatWarningsAsErrors in the build proper. A DocFX warning is almost always a
      # link that will not resolve — a README section renamed, a guide moved — which on a published site is a dead
      # link nobody reports. Failing here is how it stays impossible to ship one.
      - name: Build the site
        run: dotnet docfx docs/docfx.json --warningsAsErrors

      - name: Upload the site
        uses: actions/upload-pages-artifact@v5
        with:
          path: docs/_site

  # Never from a pull request, and never from a branch. A fork's PR must not be able to publish to the live site,
  # and even a trusted branch should not: what is deployed is what landed on master.
  deploy:
    name: Docs — deploy
    if: github.ref == 'refs/heads/master' && github.event_name != 'pull_request'
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 10

    permissions:
      pages: write       # to publish the deployment
      id-token: write    # for the OIDC token deploy-pages exchanges for one

    # One deployment at a time, and queued rather than cancelled. cancel-in-progress: true is the usual choice and
    # it is wrong for a deploy: killing a half-finished upload to leap to a newer commit can leave the live site on
    # neither. Two pushes a minute apart should publish in order.
    concurrency:
      group: pages
      cancel-in-progress: false

    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}

    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v5
```

- [ ] **Step 2: Check it parses**

Run: `python3 -c "import yaml; d = yaml.safe_load(open('.github/workflows/docs.yml')); print(sorted(d['jobs']), [d['jobs'][j]['name'] for j in ('build', 'deploy')])"`
Expected: `['build', 'deploy'] ['Docs — build', 'Docs — deploy']`.

- [ ] **Step 3: Commit, push the branch, watch the run**

```bash
git add .github/workflows/docs.yml
git commit -F - <<'EOF'
ci: the Docs workflow — build the site on every push, deploy it from master (#71)

Its own workflow so a broken cross-reference fails under its own name. Build job on the same triggers as
CI; deploy job only on master and never from a pull request, under a pages concurrency group that queues
rather than cancels, because a half-finished upload cut off for a newer commit can leave the live site on
neither.

Claude-Session: https://claude.ai/code/session_01HXy1yrc3HQAoWJX2btt6UE
EOF
git push -u origin feat/docs-site-71
gh run list --branch feat/docs-site-71 --workflow Docs --limit 1 --json databaseId,status --jq '.[0]'
```

The run appears a few seconds after the push; if the list is empty, run the list command again rather than
sleeping. Then, with the `Docs` run's id: `gh run watch <id> --exit-status; gh run view <id> --json jobs --jq '.jobs[] | "\(.name): \(.conclusion)"'`
Expected: `Docs — build: success` and `Docs — deploy: skipped`; the CI run on the same push is also green.

Report the run URL. Do not enable Pages, open the PR, or touch any repository setting — that is Task 7's.

---

### Task 7: Landing it — Pages, the PR, the merge, the required check, the wiki

This task is the controller's, not a subagent's: three of its steps change repository settings, and each is
taken only with the repository owner's go-ahead, the way a merge is.

- [ ] **Step 1: Green on the branch**

Run: `dotnet test FmpDotNet.slnx -- RunConfiguration.TreatNoTestsAsError=true && dotnet docfx docs/docfx.json --warningsAsErrors >/dev/null && git grep -n "fmpdotnet/wiki" -- . ; echo "grep exit $?"`
Expected: every test project green, the site built, `grep exit 1`.

- [ ] **Step 2: Open the pull request**

`gh pr create --base master --head feat/docs-site-71 --title "The documentation site: guides, the README and the API reference on one DocFX site (#71)" --body-file "$SCRATCH/pr-71-body.md"`, where the body summarises the
tasks above, names the three settings changes still to come, and ends with the session URL. Wait for
`.NET — build + test` and `Docs — build` to pass on the PR.

- [ ] **Step 3: Ask, then enable Pages** — before the merge, so the first `master` run's deploy has somewhere to land

```bash
gh api -X POST repos/jerbersoft/fmpdotnet/pages -f build_type=workflow
gh api repos/jerbersoft/fmpdotnet/pages --jq '{build_type, html_url}'
```

Expected: `build_type: workflow`, `html_url: https://jerbersoft.github.io/fmpdotnet/`.

- [ ] **Step 4: Ask, then merge** — the repository convention: `git checkout master && git pull --ff-only`, then
`git merge --no-ff feat/docs-site-71` with the subject `Merge branch 'feat/docs-site-71': the documentation site (#71)`,
a body, and the Claude-Session trailer; `git diff --stat feat/docs-site-71 HEAD` empty; the full suite; `git push`.
Confirm the PR shows `MERGED`. Delete the branch locally and remotely, and `docs/docfx-site-design` with it (its
commits are on `master`).

- [ ] **Step 5: Verify the deployment**

`gh run list --branch master --workflow Docs --limit 1`, `gh run watch <id> --exit-status`, then:

```bash
for u in "" "guides/getting-started.html" "README.html#plan-gating--402-and-403" "api/FmpDotNet.FmpClient.html"; do
  printf "%-45s " "/$u"; curl -s -o /dev/null -w "%{http_code}\n" "https://jerbersoft.github.io/fmpdotnet/$u"
done
```

Expected: four `200`s. (A fragment is not sent to the server; the third line checks the README page exists, and
the anchor was validated by the build.)

- [ ] **Step 6: Ask, then require `Docs — build` on master**

```bash
gh api repos/jerbersoft/fmpdotnet/rulesets/21642385 > "$SCRATCH/ruleset.json"
python3 - <<'EOF'
import json, pathlib
p = pathlib.Path("/private/tmp/claude-501/-Users-herbertsabanal-Projects-fmpdotnet/a9dbed2a-4169-446b-8d6f-dbd380761ca6/scratchpad/ruleset.json")
r = json.loads(p.read_text())
for rule in r["rules"]:
    if rule["type"] == "required_status_checks":
        checks = rule["parameters"]["required_status_checks"]
        if not any(c["context"] == "Docs — build" for c in checks):
            checks.append({"context": "Docs — build"})
body = {k: r[k] for k in ("name", "target", "enforcement", "bypass_actors", "conditions", "rules")}
pathlib.Path(str(p) + ".put").write_text(json.dumps(body))
EOF
gh api -X PUT repos/jerbersoft/fmpdotnet/rulesets/21642385 --input "$SCRATCH/ruleset.json.put" --jq '.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context'
```

Expected: both `.NET — build + test` and `Docs — build` listed.

- [ ] **Step 7: Ask, then disable the wiki**

```bash
gh api -X PATCH repos/jerbersoft/fmpdotnet -F has_wiki=false --jq '.has_wiki'
curl -s -o /dev/null -w "%{http_code} %{redirect_url}\n" https://github.com/jerbersoft/fmpdotnet/wiki
```

Expected: `false`, then `302 https://github.com/jerbersoft/fmpdotnet` (or a `301`; either lands on the repository).

- [ ] **Step 8: Close out**

Tick every box on #71 (`gh issue edit 71 --body-file`), close it as completed, and confirm the `master` runs for
both workflows are green. Report the site URL.
