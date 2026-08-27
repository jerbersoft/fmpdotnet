# FMP endpoint inventory — enumerated 2026-08-27

The denominator every coverage claim in this repo divides by, and the first time it has had evidence.
`EndpointCoverageTests.DocumentedPaths` was `230` from the project's start, backed by a prose comment and
nothing else. It is **243**.

## Provenance, and why two sources

**Source A — FMP's own documentation.** `site.financialmodelingprep.com/developer/docs` printed to PDF on
2026-08-27 (213 pages), text-extracted, every `/stable/<path>` collected. The docs site returns **HTTP 403 to
automated fetching**, so a print-out is the only way to read it mechanically; there is no published OpenAPI spec.

**Source B — an independent third-party client.** `fmpsdk-20260824.0`, a Python SDK published 2026-08-24,
read from its `endpoints/` modules. Neither source derives from the other.

**They reconcile exactly.** Source A gives 243 distinct paths, Source B gives 238. The difference is 5, and it is
entirely one convention: FMP documents six intraday chart paths (`historical-chart/1min`, `5min`, `15min`,
`30min`, `1hour`, `4hour`) that Source B collapses into a single parameterised `historical-chart/{timeframe}`.
6 − 1 = 5. This SDK follows the documented convention and models the six separately, so **243 is the right
denominator for this client**. One further difference is filing, not counting: `earnings-transcript-list` sits
under Directory here and under Earnings Transcript there — the same path either way.

**Validation.** All 82 currently-modelled paths appear in Source A with zero misses and no truncated fragments,
and 21 of 29 sections match Source B's counts exactly.

## One section is not buyable at any tier

The seven `tipranks-*` paths require a **separately-purchased add-on**, not a plan tier. Source B's maintainer
recorded on 2026-08-24 that every one of them returns 402 **even on FMP's top Ultimate tier**, with FMP's own
error naming the requirement: a "TipRanks data boost" add-on bought from the dashboard's Add-ons tab. They
implemented the seven methods against documented examples but were never able to verify a single real response.

So the honest remainder is **154 actionable paths, not 161** — the other seven cannot be built or tested without
buying the add-on first.

## The inventory

| section | documented | modelled | remaining |
|---|---|---|---|
| Statements | 27 | 8 | 19 |
| Company | 17 | 4 | 13 |
| SEC Filings | 12 | 0 | 12 |
| Senate | 12 | 0 | 12 |
| Market Performance | 11 | 0 | 11 |
| News | 10 | 0 | 10 |
| ETF & Mutual Funds | 9 | 0 | 9 |
| Technical Indicators | 9 | 0 | 9 |
| Form 13F | 8 | 0 | 8 |
| Analyst | 8 | 1 | 7 |
| Calendar | 9 | 2 | 7 |
| TipRanks (paid add-on) | 7 | 0 | 7 |
| Fundraisers | 6 | 0 | 6 |
| Indexes | 7 | 1 | 6 |
| Insider Trades | 6 | 0 | 6 |
| Discounted Cash Flow | 4 | 0 | 4 |
| Commitment of Traders | 3 | 0 | 3 |
| ESG | 3 | 0 | 3 |
| Earnings Transcript | 3 | 0 | 3 |
| Economics | 4 | 1 | 3 |
| Market Hours | 3 | 0 | 3 |
| Bulk | 18 | 18 | 0 |
| Chart | 10 | 10 | 0 |
| Commodity | 1 | 1 | 0 |
| Crypto | 1 | 1 | 0 |
| Directory | 11 | 11 | 0 |
| Forex | 1 | 1 | 0 |
| Quote | 16 | 16 | 0 |
| Search | 7 | 7 | 0 |
| **total** | **243** | **82** | **161** |

Complete: `Bulk`, `Chart`, `Commodity`, `Crypto`, `Directory`, `Forex`, `Quote`, `Search`.

## Equity depth versus asset-class breadth

**This section is a classification, not a measurement** — the counts above are measured, the assignment of a
section to "equity-only" is judgement, and it is written out row by row so it can be argued with.

Equity-only — the data exists only for listed companies:

| section | remaining |
|---|---|
| Statements | 19 |
| Company | 13 |
| SEC Filings | 12 |
| Senate | 12 |
| Market Performance | 11 |
| Form 13F | 8 |
| Analyst | 7 |
| Calendar | 7 |
| Fundraisers | 6 |
| Insider Trades | 6 |
| Discounted Cash Flow | 4 |
| ESG | 3 |
| Earnings Transcript | 3 |
| **subtotal** | **111** |

Shared, or belonging to another asset class:

| section | remaining |
|---|---|
| News | 10 |
| ETF & Mutual Funds | 9 |
| Technical Indicators | 9 |
| Indexes | 6 |
| Commitment of Traders | 3 |
| Economics | 3 |
| Market Hours | 3 |
| **subtotal** | **43** |

111 + 43 = 154 actionable.
`Market Performance` is the row most open to dispute: sector and industry performance and the movers lists are
equity constructs, but they are market-wide rather than per-company. It is counted as equity here.

The structural point survives whichever way that row falls. What this SDK has built so far is price plumbing —
Quote 16, Chart 10, Bulk 18, all complete — and one `GetQuoteAsync` serves equities, ETFs, indices, commodities,
forex and crypto alike, so **asset-class breadth came free while equity depth never got built**. Commodity, Forex
and Crypto are one path each and all three are done.

## Method, repeatable

```
python3 - <<'EOF'
import pypdf, re
r = pypdf.PdfReader('<docs printed to PDF>')
t = '\n'.join(p.extract_text() or '' for p in r.pages)
paths = sorted(set(re.findall(r'/stable/([A-Za-z0-9v/_-]+)', t)))
EOF
```

Sections are bare heading lines in the extracted text, so paths are assigned to the nearest preceding heading.
**`TipRanks` is not one of those headings** — it appears only inline — so its seven paths silently attach to
whatever section precedes them (`Bulk`) unless corrected. That is the trap in re-running this, and it is exactly
the error Source B caught: `Bulk` is 18 and complete, not 25 with 7 outstanding.

⚠️ **The printed PDF contains the reader's live API key**, because FMP renders it inline in the authorization
examples. Redact before sharing, and treat any such print-out as a credential.
