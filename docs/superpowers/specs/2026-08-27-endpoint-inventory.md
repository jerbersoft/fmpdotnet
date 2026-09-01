# FMP endpoint inventory — enumerated 2026-08-27

The denominator every coverage claim in this repo divides by, and the first time it has had evidence.
`EndpointCoverageTests.DocumentedPaths` was `230` from the project's start, backed by a prose comment and
nothing else. It is **243**.

## Provenance, and why two sources

**Source A — FMP's own documentation.** `site.financialmodelingprep.com/developer/docs` printed to PDF on
2026-08-27 (213 pages), text-extracted, every `/stable/<path>` collected. The docs site answered **HTTP 403 to
automated fetching**, so a print-out was the only way to read it mechanically; there is no published OpenAPI spec.

> **The 403 was User-Agent filtering, not a block on automation — corrected 2026-09-01 (#55).** See *Source C*
> below. The PDF route is kept in this section because it is what produced the original count and because the
> TipRanks trap under *Method* is specific to it, but nobody needs to print anything again.

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

**Re-verified 2026-08-31**, path-by-path against Source B rather than by section count, and against the table
`EndpointCoverageTests` generates from the code. The two sets differ on exactly 14 paths, and every one is
accounted for: Source B carries the 7 `tipranks-*` paths this SDK defers (#41) plus its single parameterised
`historical-chart/{timeframe}`; this SDK carries the 6 concrete intraday paths that one collapses. 7 − 5 = 2,
which is the whole of the 238-methods-there against 236-here gap, and it is a counting convention rather than
a coverage difference. **No path is modelled by Source B and missing here, and none is modelled here and
missing there.** The 243 denominator is unchanged.

A parameter-level diff over the 230 shared paths was run at the same time and does *not* reconcile: Source B
sends a documented query parameter this SDK never sends on 26 of them, with no path where the reverse holds.
That is a real gap in this client and is tracked in #46 — note that Source B validates nothing it sends, so
some fraction of those parameters will turn out to be accepted-and-ignored by FMP, which #46 treats as a
finding to record rather than a dead end.

**Source C — FMP's machine-readable documentation, found 2026-09-01 (#55).**
`https://site.financialmodelingprep.com/api-docs.md` answers **HTTP 200** with **635 KB** of
`text/markdown; charset=UTF-8` given an ordinary browser `User-Agent`. The 403 recorded above is User-Agent
filtering; it is not a block on automation, and the PDF route was never necessary. The file is structured per
endpoint — an `**Endpoint:**` line carrying the URL, a `**Parameters**` table marking required arguments with
`*`, and an `**Example Response**` JSON block — which makes it a source for the parameter surface and the
response shape, not only for the path list. See *Method* for the parse.

**It confirms 243 independently.** The file carries **278 endpoint entries** over **245 distinct URLs**; 2 of
those are `wss://` socket addresses rather than `/stable/` paths, leaving **243**. The 33-entry surplus is
re-documentation, not extra endpoints: **12 paths** appear under more than one section — `quote`, `quote-short`,
`historical-price-eod/light`, `historical-price-eod/full`, `historical-chart/1min`, `/5min` and `/1hour` five
times each, and `batch-{commodity,crypto,forex,index}-quotes` and `earnings-transcript-list` twice each. That is
(7 × 4) + (5 × 1) = 33, and it is the same asset-class re-filing Source A recorded. **The denominator is
unchanged, now on three independent sources.**

One caution learned immediately. Where a path is documented more than once, the entries can list **different**
parameters: `historical-chart/1min`, `/5min` and `/1hour` carry `extended` and `nonadjusted` under *Chart* and
neither under *Indexes*, *Commodity*, *Crypto* or *Forex*. A parse that keeps the first entry per path silently
takes whichever section came first — and for these three, four of the five entries are the short list. Union the
parameter lists across duplicate entries; do not take one.

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

## Where the remaining work is tracked

#25 is the tracking epic. The remainder was split into these on 2026-08-27, sized from the table above — every
child carries the exact path list for its group, so nobody re-derives it.

| issue | group | paths |
|---|---|---|
| #28 | Statements | 19 |
| #29 | Company | 13 |
| #36 | Form 13F and Insider Trades | 14 |
| #37 | Analyst and Calendar | 14 |
| #30 | SEC Filings | 12 |
| #31 | Senate and House trading | 12 |
| #40 | Economics, Earnings Transcripts, ESG and COT | 12 |
| #32 | Market Performance | 11 |
| #33 | News | 10 |
| #39 | Fundraisers and DCF | 10 |
| #34 | ETF and Mutual Funds | 9 |
| #35 | Technical Indicators | 9 |
| #38 | Indexes and Market Hours | 9 |
| | **actionable subtotal** | **154** |
| #41 | TipRanks — blocked on a paid add-on | 7 |
| | **total** | **161** |

#28 and #29 carry `tier: 3 adjacent` rather than `tier: 4 later`: they are the two largest groups left and the
two a trading consumer needs, so they are the natural next slices rather than long-tail work.

## Method, repeatable

**Current — Source C.** No print-out, no PDF library, no credential anywhere near it:

```
curl -sS -H 'User-Agent: Mozilla/5.0' \
  https://site.financialmodelingprep.com/api-docs.md -o api-docs.md

python3 - <<'EOF'
import re, collections
recs, section, title = [], None, None
for line in open('api-docs.md', encoding='utf-8'):
    line = line.rstrip('\n')
    if line.startswith('# '):    section = line[2:].strip()
    elif line.startswith('### '): title = line[4:].strip()
    elif line.startswith('**Endpoint:**'):
        url = re.search(r'`([^`]+)`', line).group(1)
        if '/stable/' not in url: continue          # the two wss:// socket entries
        recs.append((url.split('/stable/', 1)[1].split('?')[0], section, title))
paths = collections.Counter(p for p, _, _ in recs)
print(len(recs), 'entries', len(paths), 'paths')    # 276 entries, 243 paths
EOF
```

Read the `**Parameters**` table and `**Example Response**` block under each `**Endpoint:**` line the same way to
get the parameter surface and the response shape. **Union parameters across duplicate entries** — see the caution
under *Source C*.

**Original — Source A**, kept because it produced the first count and because the trap below is specific to it:

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
