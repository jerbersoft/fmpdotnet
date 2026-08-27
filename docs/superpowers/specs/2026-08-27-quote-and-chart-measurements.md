# Quote & Chart — measured 2026-08-27, Premium key

## Coverage
All 26 paths answered 200 on this plan. Counts match issue #24 exactly: Quote 16, Chart 10.
`historical-chart/1day`, `historical-chart/2hour`, bare `historical-price-eod` -> HTTP 404 `[]`.
So the interval set is exactly {1min,5min,15min,30min,1hour,4hour} and EOD has no bare path.

## Quote: 16 endpoints -> 5 shapes
FULL (17): symbol,name,price,changePercentage,change,volume,dayLow,dayHigh,yearHigh,yearLow,
           marketCap,priceAvg50,priceAvg200,exchange,open,previousClose,timestamp
  quote(symbol), batch-quote(symbols), + all 8 batch-* with short=false
SHORT (4): symbol,price,change,volume
  quote-short, batch-quote-short, batch-exchange-quote(exchange),
  batch-{mutualfund,etf,commodity,crypto,forex,index}-quotes
AFTERMARKET TRADE (4): symbol,price,tradeSize,timestamp   -> aftermarket-trade, batch-aftermarket-trade
AFTERMARKET QUOTE (7): symbol,bidSize,bidPrice,askSize,askPrice,volume,timestamp
                                                          -> aftermarket-quote, batch-aftermarket-quote
PRICE CHANGE (12): symbol,1D,5D,1M,3M,6M,ytd,1Y,3Y,5Y,10Y,max -> stock-price-change

## `short` flips the shape on 8 endpoints
short=false turns the 4-field row into the 17-field row on batch-exchange-quote and all six
asset-class batch-*-quotes. Payload cost measured:
  batch-etf-quotes     short 1,345,381 B  ->  full 6,629,855 B   (4.9x, 14,537 rows)
  batch-crypto-quotes  short   486,693 B  ->  full 2,200,708 B   (4.5x,  4,778 rows)

## `timestamp` is seconds on one endpoint and milliseconds on its sibling
  quote.timestamp            1787774400     seconds -> 2026-08-26 20:00:00Z = 16:00 ET (close)
  aftermarket-*.timestamp    1787819647000  millis  -> 2026-08-27 08:34:07Z = 04:34 ET (pre-market)
Same field name, same group. A shared converter is wrong by 1000x.

## Chart: 10 endpoints -> 4 shapes
LIGHT (4):    symbol,date,price,volume
FULL (10):    symbol,date,open,high,low,close,volume,change,changePercent,vwap
ADJUSTED (7): symbol,date,adjOpen,adjHigh,adjLow,adjClose,volume
              -- shared by non-split-adjusted AND dividend-adjusted, different meanings
INTRADAY (6): date,open,low,high,close,volume   -- NO symbol; low before high

## `non-split-adjusted` returns UNADJUSTED prices under adjOpen/adjHigh/adjLow/adjClose
AAPL 2020-08-28, the session before the 4:1 split effective 2020-08-31:
  non-split-adjusted  adjOpen 504.04  adjClose 499.24  volume  46,907,500   <- as traded
  full                    open 126.01     close 124.81  volume 187,630,000  <- split-adj only
  dividend-adjusted   adjOpen 122.12  adjClose 120.96  volume 187,630,000   <- split + dividend
499.24 = 4 x 124.81 exactly; 187,630,000 = 4 x 46,907,500 exactly.
So "non-split-adjusted" parses as non-(split-adjusted), i.e. raw. The adj* field names are a lie
on that endpoint, and the two endpoints are shape-identical while differing 4x in value.

## EOD truncation: hard cap 5000 rows, drops the OLDEST end, silently
  asked 2025-08-26..2026-08-26   ->  252 rows, full range honoured
  asked 2021-08-26..2026-08-26   -> 1255 rows, full range honoured
  asked 2006-08-26..2026-08-26   -> 5000 rows, got 2006-10-10.. (from silently moved)
  asked 1980-01-01..2026-08-26   -> 5000 rows, got 2006-10-10.. (identical answer)
  no from/to at all              -> 1253 rows, 2021-08-30..  (~5 years, NOT everything)
`to` is always honoured. Unlike economic-calendar there IS a hard constant, so a row-count
check is meaningful here; the honest check is still "did the oldest row reach my `from`".

## Intraday truncation: a per-interval LOOKBACK window, not a row cap
Asked 2020-01-01..2026-08-26 for each; measured oldest bar returned:
  1min   2026-08-24    3 calendar days   1169 rows
  5min   2026-08-17   10 calendar days    624 rows
  15min  2026-07-13   45 calendar days    858 rows
  30min  2026-07-28   30 calendar days    286 rows
  1hour  2026-05-29   90 calendar days    434 rows
  4hour  2026-03-02  180 calendar days    247 rows
15min (45d) is WIDER than 30min (30d) -- non-monotonic, recorded as measured, not explained.
Default with no from/to is narrower still: 1min gives 2 sessions, not 3.

## A backwards range fails differently on the two families
  historical-price-eod/light  from=2026-08-26 to=2026-08-24 -> 200 []
  historical-chart/1min       from=2026-08-26 to=2026-08-24 -> 200, 390 rows for 2026-08-24
The intraday form returns plausible, wrongly-dated data rather than nothing.

## Other measured behaviour
- Intraday `date` is Eastern wall clock "uuuu-MM-dd HH:mm:ss", bars labelled by OPEN
  (09:30 first, 15:59 last for 1min; 15:30 last for 1hour -> final bar is partial).
- Session-open bar inclusion varies: from=to=2026-08-25 -> 389 rows starting 09:31;
  a 3-day range -> 390 rows starting 09:30.
- Unknown symbol -> 200 [] on every endpoint measured (no 404).
- batch-quote silently DROPS unknown symbols (AAPL,NOSUCHTICKER -> 1 row) and echoes
  duplicates back (120 symbols with repeats -> 120 rows). No cap hit at 120.
- `quote` and `historical-price-eod/*` reject comma-separated symbols -> 200 [].
- batch-exchange-quote with no exchange -> HTTP 400 "Query Error: Invalid or missing query
  parameter - exchange"; unknown exchange -> 200 [].
- Rows are newest-first on both chart families.
- Spelling differs for the same concept: quote.changePercentage vs EOD full.changePercent.
