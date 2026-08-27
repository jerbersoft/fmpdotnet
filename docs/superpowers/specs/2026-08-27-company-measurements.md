# Company — measured 2026-08-27, Ultimate key

The 13 unmodelled paths of issue #29, measured before any of them is modelled. Every number below came
from a live call on 2026-08-27; nothing here is taken from FMP's documentation. `AAPL` is the reference
filer; `JPM`, `XOM`, `SHOP`, `KO`, `TSM`, `T`, `F`, `BAC`, `SPY` and 600 symbols harvested from
`stable/stock-list` were used wherever a single filer would have hidden the variance.

## Coverage

**All 13 answered 200.** None is gated behind an add-on on this plan, so the whole group is
implementable now. Three paths answer **400** when a required parameter is missing:
`mergers-acquisitions-search` with no `name`, `market-capitalization-batch` with empty `symbols`, and
`profile-cik` with a non-numeric `cik`.

Unknown-but-well-formed input answers an **empty array with HTTP 200**, never a 404 — measured on
`market-capitalization` (`ZZZZNOPE`), `stock-peers` (`ZZZZNOPE`),
`governance-executive-compensation` (`ZZZZNOPE`), `profile-cik` (`cik=9999999999`) and
`company-notes` (`BAC`). "Not found" is a shape here, exactly as it is for `stable/profile`.

## `employee-count` and `historical-employee-count` are the same endpoint

Two documented paths, one dataset. Byte-identical responses, compared as sorted JSON:

| symbol | rows | identical |
|---|---|---|
| `AAPL` | 32 | yes |
| `JPM` | 5 | yes |
| `SHOP` | 11 | yes |
| `XOM` (`limit=2`) | 0 | yes — both empty |

Both honour `limit` downward (`AAPL&limit=3` → 3 rows). Both answer the same nine fields:
`symbol, cik, acceptanceTime, periodOfReport, companyName, formType, filingDate, employeeCount,
source`. `employeeCount` is an integer; `source` is an EDGAR URL; `acceptanceTime` is
`2025-10-31 06:01:26` — a space-separated stamp, not ISO-8601 with a `T`.

**`XOM` answers zero rows on both paths.** A major filer with no employee history at all, so an empty
result is normal rather than a symptom.

## Market capitalisation is not integral

`market-capitalization-batch` for 20 large caps, measured 2026-08-27:

```
GOOG   4098415617064.9995
```

One row of twenty. Every other row was integral, and single-symbol `market-capitalization` for `AAPL`
answered the integral `4620348450480`. **A `long?` binding throws
`JsonException: The JSON value could not be converted to System.Nullable`1[System.Int64]` on the GOOG
row**, and a fixture captured from one symbol would never show it. This is the same defect as
`ScreenerResult.Volume`, found the same way and one day later.

`stock-peers` carries the same quantity under a different name — **`mktCap`, not `marketCap`** — and
answered integral values on all ten symbols probed (`AAPL, JPM, SPY, KO, TSM, F, T, SHOP, O, XOM`).
Ten symbols is not proof of integrality; the name difference is the measured fact.

## `market-capitalization-batch` silently drops symbols

| symbols requested | rows returned |
|---|---|
| 1 | 1 |
| 3 | 3 |
| 20 | 20 |
| 50 | 50 |
| 100 | **99** |
| 200 | **199** |
| 300 | **299** |
| 500 | **499** |

The 100-symbol request was built from the first 100 plain tickers of `stable/stock-list`. The missing
row is **`WDSP`** — a symbol FMP's own directory lists and its market-cap endpoint has no row for. Two
symbols, `AAPL,ZZZZNOPE`, answered one row the same way.

**No upper bound on batch size was found up to 500 symbols.** The endpoint neither errors nor
truncates; it answers for what it has. A caller that zips the request list against the response list
positionally corrupts every row after the first gap. Responses must be matched by `symbol`.

## `historical-market-capitalization` returns three months unless asked otherwise

| call | rows | span |
|---|---|---|
| `symbol=AAPL` | 65 | 2026-05-27 → 2026-08-27 |
| `symbol=AAPL&limit=5` | 5 | ends 2026-08-27 |
| `symbol=AAPL&limit=5000` | **65** | 2026-05-27 → 2026-08-27 |
| `symbol=AAPL&limit=100000` | **65** | 2026-05-27 → 2026-08-27 |
| `from=2024-01-01&to=2024-01-10` | 7 | the seven trading days |
| `from=2020-01-01&to=2026-08-27` | 1672 | 2020-01-02 → 2026-08-27 |
| `from=2000-01-01&to=2026-08-27` | **5000** | **2006-10-11** → 2026-08-27 |
| `from=1990-01-01&to=2026-08-27` | **5000** | **2006-10-11** → 2026-08-27 |

Two separate traps.

**`limit` cannot widen the default window.** It clamps downward from 65 and is ignored upward — 5,000
and 100,000 both answer the same 65 rows. Only `from`/`to` reach history.

**The range is capped at exactly 5,000 rows, and the cap keeps the newest.** `from=2000-01-01` and
`from=1990-01-01` return the identical span starting `2006-10-11`. A caller asking for all history
gets the most recent 5,000 sessions and no indication that anything was dropped. Walking backwards
with `to` is the only way to reach further.

## `mergers-acquisitions-latest` is the whole archive, newest first

| call | rows |
|---|---|
| bare | 100 (default) |
| `limit=10` | 10 |
| `limit=1000` | 1000 |
| `limit=5000` | **1000** — clamped |

Paged at `limit=1000`, pages 0 to 3 are full and page 4 carries 704: **4,704 rows spanning
1994-01-10 → 2026-08-25**. Page 5 and beyond answer `[]` with HTTP 200. Pages 0 and 1 share zero rows,
so paging is disjoint and the walk terminates on a short page, as `delisted-companies` does.

"Latest" names the ordering, not the contents — page 0 at `limit=1000` already reaches back to
2021-09-13.

**Three fields are nullable, and a 10-row sample shows none of them.** Over the 1,000 rows of page 0:

| field | nulls |
|---|---|
| `targetedCik` | 390 |
| `targetedSymbol` | 181 |
| `targetedCompanyName` | 1 |

`targetedCik` also carries **`"0000000000"`** as a sentinel for "unknown", so the field has two
distinct ways of saying nothing.

## `mergers-acquisitions-search` ignores `page` and `limit`

`name=Bank` answers 233 rows bare, and 233 rows with `page=0&limit=5`. `name=Apple` answers 3.
`name=zzzznope` answers `[]`. Omitting `name` answers **400**. The endpoint returns its whole result
set every time, so a caller that passes paging parameters gets the full set while believing it asked
for five rows.

## `governance-executive-compensation` ignores `year`

`symbol=AAPL` and `symbol=AAPL&year=2025` answer byte-identical bodies of 339 rows spanning
**1999 → 2025**. `JPM` answers 160. The endpoint returns the filer's whole compensation history in one
call; there is no server-side year filter to lean on.

Fifteen fields, all populated on both filers: `cik, symbol, companyName, filingDate, acceptedDate,
nameAndPosition, year, salary, bonus, stockAward, optionAward, incentivePlanCompensation,
allOtherCompensation, total, link`. The money fields are integers; `nameAndPosition` runs name and
title together in one string (`"Luca Maestri Former Senior Vice President, Chief Financial Officer"`).

## `executive-compensation-benchmark` defaults to last year, not this year

| call | rows | `year` in rows |
|---|---|---|
| bare | 377 | **2024** |
| `year=2025` | 365 | 2025 |
| `year=2010` | 386 | 2010 |
| `year=1990` | 1 | 1990, `averageCompensation` `0` |

Three fields: `industryTitle, year, averageCompensation`. **`averageCompensation` is fractional** —
`609504.1428571428` — so it is a `decimal`, not an integer, on every row that is not exactly zero.

The first call to `year=2025` took **37.18 s**; the identical call later took **0.53 s**. Cold, this
endpoint is slow enough to trip a default HTTP timeout.

## `key-executives`: two fields carry nothing, and pay is not always USD

Measured over 203 rows across 18 symbols (`AAPL, SHOP, JPM, F, KO, TSM, GE, IBM, WFC, C, BA, MMM,
PFE, MRK, DIS, NKE, CSCO, ORCL`):

| field | observed |
|---|---|
| `titleSince` | **null on all 203 rows** |
| `active` | **`true` on all 203 rows** — never `false`, never null |
| `gender` | `"male"`, `"female"` or null (9 null of the first 64) |
| `pay` | integer or null (32 null of the first 64) |
| `yearBorn` | integer or null (24 null of the first 64) |
| `currencyPay` | always present — **`"USD"` and `"TWD"`**, so pay is not comparable across rows without it |

`SPY` answers `[]` — an ETF has no executives.

## `company-notes`: `symbol` is not the ticker, and titles carry raw HTML entities

Rows by issuer: `AAPL` 7, `T` 20, `F` 16. `JPM`, `BAC`, `VZ`, `GS`, `MS`, `PG` and `JNJ` all answer
`[]` — the dataset is sparse, and an empty result is the common case rather than an error.

Four fields: `cik, symbol, title, exchange`.

**`symbol` names the note, not the issuer.** `symbol=T` answers 20 rows whose symbols are
`T, T 25, T 25B, T 26A, T 26D, T 26E, T 27C, T 28C, T 29A, T 29B, T 29D, T 30B, T 30C, T 31F, T 32,
T 32A, T 33, T 33A, T PRA, T PRC` — 19 of the 20 differ from the requested ticker, and they contain
**spaces**. Anything that treats this field as a tradeable ticker is wrong.

**`exchange` is null on 19 of `T`'s 20 rows.** A one-row sample from `AAPL` shows `"NASDAQ"` and hides
this entirely.

**Titles are HTML-escaped and FMP does not decode them.** `T`'s titles carry `&amp;` verbatim:
`"AT&amp;T Inc. 5.200% Global Notes due November 18, 2033"`. The only entity observed was `&amp;`.

## `profile-cik` is `stable/profile` keyed by CIK

Answers the identical 36 fields as `stable/profile`, in the same order, for a single-element array —
so it binds to the existing `CompanyProfile` model with no new type. Both the zero-padded
`cik=0000320193` and the bare `cik=320193` answer the same AAPL row. `cik=9999999999` answers `[]`;
`cik=notacik` answers **400**.

## `stock-peers` — four fields, one of them misspelled against the rest of the API

`symbol, companyName, price, mktCap`. `AAPL` answers 9 rows, `JPM` and `SPY` 10 each, `ZZZZNOPE` `[]`.
ETFs get peers (`SPY` → `IVV`, …), so this is not equity-only.

`mktCap` is the same quantity that every other endpoint in this group spells `marketCap`.

## Method, repeatable

```
python3 - <<'EOF'
import json, re, ssl, urllib.parse, urllib.request, pathlib, certifi
CTX = ssl.create_default_context(cafile=certifi.where())   # macOS python has no system roots
KEY = re.search(r'^\s*(?:export\s+)?FMP_API_KEY\s*=\s*["\']?([^"\'\s]+)',
                pathlib.Path('.env').read_text(), re.M).group(1)
def call(path, **p):
    p['apikey'] = KEY
    url = 'https://financialmodelingprep.com/' + path + '?' + urllib.parse.urlencode(p)
    with urllib.request.urlopen(url, timeout=120, context=CTX) as r:
        return json.loads(r.read().decode())
EOF
```

⚠️ The URL carries the key in its query string. Never log the built URL, and never write it into a
captured fixture.
