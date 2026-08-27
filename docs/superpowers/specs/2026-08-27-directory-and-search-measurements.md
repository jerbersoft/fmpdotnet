# Directory & Search — measured 2026-08-27, Premium key

## Coverage
All 17 paths answered 200 on this plan.
Directory 6 + Search 6 + five asset-class/transcript lists filed elsewhere in FMP's docs
(`commodities-list`, `cryptocurrency-list`, `forex-list`, `index-list`, `earnings-transcript-list`).

## The eleven list endpoints

| path | rows | keys |
|---|---|---|
| `available-countries` | 117 | `country` — ISO-2 codes, e.g. `{"country":"FK"}` |
| `available-exchanges` | 63 | `exchange,name,countryName,countryCode,symbolSuffix,delay` |
| `cik-list` | 512,665 over 52 pages | `cik,companyName` |
| `etf-list` | 14,567 | `symbol,name` |
| `financial-statement-symbol-list` | 68,200 | `symbol,companyName,tradingCurrency,reportingCurrency` |
| `symbol-change` | 5,456 (default answers 100) | `date,companyName,oldSymbol,newSymbol` |
| `commodities-list` | 40 | `symbol,name,exchange,tradeMonth,currency` |
| `cryptocurrency-list` | 4,793 | `symbol,name,exchange,icoDate,circulatingSupply,totalSupply` |
| `forex-list` | 1,551 | `symbol,fromCurrency,toCurrency,fromName,toName` |
| `index-list` | 425 | `symbol,name,exchange,currency` |
| `earnings-transcript-list` | 11,178 | `symbol,companyName,noOfTranscripts` |

All eleven **ignore `limit`** except `cik-list` and `symbol-change`. Asking `etf-list` for 5 rows
still transfers all 14,567 — same behaviour already recorded for `stock-list`.

## The six search endpoints -> 5 shapes

```
SYMBOL (5): symbol,name,currency,exchangeFullName,exchange
  search-symbol(query)  AAPL -> 7 rows      search-name(query)  Apple -> 37 rows
CIK (6):    symbol,companyName,cik,exchangeFullName,exchange,currency
  search-cik(cik)       0000320193 -> 1 row
CUSIP (4):  symbol,companyName,cusip,marketCap
  search-cusip(cusip)   037833100 -> 4 rows
ISIN (4):   symbol,name,isin,marketCap
  search-isin(isin)     US0378331005 -> 5 rows
VARIANT (36): a v3-era company profile — see below
  search-exchange-variants(symbol)  AAPL -> 6 rows
```

`search-cusip` calls the company `companyName`; its sibling `search-isin` calls the identical field
`name`. Same divergence already recorded between `stock-list` and `actively-trading-list`.

Undocumented parameters that work: `search-symbol` and `search-name` honour **`limit`** and
**`exchange`** (`query=AAPL` 7 rows -> `exchange=NASDAQ` 1 row). Default limit is 50 — `query=AA`
answers exactly 50. `search-cusip` and `search-isin` **ignore `limit`** (4 -> 4, 5 -> 5).

`search-cik` accepts padded and unpadded (`0000320193` and `320193` both -> 1 row) and always
echoes the padded 10-character form. All 200 rows sampled from `cik-list` are 10 characters.

All five searches answer garbage input with **HTTP 200 and `[]`**, never an error:
`query=ZZZZQQQQ9`, `cik=9999999999`, `cusip=000000000`, `isin=XX0000000000`.

## `symbol-change` hides 98% of itself behind an undocumented default

```
limit=100     ->   100 rows        (this is also the default — no limit sent)
limit=1000    ->  1000 rows
limit=10000   ->  5456 rows        <- the true total
limit=100000  ->  5456 rows        <- no server cap below 100000
```

`page` is **accepted and silently ignored**: `page=0` and `page=1` at `limit=3` both answer
`['SIC','SBEV','TUGN']`. `limit` is the only lever, and FMP documents no parameters at all for
this path.

## `cik-list` is every SEC registrant, not a symbol directory

Hard cap of 10,000 rows per page regardless of `limit` (`limit=50000` and `limit=200000` both
answer 10,000). `page` genuinely works — page 0 starts `0002150676`, page 1 starts `0002150170`.
Binary search for the last non-empty page: **page 51, 2,665 rows -> about 512,665 CIKs.**

Against `stock-list`'s 91,845. The extra rows are not companies:
`Thompson David Blair`, `TOP Private Wealth LLC.`, `Oakmont Investment Advisors, Inc.`

## Crypto supply overflows `long`, and is fractional

```
circulatingSupply  n=4792  fractional=953  >long.MaxValue=1   max 9.223372e+18
totalSupply        n=3319  fractional=944  >long.MaxValue=1   max 1.839853e+23
  SHIBDOGEUSD      circulatingSupply 9223372036854776000   totalSupply 1.8398528382123738E+23
```

Neither exceeds `decimal` (7.9e28). `circulatingSupply` null on 1 row, `totalSupply` null on 1,474.
`icoDate` null on 33, ISO `uuuu-MM-dd` on the other 4,760, malformed on none.

## `marketCap` on the identifier searches is in the listing's local currency, unlabelled

```
search-cusip 037833100      AAPL.MX  78,694,853,448,000
                            APC.DE    3,863,520,570,000
                            AAPL      4,537,071,141,960
                            APC.F     3,942,086,350,399.9995
search-isin  US0378331005   AAPL.DE                   0
```

Confirmed against `stable/profile?symbol=AAPL.MX` -> `currency: MXN`, `marketCap 78,283,607,480,000`.
Neither search endpoint carries a currency field. Sorting these rows by market cap ranks currencies.
`APC.F` shows the `.9995` double artifact already recorded on `marketCap` in #24.

## `search-exchange-variants` returns a v3-era profile, and `exchange` is inverted

36 fields on both, 29 shared. Renames confirmed by value equality on AAPL:

```
profile.change       == variants.changes   (3.55)
profile.lastDividend == variants.lastDiv   (1.05)
profile.marketCap    == variants.mktCap    (4603751738200)
profile.averageVolume 53,379,406  !=  variants.volAvg 55,604,384   <- NOT a pure rename
```

| only on `stable/profile` | only on `search-exchange-variants` |
|---|---|
| `averageVolume, change, changePercentage, exchangeFullName, lastDividend, marketCap, volume` | `changes, dcf, dcfDiff, exchangeShortName, lastDiv, mktCap, volAvg` |

**The inversion.** Same field name, opposite meaning:

```
profile.exchange           'NASDAQ'                  profile.exchangeFullName   'NASDAQ Global Select'
variants.exchange          'NASDAQ Global Select'    variants.exchangeShortName 'NASDAQ'
```

**`dcf` and `dcfDiff` disagree with `price` on the same row**, on every row, and not by rounding —
the sign of the disagreement is not even consistent:

```
symbol     price     dcf+dcfDiff
AAPL       313.45    312.96      implied price BELOW  price
APC.DE     266.25    267.95      implied price ABOVE  price
AAPL.MX    5330      5300.01     implied price BELOW  price
AAPL.NE    44.4      44.05       implied price BELOW  price
APC.F      266.45    268.9       implied price ABOVE  price
AAPL.DE    null      null        price, range, changes, dcfDiff all null
```

**`cik` is null on 5 of the 6 rows** — only the primary US listing carries one. So this endpoint is
a poor symbol->CIK bridge despite being the only profile-shaped endpoint that returns a CIK per
listing.

## Other measured behaviour

`available-exchanges.delay` is free-text prose, not a duration, and one value is null:
`15 min` x35, `Real-time` x16, `20 min` x9, `10 min` x2, null x1 (`FSX`).
`symbolSuffix` is the literal string `"N/A"` on 5 rows rather than null — appending it blindly
produces `AAPL.N/A`.

`earnings-transcript-list.noOfTranscripts` is a **string** on all 11,178 rows (`"6"`, `"2"`, `"16"`).

`commodities-list.exchange` is **null on all 40 rows** — a documented field that is never populated.
`tradeMonth` is a 3-letter month abbreviation (`"Dec"`), not a date. `currency` includes `USX`
(US cents) alongside `USD`.

`financial-statement-symbol-list.reportingCurrency` null on 149 of 68,200; `tradingCurrency` on none.
`tradingCurrency` and `reportingCurrency` differ (`TOELY`: trades USD, reports JPY).

**Subset relations, both strict, both zero outside:**
`etf-list` (14,567) ⊂ `stock-list` (91,845) — 0 symbols outside.
`financial-statement-symbol-list` (68,200) ⊂ `stock-list` — 0 symbols outside.
Same relation already recorded for `actively-trading-list`.

`symbol-change.date` is ISO `uuuu-MM-dd` on all 5,456 rows; no nulls in any of its four fields.
