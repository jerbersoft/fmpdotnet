# The documentation site — design, 2026-09-03

No issue yet; one should be opened before implementation, per CONTRIBUTING.

This repository documents itself in three places that do not know about each other. The README is the
reference: the generated endpoint table, the measured upstream behaviour, the registration paths, the
versioning scheme. The XML doc comments ship inside the packages and reach IntelliSense. And a
seventeen-page GitHub wiki carries the guides, the runbooks, the FAQ and the changelog, links into the README for every
measured number, and lives in a separate git repository that no pull request here can touch.

This design replaces the third of those with a DocFX site at **`https://jerbersoft.github.io/fmpdotnet/`**
that holds all three: the wiki's pages moved into `docs/guides/`, the README rendered as a page from the
same file, and an API reference generated from the doc comments. The wiki is then disabled. The shape is
the one `databentodotnet` reached after arguing about its own site five times (its #67, #69, #70, #78,
#80, #82), and this design skips the intermediate states rather than replaying them: a site and a wiki
never coexist here as two canonical surfaces.

The claims below about DocFX's behaviour were checked on 2026-09-03 with a throwaway build against this
repository's two projects, using DocFX 2.78.5 — the version this design pins. Each is marked *(spike)*.

## Goal

One documentation surface, in this repository, built and validated on every push: a guide that links to
a README section or an API type is checked by the build, and a behaviour change and the guide describing
it land in the same pull request.

## What this is not

- **Not a rewrite of the guides.** The wiki's pages move with their prose intact. The only edits are the
  link forms, two sentences that describe "this wiki", and the additions named under "Two guides gain a
  section". Rewriting a page is a separate decision per page, and it should be made in a diff that
  changes nothing else.
- **Not a split of the README.** The README stays the reference and stays the package README for both
  packages. Moving its sections onto the site was considered and rejected on 2026-09-03: it is the one
  copy of the measured behaviour, it ships inside every package, and the site can render it as it is.
- **Not a conversion of type names in prose to `<xref:>`.** Guides that name `FmpClient` in backticks
  keep doing so. Cross-references to the API reference are available from the first build; adopting them
  page by page is later work, and a guide that uses none is not wrong.
- **Not versioned documentation.** One site, describing `master`. There is no released version to hold
  a second copy for.
- **Not a custom domain.** `jerbersoft.github.io/fmpdotnet` is tied to the same account as the
  repository and fails only when the repository does; a registered domain can lapse on its own.
- **Not a `CHANGELOG.md` at the root.** The wiki's Changelog becomes `docs/changelog.md`, on the site,
  where the wiki's readers already found it.
- **Not a change to what the packages contain.** The doc comments are the API reference's source and are
  not edited here; `GenerateDocumentationFile` and the per-file CS1591 exemptions stay as #21 left them.

## Global constraints

- **DocFX 2.78.5, pinned in `.config/dotnet-tools.json` with `rollForward: false`.** The site's layout and
  the generated YAML schema are both version-dependent. 2.78.5 is the newest on nuget.org as of
  2026-09-03 and the version `databentodotnet` runs.
- **`--warningsAsErrors` on every build, local and CI.** A DocFX warning is nearly always a link that
  will not resolve. *(spike)* Two warnings this design relies on: `InvalidFileLink`, for a markdown link
  to a file the build does not know, and `InvalidBookmark`, for a link whose `#fragment` names no heading
  in the target file. Both are what turn a stale README anchor into a red build instead of a dead link.
- **Content is enumerated, never globbed from `docs/`.** `docs/superpowers/` holds forty specs and
  eighteen plans and is working material. A `**/*.md` glob would publish the next plan somebody writes.
- **The two shipping projects are listed by name in `docfx.json`**, not as `../src/**/*.csproj`. Whether
  a third package belongs in the published reference is a decision somebody should make in a diff, and
  a test pins the list to the projects under `src/` so the decision cannot be skipped by omission.
- **One canonical copy of each fact.** Guides link to the README; the README is not restated in a guide;
  the API reference renders the doc comments and does not restate them.
- **The job name `Docs — build` is load-bearing** once it is a required check, for the same reason
  `.NET — build + test` is: a ruleset matches a check by name and reports a renamed one as "expected"
  forever.
- **.NET SDK from `global.json`**, as `ci.yml` does. DocFX loads the projects through MSBuildWorkspace,
  so the compiler that reads the doc comments should be the one that builds them.

## The site

Six entries in the top navigation, in this order:

| Entry | Target | What it is |
|---|---|---|
| Guides | `guides/`, opening on Getting Started | The wiki's pages, in the wiki's grouping |
| Reference | `../README.md` | The repository README, rendered from the same file |
| API reference | `api/`, opening on `api/index.md` | Generated from the doc comments of both packages |
| Changelog | `changelog.md` | The wiki's Changelog |
| Packages | `https://github.com/jerbersoft/fmpdotnet/packages` | The GitHub Packages listing for this repository — verified 200 on 2026-09-03 |
| GitHub | `https://github.com/jerbersoft/fmpdotnet` | |

The order says what a reader needs first: to be taught, then to look a measured fact up, then a member
list. `href` on a folder entry makes the folder's `toc.yml` the sidebar; `topicHref` is the page the nav
link opens. Without `topicHref` an entry lands on whichever page sorts first, and the hand-written index
is built but unreachable.

**The guides sidebar** reproduces the wiki's `_Sidebar.md` — its groups and its order — because that is
the navigation the wiki's readers already know, and #82's rule holds: moving pages and reordering them
would be two changes in one diff.

| Group | Pages |
|---|---|
| Using it | Getting Started · Configuration · Endpoint Coverage · Recipes · Error Handling |
| Operating it | Rate Limits and Bulk Data · Troubleshooting |
| Understanding it | FAQ · Architecture |
| Contributing | Contributing · Development · Live Smoke Suite |
| Releases | Releases and Versioning |

Changelog leaves the Releases group for the top navigation, where a reader arriving from a package version
looks for it. Contributing, Development and Live Smoke Suite come to the site because retiring the wiki
has to give them a home and there is no `CLAUDE.md` here to absorb them; the root `CONTRIBUTING.md` stays
the short form GitHub surfaces and links to them.

**Template and metadata.** `default` plus `modern`, search on, footer text in `_appFooter`, and
`_gitContribute` pointing at `master` so every page carries an edit link to its source — for the README
page, to the README at the repository root.

**The sitemap** carries the one absolute URL in the configuration. DocFX emits relative links everywhere
else, which is why the site works unchanged under a `/fmpdotnet/` path; a sitemap has to spell the host
out.

## The README on the site

`docfx.json` lists `README.md` as content with `"src": ".."`. *(spike)* DocFX accepts a relative parent
path for a content source — it rejects an absolute one outside the docs directory with "SourceDir must
start with BaseDir, or relative path" — and renders the file at the site root as `README.html`.

*(spike)* A guide link written as `../../README.md#configuration` renders as
`../README.html#configuration`; from `docs/index.md` or `docs/changelog.md` the same target is
`../README.md#configuration`. The rendered page carries the anchors the guides use today, including the
one GitHub derives from an em-dash heading: `plan-gating--402-and-403`. A link to a fragment that is not
a heading fails the build with `InvalidBookmark`.

Two consequences for the README itself:

- *(spike)* Its two relative links to `docs/superpowers/specs/2026-08-27-endpoint-inventory.md` fail the
  build with `InvalidFileLink`, because the spec is not content. They become absolute
  `https://github.com/jerbersoft/fmpdotnet/blob/master/...` URLs. That is the right form anyway: the
  README is the package README, and a relative link is already broken on the package page.
- The page's title is the README's H1, `FmpDotNet`, under a navigation entry called Reference. That is
  accepted. A YAML title block at the top of the README would render on GitHub as a table.

The README gains one short section, `## Documentation`, after the introductory paragraphs and before
`## Status`: the site is the documentation, the guides start at Getting Started, this README is rendered
there as Reference, and the API reference is generated from the doc comments that also reach IntelliSense.
Four sentences, with the URLs. `databentodotnet`'s README carries the same section for the same reason.

## The API reference

Both projects, listed by name: `FmpDotNet/FmpDotNet.csproj` and
`FmpDotNet.Extensions.DependencyInjection/FmpDotNet.Extensions.DependencyInjection.csproj`, with
`TargetFramework` pinned to `net10.0` and `Configuration` to `Release`. *(spike)* The metadata stage over
the two produces 250 YAML files with zero warnings at 2.78.5, so nothing in the doc comments has to change
to make the first build green.

`docs/api/index.md` is hand-written and committed; `docs/api/*.yml` and `docs/api/.manifest` are
generated and gitignored. The index fronts the reference with the six namespaces:

| Namespace | Package | Contents |
|---|---|---|
| `FmpDotNet` | core | `FmpClient`, `FmpOptions`, the `FmpException` family, the two transports, `FmpRequest`, the shared enums and the criteria records |
| `FmpDotNet.Endpoints` | core | The 25 endpoint groups |
| `FmpDotNet.Models` | core | The response models |
| `FmpDotNet.Http` | core | The handlers and their bases, `TokenBucket`, `FmpBuckets` and `FmpBucketRegistry` |
| `FmpDotNet.Serialization` | core | The `JsonConverter<T>` implementations and the CSV reader |
| `FmpDotNet.Extensions.DependencyInjection` | extensions | `AddFmp` in every form, the host-builder extensions, `IFmpBuilder`, `FmpClientFactory` |

Two notes belong on that page rather than on any member. `FmpJsonContext` is `internal` and does not
appear; what is public in `FmpDotNet.Serialization` is the converters it registers, documented because
they are reachable, not because they are an entry point. And eight model files carry a file-scoped
`#pragma warning disable CS1591` — the seven period-shaped fundamentals and the COT report, whose
properties are flat transcriptions of FMP's wire fields — so their properties render without summaries,
by the decision `src/Directory.Build.props` records. The type-level remarks on each are where the real documentation is.

## The landing page

`docs/index.md` with `_layout: landing`, replacing the wiki's Home. In order: the name and one-sentence
lead; buttons to Getting Started, Reference and the API reference; the three-line usage snippet Home
opens with; Home's "three things worth knowing up front" (everything throws, bulk is a different animal,
time is NodaTime); and an Install section that names the feed, gives the two `dotnet add package` lines,
says to pin an exact prerelease, and links to the README's "Installing and versioning" for the rest. No
version badges: GitHub Packages has none, and a version typed onto the page is the one number on the site
that goes stale on every push. Home's "What this wiki is, and is not" is not carried over; its content is
the navigation.

## The build and the deploy

`.github/workflows/docs.yml`, separate from `ci.yml` on the argument `databentodotnet` makes: it answers a
different question and should fail with its own name. "Docs — build failed" says a doc comment or a guide
stopped compiling into a page; a red step inside CI sends somebody to the test output.

**Triggers** match `ci.yml`: every branch push, pull requests to `master`, and manual dispatch. Branch
pushes are built for the reason `ci.yml` gives — work sits on feature branches before a PR exists, and
that is when feedback is worth most. Runner minutes are free on this public repository, and two of
`ci.yml`'s comments that call it private are corrected in the same change so the two workflows do not
disagree about what they cost.

**The `build` job**, named `Docs — build`, on `ubuntu-latest` with a 15-minute timeout:

1. checkout;
2. `actions/setup-dotnet` with `global-json-file: global.json`;
3. the NuGet cache step from `ci.yml`, keyed off the project files;
4. `dotnet tool restore` — the pinned DocFX;
5. `dotnet restore FmpDotNet.slnx` — so a restore failure is reported as one, rather than from inside
   DocFX's MSBuildWorkspace;
6. `dotnet docfx docs/docfx.json --warningsAsErrors` — that form, not `docfx build`, because it runs the
   metadata stage as well: on a fresh checkout `docs/api/*.yml` does not exist and `build` alone would
   publish an empty API section;
7. `actions/upload-pages-artifact` from `docs/_site`.

Its concurrency group is `docs-${{ github.ref }}`, cancelling superseded runs except on `master`, as
`ci.yml` does.

**The `deploy` job**, named `Docs — deploy`, runs only when `github.ref == 'refs/heads/master'` and the
event is not a pull request. A fork's PR must not be able to publish, and even a trusted branch should not:
what is deployed is what landed on `master`. It needs `build`, takes `pages: write` and `id-token: write`,
targets the `github-pages` environment, and runs `actions/deploy-pages`. Its concurrency group is `pages`
with `cancel-in-progress: false`: killing a half-finished upload to leap to a newer commit can leave the
live site on neither, and two pushes a minute apart should publish in order.

**Pages** is configured with `build_type: workflow`, so the workflow is the source. No `gh-pages` branch
and no committed `_site`: a generated directory in version control is a merge conflict waiting for the
next doc-comment fix. `.gitignore` gains `docs/_site/`, `docs/api/*.yml` and `docs/api/.manifest`.

**The account-level hazard is already cleared.** `databentodotnet` #80 found that a custom domain on the
user Pages site, `jerbersoft.github.io`, rewrote every project page under the account into a 404. That
site now has no custom domain (`cname: null`, checked 2026-09-03), so `jerbersoft.github.io/fmpdotnet`
resolves as a project page the moment a deployment exists.

**The ruleset** on `master` gains `Docs — build` as a second required check, beside `.NET — build + test`.
A doc comment that breaks the site should be a red PR, which is the point at which somebody can still fix
it cheaply. The `deploy` job is not required: it does not run on PRs.

## The migration

Seventeen wiki files. Fourteen move — thirteen guides and the changelog — and three are superseded:

| Wiki page | Becomes |
|---|---|
| `Home` | `docs/index.md`, rewritten as the landing page above |
| `_Sidebar` | `docs/guides/toc.yml` and `docs/toc.yml` |
| `_Footer` | `_appFooter` in `docfx.json` |
| `Getting-Started` | `docs/guides/getting-started.md` |
| `Configuration` | `docs/guides/configuration.md` |
| `Endpoint-Coverage` | `docs/guides/endpoint-coverage.md` |
| `Recipes` | `docs/guides/recipes.md` |
| `Error-Handling` | `docs/guides/error-handling.md` |
| `Rate-Limits-and-Bulk-Data` | `docs/guides/rate-limits-and-bulk-data.md` |
| `Troubleshooting` | `docs/guides/troubleshooting.md` |
| `FAQ` | `docs/guides/faq.md` |
| `Architecture` | `docs/guides/architecture.md` |
| `Contributing` | `docs/guides/contributing.md` |
| `Development` | `docs/guides/development.md` |
| `Live-Smoke-Suite` | `docs/guides/live-smoke-suite.md` |
| `Releases-and-Versioning` | `docs/guides/releases-and-versioning.md` |
| `Changelog` | `docs/changelog.md` |

The source is the clone at the session scratchpad, at wiki commit `662a4ac`, which is the wiki's head
after this session's three updates (#61, #65, #69). Kebab-case filenames, as `databentodotnet` uses.

**Link rewrites**, counted on 2026-09-03 across the fourteen pages, applied by a throwaway script so the
diff is mechanical and a reviewer can check the rule rather than every instance:

| Form | Count | Becomes |
|---|---|---|
| `[[Page Name]]`, one of them `[[FAQ\|FAQ]]` | 70 | `[Page Name](page-name.md)` from a guide; `guides/page-name.md` from `changelog.md`; `[[Changelog]]` from a guide is `../changelog.md`. No migrating page links to `Home` |
| `https://github.com/jerbersoft/fmpdotnet/blob/master/README.md#anchor` | 17, at 10 distinct anchors, all in guides | `../../README.md#anchor` — the build validates every one. (The wiki's five other README links were on Home, the sidebar and the footer, none of which move) |
| `https://github.com/jerbersoft/fmpdotnet/blob/master/docs/superpowers/specs/...` | 2 | Unchanged: absolute, because specs are not on the site |

**Two sentences describe "this wiki"** and are reworded to describe the site: the footer's, which
becomes `_appFooter`, and `Contributing` line 113 ("This wiki holds guides and process…"). Version
examples such as `0.1.0-ci.79` in Getting Started and Releases and Versioning stay as they are; they
are examples of a shape, and the README's "Installing and versioning" is the canonical description.

**Inbound references**, every one in a tracked file, counted with `git grep fmpdotnet/wiki`:

| File | Links | Becomes |
|---|---|---|
| `CONTRIBUTING.md` | 7 | `https://jerbersoft.github.io/fmpdotnet/guides/<page>.html` — absolute, because GitHub renders this file, not the site |
| `SECURITY.md` | 3 | The same form |
| `README.md` | 0 | The new `## Documentation` section, above |

`Directory.Build.props` under `src/` moves `PackageProjectUrl` from the repository to the site. The
reasoning is `databentodotnet` #85's: NuGet renders this property as "Project website"; the repository is
rendered separately as "Source repository" from `RepositoryUrl`, which is unchanged and is what Source
Link uses; and the package README this build already freezes into every package page will carry site
URLs, so pointing the property at the site adds no new class of permanence risk. The comment on the
property says so.

**Two guides gain a section.** `Contributing` gains a "where a fact lives" table, which is the rule this
design rests on and has no other home here:

| Content | Home |
|---|---|
| Guides, runbooks, troubleshooting, FAQ | `docs/guides/`, in the same PR as the behaviour they describe |
| Measured upstream behaviour, the endpoint table, the registration paths, versioning | `README.md`, rendered on the site as Reference |
| API | The XML doc comments; the site renders them and does not restate them |
| Changelog | `docs/changelog.md` |
| Designs, plans and measurements | `docs/superpowers/`, never published |

`Development` gains the two local commands — `dotnet tool restore && dotnet docfx docs/docfx.json
--warningsAsErrors` to build, and the same with `--serve` to preview — its layout tree gains the `docs/`
entries, and its "The two workflows" section becomes three. The Changelog gains an entry for this issue
under Unreleased.

## Retirement

After the merge, in this order, each step a setting outside the working tree and each taken with the
repository owner's go-ahead, the way merges are:

1. **Enable Pages** with `build_type: workflow` — before the merge's `master` run reaches `deploy`, which
   fails against a repository with no Pages site. Enabling it earlier is harmless: with `workflow` as the
   source there is nothing to serve until a deployment exists.
2. **Merge**, watch `Docs — deploy`, and verify the URL: the landing page, one guide, the README page's
   anchor a guide links to, and one API page, each 200.
3. **Require `Docs — build`** on the `master` ruleset.
4. **Disable the wiki** (`has_wiki: false`). Not delete: disabling is enough, and `databentodotnet`
   verified after doing the same that GitHub then 302s every `/wiki/*` path to the repository home page,
   so a stale link somewhere lands one click from the guides rather than on a 404. The wiki's git history
   survives, hidden, and the scratchpad clone is a second copy.

Steps 1 and 4 are the whole of what this design cannot do from inside a pull request.

## Testing

**The gate is the build.** `dotnet docfx docs/docfx.json --warningsAsErrors` exits non-zero on any
warning, and the two warnings that matter — `InvalidFileLink` and `InvalidBookmark` — were both observed
firing in the spike on exactly the mistakes this design has to catch. `Docs — build` runs it on every
push; a developer runs it before opening a PR.

**Two facts DocFX does not check** get tests in `tests/FmpDotNet.Tests/DocsSiteTests.cs`, finding the
repository root the way `EndpointCoverageTests` does (walking up to `FmpDotNet.slnx`):

- **Every project under `src/` is named in `docfx.json`, and every name there exists.** Read with
  `System.Text.Json`. This is the "names what ships" guard `ci.yml`'s pack step makes for the feed,
  made for the reference.
- **Every `docs/guides/*.md` is in `docs/guides/toc.yml`, and every `href` there exists.** DocFX builds
  a page nobody links to without a word. A regex over `href:` lines is enough; the test project has no
  YAML dependency and should not gain one for a sidebar.

Neither test needs DocFX installed, so `.NET — build + test` runs them as it runs everything else.

**Not tested by code:** that no tracked file links to the wiki. That is a definition-of-done item checked
once with `git grep`; after the wiki is disabled a new link would redirect to the repository home, which
is low harm, and a test that greps the repository for a URL is the kind that gets deleted the first time
it is inconvenient.

## File layout

```
.config/dotnet-tools.json                 new — docfx 2.78.5, rollForward false
.github/workflows/docs.yml                new — Docs — build, Docs — deploy
.github/workflows/ci.yml                  two comment lines corrected: the repository is public
.gitignore                                docs/_site/, docs/api/*.yml, docs/api/.manifest
docs/docfx.json                           new
docs/toc.yml                              new — the top navigation
docs/index.md                             new — the landing page
docs/changelog.md                         moved from the wiki
docs/api/index.md                         new — hand-written front of the reference
docs/guides/toc.yml                       new — the sidebar
docs/guides/*.md                          thirteen pages moved from the wiki
docs/superpowers/                         unchanged, unlisted
README.md                                 ## Documentation; two spec links made absolute
CONTRIBUTING.md                           seven links to the site
SECURITY.md                               three links to the site
src/Directory.Build.props                 PackageProjectUrl → the site
tests/FmpDotNet.Tests/DocsSiteTests.cs    new — the two tests above
```

## Risks

- **The README is long for a web page.** 1,086 lines, 368 of them the generated endpoint table. DocFX
  renders it in one page with a right-hand heading outline, which is how the wiki's readers used it on
  GitHub. If the table ever wants its own page, that is a README change first, since the README is the
  single copy.
- **GitHub and DocFX derive heading anchors independently.** *(spike)* They agree on every anchor the
  guides use today, em-dash heading included. The build validates the site's side; nothing validates
  GitHub's, and nothing needs to, because no tracked file links to a README anchor on GitHub.
- **A DocFX minor release can change the rendered site.** That is what the pin is for. Bumping it is a
  deliberate diff that somebody looks at the site after.
- **The feed still needs authentication to restore from.** The site's install section can say how to add
  the source; it cannot make GitHub Packages anonymous. The README's "Installing and versioning" already
  covers the grant, and the landing page links there rather than restating it.
- **`push: branches: ['**']` builds the site on every branch push.** A few minutes per push on a public
  repository, for feedback at the moment a broken cross-reference is cheapest to fix — the same trade
  `ci.yml` makes.

## Definition of done

- `dotnet tool restore && dotnet docfx docs/docfx.json --warningsAsErrors` is green locally: 0 warnings
- Every page in the migration table is reachable from the navigation, and the top nav is Guides →
  Reference → API reference → Changelog → Packages → GitHub
- `git grep fmpdotnet/wiki` over tracked files finds nothing
- `dotnet test FmpDotNet.slnx` is green with `DocsSiteTests` included
- Merged; `Docs — deploy` green; the four URLs in "Retirement" step 2 return 200
- `Docs — build` is a required check on `master`
- The wiki is disabled and `https://github.com/jerbersoft/fmpdotnet/wiki` redirects to the repository
