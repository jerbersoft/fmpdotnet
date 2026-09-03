# Getting Started

From nothing to a working call, in order. Budget ten minutes, most of it on step 1.

## Prerequisites

* **.NET 10 SDK.** The repository pins a feature band in `global.json`; a consumer only needs a 10.x SDK.
* **An FMP API key.** The SDK targets **Premium** as the lowest paid tier — see
  [the throttle note](#4-set-the-throttle-to-your-tier) below for why that matters even on a bigger plan.
* **A GitHub personal access token** with the `read:packages` scope, to restore the package.

## 1. Add the package source

The packages are published to **this repository's GitHub Packages NuGet feed**, not to nuget.org.

> **GitHub Packages requires authentication for every NuGet restore**, including public packages. There is no
> anonymous read. This is a GitHub Packages property, not a choice this project made — see the
> [FAQ](faq.md) for why the package is not on nuget.org.

Create a `nuget.config` beside your solution file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="jerbersoft" value="https://nuget.pkg.github.com/jerbersoft/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <jerbersoft>
      <add key="Username" value="%GITHUB_USERNAME%" />
      <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
    </jerbersoft>
  </packageSourceCredentials>
</configuration>
```

The `%VAR%` form reads from environment variables, so **the token never lands in a file you might commit**. Set
them in your shell profile or your CI's secret store:

```bash
export GITHUB_USERNAME=your-github-login
export GITHUB_PACKAGES_TOKEN=ghp_...        # read:packages is the only scope needed
```

**In GitHub Actions, you do not need a PAT at all.** A workflow's own `GITHUB_TOKEN` can read this feed, because
the package grants read access to consuming repositories through its *Manage Actions access* setting. That grant
is made once per package — both `FmpDotNet` and `FmpDotNet.Extensions.DependencyInjection` need it — and is not
a secret in either repository.

```yaml
- name: Restore
  run: dotnet restore
  env:
    GITHUB_USERNAME: ${{ github.actor }}
    GITHUB_PACKAGES_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

If restore fails here, **[Troubleshooting](troubleshooting.md)** has the four failure modes and what each one looks like.

## 2. Install, pinned

```bash
dotnet add package FmpDotNet.Extensions.DependencyInjection --version 0.1.0-ci.79
```

That brings `FmpDotNet` — the client, the models and the transports — with it. The extensions package is the
registration surface: `AddFmp` in every form, the `IHostApplicationBuilder` sugar and `FmpClientFactory`. A project
that references both directly pins them to the **same** version: the extensions package depends on the core as a
floor, not an exact version, so NuGet will otherwise pair an older `AddFmp` with a newer core, and that pairing
breaks the first time the core reshapes something the older wiring constructs.

**Pin the exact prerelease. Do not float.** Every push to `master` publishes a new version, so a floating
reference is a build that changes underneath you without a commit. Pinning also makes *"which SDK did this commit
build against"* answerable from your own git history — see **[Releases and Versioning](releases-and-versioning.md)**.

Browse the available versions on the repository's
[Packages page](https://github.com/jerbersoft/fmpdotnet/packages).

## 3. Register the client

```csharp
using FmpDotNet;
using FmpDotNet.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddFmp(builder.Configuration);   // binds the "Fmp" configuration section
```

or, configuring in code:

```csharp
builder.Services.AddFmp(o =>
{
    o.ApiKey = Environment.GetEnvironmentVariable("FMP_API_KEY")!;
});
```

On a host built with `Host.CreateApplicationBuilder` or `WebApplication.CreateBuilder`, the same thing is one
line on the builder itself:

```csharp
builder.AddFmp();                                  // binds "Fmp" off builder.Configuration
```

`AddFmp` registers **two** named `HttpClient`s — `"fmp"` for ordinary endpoints, `"fmp-bulk"` for `*-bulk` — with
the retry, throttle and timeout handlers on each, the reservoir pair they draw from, and `FmpClient` itself.
Calling it again is safe: a second call re-configures the options and wires nothing twice.

No container at all? `FmpClientFactory.Create("…")` builds a private one through the same `AddFmp` and hands you
a client that owns it — dispose the client when you are done. Named registrations, for a process holding more
than one FMP configuration, and putting your own handlers on the clients are in the README's
[Registering the SDK](../../README.md#registering-the-sdk).

Options are validated at **startup**, not at first call, so a bad `BaseUrl` or a `PerMinuteCap` of `0` fails while
you are still looking at the console. The API key is **deliberately not validated** — an SDK cannot know whether
its caller intends to make a request. Assert it in your host if you need to.

## 4. Set the throttle to your tier

The default `PerMinuteCap` is **660**, calibrated to Premium's 750/min because that is the lowest paid tier the
SDK targets. A default tuned to a higher tier would trip 429s for everyone below it.

**On Ultimate, raise it.** Ultimate allows 3,000/min, so the default spends roughly a fifth of what you are paying
for. Use about 88% of your tier's published limit to keep the same headroom:

```json
{ "Fmp": { "ApiKey": "…", "PerMinuteCap": 2640 } }
```

Full option list in **[Configuration](configuration.md)**.

## 5. Make a call

```csharp
var fmp = provider.GetRequiredService<FmpClient>();

var profile = await fmp.Company.GetProfileAsync("AAPL");
Console.WriteLine($"{profile?.CompanyName} — {profile?.Sector}");
```

`FmpClient` is disposable, so a container tracks a resolved client until the scope it came from ends. Inject it
into a scoped or transient service — an ASP.NET Core request is already a scope — or resolve one instance and
keep it, rather than resolving a fresh client per call from the root provider.

Twenty-five groups hang off `FmpClient`. The ten most people start with:

| Group | What it reaches |
|---|---|
| `fmp.Company` | Profiles, share float, market cap, executives, employee counts, M&A, the delisting archive |
| `fmp.Statements` | Income / balance / cash flow, TTM and as-reported variants, growth, ratios, metrics, scores |
| `fmp.Quote` | Quotes — full and short, aftermarket, price change, and the per-asset-class batches |
| `fmp.Chart` | End-of-day history (four adjustment variants) and six intraday intervals |
| `fmp.Directory` | The symbol universe, exchanges, sectors, industries, countries, CIK list, symbol changes |
| `fmp.Search` | Symbol / name / CIK / CUSIP / ISIN lookup, exchange variants, and the company screener |
| `fmp.Calendar` | Per-symbol earnings history and the whole-market earnings calendar |
| `fmp.Analyst` | Forward consensus estimates |
| `fmp.Economics` | The macro release calendar |
| `fmp.Bulk` | The `*-bulk` whole-universe CSV feeds, streamed |

Which specific paths are modelled is in **[Endpoint Coverage](endpoint-coverage.md)**; the table itself is generated from the code.

## 6. Handle the failures that will actually happen

Three you should write code for on day one:

```csharp
try
{
    var scores = await fmp.Statements.GetScoresAsync("AAPL");
}
catch (FmpPlanRestrictedException ex) when (ex.IsRejectedCredential)   // 403 — the key
{
    logger.LogError("FMP rejected the key: {Message}", ex.Message);
}
catch (FmpPlanRestrictedException ex)                                  // 402 — the plan
{
    logger.LogWarning("Not entitled on this plan: {Message}", ex.Message);
}
catch (FmpRateLimitedException ex)                                     // 429, already waited out
{
    logger.LogWarning("Throttled; upstream asked for {RetryAfter}", ex.RetryAfter);
}
```

A `null` return is never one of these. **[Error Handling](error-handling.md)** covers the full hierarchy and what null means where.

## Two gotchas that catch everyone

**Class-share tickers need FMP's hyphenated spelling.** `BRK.B` answers `[]`; `BRK-B` answers a row. It surfaces
as an empty result rather than an error, so a dotted ticker looks exactly like a symbol FMP has never heard of.

**A `Sector` or `Industry` string must come from the reference lists.** The screener returns `[]` with HTTP 200
for an unrecognised value, which is indistinguishable from a filter that legitimately matched nothing. Get the
spellings from `fmp.Directory.GetSectorsAsync()` and `GetIndustriesAsync()`.

## Next

* **[Recipes](recipes.md)** — worked solutions to real tasks
* **[Configuration](configuration.md)** — every option, its default, and its units
* **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)** — before you touch anything ending in `-bulk`
