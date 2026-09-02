# Plan tier provenance — where every `Plan tier —` note comes from

**Recorded 2026-09-02 for #45.** Every `*Endpoints` class, and every member whose tier differs from its class's
floor, carries a paragraph opening `Plan tier — …`. This file is the evidence behind those paragraphs: which
source said what, on which date, and how that maps onto this SDK's groups, so that a note can be re-verified
or corrected later without re-deriving it.

**These are dated observations, not a contract.** Nothing in the SDK reads them. `FmpPlanRestrictedException`
stays the only authority on entitlement, 402 and 403 keep meaning different things, and no call is gated
before it is sent. What changed in #45 is prose: a reader on Starter can now find out, per method, what was
observed below the tier this repo measures on — and by whom.

## The three sources

**Source A — this repository.** One key, on FMP's Ultimate plan. Every one of the 236 modelled paths answered
HTTP 200 on it: the ordinary endpoints in the smoke sweep recorded in `tests/FmpDotNet.SmokeTests/baseline-ordinary.txt`
(re-recorded 2026-09-02), the eighteen `*-bulk` paths in `baseline-bulk.txt` (2026-08-27). That is the whole
of what this repo can measure about tiers: an Ultimate key proves nothing about the plans below it. It is
why, until #45, the class docs said "measured against an Ultimate key" and stopped.

**Source B — `fmpsdk` 20260824.0**, the independent Python client that is also Source B of the
[endpoint inventory](2026-08-27-endpoint-inventory.md), read from its published sdist at `~/Projects/fmpsdk`.
Its README says every method's docstring states the tier it needs, "verified live against a real key at
every tier from Free through Ultimate". Read directly, the docstrings say less than that:

- 21 of its 29 endpoint modules carry a tier sentence, at module level, at method level, or both; the
  other 8 (`analyst`, `commodity`, `crypto`, `dcf`, `forex`, `fundraisers`, `market_hours`,
  `market_performance`) say nothing, and inside `calendar`, `company`, `economics` and `statements` most
  members say nothing.
- Where it names a date, it is **2026-08-23** for "confirmed working on Premium" and **2026-08-24** for
  "confirmed working on Ultimate". Its "402s on the free tier" and "requires Starter" sentences carry no
  date of their own; the package version pins them to no later than 2026-08-24.
- It validates nothing it sends (#46), and its docstrings follow FMP's documentation as much as FMP's
  behaviour. Tier sentences are better evidenced than its parameter lists — the README describes real keys
  at each tier — but they remain that project's observations on a date.

**Silence in Source B is not a Free-tier claim.** The README's "every method" is not borne out by the files,
so a member with no sentence is a member nobody recorded, and the notes here say "no floor on record" for
it rather than "Free". Only members Source B explicitly records as working on the free tier are labelled
Free.

Every Source B claim is transcribed verbatim in the appendix. None of them contradicts Source A — every
"confirmed working on Ultimate" agrees with the 200s here — so the rule "a second-hand claim that contradicts
our own measurement loses" never had to be applied.

**Source C — the application this SDK replaces** (`trader`, `Trader.Adapters.MarketData.Fmp`). Its specs
record two live probes on its own keys:

| what | key | result | where |
|---|---|---|---|
| `profile-bulk?part=0` | free, 2026-07-23 | **402** | `2026-07-23-trd-spec-024-instrument-company-profile-design.md` |
| `profile-bulk?part=0` | "this subscription", plan not named, 2026-08-07 and 2026-08-15 | **402 Restricted Endpoint** | `2026-08-07-worker-decomposition-data-integrity-design.md`, `2026-08-15-universe-symbol-manager-design.md` |
| `shares-float-all` | free, 2026-07-23 | **200**, 87 sample symbols | as above |
| `shares-float-all` | Premium | **200** with a partial page — "NOT 402" | `InstrumentProfileRefreshRunnerTests.cs:151` |

## A correction: the "402 on Premium" evidence was misread

`FmpPlanRestrictedException`'s remarks, `BulkEndpoints`' class remarks, the README's "no tier map" paragraph,
and commit 61ba8d7 (2026-08-26) all say that the predecessor recorded **both** `profile-bulk` and
`shares-float-all` as 402 on Premium, and that both answered 200 when re-probed on 2026-08-26 — offered as
evidence that "entitlement moves".

Source C's own record, above, says otherwise on both counts:

1. `shares-float-all` was **never** recorded as 402 by the predecessor. It answered 200 with a partial page on
   the free key and on Premium, and the regression test says "NOT 402" in so many words.
2. `profile-bulk` was recorded as 402 on the **free** key, and again on a plan the specs do not name.
3. The 2026-08-26 re-probe was on this repository's key, which is Ultimate. A 402 on one plan and a 200 on a
   higher one is the ladder working as described, not entitlement moving.

So the cited evidence does not show gating changing over time. The decision it was cited for — carry no
runtime tier map, never cache a refusal — stands on its own reasoning: FMP restricts individual keys (403 for
abusing the bulk surface, per its own error text), plans get re-tiered, and a cached "unavailable" goes stale
silently. #45 leaves the decision alone and corrects the evidence, with dated notes beside the originals.

Source B's record for the same two paths, for what it is worth: `profile-bulk` needs Ultimate (402 on free,
Starter and Premium; 2026-08-24), which agrees with Source C's 402s; `shares-float-all` has no sentence at all.

## The ladder, mapped onto this SDK's groups

`fmpsdk`'s modules do not partition the API the way `FmpClient`'s properties do — its `sec_filings` carries
this SDK's `Directory.GetSicCodesAsync` and `Search.FindIndustryClassificationAsync`, its `insider_trades`
carries `InstitutionalOwnership.GetBeneficialOwnershipAsync`, its `earnings_transcript` carries
`Directory.GetTranscriptSymbolsAsync` — so the mapping is by path, not by module. A class whose members
all sit on one rung carries that rung in its note; a class whose members differ carries `mixed`, spells the
ladder out member by member, and the members off its *main rung* — the tier most of its members share — carry
notes of their own. That is the issue's shape: a member note only where a member departs from its group's floor.

| group | class note | main rung | members carrying their own note |
|---|---|---|---|
| Analyst | no floor on record | — | — |
| Bulk | Ultimate | Ultimate | — |
| Calendar | mixed | no record (six) | `GetIpoCalendarAsync`, `GetIpoDisclosuresAsync`, `GetIpoProspectusesAsync` — Starter |
| Chart | mixed | Free (four end-of-day) | `GetIntradayAsync` — Starter |
| Company | mixed | no record (fourteen) | `GetLatestMergersAcquisitionsAsync` — Starter; `SearchMergersAcquisitionsAsync`, `GetExecutiveCompensationBenchmarkAsync` — Premium |
| Congress | mixed | Starter (six filtered trade methods) | `GetHouseLatestAsync`, `GetSenateLatestAsync` — Free; `GetProfilesAsync`, `GetPositionsAsync`, `GetNetWorthAsync`, `GetNetWorthSummaryAsync` — Premium |
| Cot | Premium | Premium | — |
| Directory | mixed | Starter (ten `directory` paths plus `all-industry-classification`) | `GetIndexListAsync`, `GetSicCodeAsync`, `GetSicCodesAsync`, `SearchSicCodesAsync` — Free; `GetTranscriptSymbolsAsync` — Ultimate; the three asset-class lists have no record and no note |
| DiscountedCashFlow | no floor on record | — | — |
| Economics | mixed | no record (three) | `GetEconomicCalendarAsync` — Starter |
| Esg | Ultimate | Ultimate | — |
| EtfAndFunds | mixed | Ultimate (`etf/holdings`, `etf/asset-exposure`, four `funds/*`) | `GetEtfInfoAsync`, `GetEtfCountryWeightingsAsync`, `GetEtfSectorWeightingsAsync` — Starter |
| Fundraisers | no floor on record | — | — |
| Indexes | Premium | Premium | — |
| InsiderTrades | mixed | Starter (`search`, `reporting-name`, `statistics`) | `GetLatestAsync`, `GetTransactionTypesAsync` — Free |
| InstitutionalOwnership | mixed | Ultimate (eight `institutional-ownership/*`) | `GetBeneficialOwnershipAsync` — Starter |
| MarketHours | no floor on record | — | — |
| MarketPerformance | no floor on record | — | — |
| News | mixed | Starter (`general-latest` and the six stock/crypto/forex) | `GetArticlesAsync` — Free; `SearchPressReleasesAsync`, `GetPressReleasesLatestAsync` — Premium |
| Quote | mixed | Ultimate (exchange batch and six asset-class batches: fourteen methods over seven paths) | the five single-symbol methods — Free; `GetQuotesAsync`, `GetShortQuotesAsync`, `GetAftermarketQuotesAsync`, `GetAftermarketTradesAsync` — Starter |
| Search | mixed | Starter (CUSIP, ISIN, exchange variants, screener, industry classification) | `FindBySymbolAsync`, `FindByNameAsync`, `FindByCikAsync` — Free |
| SecFilings | Free | Free | — |
| Statements | mixed | no record (twenty-three) | `GetIncomeStatementTtmAsync`, `GetBalanceSheetTtmAsync`, `GetCashFlowTtmAsync`, `GetLatestStatementsAsync`, `StreamLatestStatementsAsync` — Ultimate |
| TechnicalIndicators | Starter | Starter | — |
| Transcripts | Ultimate | Ultimate | — |

Twenty-five class notes and forty-five member notes; `grep -rn "Plan tier —" src/FmpDotNet/Endpoints` lists them.

Two placements worth a second look, because they are easy to misread as errors: `earnings-transcript-list`
lives under `Directory` here and under `earnings_transcript` in Source B, which puts a single Ultimate-gated
path among Starter listings; and `acquisition-of-beneficial-ownership` lives under `InstitutionalOwnership`
here and under `insider_trades` there, which puts a single Starter path among eight Ultimate ones. Both are
faithful to the source.

The seven `tipranks-*` paths are not in the table because they are not modelled (#41): Source B recorded
402 on every one of them even on Ultimate on 2026-08-24, naming a separately purchased add-on.

## The fixed phrase

Every note opens with one of:

- `Plan tier — Free, second-hand.` / `Starter` / `Premium` / `Ultimate` — a floor Source B recorded and this
  repo could not verify;
- `Plan tier — mixed, second-hand.` — a class whose members sit on more than one rung; the paragraph lists
  them and the members above the floor carry their own notes;
- `Plan tier — no floor on record.` — every path answered 200 on Ultimate here and Source B recorded nothing
  below it.

`EndpointCoverageTests.Every_endpoint_group_records_the_plan_tier_it_was_observed_on` holds every class to
carrying one, read from the shipped `FmpDotNet.xml` rather than the sources because the XML is what
IntelliSense shows; `Every_plan_tier_note_uses_the_fixed_vocabulary` holds every note, class or member, to
finishing the way it starts, so "second-hand" cannot quietly be dropped from a claim this repo never made.

## Appendix — Source B's tier sentences, verbatim

Extracted 2026-09-02 from the module and method docstrings of `fmpsdk` 20260824.0 by parsing the sources;
`—` is a method whose docstring has no tier sentence. Paths are as the docstrings name them.

### `analyst.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `analyst-estimates` | — |
| `ratings-snapshot` | — |
| `ratings-historical` | — |
| `price-target-summary` | — |
| `price-target-consensus` | — |
| `grades` | — |
| `grades-historical` | — |
| `grades-consensus` | — |

### `bulk.py`

Module docstring: Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `profile-bulk` | — |
| `rating-bulk` | — |
| `dcf-bulk` | — |
| `scores-bulk` | — |
| `price-target-summary-bulk` | — |
| `etf-holder-bulk` | — |
| `upgrades-downgrades-consensus-bulk` | — |
| `key-metrics-ttm-bulk` | — |
| `ratios-ttm-bulk` | — |
| `peers-bulk` | — |
| `earnings-surprises-bulk` | — |
| `income-statement-bulk` | — |
| `income-statement-growth-bulk` | — |
| `balance-sheet-statement-bulk` | — |
| `balance-sheet-statement-growth-bulk` | — |
| `cash-flow-statement-bulk` | — |
| `cash-flow-statement-growth-bulk` | — |
| `eod-bulk` | — |

### `calendar.py`

Module docstring: The 3 `ipos_*` methods require an FMP Starter-tier plan or higher — all 3 402 on the free tier.

| path | method docstring |
|---|---|
| `dividends` | — |
| `dividends-calendar` | — |
| `earnings` | — |
| `earnings-calendar` | — |
| `ipos-calendar` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `ipos-disclosure` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `ipos-prospectus` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `splits` | — |
| `splits-calendar` | — |

### `chart.py`

Module docstring: `historical_chart` requires an FMP Starter-tier plan or higher — 402s on the free tier; the 4 `historical_price_eod_*` methods all work on the free tier.

| path | method docstring |
|---|---|
| `historical-chart/{timeframe}` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `historical-price-eod/light` | — |
| `historical-price-eod/full` | — |
| `historical-price-eod/non-split-adjusted` | — |
| `historical-price-eod/dividend-adjusted` | — |

### `commitment_of_traders.py`

Module docstring: Requires an FMP Premium-tier plan or higher — 402s on the free and Starter tiers (confirmed working on Premium 2026-08-23).

| path | method docstring |
|---|---|
| `commitment-of-traders-report` | — |
| `commitment-of-traders-analysis` | — |
| `commitment-of-traders-list` | — |

### `commodity.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `commodities-list` | — |

### `company.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `profile` | — |
| `profile-cik` | — |
| `company-notes` | — |
| `stock-peers` | — |
| `delisted-companies` | — |
| `employee-count` | — |
| `historical-employee-count` | — |
| `market-capitalization` | — |
| `market-capitalization-batch` | — |
| `historical-market-capitalization` | — |
| `shares-float` | — |
| `shares-float-all` | — |
| `mergers-acquisitions-latest` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `mergers-acquisitions-search` | Requires an FMP Premium-tier plan or higher (402 on free and Starter; confirmed working on Premium 2026-08-23). |
| `key-executives` | — |
| `governance-executive-compensation` | — |
| `executive-compensation-benchmark` | Requires an FMP Premium-tier plan or higher (402 on free and Starter; confirmed working on Premium 2026-08-23). |

### `congress.py`

Module docstring: `house_latest` and `senate_latest` (the two parameterless listings) work on the free tier. The 6 symbol/id/name-scoped trade lookups (`house_trades`, `house_trades_by_id`, `house_trades_by_name`, `senate_trades`, `senate_trades_by_id`, `senate_trades_by_name`) require an FMP Starter-tier plan or higher. `senate_profile`, `senate_positions`, `senate_net_worth`, and `senate_net_worth_aggregated` require an FMP Premium-tier plan or higher (402 on free and Starter; confirmed working on Premium 2026-08-23).

| path | method docstring |
|---|---|
| `house-latest` | — |
| `house-trades` | — |
| `house-trades-by-id` | — |
| `house-trades-by-name` | — |
| `senate-latest` | — |
| `senate-trades` | — |
| `senate-trades-by-id` | — |
| `senate-trades-by-name` | — |
| `senate-profile` | — |
| `senate-positions` | — |
| `senate-net-worth` | — |
| `senate-net-worth-aggregated` | — |

### `crypto.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `cryptocurrency-list` | — |

### `dcf.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `discounted-cash-flow` | — |
| `levered-discounted-cash-flow` | — |
| `custom-discounted-cash-flow` | — |
| `custom-levered-discounted-cash-flow` | — |

### `directory.py`

Module docstring: Requires an FMP Starter-tier plan or higher — every method here 402s on the free tier.

| path | method docstring |
|---|---|
| `stock-list` | — |
| `financial-statement-symbol-list` | — |
| `cik-list` | — |
| `symbol-change` | — |
| `etf-list` | — |
| `actively-trading-list` | — |
| `available-exchanges` | — |
| `available-sectors` | — |
| `available-industries` | — |
| `available-countries` | — |

### `earnings_transcript.py`

Module docstring: Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `earning-call-transcript` | — |
| `earning-call-transcript-dates` | — |
| `earning-call-transcript-latest` | — |
| `earnings-transcript-list` | — |

### `economics.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `treasury-rates` | — |
| `economic-indicators` | — |
| `economic-calendar` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |
| `market-risk-premium` | — |

### `esg.py`

Module docstring: Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `esg-disclosures` | — |
| `esg-ratings` | — |
| `esg-benchmark` | — |

### `forex.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `forex-list` | — |

### `fundraisers.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `crowdfunding-offerings` | — |
| `crowdfunding-offerings-latest` | — |
| `crowdfunding-offerings-search` | — |
| `fundraising` | — |
| `fundraising-latest` | — |
| `fundraising-search` | — |

### `funds.py`

Module docstring: `etf_info`, `etf_country_weightings`, and `etf_sector_weightings` require an FMP Starter-tier plan or higher. The other 6 (`etf_holdings`, `etf_asset_exposure`, and all 4 `funds_disclosure*` methods) require an FMP Ultimate-tier plan (402 on free, Starter, and Premium; confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `etf/holdings` | — |
| `etf/info` | — |
| `etf/country-weightings` | — |
| `etf/asset-exposure` | — |
| `etf/sector-weightings` | — |
| `funds/disclosure` | — |
| `funds/disclosure-dates` | — |
| `funds/disclosure-holders-latest` | — |
| `funds/disclosure-holders-search` | — |

### `indexes.py`

Module docstring: Only `index_list` works on the free tier — the 3 constituent-list methods and their 3 `historical_*` counterparts all require an FMP Premium-tier plan or higher (402 on free and Starter; confirmed working on Premium 2026-08-23).

| path | method docstring |
|---|---|
| `index-list` | — |
| `sp500-constituent` | — |
| `nasdaq-constituent` | — |
| `dowjones-constituent` | — |
| `historical-sp500-constituent` | — |
| `historical-nasdaq-constituent` | — |
| `historical-dowjones-constituent` | — |

### `insider_trades.py`

Module docstring: Only `insider_trading_latest` and `insider_trading_transaction_type` work on the free tier — the other 4 require an FMP Starter-tier plan or higher.

| path | method docstring |
|---|---|
| `insider-trading/latest` | — |
| `insider-trading/search` | — |
| `insider-trading/reporting-name` | — |
| `insider-trading-transaction-type` | — |
| `insider-trading/statistics` | — |
| `acquisition-of-beneficial-ownership` | — |

### `institutional_ownership.py`

Module docstring: Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `institutional-ownership/latest` | — |
| `institutional-ownership/extract` | — |
| `institutional-ownership/dates` | — |
| `institutional-ownership/extract-analytics/holder` | — |
| `institutional-ownership/holder-performance-summary` | — |
| `institutional-ownership/holder-industry-breakdown` | — |
| `institutional-ownership/symbol-positions-summary` | — |
| `institutional-ownership/industry-summary` | — |

### `market_hours.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `exchange-market-hours` | — |
| `all-exchange-market-hours` | — |
| `holidays-by-exchange` | — |

### `market_performance.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `biggest-gainers` | — |
| `biggest-losers` | — |
| `most-actives` | — |
| `sector-performance-snapshot` | — |
| `industry-performance-snapshot` | — |
| `historical-sector-performance` | — |
| `historical-industry-performance` | — |
| `sector-pe-snapshot` | — |
| `industry-pe-snapshot` | — |
| `historical-sector-pe` | — |
| `historical-industry-pe` | — |

### `news.py`

Module docstring: `fmp_articles` works on the free tier. `news_general_latest` and the `news_stock`/`news_crypto`/`news_forex` family (with their "-latest" siblings) — 7 methods — require an FMP Starter-tier plan or higher. `news_press_releases` and `news_press_releases_latest` require an FMP Premium-tier plan or higher (402 on free and Starter; confirmed working on Premium 2026-08-23).

| path | method docstring |
|---|---|
| `fmp-articles` | — |
| `news/general-latest` | — |
| `news/press-releases` | — |
| `news/press-releases-latest` | — |
| `news/stock` | — |
| `news/stock-latest` | — |
| `news/crypto` | — |
| `news/crypto-latest` | — |
| `news/forex` | — |
| `news/forex-latest` | — |

### `quote.py`

Module docstring: The 5 single-symbol methods (`quote`, `quote_short`, `aftermarket_quote`, `aftermarket_trade`, `stock_price_change`) work on the free tier. `batch_quote`, `batch_quote_short`, `batch_aftermarket_quote`, and `batch_aftermarket_trade` require an FMP Starter-tier plan or higher. The remaining 7 `batch_*` methods (`batch_exchange_quote` and the 6 whole-asset-class ones — `batch_etf_quotes`, `batch_mutualfund_quotes`, `batch_commodity_quotes`, `batch_crypto_quotes`, `batch_forex_quotes`, `batch_index_quotes`) require an FMP Ultimate-tier plan (402 on free, Starter, and Premium; confirmed working on Ultimate 2026-08-24).

| path | method docstring |
|---|---|
| `quote` | — |
| `quote-short` | — |
| `aftermarket-quote` | — |
| `aftermarket-trade` | — |
| `stock-price-change` | — |
| `batch-quote` | — |
| `batch-quote-short` | — |
| `batch-aftermarket-quote` | — |
| `batch-aftermarket-trade` | — |
| `batch-exchange-quote` | — |
| `batch-etf-quotes` | — |
| `batch-mutualfund-quotes` | — |
| `batch-commodity-quotes` | — |
| `batch-crypto-quotes` | — |
| `batch-forex-quotes` | — |
| `batch-index-quotes` | — |

### `search.py`

Module docstring: `search_symbol`, `search_name`, and `search_cik` work on the free tier; `search_cusip`, `search_isin`, `search_exchange_variants`, and `company_screener` require an FMP Starter-tier plan or higher.

| path | method docstring |
|---|---|
| `search-symbol` | — |
| `search-name` | — |
| `search-cik` | — |
| `search-cusip` | — |
| `search-isin` | — |
| `search-exchange-variants` | — |
| `company-screener` | Requires an FMP Starter-tier plan or higher — 402s on the free tier. |

### `sec_filings.py`

Module docstring: Only `industry_classification_search` and `all_industry_classification` require an FMP Starter-tier plan or higher — the other 10 work on the free tier.

| path | method docstring |
|---|---|
| `sec-filings-8k` | — |
| `sec-filings-financials` | — |
| `sec-filings-search/form-type` | — |
| `sec-filings-search/symbol` | — |
| `sec-filings-search/cik` | — |
| `sec-filings-company-search/name` | — |
| `sec-filings-company-search/symbol` | — |
| `sec-filings-company-search/cik` | — |
| `sec-profile` | — |
| `standard-industrial-classification-list` | — |
| `industry-classification-search` | — |
| `all-industry-classification` | — |

### `statements.py`

Module docstring: *no tier sentence*

| path | method docstring |
|---|---|
| `income-statement` | — |
| `income-statement-ttm` | Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24). |
| `income-statement-as-reported` | — |
| `income-statement-growth` | — |
| `balance-sheet-statement` | — |
| `balance-sheet-statement-ttm` | Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24). |
| `balance-sheet-statement-as-reported` | — |
| `balance-sheet-statement-growth` | — |
| `cash-flow-statement` | — |
| `cash-flow-statement-ttm` | Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24). |
| `cash-flow-statement-as-reported` | — |
| `cash-flow-statement-growth` | — |
| `financial-statement-full-as-reported` | — |
| `latest-financial-statements` | Requires an FMP Ultimate-tier plan — 402s on free, Starter, and Premium (confirmed working on Ultimate 2026-08-24). |
| `key-metrics` | — |
| `key-metrics-ttm` | — |
| `ratios` | — |
| `ratios-ttm` | — |
| `financial-scores` | — |
| `owner-earnings` | — |
| `enterprise-values` | — |
| `financial-growth` | — |
| `financial-reports-dates` | — |
| `financial-reports-json` | — |
| `financial-reports-xlsx` | — |
| `revenue-product-segmentation` | — |
| `revenue-geographic-segmentation` | — |

### `technical_indicators.py`

Module docstring: Requires an FMP Starter-tier plan or higher — every method here 402s on the free tier.

| path | method docstring |
|---|---|
| `technical-indicators/sma` | — |
| `technical-indicators/ema` | — |
| `technical-indicators/wma` | — |
| `technical-indicators/dema` | — |
| `technical-indicators/tema` | — |
| `technical-indicators/rsi` | — |
| `technical-indicators/standarddeviation` | — |
| `technical-indicators/williams` | — |
| `technical-indicators/adx` | — |

### `tipranks.py`

Module docstring: **Not gated by plan tier at all** — confirmed 2026-08-24 that every method still 402s even on FMP's top Ultimate tier. FMP's own error message names the real requirement: a separate paid add-on ("TipRanks data boost"), purchased independently of the Free/Starter/Premium/Ultimate ladder, via the dashboard's Add-ons tab.

| path | method docstring |
|---|---|
| `tipranks-search` | — |
| `tipranks-pit-symbol` | — |
| `tipranks-pit-analyst` | — |
| `tipranks-symbol-summary` | — |
| `tipranks-analyst-summary` | — |
| `tipranks-firm-summary` | — |
| `tipranks-analysts` | — |

