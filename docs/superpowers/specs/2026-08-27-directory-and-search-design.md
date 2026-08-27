# Directory and Search coverage — design

**Issue:** [#25](https://github.com/jerbersoft/fmpdotnet/issues/25) — Coverage: the remaining long tail
**Measured:** 2026-08-27, Premium key, ~60 calls on the ordinary throttle
**Status:** approved

Issue #25 is not a unit of work and says so. This is the first slice taken out of it: the six
`stable/search-*` paths, the six remaining `Directory` paths, and the five single-path lists FMP
files under Commodity, Crypto, Forex, Indexes and Earnings Transcript. Seventeen paths, into two
facades that already exist.

Everything below was measured before it was modelled. The raw capture is
`2026-08-27-directory-and-search-measurements.md`; the load-bearing figures are repeated here so this
document stands alone.

## What #25 actually contains, since the issue's own list is wrong

The issue names Commodity, Forex, Crypto and Indexes as long-tail groups and omits Statements,
Company, Analyst, Calendar, Directory, Search and Economics entirely. Measured against the generated
coverage table, that is backwards in both directions.

Commodity, Forex and Crypto contribute **one path each** — their symbol lists. Everything else in
those sections is `quote` and `historical-price-eod` re-documented, which `fmp.Quote` and `fmp.Chart`
already reach. Indexes contributes 7, none of them quotes.

Meanwhile **61 of the 165 remaining paths sit in facades the SDK has already opened**: Statements 19,
Company 13, Analyst 7, Calendar 7, Directory 6, Search 6, Economics 3. A third of the "long tail" is
finishing rooms already framed rather than building new ones.

Splitting the 165 by asset class gives the sharper number:

| | covered | remaining | done |
|---|---|---|---|
| equity-only groups | 15 | 106 | 12% |
| shared / asset-agnostic | 50 | 59 | 46% |

The inversion is structural. What has been built so far is price plumbing — Quote 10, Chart 10,
Bulk 18 — and one `GetQuoteAsync` serves equities, crypto, forex, commodities and indices alike, so
asset-class breadth came free while equity depth never got built. **This slice deepens the 46%, not
the 12%**, and was chosen with that trade-off explicit. The equity groups are the natural next slices.

The issue text should be corrected when this lands.

## The shapes

Eleven list endpoints returning eleven distinct row shapes — there is almost no collapse here, unlike
Quote and Chart, where sixteen endpoints gave five. Two of the eleven match shapes the SDK already
models, leaving nine new. Six search endpoints collapse to five shapes, because `search-symbol` and
`search-name` are identical. Nine plus five is the fourteen new models below.

Two reuses, both measured rather than assumed:

- **`etf-list` sends `{symbol, name}`** — the exact `actively-trading-list` wire shape — so it reuses
  `CompanySymbol` and that endpoint's existing internal wire type unchanged.
- **`available-countries` returns single-key rows** of ISO-2 codes, so it goes through the existing
  `Labels()` helper to `IReadOnlyList<string>`, exactly like sectors and industries.

## The five things a shape-only reading would get wrong

### 1. `symbol-change` hides 98% of itself, and `page` is a decoy

The default answers 100 rows. The true total is **5,456**.

```
limit=100    ->  100      limit=10000   -> 5456   <- true total
limit=1000   -> 1000      limit=100000  -> 5456   <- no server cap below 100000
```

`page` is accepted and **silently ignored** — `page=0` and `page=1` at `limit=3` both answer
`['SIC','SBEV','TUGN']`. FMP documents no parameters at all for this path, so `limit` is both
undocumented and the only lever.

The SDK sends `limit=10000` unconditionally and exposes **no parameter**. There is no correct smaller
answer for a symbol-change history — a caller reconciling old tickers needs all of it or the
reconciliation is silently wrong — and `page` cannot be offered because offering it would imply it
works. 10,000 is headroom against a measured 5,456, not a guess: the ceiling was probed to 100,000.

### 2. `cik-list` is every SEC registrant, and needs both shapes

512,665 rows across 52 pages, hard-capped at 10,000 per page regardless of `limit`. `page` genuinely
works here — the opposite of `symbol-change`, on a sibling endpoint in the same group.

It is not a symbol directory. Against `stock-list`'s 91,845 symbols, the extra half-million rows are
individuals and advisory firms: `Thompson David Blair`, `TOP Private Wealth LLC.`

So it gets both shapes: `GetCikListAsync(page, limit)` for a caller who wants one page, and
`StreamCikListAsync()` which walks all 52 and stops on the first short page. `StreamAllProfilesAsync`
in `BulkEndpoints` is the precedent for the streaming half.

### 3. Crypto supply overflows `long`

```
circulatingSupply  953 of 4,792 fractional,   1 above long.MaxValue
totalSupply        944 of 3,319 fractional,   1 above long.MaxValue, max 1.84e23
  SHIBDOGEUSD      9223372036854776000  and  1.8398528382123738E+23
```

Both become `decimal?` — 1.84e23 against decimal's 7.9e28 leaves five orders of headroom. This is
#24's fractional-volume trap in a new field, and the reason to type it from the sweep rather than
from one symbol: no major coin exhibits either problem.

### 4. `marketCap` on the identifier searches is in the listing's local currency, unlabelled

AAPL's CUSIP returns four listings. The first is `AAPL.MX` at 78,694,853,448,000 — MXN, confirmed
against `stable/profile?symbol=AAPL.MX`. Neither `search-cusip` nor `search-isin` carries a currency
field, and neither carries an exchange field either, so **nothing on the row says which currency the
number is in**.

It stays `decimal?` with the hazard documented on the property. Dropping a field FMP sends would be
worse, but the documentation must say plainly that sorting these rows by market cap ranks currencies
rather than companies — the Mexican listing sits 17x above the US one.

### 5. `search-exchange-variants` is a v3-era profile with `exchange` inverted

36 fields against `CompanyProfile`'s 36, 29 shared, and the seven that differ are not cosmetic:

| only on `stable/profile` | only on `search-exchange-variants` |
|---|---|
| `averageVolume, change, changePercentage, exchangeFullName, lastDividend, marketCap, volume` | `changes, dcf, dcfDiff, exchangeShortName, lastDiv, mktCap, volAvg` |

Three are confirmed renames by value equality on AAPL — `change`/`changes`, `lastDividend`/`lastDiv`,
`marketCap`/`mktCap`. `averageVolume` and `volAvg` are **not**: 53,379,406 against 55,604,384.

The inversion is the part that would produce silently wrong code:

```
profile.exchange   'NASDAQ'                variants.exchange           'NASDAQ Global Select'
profile.exchangeFullName 'NASDAQ Global Select'   variants.exchangeShortName  'NASDAQ'
```

A caller who reuses `CompanyProfile` here, or who writes `row.Exchange == "NASDAQ"`, gets nothing and
no error. `ExchangeVariant` is therefore its own model, and documents the inversion on both
properties.

Two further hazards on this endpoint, both documented rather than corrected:

- **`dcf + dcfDiff` disagrees with `price` on every row**, and the sign of the disagreement is not
  consistent — AAPL implies 312.96 against a `price` of 313.45, APC.DE implies 267.95 against 266.25.
  The two are computed against different prices and the row does not say which.
- **`cik` is null on 5 of 6 rows** — only the primary US listing carries one. This is the only
  profile-shaped endpoint returning a CIK per listing, and it is not a usable symbol->CIK bridge.

## Surface

**`fmp.Search`** — six new methods joining `ScreenAsync`. Named `FindBy*` rather than `Get*`
deliberately: these return *candidates*, plural and ranked, where `Get*` across this SDK returns the
thing you named. `ScreenAsync` already establishes that non-`Get` verbs belong here.

| method | path | returns |
|---|---|---|
| `FindBySymbolAsync(query, limit?, exchange?)` | `search-symbol` | `IReadOnlyList<SymbolSearchResult>` |
| `FindByNameAsync(query, limit?, exchange?)` | `search-name` | `IReadOnlyList<SymbolSearchResult>` |
| `FindByCikAsync(cik)` | `search-cik` | `IReadOnlyList<CikSearchResult>` |
| `FindByCusipAsync(cusip)` | `search-cusip` | `IReadOnlyList<CusipSearchResult>` |
| `FindByIsinAsync(isin)` | `search-isin` | `IReadOnlyList<IsinSearchResult>` |
| `GetExchangeVariantsAsync(symbol)` | `search-exchange-variants` | `IReadOnlyList<ExchangeVariant>` |

Every one returns a list, never `T?`. One CUSIP measured 4 rows and one ISIN 5, because an instrument
is listed in several places — a single-row return would silently pick one listing, in an unspecified
currency, which is exactly trap 4.

`limit` and `exchange` appear only on the two endpoints that honour them. `search-cusip` and
`search-isin` ignore `limit`, so the SDK will not offer a parameter that does nothing.

**`fmp.Directory`** — twelve new methods over eleven paths, joining the existing four. `cik-list` gets
two, for the reason in trap 2.

`GetCountriesAsync` · `GetExchangesAsync` · `GetEtfListAsync` · `GetFinancialStatementSymbolsAsync` ·
`GetSymbolChangesAsync` · `GetCikListAsync` + `StreamCikListAsync` · `GetCommodityListAsync` ·
`GetCryptocurrencyListAsync` · `GetForexListAsync` · `GetIndexListAsync` · `GetTranscriptSymbolsAsync`

**The five asset-class lists go here rather than into new facades.** FMP files them under Commodity,
Crypto, Forex, Indexes and Earnings Transcript, so the SDK's grouping diverges from the documentation
at exactly these five paths. The reason: they do Directory's job — "what exists" — and #24 already
declined per-asset-class facades on the grounds that one `GetQuoteAsync` serves them all. An
`fmp.Crypto` holding a single list method would invite `fmp.Crypto.GetQuoteAsync`, which will never
live there. The divergence is documented on each method.

## Models

Fourteen new records, plus the two reuses above.

`SymbolSearchResult` · `CikSearchResult` · `CusipSearchResult` · `IsinSearchResult` ·
`ExchangeVariant` · `ExchangeInfo` · `FinancialStatementSymbol` · `SymbolChange` · `CikEntry` ·
`CommodityInfo` · `CryptocurrencyInfo` · `ForexPair` · `IndexInfo` · `TranscriptSymbol`

`search-symbol` and `search-name` return identical five-field shapes and share
`SymbolSearchResult`.

`search-cusip` and `search-isin` get **separate** models. Their company-name divergence — `companyName`
against `name` — is packaging and gets unified the `CompanySymbol` way, with internal wire shapes
mapped in the endpoint class. But `cusip` and `isin` are different facts, and a shared model would
carry one permanently-null field on every row.

Field typing decided from the sweep rather than from a sample:

- `CryptocurrencyInfo.CirculatingSupply` and `.TotalSupply` — `decimal?` (trap 3)
- `CusipSearchResult.MarketCap`, `IsinSearchResult.MarketCap` — `decimal?`, hazard documented (trap 4)
- `TranscriptSymbol.NoOfTranscripts` — `int?`. The wire sends a **string** on all 11,178 rows;
  `FmpJsonContext` already sets `NumberHandling = AllowReadingFromString`, so no converter is needed,
  but the model documents that the tolerance is load-bearing here rather than incidental.
- `ExchangeInfo.Delay` — `string?`, **not** a `Duration`. The wire is free-text prose with five
  distinct values (`15 min` x35, `Real-time` x16, `20 min` x9, `10 min` x2) and one null (`FSX`).
  Parsing it would mean inventing a mapping FMP does not publish.
- `ExchangeInfo.SymbolSuffix` — `string?`, documenting that 5 of 63 rows carry the literal `"N/A"`
  rather than null, so appending it blindly produces `AAPL.N/A`.
- `CommodityInfo.Exchange` — `string?`, documenting that it is null on **all 40 rows**. The #26
  baseline guard will record it empty; that is correct rather than drift.

## Testing

Offline unit tests per endpoint against captured fixtures, following `ChartEndpointsTests`. The ones
that earn their place, because each covers a failure invisible in a passing response:

- `A_symbol_change_request_asks_for_more_than_the_hidden_default` — asserts the request URL carries
  `limit=10000`. The response looks healthy either way; only the URL shows the bug.
- `The_cik_stream_walks_every_page_until_one_comes_back_short`
- `A_crypto_supply_beyond_long_max_is_read_rather_than_refused` — fixture pinned to `SHIBDOGEUSD`
- `A_fractional_crypto_supply_is_read_rather_than_refused`
- `An_exchange_variant_reads_the_code_from_exchangeShortName` — the inversion, asserted directly
- `An_exchange_variant_is_not_a_company_profile` — asserts the two models do not bind each other's
  payloads, so a future refactor cannot quietly merge them
- `A_cusip_match_and_an_isin_match_agree_on_the_company_name` — proves the wire-shape unification
- `An_unknown_identifier_reads_as_an_empty_list` — all five searches, each with its own stub response,
  since one `StubHandler` response cannot serve five calls
- `A_transcript_count_arrives_as_a_string_and_reads_as_a_number`
- `Only_the_endpoints_that_honour_limit_send_it` — asserts `search-cusip` and `search-isin` omit it

Each mutation-checked by breaking the code and confirming the specific test fails.

## Elsewhere

`FmpJsonContext` gains fourteen `JsonSerializable` entries. No DI or `FmpClient` change — both
facades already exist and are registered.

`Probe.Argument` in the smoke suite needs cases for the new parameter names (`cik`, `cusip`, `isin`,
`query`), and this is the same failure mode #24 hit with `exchange`: any unrecognised string maps to
`LiveApi.Symbol`, so `cusip=AAPL` would record `rows 0` as a baseline and agree with itself forever.
The names get explicit constants on `LiveApi` with the reasoning attached, as `Exchange` did.

README coverage table regenerates to **82 of 230**. The "Reaching an endpoint that is not modelled"
section needs its remaining-paths prose corrected — it currently repeats the issue's wrong group list.

The smoke sweep gains 17 endpoints, four of them whole-universe downloads
(`financial-statement-symbol-list` 68,200 rows, `etf-list` 14,567, `earnings-transcript-list` 11,178,
`cryptocurrency-list` 4,793). Sweep timing gets re-measured and the workflow timeout comment updated
rather than assumed to still hold.

## Deliberately not in scope

- **The equity groups.** 106 paths across Statements, Company, SEC Filings, Market Performance,
  Form 13F, Analyst, Calendar, Insider Trades, Senate, Fundraisers, DCF, ESG and Transcripts. Each is
  its own slice of #25.
- **A symbol -> CIK convenience.** `search-cik` goes the other way, and `search-exchange-variants`
  returns a CIK on only the primary listing. A caller with a CIK from EDGAR or their own store can
  pass it to any CIK-keyed endpoint directly; nothing here blocks that.
- **Parsing `ExchangeInfo.Delay` into a `Duration`.** See the model note above.
- **`fmp.Commodity` / `fmp.Crypto` / `fmp.Forex` / `fmp.Indexes` facades.** Reconsider if and when
  the seven Indexes constituent paths land — that group has a future beyond one list method, and the
  other three do not.
