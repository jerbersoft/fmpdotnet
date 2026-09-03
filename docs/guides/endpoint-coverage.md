# Endpoint Coverage

How to find out whether the endpoint you need exists, and what to do when it does not.

## The table is generated, not maintained

The coverage table in the
[README](../../README.md#endpoint-coverage) is **produced from the
code by a test**, not written by hand. Every public endpoint method is driven against a stub and the path it
*actually requests* is recorded.

That means:

* Renaming a method, deleting one, or adding an endpoint without a table entry **fails the build**.
* The table cannot describe an endpoint that does not exist, or miss one that does.
* **Do not edit it by hand.** Run `FMPDOTNET_UPDATE_README=1 dotnet test` and commit the result — see
  **[Development](development.md)**.

So the README table is the answer to "is this modelled?", and it is answerable with certainty. This page explains
how to *read* it, and what the shape of the remainder is.

## The ten groups

Coverage is organised by the property on `FmpClient`, not by FMP's own documentation sections.

| Group | Shape of what it returns |
|---|---|
| `fmp.Analyst` | Forward consensus estimates. |
| `fmp.Bulk` | `IAsyncEnumerable<T>` over CSV. See **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)** before using. |
| `fmp.Calendar` | Per-symbol earnings history; the whole-market earnings calendar. |
| `fmp.Chart` | Price history — four EOD adjustment variants, six intraday intervals. |
| `fmp.Company` | Profile, float, market cap, executives, employees, M&A, delistings. |
| `fmp.Directory` | The reference vocabularies and the symbol universe. |
| `fmp.Economics` | The macro release calendar. |
| `fmp.Quote` | Quotes, aftermarket, price change, and per-asset-class batches. |
| `fmp.Search` | Identifier lookup and the company screener. |
| `fmp.Statements` | The fundamentals family — base, TTM, as-reported, growth, ratios, metrics, scores. |

### One method can serve several paths, and vice versa

The table's `Method` column is a **list**, because the mapping is not one-to-one:

* `stable/historical-chart/1min` … `4hour` — six paths, one `GetIntradayAsync(symbol, interval, from, to)`.
  The interval is a `ChartInterval` enum member, so the path segment cannot be misspelled.
* `stable/profile-bulk` — two methods. `StreamProfilesAsync(part)` reads one part;
  `StreamAllProfilesAsync()` walks every part.
* `stable/batch-etf-quotes` — two methods, `GetEtfQuotesAsync` (short shape) and `GetEtfQuotesFullAsync`
  (full shape), because FMP serves both from one path.
* `stable/cik-list` — a paged `GetCikListAsync(page, limit)` and a walking `StreamCikListAsync()`.

### The vocabularies are enums, not strings

Where FMP takes a fixed vocabulary, the SDK takes an enum, so a typo is a compile error rather than an HTTP 200
with no rows:

| Enum | Members | Sent as |
|---|---|---|
| `FiscalPeriod` | `Annual`, `Quarter`, `Q1`, `Q2`, `Q3`, `Q4` | `annual`, `quarter`, `Q1`… |
| `BulkFiscalPeriod` | `Annual`, `Q1`, `Q2`, `Q3`, `Q4` | as above — bulk has **no** `quarter` |
| `ChartInterval` | `OneMinute` … `FourHours` | `1min`, `5min`, `15min`, `30min`, `1hour`, `4hour` |

`BulkFiscalPeriod` is a separate type rather than a reuse, because the bulk endpoints genuinely do not accept the
rolling `quarter` value. Two enums make that unrepresentable; one enum plus a runtime check would not.

## Why the denominator is 243

FMP documents its API by asset class as well as by function, so the same path is documented several times over —
the Commodity, Forex, Crypto and Index sections are very largely `stable/quote` and `stable/historical-price-eod`
re-documented under new headings.

The denominator used here is therefore the **unique-path count**, which was
[enumerated and cross-checked against two independent sources](https://github.com/jerbersoft/fmpdotnet/blob/master/docs/superpowers/specs/2026-08-27-endpoint-inventory.md).
Counting documentation pages instead would produce a larger, flattering, and meaningless number.

This is also why one `GetQuoteAsync` covers so much ground: `GetQuoteAsync("BTCUSD")`, `("EURUSD")`, `("^GSPC")`
and `("GCUSD")` were each measured returning the ordinary seventeen-field quote.

## What is not modelled, and why

The remainder is **unbuilt rather than blocked**. Build order follows what consumers actually call, rather than
what FMP documents first.

Two things worth knowing about the shape of the remainder:

* **The `tipranks-*` paths cannot be built.** They need a separately purchased add-on and return 402 even on FMP's
  top tier, so no amount of plan upgrading makes them testable.
* **The balance is lopsided toward equity depth, for a structural reason.** The price plumbing — Quote, Chart and
  Bulk — is complete, and one quote method serves every asset class, so asset-class *breadth* came free while
  equity *depth* is what remains.

The [endpoint inventory](https://github.com/jerbersoft/fmpdotnet/blob/master/docs/superpowers/specs/2026-08-27-endpoint-inventory.md)
splits the remainder section by section. Each group is tracked as an issue carrying its measured path list.

## Reaching an endpoint that is not modelled

**`FmpTransport` is public precisely so nothing here blocks you.**

Go through it rather than building a second `HttpClient`. The transport carries the shared throttle, the timeout,
the 429 handling and the error classification. A call made any other way has **none** of them — including the
shared reservoir, so it would not even count against the budget the rest of your calls are pacing themselves
within.

### Ordinary endpoints

The SDK is AOT-compatible and never reflects over your model, so `GetListAsync` takes a `JsonTypeInfo` rather than
a bare `T`. Declare a context for your own types:

```csharp
public sealed record RatingSnapshot
{
    [JsonPropertyName("symbol")] public required string Symbol { get; init; }
    [JsonPropertyName("rating")] public string? Rating { get; init; }
}

[JsonSerializable(typeof(List<RatingSnapshot>))]
public sealed partial class MyFmpJson : JsonSerializerContext;
```

Then resolve the same transport the typed endpoints use — unkeyed for the default registration, or
`GetRequiredKeyedService<FmpTransport>("research")` for a named one:

```csharp
var transport = provider.GetRequiredService<FmpTransport>();

IReadOnlyList<RatingSnapshot> rows = await transport.GetListAsync(
    new FmpRequest("stable/ratings-snapshot").With("symbol", "AAPL"),
    MyFmpJson.Default.ListRatingSnapshot,
    ct);
```

`FmpRequest` appends the API key itself, so **no caller ever builds a URL by hand** — which is also what keeps the
key out of logs and exception messages.

### Bulk endpoints

An unmodelled `*-bulk` path goes through `FmpBulkTransport`, the same transport bound to the bulk client. The
tighter throttle and the ten-minute timeout come with it, and CSV is mapped a row at a time so nothing buffers:

```csharp
var transport = provider.GetRequiredService<FmpBulkTransport>();

await foreach (var row in transport.StreamCsvAsync(
    new FmpRequest("stable/some-bulk"),
    csv => new MyRow { Symbol = csv.GetString("symbol")!, Price = csv.GetDecimal("price") },
    ct))
{
    // ...
}
```

Read **[Rate Limits and Bulk Data](rate-limits-and-bulk-data.md)** first. Bulk errors arrive under HTTP 200, and a naive
parse reads a throttle refusal as "no data today".

### Modelling one properly instead

If the endpoint is one the SDK should carry, that is a welcome contribution — **[Contributing](contributing.md)**
describes the measure-first workflow, which exists because most of the hard-won knowledge in this SDK came from probing
the live API rather than reading the docs.
