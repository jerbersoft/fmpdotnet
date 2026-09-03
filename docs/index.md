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
