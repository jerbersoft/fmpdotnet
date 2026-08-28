# SEC Filings — measurements

Every fact the [design](2026-08-28-sec-filings-design.md) rests on, with the date it was measured. Measured
against the live API on **2026-08-28** across seven probe passes, roughly 90 calls, all ordinary JSON endpoints.
No `*-bulk` path was touched.

## Entitlement — all twelve are reachable

No path returned 402. Five answered 200 with no parameters at all; the other seven returned 400 naming the
parameter they wanted, which is itself proof of reachability.

| path | bare call |
|---|---|
| `stable/all-industry-classification` | 200, 100 rows |
| `stable/sec-filings-8k` | 200, 100 rows |
| `stable/sec-filings-financials` | 200, 100 rows |
| `stable/sec-profile` | 200, 1 row — **defaults to AAPL** with no parameter |
| `stable/standard-industrial-classification-list` | 200, 444 rows |
| `stable/industry-classification-search` | 400 — "Please enter at least one search value: cik, sicCode, or symbol." |
| `stable/sec-filings-company-search/cik` | 400 — requires `cik` |
| `stable/sec-filings-company-search/name` | 400 — requires `company` |
| `stable/sec-filings-company-search/symbol` | 400 — requires `symbol` |
| `stable/sec-filings-search/cik` | 400 — requires `cik`, then `from`, then `to` |
| `stable/sec-filings-search/form-type` | 400 — requires `formType`, then `from`, then `to` |
| `stable/sec-filings-search/symbol` | 400 — requires `symbol`, then `from`, then `to` |

The search paths reveal their requirements one at a time: supplying `symbol` alone yields
"Invalid or missing query parameter - from", and supplying `symbol` and `from` yields the same message for `to`.
Both bounds are mandatory.

## Twelve paths, four shapes

**The seven-field company row**, served identically by five paths — `symbol`, `name`, `cik`, `sicCode`,
`industryTitle`, `businessAddress`, `phoneNumber`: `all-industry-classification`,
`industry-classification-search`, and all three `sec-filings-company-search/*`.

The five are the same data, not merely the same field names. For CIK `0000070858` (Bank of America),
`all-industry-classification` and `sec-filings-company-search/cik` returned byte-identical values for `symbol`,
`name`, `cik`, `sicCode`, `industryTitle` and `phoneNumber`. Only `businessAddress` differed, and only in
encoding — see below.

**The filing row.** `sec-filings-8k` and `sec-filings-financials` send eight fields: `symbol`, `cik`,
`filingDate`, `acceptedDate`, `formType`, `hasFinancials`, `link`, `finalLink`. The three `sec-filings-search/*`
paths send the same seven minus `hasFinancials`.

**`sec-profile`** sends 35 fields and stands alone. **`standard-industrial-classification-list`** sends three —
`office`, `sicCode`, `industryTitle` — and stands alone.

## `sec-filings-8k` and `sec-filings-financials` differ by filter, not by shape

Measured over 1,000 rows each:

| | `sec-filings-8k` | `sec-filings-financials` |
|---|---|---|
| `formType` | `8-K` × 1000 | `8-K` × 861, `6-K` × 137, `10-K` × 2 |
| `hasFinancials` | null × 107, false × 725, true × 168 | **true × 1000** |

So `8k` filters by form and `financials` filters by the presence of financial data. On `financials` the
`hasFinancials` field is constant and therefore carries no information.

## `from` and `to` filter `acceptedDate`, not `filingDate`

This was measured as a hypothesis test rather than inferred. `sec-filings-financials` with
`from=2025-03-01&to=2025-03-05&limit=1000` returned **722 rows** — comfortably under the 1,000 cap, so
truncation cannot explain the result.

- 16 rows carried a `filingDate` later than the requested `to`.
- **16 of those 16** carried an `acceptedDate` inside the requested range.
- **Zero rows** in the whole response carried an `acceptedDate` outside it; the span was 2025-03-03 to 2025-03-05.
- Every one of the 16 was accepted after 19:00 ET — past EDGAR's cutoff, so EDGAR stamps the next business day.

The same overshoot appears on `sec-filings-search/form-type` (398 rows for 2025-03-01..03-05, 7 outside) and on
`sec-filings-8k` (21 outside). It is not truncation and not a rounding error: the filter is applied to a field
the caller does not filter on.

Corroborating evidence from an independent angle — the `acceptedDate` hour distribution over 1,000 8-K rows:

```
06:61  07:88  08:74  09:42  10:11  11:19  12:27  13:15  14:23  15:14
16:434 17:109 18:8   19:5   20:3   21:45  22:17  23:1   00:1   02:3
```

The 16:00 spike is the post-close filing surge; the 63 rows from 21:00 onward are exactly the population that
spills into the following `filingDate`.

One non-finding, recorded so it is not mistaken for a defect later: a `from` of 2025-03-01 yields a minimum
`filingDate` of 2025-03-03 because 1 March 2025 was a Saturday.

## Pagination behaves three different ways across the twelve

**`all-industry-classification` cannot be paginated, and the only complete-data path is an anomaly.**

| call | rows | bytes |
|---|---|---|
| `page=0&limit=5` | 5 | 1,439 |
| `page=0&limit=1000` | 1,000 | — |
| `page=0&limit=5000` | 1,000 | — |
| `page=0&limit=26000` | 1,000 | — |
| `page=0&limit=30000` | 1,000 | — |
| `page=0`, no limit | 100 | — |
| `page=1&limit=5` | **25,952** | 7,288,535 |
| `page=2&limit=5` | **25,952** | 7,288,535 |
| `page=1&limit=10` | **25,952** | 7,288,535 |
| `page=1`, no limit | **25,952** | 7,288,535 |

Page 0 honours `limit` but caps at 1,000. Any non-zero `page` returns the entire 25,952-row universe,
byte-identical across page numbers and ignoring `limit` entirely. Since the dataset is 25,952 rows and page 0
tops out at 1,000, rows 1,001 onward are reachable **only** through the anomaly.

**The filing feeds paginate properly.** `sec-filings-8k`, `sec-filings-financials` and `sec-filings-search/*` all
return distinct rows for `page=0` versus `page=1`, and all cap at 1,000 — `limit=5000` and `limit=2000` both
return exactly 1,000 without saying so.

**Two paths ignore `limit` silently.** `standard-industrial-classification-list` returned all 444 rows for every
combination of `page` and `limit` tried. `sec-filings-company-search/name` returned 52 rows for `company=Apple`
with and without `limit=5`.

## `businessAddress` arrives in two encodings for one shape

| path | bracketed values |
|---|---|
| `all-industry-classification` | **1000 of 1000** |
| `sec-filings-company-search/name` (`company=Bank`) | **0 of 976** |
| `sec-profile` | 0 — comma-joined, with a separate `mailingAddress` |

The bracketed form is a stringified Python list:

```
"['BANK OF AMERICA CORPORATE CENTER', 'CHARLOTTE NC 28255']"
```

and the sibling path sends the same address as:

```
"BANK OF AMERICA CORPORATE CENTER, CHARLOTTE NC 28255"
```

**The joined form is reproducible.** On five randomly sampled CIKs, `", ".join(parts)` of the bracketed value
matched the sibling path's string exactly, 5 of 5. FMP itself publishes the normalisation target.

**But the bracketed form is not reliably parseable.** Of 1,000 sampled values, 999 parse as a Python literal and
one does not:

```
"['NO. 65', 'LN', '114', 'XISHI RD.', 'XI'AN VIL.', 'TAICHUNG CITY  ']"   (AGCC, cik 0002060016)
```

`XI'AN` carries an unescaped apostrophe inside a single-quoted repr, which means the string was built by naive
formatting rather than by a serialiser. Any apostrophe in an address — Xi'an, O'Brien, L'Oréal — produces the
same broken output, so this is a systematic class rather than one bad row. A textual normalisation that strips
the brackets and splits on `', '` handles this row correctly where a real parse fails on it.

Element counts across the 1,000: one element × 1, two × 737, three × 229, four × 27, five × 5. Nineteen values
contain a comma or quote inside an element, which is why splitting the *joined* form back apart would be lossy —
the normalisation runs in one direction only.

## `sec-profile` sends almost everything as a string

Sampled across AAPL, TSM, SHEL, BRK-B, NVO and SPY — all six returned exactly one row.

- Every value is a JSON string except `isActive`, `isEtf`, `isAdr` and `isFund`, which are real booleans.
- `employees` is a string: `"166000"`. `AllowReadingFromString` is already set globally, so `int?` binds.
- `ipoDate` is a plain ISO date: `"1980-12-12"`.
- `fiscalYearEnd` is a **month and day with no year**: `"09-30"`.
- `fiftyTwoWeekRange` is a formatted string, not a pair of numbers: `"225.95 - 344.57"`.
- `securityType` was **null on all six symbols**.
- `sicCode`, `sicDescription`, `ceo` and `stateOfIncorporation` were each blank on one of the six.

The path accepts `symbol` or `cik` and answers identically for AAPL and CIK `0000320193`.

## `filingDate` is a date wearing a dummy time

Across **2,115 rows** sampled from `sec-filings-8k`, `sec-filings-financials` and `sec-filings-search/form-type`,
the time component of `filingDate` was `00:00:00` in **2,115 of 2,115** cases. `acceptedDate` was 19 characters
in all 2,115 — the `uuuu-MM-dd HH:mm:ss` form the existing Eastern converter already reads.

## `sec-filings-company-search/name` matches loosely, and short queries return nothing

`company=Apple`, `company=apple` and `company=Appl` each returned the same 52 rows, so matching is
case-insensitive and not an exact-name comparison. `company=a` returned **0 rows**, so very short queries are
rejected rather than matching broadly. The exact rule was not established and the SDK does not assert one.
