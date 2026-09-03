# Development

Building, testing and changing the SDK itself. For the workflow around a change — branches, PRs, review — see
**[Contributing](contributing.md)**.

## Setup

```bash
git clone https://github.com/jerbersoft/fmpdotnet.git
cd fmpdotnet
dotnet restore FmpDotNet.slnx
dotnet build FmpDotNet.slnx
dotnet test FmpDotNet.slnx
```

**No API key is needed.** Every live test skips itself when `FMP_API_KEY` is unset, so a fresh clone runs the whole
solution green and offline.

The SDK feature band is pinned in `global.json`, so your machine and the CI runner resolve the same compiler. Do
not pass `--dotnet-version` anywhere; it would let the two disagree silently.

**The documentation site** builds from `docs/` with DocFX, pinned at 2.78.5 in `.config/dotnet-tools.json`:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --warningsAsErrors    # what docs.yml runs: metadata, then build; a warning fails it
dotnet docfx docs/docfx.json --serve               # the same, then serves the site locally
```

A DocFX warning is nearly always a link that will not resolve — a README section that was renamed, a guide that
moved — which on a published site is a dead link nobody reports. `docs/api/*.yml` and `docs/_site/` are generated
and gitignored; `docs/api/index.md` is hand-written.

## Layout

```
src/FmpDotNet/                                    the core package
  FmpClient.cs              the 25 endpoint groups over two transports; disposable
  FmpTransport.cs           GetListAsync / GetObjectAsync / GetBytesAsync / StreamCsvAsync
  FmpBulkTransport.cs       the same surface, bound to the bulk HttpClient
  FmpRequest.cs             URL construction — appends the API key, never exposes it
  FmpOptions.cs             configuration surface
  FmpException.cs           the whole exception hierarchy, in one file
  Endpoints/                one class per group
  Models/                   the wire shapes
  Http/                     handlers, token buckets, the per-key bucket registry, URI redaction
  Serialization/            source-generated JSON context, NodaTime converters, the CSV reader

src/FmpDotNet.Extensions.DependencyInjection/     the registration package
  FmpServiceCollectionExtensions.cs       the AddFmp overloads
  FmpHostApplicationBuilderExtensions.cs  AddFmp on IHostApplicationBuilder
  FmpRegistration.cs                      the one wiring path every overload ends in
  IFmpBuilder.cs, FmpBuilder.cs           the consumer-handler surface
  FmpClientFactory.cs                     a client with no host container
  FmpOptionsBinder.cs                     by-name binding of an Fmp section

tests/FmpDotNet.Tests/                                  stub-driven unit suite — runs everywhere, no key
tests/FmpDotNet.Extensions.DependencyInjection.Tests/   the registration surface, against a real container
tests/FmpDotNet.SmokeTests/                             live API sweep — skips itself without a key

docs/                                   the documentation site — jerbersoft.github.io/fmpdotnet
  docfx.json, toc.yml, index.md         configuration, top navigation, landing page
  guides/                               these pages, and their sidebar toc.yml
  api/index.md                          hand-written front of the generated API reference
  changelog.md
  superpowers/                          designs, plans and measurements; never published
```

## Build policy

`Directory.Build.props` applies to every project: `net10.0`, nullable enabled, implicit usings, latest language
version, and **`TreatWarningsAsErrors`**.

That last one covers **`NU*` as well as `CS*`**, which means a newly published security advisory against any
package — direct or transitive — **fails the build** rather than adding a line nobody reads.

It also carries the trim/AOT analysers. The library declares `IsAotCompatible`, which turns **`IL2026` and
`IL3050` into build errors**: any reflection-based JSON or configuration binding that creeps back in fails *here*,
not months later in a consumer's trimmed publish. Those two errors are what forced the source-generated
`JsonSerializerContext` and the by-name options binding in the first place — treat them as design constraints, not
as warnings to suppress.

## Tests

```bash
dotnet test FmpDotNet.slnx
```

The unit suite is **entirely stub-driven** — `StubHandler` sits in the handler position and returns fixtures from
`tests/FmpDotNet.Tests/Fixtures`. No test in that project reaches the network.

CI runs it with an extra flag that turns it from a formality into a gate:

```bash
dotnet test FmpDotNet.slnx --no-build --logger trx -- RunConfiguration.TreatNoTestsAsError=true
```

**VSTest treats "discovery selected zero tests" as a warning and still exits 0.** Without that setting a green
tick can mean nothing ran at all — a renamed test project, a broken discovery or a filter typo all reach that
state. The setting makes zero selected tests exit 1, per project.

### What a stub suite cannot see

A stub keeps saying what it always said. It cannot notice FMP renaming a field — and a rename does not even fail,
because almost every model property is nullable and not `required`, so `System.Text.Json` deserialises the missing
name to null and the rows keep arriving looking correct.

That blind spot is covered by the **[Live Smoke Suite](live-smoke-suite.md)**, which calls the real API weekly.

## Regenerating the endpoint coverage table

The table in the README is **generated from the code**. Adding, renaming or deleting an endpoint method without
regenerating **fails the build**.

```bash
FMPDOTNET_UPDATE_README=1 dotnet test
```

Then commit the README change with the code change that caused it. `EndpointCoverageTests` drives every public
endpoint method against a stub and records the path it *actually requests*, so the table is derived from behaviour
rather than from intent — which is what makes it trustworthy in a way a hand-maintained list is not.

Never edit the block between the generated markers by hand.

## Adding an endpoint

The workflow is **measure first**, and it is not ceremony — most of the hard-won knowledge in this SDK came from
probing the live API and finding that it does not do what the documentation implies.

1. **Probe the live path** and record what actually comes back — the row shape, the ordering, the null pattern,
   what happens at the limits, and what an invalid input does. Several endpoints report bad input as **data**.
2. **Write the model** against the captured response. Nullable, not `required`. Reach for `decimal` over `long`
   and `int` — see the **[FAQ](faq.md)** for the two measured cases where an integral type costs you the whole response.
3. **Add the method** to the right endpoint class, taking an enum wherever FMP takes a fixed vocabulary.
4. **Add unit tests** driven by a fixture in `Fixtures/`, including the failure and empty cases.
5. **Regenerate the coverage table.**
6. **Add it to the sweep** so it joins the weekly live run — an endpoint the sweep skips is an endpoint whose
   renamed field goes unnoticed until a consumer hits it. An offline test enforces that the sweep can still reach
   every endpoint, so this step fails the build if you forget it.
7. **Re-record the smoke baseline** — see **[Live Smoke Suite](live-smoke-suite.md)**.
8. **Document anything surprising** in the README's upstream-behaviour section, with the measurement that
   established it. A claim without a measurement behind it does not belong there.

## Working on a bulk mapper

Set the developer disk cache so you are not re-downloading a 69 MB payload against a throttle that allows two
calls a minute:

```json
{ "Fmp": { "DeveloperBulkCacheDirectory": ".fmp-bulk-cache" } }
```

The first call to each bulk URL is written there; every later call to the same URL is replayed. Delete the
directory to refetch. It is git-ignored.

**Verify a bulk mapper by streaming the whole response through it, not a sample.** Every bulk model in this
repository was checked that way — 3.2 million rows and roughly 560 MB across the milestone.

Then confirm you did not accidentally buffer: `BulkStreamingMemoryTests` exists for that, and lives in its own
non-parallel collection because a memory measurement is meaningless while other tests are allocating alongside it.

Full detail in **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)** and
[Working on a bulk mapper](../../README.md#working-on-a-bulk-mapper).

## Local configuration and secrets

`.env` and its variants are git-ignored, as is the developer bulk cache. Never commit a key.

For the smoke suite, pass the key on the command line rather than storing it:

```bash
FMP_API_KEY=… dotnet test tests/FmpDotNet.SmokeTests
```

## The four workflows

| Workflow | Trigger | What it guards |
|---|---|---|
| **CI** (`ci.yml`) | every push to any branch, PRs to `master` | build + test |
| **Docs** (`docs.yml`) | every push to any branch, PRs to `master` | that the site builds with zero warnings; deploys it from `master` |
| **Publish** (`publish.yml`) | a published GitHub Release; a CI run that passed on `master` | that what reaches nuget.org was packed from a tested commit, matches its tag, and is exactly the set `PACKAGES` names |
| **Live smoke** (`smoke.yml`) | Mondays 06:17 UTC, or manual dispatch | that FMP still sends the shapes the SDK reads |

They are separate because the smoke suite's answer **changes with the market rather than with the commit** — a
push-triggered run would report yesterday's earnings calendar as a regression in today's diff — and because its bulk
tier spends the key's standing. Docs is separate so that a broken cross-reference fails under its own name rather than
as "CI failed". Publish is separate because nuget.org's Trusted Publishing policy binds to a workflow file by name, so
the workflow that pushes has to be the one the policy names — which is also why it is never called as a reusable
workflow.

Branch pushes are built, not just PRs, because work here happens on feature branches that may sit a while before
one is opened.

### Three names you must not change casually

The CI job is called **`.NET — build + test`** and the docs build job **`Docs — build`**, and `master`'s ruleset
requires both checks **by name**.

The third is the CI workflow's own `name: CI`, which `publish.yml` matches on with `workflow_run`. Renaming it
does not fail anything — CI stays green and Publish simply never fires again, so prereleases stop appearing with
no error to notice.

Rename either job and the rule stops matching. GitHub does not report an error — it reports a check that is
"expected" and never arrives, so every PR waits forever on something that already passed under a different name.
**If you rename either, update the ruleset in the same change:**

```bash
gh api repos/jerbersoft/fmpdotnet/rulesets
```
