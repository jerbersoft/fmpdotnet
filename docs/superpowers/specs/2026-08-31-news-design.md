# News — design

What issue [#33](https://github.com/jerbersoft/fmpdotnet/issues/33) builds: one new facade, `fmp.News`, covering
all ten remaining News paths. Coverage goes **216 → 226 of 243**.

Every fact this document argues from was measured against the live API on 2026-08-29 and 2026-08-31, and is
recorded with its date in [the measurements](2026-08-30-news-measurements.md) (committed `e27b26b`, addendum
`1aaa02b`). Where this document states a number, that file is where it came from. Nothing here was read from
FMP's documentation.

## The shape of the problem

**Ten paths, two shapes.** Nine of the ten share one shape *exactly* — same eight keys in the same order, across
2,250 measured rows. The tenth, `fmp-articles`, carries the same eight concepts under six different names.

| record | serves | rows measured |
|---|---|---|
| `NewsArticle` | the five `-latest` feeds and the four search paths | 2,250 |
| `FmpArticle` | `fmp-articles` | 200 |

That is the whole of the consolidation, and it is not the work. The work is that **the two families differ in
what their parameters do**, and every difference is silent:

| trap | measured behaviour | affected |
|---|---|---|
| a parameter that is accepted and ignored | `symbols` on the five `-latest` feeds — `stock-latest?symbols=AAPL` returned 20 rows carrying 20 distinct symbols | 5 paths |
| a default that is a real symbol | omitting `symbols` substitutes AAPL, AAPL, BTCUSD, EURUSD by path | 4 paths |
| a near-miss parameter name | `symbol=MSFT` is dropped and the AAPL default takes over, byte-identical to the bare call | 4 paths |
| case that returns nothing | `symbols=aapl` and `symbols=Aapl` each return 0 rows | 4 paths |
| a vocabulary that is not the obvious one | `crypto?symbols=BTC` returns 0 rows; `BTCUSD` returns 250 | 1 path |
| a floor with no error on it | no row older than three months is reachable without an explicit `from` | 9 paths |
| malformed dates dropped, not rejected | `from=hello&to=world` returns the default response byte-for-byte | 9 paths |
| a backwards range answered, not refused | `from=2026-01-09&to=2026-01-05` returns 0 rows | 9 paths |
| two spellings of a ceiling | `page=101` is HTTP 400; `fmp-articles` has no page ceiling and repeats forever | 10 paths |
| a body that is markup | `content` carries HTML on 200 of 200 rows; `text` carries none on 2,250 | 1 path |
| a ticker that will not round-trip | `tickers` is `NASDAQ:CSIQ`; `symbols=NASDAQ:CSIQ` returns 0 rows | 1 path |
| a row that is not an article | a multi-symbol query returns one row per article-symbol pairing | 4 paths |

**Every one of these returns HTTP 200 with well-formed rows.** Not one is an error the caller can catch. The
design's job is to make the SDK's surface disagree with the wire wherever the wire is quietly wrong, and to say
so in the documentation wherever it cannot.

### Five decisions were the user's and are settled

1. **Two records**, `NewsArticle` and `FmpArticle`, not one. The wire names differ on six of eight fields; one
   record with two names per field is not available.
2. **`symbols` is required** on the four search paths, though the wire makes it optional. See below.
3. **`to` without `from` is allowed and documented**, not guarded. It is the one trap in the table that is
   sometimes a right answer.
4. **A lowercase symbol throws**, naming the fix, rather than being uppercased or passed through.
5. **`fmp-articles.date` binds as UTC** while the nine feeds' `publishedDate` binds as Eastern.

## The public surface

### `fmp.News` — ten methods over ten paths

```csharp
Task<IReadOnlyList<NewsArticle>> GetGeneralLatestAsync(
    LocalDate? from = null, LocalDate? to = null, int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<NewsArticle>> GetStockLatestAsync(/* same signature */);
Task<IReadOnlyList<NewsArticle>> GetCryptoLatestAsync(/* same signature */);
Task<IReadOnlyList<NewsArticle>> GetForexLatestAsync(/* same signature */);
Task<IReadOnlyList<NewsArticle>> GetPressReleasesLatestAsync(/* same signature */);

Task<IReadOnlyList<NewsArticle>> SearchStockAsync(
    IEnumerable<string> symbols, LocalDate? from = null, LocalDate? to = null,
    int? limit = null, int? page = null, CancellationToken ct = default);
Task<IReadOnlyList<NewsArticle>> SearchCryptoAsync(/* same signature */);
Task<IReadOnlyList<NewsArticle>> SearchForexAsync(/* same signature */);
Task<IReadOnlyList<NewsArticle>> SearchPressReleasesAsync(/* same signature */);

Task<IReadOnlyList<FmpArticle>> GetArticlesAsync(int? limit = null, int? page = null, CancellationToken ct = default);
```

Parameter order follows `CompanyEndpoints.GetHistoricalMarketCapAsync`: the identifier, then the range, then
paging, then the token.

### Why `-latest` and search are separate method families

Because `symbols` binds on four paths and is **accepted and silently ignored** on five. A single method carrying
an optional `symbols` would offer a parameter it cannot honour on half the paths it serves, and the measured
proof is unambiguous: `stock-latest?symbols=AAPL` returned 20 rows carrying 20 distinct symbols. A caller who
passed a filter and got an unfiltered feed has no way to notice.

The split also gives decision 2 somewhere to stand. Requiring `symbols` on the search paths costs the caller
nothing precisely because the "give me everything" call already exists under its own name — it is
`GetStockLatestAsync`, and it is honest about taking no filter.

### Why ten methods and not one enum-driven method

[#35](https://github.com/jerbersoft/fmpdotnet/issues/35) put nine technical-indicator paths through one method
because the nine were one call with one parameter changing. These are not. **Each category carries a different
symbol vocabulary** — stock tickers, `BTCUSD`-style crypto pairs, `EURUSD`-style forex pairs — and the measured
trap is that the wrong vocabulary returns 0 rows rather than an error. An enum parameter would sit next to the
`symbols` argument while hiding which vocabulary that argument must be drawn from, which is the one thing the
caller most needs to know.

`general-latest` settles it on its own: it has no symbol at all. `symbol` was null on **250 of 250** rows. It
cannot join a family whose defining parameter it does not have.

### Why `GetArticlesAsync` takes neither symbols nor dates

Both were measured inert on this path, not merely undocumented. `?symbols=AAPL` and
`?from=2026-01-05&to=2026-01-09` each returned a response **byte-identical** to the bare call (md5
`ef237db2c028584a50a0ea8af99d928d`). Offering either parameter would be offering a control that does nothing.

### Why `limit` and `page` are nullable rather than defaulted

`int? limit = null` sends nothing and lets FMP's own default of 20 rows apply. The alternative — an SDK-chosen
default, as `CongressEndpoints` uses — invents a page size the wire did not ask for and quietly changes what an
unparameterised call returns. The measured default is recorded in the XML doc so the caller can see it without
running anything.

## The models

### Every property is nullable

Following the established convention. The measured null counts are recorded in each property's XML doc rather
than encoded in the type, because "never null in 2,250 rows" and "cannot be null" are different statements and
only the first was measured.

### `NewsArticle` — eight properties, one structurally absent on a whole feed

| property | wire | type | measured null |
|---|---|---|---|
| `Symbol` | `symbol` | `string?` | 310 of 2,250 |
| `PublishedDate` | `publishedDate` | `Instant?` | 0 of 2,250 |
| `Publisher` | `publisher` | `string?` | 0 of 2,250 |
| `Title` | `title` | `string?` | 0 of 2,250 |
| `Image` | `image` | `string?` | 14 of 2,250 |
| `Site` | `site` | `string?` | 6 of 2,250 |
| `Text` | `text` | `string?` | 0 of 2,250 |
| `Url` | `url` | `string?` | 0 of 2,250 |

`Symbol`'s nulls are structural, not incidental, and the XML doc says so with the numbers: 250 of 250 on
`general-latest`, 46 of 250 on `stock-latest`, 13 on `press-releases-latest`, 1 on `crypto-latest`, and **0 on
all four search paths**. General news has no ticker; a symbol-filtered query cannot lack one.

`Text` is documented as plain text — 0 of 2,250 rows carried an HTML tag — which is the property `FmpArticle`
does not share.

### `FmpArticle` — six renamed fields, an HTML body, and a ticker that will not round-trip

| property | wire | type | note |
|---|---|---|---|
| `Title` | `title` | `string?` | the one name shared with `NewsArticle` |
| `Date` | `date` | `Instant?` | **UTC**, not Eastern — see below |
| `Content` | `content` | `string?` | HTML on 200 of 200 rows, median 3,013 chars |
| `Link` | `link` | `string?` | `NewsArticle` spells this `url` |
| `Author` | `author` | `string?` | 7 distinct values across 200 rows |
| `Tickers` | `tickers` | `string?` | plural name, one value, exchange-prefixed |
| `Image` | `image` | `string?` | |
| `Site` | `site` | `string?` | the constant `"Financial Modeling Prep"` on all 200 rows |

Nothing on this record was null in the 200-row sample.

**`Tickers` gets two computed properties beside it**, following the `ExchangeMarketHours` precedent of keeping
the wire value under its wire name and parsing beside it rather than over it:

```csharp
[JsonIgnore] public string? Symbol => SplitTicker(Tickers).symbol;
[JsonIgnore] public string? Exchange => SplitTicker(Tickers).exchange;
```

The measured reason: every one of 200 rows carried an exchange prefix — NASDAQ 101, NYSE 86, OTC 10, AMEX 3 — and
`symbols=NASDAQ:CSIQ` returns **0 rows** while `symbols=CSIQ` returns 20. `Symbol` is the property that feeds
back into a search call; `Tickers` is not, and its XML doc says so. The parse returns `null` for both when there
is no single colon, because the plural name is a standing warning that a future row may carry more than one
value even though none of the 200 measured did — **not one comma appeared in the sample**.

## Converters — two existing ones applied, none new

This slice adds no converter. It applies two the SDK already carries, and picking the wrong one compiles,
deserialises, and is wrong by four to five hours:

| record | property | converter |
|---|---|---|
| `NewsArticle` | `PublishedDate` | `NullableEasternInstantJsonConverter` |
| `FmpArticle` | `Date` | `NullableFmpInstantJsonConverter` (UTC) |

The evidence for each: `publishedDate` by the DST discriminator over 1,803 rows on two days six months apart —
the wire's `16:05` and `08:00` clusters do not shift between EDT and EST, where a stored instant would.
`fmp-articles.date` by distribution against a known-Eastern control feed — r = +0.656 at lag 4, against
r = −0.225 at lag 0, the only alignment Eastern permits and the worst in the whole 24-hour sweep.

Both XML docs carry the evidence and the date, because the two converters differ only in the zone they read the
same `"yyyy-MM-dd HH:mm:ss"` shape as, and a future editor has nothing else to go on.

**The `FmpArticle.Date` binding is the weaker of the two and its doc says so.** It rests on inference from
distribution, not a direct clock comparison. The direct test — comparing a newly appeared article's wire `date`
against FMP's own `Date` response header — is recorded as outstanding in the measurements addendum, and remains
un-run because the path published nothing between 2026-08-28 21:05:54 and at least 2026-08-31 09:38 UTC.

## Guards

Four, three of them new to this group and one shared.

1. **`Symbols(path, symbols)`** — the request builder for the four search paths, modelled on
   `QuoteEndpoints.Batch`. Throws on a null list; drops blank entries; throws `ArgumentException` when the list is
   entirely blank, because that request cannot mean anything and the rows it would answer read as "none of these
   symbols are known". A trailing comma is tolerated upstream — `symbols=AAPL,` matched `symbols=AAPL` — so
   dropping blanks changes no measured behaviour.
2. **The uppercase guard**, inside `Symbols`. Throws `ArgumentException` naming the fix when an entry is not
   already uppercase. Measured: `symbols=aapl` and `symbols=Aapl` each return 0 rows, which reads as "this
   symbol has no news". This follows the comma-list and `quarter=0` guards: reject the argument that buys a
   silent wrong answer out of the key's quota.
3. **`ThrowIfPagingOutOfRange(limit, page)`**, private to this group and written twice with different
   constants, because the two path families measured different ceilings:
   - the nine `news/*` paths: `limit` in 1..**250**, `page` in 0..**100**
   - `fmp-articles`: `limit` in 1..**200**, `page` at or above 0 with **no upper bound**

   `limit` is rejected at zero and below because `limit=0` and `limit=-1` each return **one row** rather than
   erroring or returning nothing.
4. **`DateRange.ThrowIfBackwards(from, to)`** — the shared helper, unchanged. Measured here:
   `from=2026-01-09&to=2026-01-05` returns 0 rows at HTTP 200.

`page=101` on the nine feeds is already HTTP 400 with a plain-text body, which `FmpTransport` reads into
`FmpApiException.ErrorMessage`. The guard is still worth having: it turns a wasted call into an exception that
names the ceiling.

## What is documented rather than guarded

Each of these gets XML documentation carrying the measurement and its date. None gets code.

- **The three-month floor, and `to` without `from`.** No row older than three calendar months is reachable
  without an explicit `from`; `to=2026-01-09` alone returns 0 rows while `from=2026-01-05&to=2026-01-09` returns
  20. This is left unguarded by decision: unlike a transposed range, `to` alone inside the floor is a correct
  request with a correct answer, and a client-side threshold could not be written honestly — one day's
  measurement separated the floor from 90 days but could not separate "three calendar months" from "92 days".
- **`fmp-articles` has no page ceiling and never errors — it repeats its last page forever.** Pages 1000, 1400,
  1600, 2000 and 10000 all returned the identical two rows. **A caller paging until the response is empty never
  terminates on this path.** This cannot be guarded, because the corpus end moves; it can only be said plainly.
- **A row is an article-symbol pairing, not an article.** A multi-symbol query returns the same article once per
  matching symbol — 19 of 250 urls twice on `crypto?symbols=BTCUSD,ETHUSD`, every one under a different symbol,
  and zero same-symbol repeats. Counting rows over-counts articles.
- **Malformed dates are dropped, not rejected**, and the surviving parameter still applies — which is how a
  malformed `from` with a valid `to` lands on the floor and returns nothing.
- **`Content` is markup FMP wrote.** A caller rendering it into a page is rendering HTML from the wire.
- **How the feeds relate.** Each search path is a filtered view of its `-latest` feed: all 21 `news/stock` rows
  inside the window where both samples are complete appear in `stock-latest` by url. `press-releases-latest` is
  a subset of `stock-latest`, sharing 53 of 250 urls. Every other pair of feeds shares **zero**.
- **`fmp-articles` may produce nothing on a given day.** Measured 2026-08-31: weekdays carried 22 to 53 rows,
  the 2026-08-22 weekend carried 1 and 2, and the 2026-08-29 weekend carried none at all. The path had been
  silent for 60.5 hours at the time of measurement. An empty response is not evidence of a broken call.

## Serialisation and wiring

`FmpJsonContext` gains **two** entries for ten paths:

```csharp
// News (#33). TWO entries for ten paths: nine feeds were measured to share one key tuple exactly,
// and fmp-articles renames six of the eight.
[JsonSerializable(typeof(List<NewsArticle>))]
[JsonSerializable(typeof(List<FmpArticle>))]
```

`FmpClient` gains one property, `public NewsEndpoints News { get; }`, constructed like every other facade.

## Testing

**Unit tests**, against `StubHandler`, one file `tests/FmpDotNet.Tests/NewsTests.cs`:

- Each of the four guards, including one test per rejected shape: null list, all-blank list, lowercase entry,
  mixed-case entry, `limit=0`, `limit=251` on a feed, `limit=201` on `fmp-articles`, `page=101` on a feed, and a
  backwards range.
- **No page ceiling on `fmp-articles`** — `page=10000` must reach the transport rather than throw. This is the
  test that fails if someone later "tidies" the two paging guards into one.
- Both converters, each with a summer and a winter timestamp, asserting the resulting `Instant` differs by the
  measured offset. This is the test that fails when the two converters are swapped — the failure the design
  exists to prevent.
- `Tickers` parsing: `"NASDAQ:CSIQ"` yields `Symbol == "CSIQ"` and `Exchange == "NASDAQ"`; a value with no colon
  yields null for both; `null` yields null for both.
- That the five `-latest` methods send no `symbols` parameter at all, asserted on the built request.

**Smoke sweeps**: ten new entries in `tests/FmpDotNet.SmokeTests/Sweeps.cs`, driving all ten paths against the
live API. The ordinary baseline gains ten endpoint blocks, taking it from 196 endpoints to 206.

## Documentation deliverables

- XML documentation on `NewsEndpoints`, both records, and every property, carrying the measurements above with
  their dates.
- The README's generated coverage block regenerated via `FMPDOTNET_UPDATE_README`, moving 216 → 226 of 243.
- The README's prose count of remaining groups updated: News leaves the remaining-coverage list.
- Issue #25's table updated with the shipped row.

## Files

**Create**

- `src/FmpDotNet/Endpoints/NewsEndpoints.cs` — the facade, ten methods, four guards
- `src/FmpDotNet/Models/NewsArticle.cs`
- `src/FmpDotNet/Models/FmpArticle.cs`
- `tests/FmpDotNet.Tests/NewsTests.cs`

**Modify**

- `src/FmpDotNet/FmpClient.cs` — one facade property
- `src/FmpDotNet/Serialization/FmpJsonContext.cs` — two entries
- `tests/FmpDotNet.SmokeTests/Sweeps.cs` — ten sweep entries
- `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt` — regenerated
- `README.md` — regenerated coverage block, prose count

## What this design does not do

- **It does not deduplicate the article-symbol pairings.** The pairing is what the wire sends and what the
  caller asked for; collapsing rows would discard the symbol that made each row match.
- **It does not compensate for the three-month floor** by sending a `from` the caller did not supply. The same
  reasoning as `holidays-by-exchange`: a request that does not match the arguments passed turns every debugging
  session into a puzzle.
- **It does not validate symbol vocabularies.** There is no client-side list of valid tickers, crypto pairs or
  currency pairs, for the reason `MarketHoursEndpoints` gives for exchange codes: the vocabulary is upstream's
  and will go stale.
- **It does not model the relationship between feeds.** That the search paths are filtered views of the
  `-latest` feeds, and that `press-releases-latest` is a subset of `stock-latest`, is documented rather than
  expressed in types. It is a measured property of the data on one day, not a contract.
- **It does not strip HTML from `Content`.** The wire sends markup; the record carries what the wire sent.
