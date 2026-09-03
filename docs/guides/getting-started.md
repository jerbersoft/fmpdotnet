# Getting Started

From nothing to a working call, in order. Budget five minutes.

## Prerequisites

* **.NET 10 SDK.** The repository pins a feature band in `global.json`; a consumer only needs a 10.x SDK.
* **An FMP API key.** The SDK targets **Premium** as the lowest paid tier — see
  [the throttle note](#3-set-the-throttle-to-your-tier) below for why that matters even on a bigger plan.

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

## 2. Register the client

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

## 3. Set the throttle to your tier

The default `PerMinuteCap` is **660**, calibrated to Premium's 750/min because that is the lowest paid tier the
SDK targets. A default tuned to a higher tier would trip 429s for everyone below it.

**On Ultimate, raise it.** Ultimate allows 3,000/min, so the default spends roughly a fifth of what you are paying
for. Use about 88% of your tier's published limit to keep the same headroom:

```json
{ "Fmp": { "ApiKey": "…", "PerMinuteCap": 2640 } }
```

Full option list in **[Configuration](configuration.md)**.

## 4. Make a call

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

Which specific paths are modelled is in **[Endpoint Coverage](endpoint-coverage.md)**; the table itself is generated
from the code.

## 5. Handle the failures that will actually happen

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

A `null` return is never one of these. **[Error Handling](error-handling.md)** covers the full hierarchy and what null
means where.

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
