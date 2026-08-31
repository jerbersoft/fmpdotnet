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

## Addendum — `fmp-articles.date` is UTC, not Eastern (measured 2026-08-30)

The DST discriminator above ran on `press-releases-latest`, a Shape A path. It settles `publishedDate` for the nine
feeds and says nothing about Shape B's `date`. Shape B cannot be measured the same way: its reachable history
starts 2026-06-26, entirely inside EDT, so no DST boundary is in range, and the feeds share zero urls, so the same
article cannot be compared across shapes.

**Shape B's daily profile matches a known-Eastern editorial feed only under a clock offset.** `news/general-latest`
is Eastern by the shared shape, and is editorial rather than wire-scheduled, so it is the fair control for FMP's
own output. Hour-of-day histograms, weekdays only, 894 control rows over eight complete days against 779 Shape B
rows:

| hypothesis for `date` | lag it requires | correlation with the control |
|---|---|---|
| Eastern wall clock | 0 | **r = −0.225** |
| UTC | 4 | r = +0.656 |
| observed peak | 5–6 | r = +0.83 |

Lag 0 — the only alignment Eastern permits — is the worst in the whole 24-hour sweep. The peak sitting at 5–6
rather than exactly 4 is expected: FMP writes *about* the news the aggregate feed carries, so its profile trails by
an hour or two on top of the clock offset. The raw histogram says the same plainly — Shape B troughs at 05:00–07:00
and peaks at 21:00. Read as Eastern, FMP would be near-silent through the pre-market hours where Shape A peaks
(08:00, 138 rows) and busiest at 9pm. Read as UTC, that becomes a 17:00 ET peak just after the close and an
08:00–09:00 ET morning ramp.

**So the two shapes take different converters.** `NullableEasternInstantJsonConverter` for the nine feeds'
`publishedDate`; `NullableFmpInstantJsonConverter` (UTC) for `fmp-articles.date`. One facade, both converters.

**This is inference from distribution, not a direct clock comparison.** Three routes to a direct one were tried and
are closed: the rendered article page answers **403**; legacy `api/v3/fmp/articles` answers **403**, not entitled
on this key; and matching an article to the press release it reports gives real pairs (CRWD, DG, AFRM, S, ULTA) but
FMP writes 12–50 hours after the release — far too loose to bound a four-hour question.

**The direct test is pending and needs a weekday.** Poll `fmp-articles` until an article appears that was not in the
seed set, and compare its wire `date` to FMP's own `Date` response header at that moment: a gap near zero proves
UTC, a gap near four hours proves Eastern. Four polls on Sunday 2026-08-30 (08:43–09:29 UTC) saw no new article,
which is uninformative — measured publication is 44.6 articles per weekday against **3.5 per weekend day**, so the
expected wait is ~7 hours on a Sunday and ~20 minutes on a weekday.

**Corpus extent, corrected.** `fmp-articles` paginates for real well past page 0: pages 0–3 at `limit=200` returned
800 rows with 800 distinct links and no overlap, spanning 2026-08-05 to 2026-08-28. The page-repetition described
above begins only past the end of the corpus.

## Addendum — the direct test is still un-run, because Shape B stopped publishing (measured 2026-08-31)

Measured Monday **2026-08-31 09:38:08 UTC**, taken from FMP's own `Date` response header rather than the local
clock. That is 05:38 ET — pre-market, with European trading under way.

**The nine feeds are live.** `stock-latest`'s newest row was **7 minutes old** and `general-latest`'s **17
minutes**, reading `publishedDate` as Eastern; fifty rows of `stock-latest` spanned only 2.35 hours. Read as UTC
instead, the newest row of a general newswire would be 4.3 hours stale on a Monday morning, with a European
market story at the top of it. That is one more brick on the Eastern side, not a proof — the DST discriminator
above remains the evidence.

**`fmp-articles` has published nothing since 2026-08-28 21:05:54 — a gap of 60.5 hours.** Per-day counts from a
single 200-row capture of page 0, spanning 2026-08-21 17:10:30 to 2026-08-28 21:05:54:

| date | day | rows | first..last |
|---|---|---|---|
| 2026-08-21 | Fri | 9 | 17:10..21:00 |
| 2026-08-22 | Sat | 1 | 18:00 |
| 2026-08-23 | Sun | 2 | 16:00..17:00 |
| 2026-08-24 | Mon | 22 | 01:00..23:00 |
| 2026-08-25 | Tue | 41 | 03:00..23:07 |
| 2026-08-26 | Wed | 40 | 00:00..23:05 |
| 2026-08-27 | Thu | 53 | 00:00..23:00 |
| 2026-08-28 | Fri | 32 | 00:00..21:05 |
| 2026-08-29 | Sat | **0** | — |
| 2026-08-30 | Sun | **0** | — |
| 2026-08-31 | Mon | **0** | none by 09:38 UTC |

**This corrects the weekend figure quoted above.** The 3.5-articles-per-weekend-day rate is an average, not a
floor: the 2026-08-22 weekend carried 1 and 2 rows, and the 2026-08-29 weekend carried none at all. **A caller
cannot assume this path produces anything on a given day.**

The stall is not the timezone question wearing a disguise. Every weekday in the table opened between 00:00 and
03:00 on the wire clock, so under either hypothesis — Eastern or UTC — Monday rows should already exist at 09:38
UTC. They do not. Whatever is behind it, a four-hour offset cannot account for a 60-hour silence.

**The consequence for the pending direct test: it remains un-run.** It compares a *newly appeared* article's wire
`date` against FMP's `Date` header at that moment, and no article has appeared in 60.5 hours — there is nothing
to poll against. The weekday gate has been reached and passed without the test becoming runnable.

**This does not reopen the converter decision.** `fmp-articles.date` is bound as UTC on the strength of the
distributional measurement above, and this addendum leaves that evidence exactly as it stood. The direct test was
always corroboration. It is recorded here as outstanding rather than quietly dropped, and it stays outstanding
until the path publishes again.

## Addendum — the sweep's own window and vocabularies (measured 2026-08-31)

The smoke sweep synthesises its arguments by parameter name, so the window and the symbol list it *would*
send are facts about this SDK that had never been checked against these ten paths. Twelve calls, all at
`limit=5&page=0`, over `from=2026-05-26&to=2026-08-24` — ninety days ending a week ago, which is what
`LiveApi.RangeStart` and `LiveApi.SettledWeekday` computed on 2026-08-31 (a Monday; `SettledWeekday` landed on
Monday 2026-08-24, and `RangeStart` ninety days before it on 2026-05-26).

| path | symbols sent | rows | span |
|---|---|---|---|
| `news/general-latest` | — | 5 | 2026-08-24 22:14:33 .. 2026-08-24 23:48:41 |
| `news/stock-latest` | — | 5 | 2026-08-24 23:45:00 .. 2026-08-24 23:57:00 |
| `news/crypto-latest` | — | 5 | 2026-08-24 23:44:21 .. 2026-08-24 23:56:29 |
| `news/forex-latest` | — | 5 | 2026-08-24 23:09:55 .. 2026-08-24 23:44:16 |
| `news/press-releases-latest` | — | 5 | 2026-08-24 23:42:00 .. 2026-08-24 23:56:00 |
| `news/stock` | `AAPL,MSFT` | 5 | 2026-08-24 16:55:46 .. 2026-08-24 19:37:07 |
| `news/crypto` | `BTCUSD,ETHUSD` | 5 | 2026-08-24 23:39:31 .. 2026-08-24 23:47:22 |
| `news/forex` | `EURUSD,USDJPY` | 5 | 2026-08-24 12:51:53 .. 2026-08-24 21:08:50 |
| `news/press-releases` | `AAPL,MSFT` | 5 | 2026-08-12 07:00:00 .. 2026-08-18 04:00:00 |
| `fmp-articles` | n/a | 5 | 2026-08-28 21:00:00 .. 2026-08-29 13:00:21 |

**The window is safe.** A `from` ninety days before a settled weekday sits past the three-month floor
measured 2026-08-29, and the floor binds only a call with no `from` at all — so an explicit one reaches
through it, which these 50 rows (5 rows apiece across all ten paths above) confirm rather than assume.

**The equity vocabulary is not the crypto or forex one, and the sweep would have recorded the difference as
health.** The same two search paths asked with the sweep's current argument:

| path | `symbols=AAPL,MSFT` | `symbols=` its own vocabulary |
|---|---|---|
| `news/crypto` | 0 rows | 5 rows |
| `news/forex` | 0 rows | 5 rows |

Both AAPL columns measured 0. A zero-row answer records `outcome empty` with no properties, and every run
after it agrees — the endpoint would be probed weekly and never checked. `LiveApi.CryptoPairs` and
`LiveApi.ForexPairs` exist for that reason, and are pinned by a test rather than left to a comment.

**`fmp-articles` still answers** at `limit=5&page=0`, 5 rows spanning 2026-08-28 21:00:00 .. 2026-08-29
13:00:21, despite having published nothing new since 2026-08-28 21:05:54. The stall recorded in the previous
addendum is a stall in *publication*, not in the corpus — which is what makes it a documentation problem
rather than a broken path.

**Fixtures captured from this run** and committed to `tests/FmpDotNet.Tests/Fixtures/`:
`news-stock-latest.head.json` (3 rows, `limit=3`, row 0 `symbol="SAIC"` with no null values on any key),
`news-general-latest.head.json` (3 rows, `limit=3`, `symbol` null on 3 of 3), `fmp-articles.head.json` (2
rows, `limit=2`, `tickers` colon-prefixed on both captured rows: `NASDAQ:NRIM`, `NASDAQ:CSIQ`). No request URL
and no key appears in any of them; they are response bodies as FMP sent them.

**Task 3's two extra fixture-gate checks, run against the captured `fmp-articles.head.json` rows before
copying them in, both passed on 2026-08-31:** `Assert.All(rows, r => Assert.Empty(Binding.Unbound(r)))` — no
row is missing a non-blank value for any of the eight bound keys (`title`, `date`, `content`, `link`,
`author`, `site`, `image`, `tickers`) — and `Assert.All(rows, r => Assert.Equal("Financial Modeling Prep",
r.Site))` — both captured rows carry `site == "Financial Modeling Prep"`.
