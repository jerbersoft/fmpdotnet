# News — measurements

Issue #33. Ten paths, measured against the live FMP API on **2026-08-29 UTC** (2026-08-30 local): 114 captures,
7,194 rows. Every number below came from one of those captures. Nothing here is taken from FMP's documentation.

The API key travels in the query string, so no built URL appears in this document and no capture was kept in the
repository.

## Entitlement — all ten answer, and all ten default to 20 rows

| path | HTTP | default rows |
|---|---|---|
| `stable/fmp-articles` | 200 | 20 |
| `stable/news/general-latest` | 200 | 20 |
| `stable/news/stock-latest` | 200 | 20 |
| `stable/news/stock` | 200 | 20 |
| `stable/news/crypto-latest` | 200 | 20 |
| `stable/news/crypto` | 200 | 20 |
| `stable/news/forex-latest` | 200 | 20 |
| `stable/news/forex` | 200 | 20 |
| `stable/news/press-releases-latest` | 200 | 20 |
| `stable/news/press-releases` | 200 | 20 |

No path returned 402 or 403. The four search paths answer **with no parameters at all** — see "The default symbol"
below for why that is a hazard rather than a convenience.

## Two shapes, not ten

Nine of the ten paths share one shape **exactly** — same eight keys, same order, measured across 2,250 rows:

```
symbol, publishedDate, publisher, title, image, site, text, url
```

`fmp-articles` is the outlier. Same eight concepts, and it renames six of them:

| concept | the nine feeds | `fmp-articles` |
|---|---|---|
| headline | `title` | `title` |
| timestamp | `publishedDate` | `date` |
| body | `text` | `content` |
| link | `url` | `link` |
| attribution | `publisher` | `author` |
| ticker | `symbol` | `tickers` |
| image | `image` | `image` |
| source | `site` | `site` |

The bodies are not the same kind of value. `text` is **plain text** — 0 of 2,250 rows carry an HTML tag, median
length 88–462 characters by path. `content` is **HTML** — 200 of 200 rows carry tags (`<ul>`, `<li>`, `<strong>`),
median length 3,013 characters. A caller rendering `content` into a page is rendering markup FMP wrote.

`site` on `fmp-articles` is the constant `"Financial Modeling Prep"` across all 200 rows, and `author` takes 7
distinct values. These are FMP's own articles, not a feed of anyone else's.

## `publishedDate` is Eastern wall clock, not UTC

**This is the typing decision of the slice, and the intuitive answer is wrong.** The SDK already carries two
converters for this exact `"yyyy-MM-dd HH:mm:ss"` shape, differing only in the zone they read it as:
`NullableFmpInstantJsonConverter` (UTC, established on the economic calendar) and
`NullableEasternInstantJsonConverter` (Eastern, established against EDGAR). Reading news with the first would put
every timestamp 4–5 hours early.

Measured by the same DST discriminator the existing converters were established with — two **complete** calendar
days of `press-releases-latest`, gathered by paging until the day ran short so no hour is under-represented:

| day | zone in effect | rows | peak hours | top five `HH:MM` values, in rank order |
|---|---|---|---|---|
| 2026-08-27 | EDT (UTC−4) | 964 | 16:00 (170), 08:00 (138) | 08:00, 07:00, 08:30, **16:05**, 12:00 |
| 2026-01-14 | EST (UTC−5) | 839 | 08:00 (130), 09:00 (124), 16:00 (119) | 09:00, 08:00, 08:30, 07:00, **16:05** |

Two things fall out of that table:

- **The clusters are the canonical US wire times read as Eastern.** Pre-market 07:00/07:30/08:00/08:30/09:00 and
  post-close 16:04/16:05/16:15/16:30 — US equities close at 16:00 ET. Under a UTC reading the post-close cluster
  would land at 20:0x; hour 20 holds 14 rows on the summer day against 170 in hour 16.
- **The wire values do not shift between summer and winter.** `16:05` and `08:00` are the top clusters on both
  days. A stored instant would move by an hour across the DST boundary; a stripped wall clock does not. This is
  the mirror image of the economic-calendar measurement that established the UTC converter, where the wire values
  *did* shift by an hour six months apart.

This is inference from 1,803 rows on two days six months apart, not a per-article cross-check against an
authoritative external clock — but it is the same standard, on a larger sample, than the two rows that
established each existing converter.

## The parameter vocabulary is not uniform

Measured per path, not assumed. ✓ binds, ✗ is accepted and **silently ignored**.

| path | `symbols` | `from`/`to` | `page` | max `limit` | max `page` |
|---|---|---|---|---|---|
| `news/stock`, `news/crypto`, `news/forex`, `news/press-releases` | ✓ | ✓ | ✓ | 250 | 100 |
| `news/*-latest`, `news/general-latest` | ✗ | ✓ | ✓ | 250 | 100 |
| `fmp-articles` | ✗ | ✗ | ✓ | **200** | **none** |

`fmp-articles` ignoring `symbols` and `from`/`to` is not read off a doc page: `?symbols=AAPL` and
`?from=2026-01-05&to=2026-01-09` each returned a response **byte-identical** to the bare call (md5
`ef237db2c028584a50a0ea8af99d928d`). `limit=201` is byte-identical to `limit=200`.

`stock-latest?symbols=AAPL` returned 20 rows carrying 20 distinct symbols — the filter is not applied.

## Six ways to get a plausible wrong answer

Every one of these returns HTTP 200 and well-formed rows. None of them errors.

1. **`symbol` instead of `symbols`.** `news/stock?symbol=MSFT` returns 20 **AAPL** rows — byte-identical (md5
   `583851f81eafc30c11b59412730a278e`) to the call with no parameters at all. The singular spelling is dropped and
   the default takes over. `symbols=MSFT` returns 20 MSFT rows.

2. **The default symbol.** Omitting `symbols` does not mean "everything". Each search path substitutes one
   hard-coded symbol: `news/stock` → **AAPL**, `news/press-releases` → **AAPL**, `news/crypto` → **BTCUSD**,
   `news/forex` → **EURUSD**. `symbols=` (empty) behaves the same way.

3. **Case.** `symbols=aapl` and `symbols=Aapl` each return **0 rows**. The vocabulary is exact uppercase.

4. **The crypto vocabulary is the pair, not the coin.** `news/crypto?symbols=BTC` returns 0 rows; `BTCUSD`
   returns 250.

5. **Malformed dates are dropped, not rejected.** `from=hello&to=world` and `from=01-05-2026&to=09-01-2026` each
   returned the default response byte-for-byte. The parameter that survives still applies: `from=01-05-2026`
   (malformed) with a valid `to=2026-01-09` returns 0 rows, because the dropped `from` falls back to the implicit
   floor below and the range is then backwards.

6. **A backwards range is empty, not an error.** `from=2026-01-09&to=2026-01-05` returns 0 rows.

A trailing comma is tolerated: `symbols=AAPL,` matches `symbols=AAPL`.

## The implicit floor: three months, and `to` alone falls off it

Omitting `from` does not mean "from the beginning". Measured 2026-08-29 UTC on `news/stock?symbols=AAPL`:

| `to` (no `from`) | rows | oldest row returned |
|---|---|---|
| `2026-05-28` | 0 | — |
| `2026-05-29` | 9 | 2026-05-29 05:25:00 |
| `2026-05-30` | 12 | 2026-05-29 05:25:00 |
| `2026-05-31` | 16 | 2026-05-29 05:25:00 |
| `2026-06-01` | 20 | 2026-05-31 18:56:31 |

**No row older than 2026-05-29 05:25:00 is reachable without an explicit `from`** — exactly three calendar months
before FMP's current date, and 92 days. One day's measurement cannot separate "three calendar months" from
"92 days"; it does rule out 90.

The consequence is the trap: **`to` on its own returns 0 rows for any date older than the floor.** `to=2026-01-09`
alone returns nothing, while `from=2026-01-05&to=2026-01-09` returns 20 rows for the same window. The floor binds
the `-latest` paths too — `stock-latest`, `general-latest` and `press-releases-latest` each returned 0 rows for
`to=2026-01-09` alone.

An explicit `from` escapes it entirely: `from=2011-01-01&to=2011-12-31` returned rows dated 2011-07-20 to
2011-12-12, and `from=2011-01-01&to=2011-06-30` returned rows dated from 2011-02-24 08:30:00.

`from=X&to=X` returns that whole calendar day, inclusive at both ends — the 2026-08-27 sweep above spans
`00:00:00` to `23:33:00` and the 2026-01-14 sweep spans `00:30:00` to `23:59:00`.

## Ceilings, and what happens past them

- **`limit` caps at 250** on the nine `news/*` paths: `limit=1000` and `limit=5000` both returned 250 rows,
  byte-identical. `fmp-articles` caps at **200**.
- **`limit=0` and `limit=-1` return 1 row.** Not an error, not empty.
- **`page` caps at 100** on the `news/*` paths. `page=101` is HTTP **400** with a plain-text body — not JSON:
  `Maxmium Query Parameter: The maximum page number for this endpoint is '100'. Please use a different query or
  adjust your api request accordingly.` (FMP's spelling of "Maximum".) `FmpTransport` already reads a non-success
  body into `FmpApiException.ErrorMessage`, so this needs no new plumbing.
- **`fmp-articles` has no page ceiling and never errors — it repeats the last page forever.** Pages 1000, 1400,
  1600, 2000 and 10000 all return the identical two rows dated `2026-06-26 03:00:18` and `2026-06-26 02:00:26`.
  Page 850 still advances (2026-07-10), so the corpus ends between pages 850 and 1000 at `limit=2`. **A caller
  paging until the response is empty never terminates on this path.**
- `page=-1` is byte-identical to `page=0`.

## A row is an article-symbol pairing, not an article

A multi-symbol query returns the same article once per matching symbol:

| query | rows | urls appearing twice | of those, carrying a *different* symbol |
|---|---|---|---|
| `news/crypto?symbols=BTCUSD,ETHUSD` | 250 | 19 | 19 |
| `news/forex?symbols=EURUSD,USDJPY` | 250 | 18 | 18 |
| `news/stock?symbols=AAPL,MSFT,NVDA,TSLA,AMZN` | 250 | 14 | 14 |

Zero same-symbol repeats in any of the three. Counting rows over-counts articles; the pairing is the record.
`symbols` took 30 symbols in one call without complaint (250 rows covering 25 of the 30).

## Nullability

2,250 rows across the nine feed paths, 200 rows of `fmp-articles`. No field on either shape is ever a number and
no field is ever an empty string — every present value is a JSON string.

| field | null | of | note |
|---|---|---|---|
| `symbol` | 310 | 2,250 | concentrated, see below |
| `image` | 14 | 2,250 | |
| `site` | 6 | 2,250 | all six on `crypto-latest`/`crypto` |
| `publishedDate`, `publisher`, `title`, `text`, `url` | 0 | 2,250 | never null in the sample |
| every `fmp-articles` field | 0 | 200 | |

`symbol`'s nulls are not spread evenly, and the pattern is structural rather than incidental:

| path | `symbol` null |
|---|---|
| `general-latest` | **250 of 250** |
| `stock-latest` | 46 of 250 |
| `press-releases-latest` | 13 of 250 |
| `crypto-latest` | 1 of 250 |
| all four symbol-filtered search paths | 0 of 250 |

**General news has no ticker at all** — every row, without exception. The unfiltered `-latest` feeds carry
untagged rows; a symbol-filtered query cannot.

## Format, ordering and duplicates

- **Every one of 2,450 timestamps matches `YYYY-MM-DD HH:MM:SS`** — space-separated, no `T`, no offset, no
  timezone marker.
- Every response is ordered **descending** by timestamp, and never *strictly* — ties are common (54 of 250 on
  `press-releases-latest`, 43 on `stock-latest`). Paging is stable across the ties measured: pages 0, 1 and 2 at
  `limit=5` returned five rows each and 15 distinct urls, with no row repeated across a page boundary.
- Every `url` and `link` is absolute `http(s)`. Within a single-symbol response, no url repeats.

## How the feeds relate

- **The search path is a filtered view of its `-latest` feed.** Inside the window where both 250-row samples are
  complete (2026-08-29 08:02:05 .. 17:30:00), **all 21** `news/stock` rows appear in `stock-latest` by url.
- **`press-releases-latest` is a subset of `stock-latest`**: the two samples share 53 of 250 urls. Every other
  pair of feeds — general, stock, crypto, forex, and `fmp-articles` — shares **zero**.

## Vocabularies and coverage extents

| path | publishers | distinct symbols in 250 rows | busiest publisher |
|---|---|---|---|
| `stock-latest` | 28 | 146 | The Motley Fool (54) |
| `general-latest` | 28 | 0 | Seeking Alpha (51) |
| `crypto-latest` | 39 | 69 | Blockchain News (30) |
| `forex-latest` | 9 | 24 | FX Street (136 of 250) |
| `press-releases-latest` | 6 | 143 | Newsfile Corp (83) |

History, measured with an explicit `from`/`to`:

| path | reachable | empty |
|---|---|---|
| `news/stock` (AAPL) | 2011 — oldest measured 2011-02-24 08:30:00 | 2010, 2008 |
| `news/press-releases` (AAPL) | 2015 | — |
| `fmp-articles` | back to 2026-06-26 02:00:26 only, ~2 months | — |

Daily volume, for sizing: 964 press releases on 2026-08-27, 839 on 2026-01-14.

## `tickers` is singular, prefixed, and not a `symbols` value

All 200 `fmp-articles` rows carry exactly one ticker despite the plural name — **not one comma in the sample** —
and every one carries an exchange prefix: NASDAQ 101, NYSE 86, OTC 10, AMEX 3. A caller cannot feed `tickers`
back into a `symbols=` query without stripping the prefix: `symbols=NASDAQ:CSIQ` returns **0 rows**, while the
same ticker stripped to `symbols=CSIQ` returns 20.

## What the design has to decide

1. **One record or two.** Nine paths share a shape exactly; `fmp-articles` renames six of eight fields and carries
   HTML where the others carry plain text. One record with two names per field is not available — the wire names
   differ.
2. **Whether `symbols` is optional in C#.** It is optional on the wire, but omitting it silently means AAPL,
   BTCUSD or EURUSD depending on the path. An optional parameter reproduces the trap; a required one contradicts
   the wire.
3. **Whether to guard `to` without `from`.** The wire accepts it and answers 0 rows for anything older than three
   months.
4. **Whether the facade models the 100-page ceiling**, and whether it models `fmp-articles`' absent one — the
   path that never terminates a page-until-empty loop.
5. **Which converter binds the timestamp.** The measurement above says `NullableEasternInstantJsonConverter`.
   Picking the similarly-named UTC one compiles, deserialises, and is wrong by 4–5 hours.
6. **Whether `tickers` is parsed** into a symbol and an exchange, or passed through as FMP spells it.
7. **How the nine feeds map onto methods** — five `-latest` paths, four search paths, one article path, and the
   measured fact that the search paths are filtered views of the feeds rather than separate corpora.
