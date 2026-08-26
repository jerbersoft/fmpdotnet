using System.Text.Json.Serialization;

namespace FmpDotNet.Models;

/// <summary>Altman Z and Piotroski F for one symbol, with the figures they were computed from. From
/// <c>stable/financial-scores</c>.
///
/// <para>The endpoint answers a <b>single-element array</b> rather than an object, and it holds no history, so the
/// SDK surfaces it as one nullable record rather than a list. An unknown symbol answers <c>[]</c> with HTTP 200 —
/// "not found" is a shape here, not a status code, the same rule <c>stable/profile</c> and
/// <c>stable/shares-float</c> follow. So does a security the scores do not apply to: <c>SPY</c> measured
/// <c>[]</c> on 2026-08-26, because both scores are built from issuer accounts an ETF does not file. A caller
/// cannot tell "no such symbol" from "not applicable" by the response, only by knowing the security.</para>
///
/// <para><b>There is no date on this response, and no period and no fiscal year either.</b> The eleven properties
/// below are the entire payload, measured against the live API on 2026-08-26 — none missing and none extra. A
/// reader will look for a <c>date</c> to key or stage the row by and there is not one: this is a point-in-time
/// snapshot with nothing to say when it was computed or which fiscal period it covers. Whoever stores it has to
/// stamp it at fetch time, and two rows for the same symbol cannot be ordered by anything in the payload.</para>
///
/// <para>The inputs are also <b>not</b> the latest annual statement's figures, so do not try to reconcile them
/// against <c>stable/balance-sheet-statement</c> or <c>stable/income-statement</c>. Measured on AAPL, 2026-08-26,
/// against the FY2025 statements captured the same day: <see cref="TotalAssets"/> 383,266,000,000 here against
/// 359,241,000,000 there, <see cref="RetainedEarnings"/> +11,326,000,000 against −14,264,000,000,
/// <see cref="Revenue"/> 466,823,000,000 against 416,161,000,000, and <see cref="WorkingCapital"/> +492,000,000
/// against a fiscal-year-end figure of −17,674,000,000. The revenue running above the fiscal year's says trailing
/// twelve months rather than a fiscal period, and <see cref="MarketCap"/> is a live quote-time value
/// (4,574,891,083,660 against 3,818,743,810,000 at the FY2025 close). Mixed vintages with no date to disambiguate
/// them is exactly why the paragraph above matters.</para>
///
/// <para><b>Altman Z</b> is a bankruptcy-distress score: five balance-sheet and income ratios, weighted and summed,
/// where the conventional reading is that below 1.8 is distressed and above 3.0 is safe. <b>Piotroski F</b>
/// (<see cref="PiotroskiScore"/>) is an accounting-quality score on 0–9 — nine yes/no tests of profitability,
/// leverage and operating efficiency, one point each. The nine remaining fields on this response are here because
/// seven of them are precisely the Altman Z inputs, plus the ticker and the currency those seven are measured in;
/// none of Piotroski's inputs are sent, so that score cannot be checked from this payload.</para>
///
/// <para>The formula behind <see cref="AltmanZScore"/> was <b>verified</b> against the AAPL capture on 2026-08-26
/// rather than assumed: the classic public-manufacturer weighting
/// <c>1.2·(workingCapital/totalAssets) + 1.4·(retainedEarnings/totalAssets) + 3.3·(ebit/totalAssets) +
/// 0.6·(marketCap/totalLiabilities) + 1.0·(revenue/totalAssets)</c> reproduces the reported
/// <c>12.553407594048608</c> exactly. Note the fourth term uses market capitalisation against <i>total</i>
/// liabilities — the original uses market value of equity against total liabilities, so FMP is not using one of
/// the private-firm or non-manufacturer variants.</para>
///
/// <para>Every figure is <see langword="decimal"/>, not double. <see cref="MarketCap"/> was measured at
/// 4,574,891,083,660 and <see cref="AltmanZScore"/> at 12.553407594048608, which is 17 significant digits —
/// decimal holds both exactly, double rounds and would make a recomputed score disagree with the reported one in
/// the last places. <see cref="PiotroskiScore"/> is decimal too despite being a count; the reasoning is on that
/// property and is not the same reasoning.</para></summary>
public sealed record FinancialScores
{
    /// <summary>Ticker <b>as FMP spells it</b>, read from the response rather than echoed back from the argument
    /// the caller passed. The distinction is small and worth keeping: FMP's spelling is the authoritative one, and
    /// it is not always the caller's. Class-share tickers need the hyphenated form (<c>BRK-B</c>, not
    /// <c>BRK.B</c>) — the dotted spelling answers <c>[]</c> and so reads as an unknown symbol — so a caller that
    /// echoes its own argument can end up storing a spelling FMP will not accept on the next request, and will
    /// never notice the two disagreeing.</summary>
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";

    /// <summary>ISO currency the seven figures below are measured in — not necessarily USD, and not necessarily
    /// the currency the symbol trades in.</summary>
    [JsonPropertyName("reportedCurrency")] public string? ReportedCurrency { get; init; }

    /// <summary>Altman Z, the bankruptcy-distress score. Reproduced exactly from the seven figures below by the
    /// classic weighting given on the type; conventionally read as distressed below 1.8, safe above 3.0. Not
    /// clipped, so a cash-rich company with small liabilities scores far outside that range — AAPL measured
    /// 12.553407594048608 on 2026-08-26, almost all of it the <c>marketCap/totalLiabilities</c> term.</summary>
    [JsonPropertyName("altmanZScore")] public decimal? AltmanZScore { get; init; }

    /// <summary>Piotroski F, an accounting-quality score on <b>0–9</b>: nine yes/no tests of profitability,
    /// leverage/liquidity and operating efficiency, one point each. AAPL measured 9, as a JSON integer. None of
    /// its nine inputs are on this response, so unlike <see cref="AltmanZScore"/> it cannot be recomputed or
    /// audited from the payload.
    ///
    /// <para><b>The range is 0–9 and integral by construction, but the type is deliberately
    /// <see langword="decimal"/> and not <see langword="int"/>.</b> That is not a claim that a fractional score
    /// exists — none has been observed, and by definition none should. It is insurance, and the asymmetry behind
    /// it is what decides the question: <see langword="int"/> costs nothing until the day FMP serialises this
    /// through a float, and on that day it costs the <i>entire response</i>. Measured, not assumed —
    /// <c>System.Text.Json</c> reading into an <c>int?</c> throws on <c>8.5</c> <b>and equally on
    /// <c>9.0</c></b>, which is the same score written differently, and the throw aborts the whole
    /// deserialisation rather than one field. So all eleven properties are lost to a purely cosmetic change
    /// upstream. The context's <c>AllowReadingFromString</c> does not help: it rescues a quoted <c>"9"</c> and
    /// does nothing for an unquoted <c>9.0</c>.</para>
    ///
    /// <para>The precedent is <see cref="SharesFloat.FloatShares"/>, which is decimal for the same failure mode
    /// after FMP was observed serialising a share count as <c>25595002.125</c>. FMP does serialise counts as
    /// floating point elsewhere — <c>profile-bulk</c>'s <c>volume</c> arrives as <c>73305.59636</c> — so this is
    /// a shape the upstream is known to produce, even though it has not been seen on this field. Neither choice
    /// is reversible once callers depend on it, so it goes to the cheaper failure: a caller who wants an integer
    /// writes <c>(int)score</c>, which is a small annoyance forever, against losing a symbol's whole row.</para></summary>
    [JsonPropertyName("piotroskiScore")] public decimal? PiotroskiScore { get; init; }

    // ---- The Altman Z inputs. See the formula on the type; every one of these is a term in it. ----

    /// <summary>Current assets less current liabilities. Altman's X1 numerator. Can be negative — AAPL's fiscal
    /// year end was −17,674,000,000 while this row reported +492,000,000, which is part of the evidence that these
    /// figures are trailing rather than fiscal-period.</summary>
    [JsonPropertyName("workingCapital")] public decimal? WorkingCapital { get; init; }

    /// <summary>Total assets. The denominator of four of the five Altman terms, so a zero here would make the
    /// score meaningless rather than merely large.</summary>
    [JsonPropertyName("totalAssets")] public decimal? TotalAssets { get; init; }

    /// <summary>Accumulated retained earnings. Altman's X2 numerator, and routinely negative for a company that
    /// has returned more than it has earned.</summary>
    [JsonPropertyName("retainedEarnings")] public decimal? RetainedEarnings { get; init; }

    /// <summary>Earnings before interest and taxes. Altman's X3 numerator.</summary>
    [JsonPropertyName("ebit")] public decimal? Ebit { get; init; }

    /// <summary>Market capitalisation, used as Altman's X4 numerator against <see cref="TotalLiabilities"/>. A
    /// live quote-time value rather than a period-end one — measured 4,574,891,083,660 for AAPL on 2026-08-26
    /// against 3,818,743,810,000 at the FY2025 close — so a row refetched an hour later can carry a different
    /// score with every other input unchanged.</summary>
    [JsonPropertyName("marketCap")] public decimal? MarketCap { get; init; }

    /// <summary>Total liabilities, current and long-term. Altman's X4 denominator.</summary>
    [JsonPropertyName("totalLiabilities")] public decimal? TotalLiabilities { get; init; }

    /// <summary>Sales. Altman's X5 numerator, and the term that carries the unit weight.</summary>
    [JsonPropertyName("revenue")] public decimal? Revenue { get; init; }
}
