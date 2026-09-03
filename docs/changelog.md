# Changelog

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning is described in
**[Releases and Versioning](guides/releases-and-versioning.md)**.

> **No release has been cut.** Everything published so far is a CI prerelease — `0.1.0-ci.<run number>` — and there
> are no git tags. The history below is **reconstructed from the commit log**, grouped into the slices the work
> was actually built as, so that it is useful now rather than starting empty at 1.0. Entries carry their issue
> numbers; dates are commit dates.
>
> Going forward, changes should be added under **Unreleased** as they land.

---

## [Unreleased]

Everything below is in `master` and available in the latest `0.1.0-ci.N` prerelease.

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

### Host registration — #65 · 2026-09-02 → 2026-09-03

Four ways to register the SDK, one wiring path. The design is at
`docs/superpowers/specs/2026-09-01-host-registration-design.md`; the README gained a "Registering the SDK" section.

**Added**
- `FmpClientFactory.Create` — a client with no host container. It builds a private container through `AddFmp`,
  validates in `Create`, and owns what it built: `Dispose` disposes the container and both `HttpClient`s, and a
  disposed client refuses to send.
- `AddFmp` on `IHostApplicationBuilder`, binding `Fmp` or `Fmp:{name}` off the builder's configuration.
- **Named registrations** — `AddFmp("research", …)` — resolved with `[FromKeyedServices("research")]`, bound from
  `Fmp:research`, validated under the name, on `HttpClient`s named `fmp:research` and `fmp-bulk:research`.
- `IFmpBuilder` — `ConfigureStandardClient`, `ConfigureBulkClient`, `ConfigureAllClients`, `UseBucketRegistry`.
  Consumer handlers sit **outermost**: entered once per logical call while the SDK's retry runs beneath them, so a
  second retry policy stacked on top multiplies with the SDK's own.
- `FmpBucketRegistry` in the core's `Http/`: one reservoir pair per API key within a container, keyed on a SHA-256
  of the key. First writer wins on caps, with a warning naming both registrations once per disagreement. Shared
  between a host and a factory-built client, it joins their reservoirs.
- `Microsoft.Extensions.Hosting.Abstractions` on the extensions package, and on the core's forbidden list.

**Fixed**
- A second `AddFmp` for the same registration appended a second handler chain — measured at **nine sends** for a
  three-attempt call. It now re-configures the options and wires nothing twice, and throws if the later call
  carries an `IFmpBuilder` callback that could no longer take effect.

**Changed**
- ⚠️ **breaking** `FmpClient` is a composition of two transports — `FmpClient(FmpTransport, FmpBulkTransport)` —
  and the 25-argument constructor is gone. It had no caller.
- ⚠️ **breaking** `FmpClient` is `IDisposable`. A container tracks a resolved client until its scope ends, so
  resolve it inside a scope or hold one instance.
- ⚠️ **breaking** The seven handler types are constructed inside the chain rather than registered as services;
  `GetRequiredService<FmpRetryHandler>()` and the like now throw.
- The default registration uses keyed services under the name `""`; the unkeyed `FmpTransport`,
  `FmpBulkTransport` and `FmpBuckets` still resolve for it.

### The registration package — #61 · 2026-09-02

`AddFmp` moved out of the core into **`FmpDotNet.Extensions.DependencyInjection`**, published beside it.

**Added**
- The `FmpDotNet.Extensions.DependencyInjection` package, carrying `Microsoft.Extensions.Http` and
  `Microsoft.Extensions.Configuration.Abstractions` with a project reference to the core.
- `PackageBoundaryTests` reads the core assembly's compiled references, so a container, HTTP-factory or
  configuration dependency creeping back into the core fails the build.

**Changed**
- ⚠️ **breaking** The namespace is `FmpDotNet.Extensions.DependencyInjection`, not `FmpDotNet.DependencyInjection`.
- The core references `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions` and NodaTime
  only. A consumer with a container of its own can reference the core alone.
- Both packages are versioned and published together; a consumer referencing both pins them to the same version.

### Company coverage — #29 · 2026-08-27 → 2026-08-28

Thirteen Company paths modelled, taking generated coverage to **114 of FMP's 243 unique `stable/` paths**.

**Added**
- The three market-capitalization paths — `market-capitalization`, `market-capitalization-batch`,
  `historical-market-capitalization`.
- `stock-peers`.
- Both employee-count paths — `employee-count` and `historical-employee-count` — mapped onto one dataset.
- `key-executives`.
- Executive compensation and its industry benchmark — `governance-executive-compensation`,
  `executive-compensation-benchmark`.
- `company-notes`.
- The two mergers-and-acquisitions paths — `mergers-acquisitions-latest`, `mergers-acquisitions-search`.

**Fixed**
- **Market cap is fractional on `stable/profile`, so it cannot be a `long`.** Reading it as an integer throws and
  aborts the whole response rather than the field.

**Changed**
- The live smoke baseline records the shapes of all thirteen new endpoints.
- Four documentation claims a whole-branch review found **false** were corrected, and `Quote.MarketCap` no longer
  claims the profile endpoint is integral.

### Statements coverage — #28 · 2026-08-27

Nineteen Statements paths, twenty methods. Coverage reached 101 of 243 before Company landed.

**Added**
- The three rolling-twelve-month statements — income, balance sheet, cash flow TTM.
- The three growth paths and the two TTM metric snapshots (`key-metrics-ttm`, `ratios-ttm`).
- The four as-reported paths and both revenue segmentations (geographic and product).
- Owner earnings — **including its 50-row ceiling, which the endpoint does not report**.
- Financial report dates, the rendered JSON report, and the XLSX workbook.
- The market-wide recency feed `latest-financial-statements`, and its page ceiling — with both a paged
  `GetLatestStatementsAsync` and a walking `StreamLatestStatementsAsync`.
- `FmpTransport` learned to read a **JSON object** and a **binary body**, which the report and workbook paths need.
- The five CSV-built statement models learned to bind JSON as well, so one model serves both pipelines.

**Changed**
- **`FiscalPeriod` widened from five members to six**, and an explicit full-history limit is now sent. The
  previous default-limit truncation already affected the eight endpoints that had shipped.

**Fixed**
- `company-screener` volume modelled as `decimal`, not `long`.
- The claim that owner-earnings `maintenanceCapex` is always negative was removed — it is not.

### Endpoint inventory — #25 · 2026-08-27

**Changed**
- FMP's endpoint inventory enumerated and cross-checked against two independent sources. **The denominator was
  corrected from 230 to 243** unique `stable/` paths — the asset-class sections re-document existing paths rather
  than adding endpoints.
- Documented that the `PerMinuteCap` default of 660 is calibrated to **Premium's 750/min**, and that Ultimate
  allows 3,000.

### Directory and Search coverage — #25 · 2026-08-27

Seventeen paths, eighteen methods — the sweep grew from 49 endpoints to 67.

**Added**
- The country list and the ETF list.
- The four asset-class symbol lists — commodities, cryptocurrency, forex, index.
- Exchanges, financial-statement symbols, and earnings-transcript symbols.
- The symbol-change archive, **asked for in full rather than at its hidden default**.
- The SEC registrant index — `cik-list` — one page at a time (`GetCikListAsync`) or all of it
  (`StreamCikListAsync`).
- Symbol, name, CIK, CUSIP and ISIN lookup.
- Exchange variants, **and the v3 profile shape hiding behind a `stable/` path**.

**Fixed**
- The search limit guard's `ParamName` was wrong; it is now correct, documented and tested.
- Two unmeasured ordering and type claims were removed from the Directory documentation, and blank input is now
  proven rejected on all five search methods.

### Quote and Chart coverage — #24 · 2026-08-27

**Added**
- The Quote and Chart groups — **26 endpoints**. Quotes full and short, aftermarket trade and quote, price change,
  and the per-asset-class batches; end-of-day history in four adjustment variants, and six intraday intervals
  behind one method and a `ChartInterval` enum.

This is the slice that made asset-class breadth largely free: one `GetQuoteAsync` was measured serving equities,
ETFs, indices, commodities, forex and crypto alike.

### Live smoke suite — #26 · 2026-08-27

**Added**
- A second test project that calls the **real** FMP API weekly and records **which fields carried a value**, not
  merely that a call succeeded — because a rename does not fail a nullable model. One line per property, so a
  rename is a one-line diff. Runs Mondays at 06:17 UTC; the `*-bulk` sweep is opt-in because FMP restricts keys
  that call those endpoints often.

See **[Live Smoke Suite](guides/live-smoke-suite.md)**.

**Changed**
- The bulk-streaming memory test was isolated into its own non-parallel collection — a memory measurement is
  meaningless while other tests allocate alongside it.

### Packaging, publishing and generated docs — #10, #21, #23 · 2026-08-26

**Added**
- Publishing to this repository's **GitHub Packages** NuGet feed, so a consumer can restore a pinned version.
- Licence, symbol package and **Source Link**, making the package properly consumable — a debugger steps into the
  SDK's source at the exact commit the binary was built from.
- The README's **endpoint coverage table is now generated from the code**. Adding, renaming or deleting an
  endpoint without regenerating fails the build.

### Breaking: one error channel — 2026-08-26

**Removed** ⚠️ **breaking**
- **Every failure now throws. Nothing returns `null` to signal one.** The previous nullable-return imitation of
  `TryX` put two error channels on one signature and gave `null` a meaning the signature could not carry.
  (`refactor!: one error channel`)
- **Members that existed only to serve other members were deleted.**
  (`refactor!: delete the members that only existed to serve other members`)

`null` now always means an answer FMP genuinely gave. See **[Error Handling](guides/error-handling.md)**.

**Fixed**
- **A rejected key is no longer reported as a billing problem.** 402 and 403 are both
  `FmpPlanRestrictedException` but are worded differently and expose `IsPlanLimitation` / `IsRejectedCredential`,
  because a 403 points at the credential at least as often as at the plan.

### Bulk coverage — #11 – #16 · 2026-08-26

**Added**
- The `*-bulk` surface: the statement family and the TTM downloads; price targets, analyst consensus and earnings
  surprises; ratings, DCF, scores, peers and ETF holdings.
- **A developer disk cache for the bulk endpoints** — replay a downloaded response from disk while iterating on a
  CSV mapper. Off by default, bulk-only, and it logs a warning the first time it serves anything. It is
  deliberately *not* a caching layer. See **[Rate Limits and Bulk Data](guides/rate-limits-and-bulk-data.md)**.

### Universe, delistings and the screener — #17 – #20 · 2026-08-26

**Added**
- The symbol universe (`stock-list`, `actively-trading-list`) and the delisting archive — the latter enforcing
  FMP's **undocumented 100-row `limit` cap** at the call site rather than letting the clamp happen silently.
- The company screener, **behind a typed `ScreenerCriteria` object**, so a misspelled filter will not compile —
  FMP silently ignores unrecognised parameter names and widens the query.

### First endpoints — #1 – #9 · 2026-08-26

**Added**
- The seven period-shaped fundamentals endpoints (#4).
- Shares float and shares outstanding (#1).
- `available-sectors` and `available-industries` as a Directory group (#2).
- Altman Z and Piotroski F scores (#5).
- The economic calendar as an Economics group (#9).
- Analyst estimates as an Analyst group (#7).
- Earnings and the earnings calendar as a Calendar group (#6, #8) — including the
  **silent 4000-row truncation** signal.
- `profile-bulk` and `shares-float-all`, the whole-universe fast paths (#3).
- Calendar, Analyst and Economics wired onto `FmpClient`.

**Fixed**
- **The API key is kept out of the timeout exception message.**

### Foundations — 2026-08-26

**Added**
- The .NET 10 solution scaffold.
- `FmpTransport`, the throttling, and the first endpoint on each pipeline.
- CI on GitHub Actions (#22) — later given a `master` ruleset requiring a PR and the named check.

**Changed**
- The root namespace and assembly were renamed to **`FmpDotNet`** — a package called `FinancialModelingPrep` would
  read as something FMP publishes and supports, and this is an independent client.

---

## Conventions used here

- ⚠️ **breaking** marks a change that removed or altered a public member. Commits carry `!` — `refactor!:`.
- Until 1.0, **treat a minor bump as potentially breaking**. The surface is still being shaped by what the live
  API turns out to do, and the breaking changes so far *removed* members after measurement or use showed they were
  the wrong shape.
- Issue numbers link the slice to its tracking issue.
