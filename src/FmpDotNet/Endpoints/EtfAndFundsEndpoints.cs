using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>ETFs and mutual funds — what a fund holds, who holds a fund, and the SEC filings behind both.
///
/// <para><b>Three things hold across all nine paths, measured 2026-08-30, and a caller should read them
/// once.</b></para>
///
/// <list type="number">
///   <item><description><b>There is no pagination anywhere in this group.</b> <c>limit</c> and <c>page</c>
///     were ignored on every path — verified by byte-identical responses with and without them, including a
///     17,252-row, 4.9 MB <c>etf/holdings?symbol=BND</c>. There are therefore no walk helpers and no page
///     ceilings here, unlike elsewhere on this client, and <b>no way to ask for less than
///     everything</b>. Two methods can return a great deal: <see cref="GetEtfHoldingsAsync"/> and
///     <see cref="SearchFundsByNameAsync"/>, whose <c>name=Trust</c> query returned <b>66,065 rows and
///     27.4 MB</b>.</description></item>
///   <item><description><b>Unknown input answers <c>[]</c> at HTTP 200, not an error.</b> An unknown symbol,
///     a stock symbol on an ETF-only path (AAPL returned <c>[]</c> on all four), a year outside a fund's
///     coverage, and a <c>quarter</c> of 0 or 5 all do this. Only a missing or malformed parameter is a
///     400.</description></item>
///   <item><description><b>One symbol per call.</b> <c>symbol=SPY,QQQ</c> answers <c>[]</c> at HTTP 200 —
///     a silent wrong answer — and the plural <c>symbols=</c> is a 400. Every method here rejects a comma
///     rather than letting that happen.</description></item>
/// </list>
///
/// <para><b>Method names carry <c>Etf</c> or <c>Fund</c> on purpose.</b> <c>GetHoldings</c> and
/// <c>GetDisclosure</c> on one facade would read as two views of one thing. They point opposite ways:
/// <see cref="GetEtfHoldingsAsync"/> is what a fund owns, <see cref="GetFundHoldersAsync"/> is who owns a
/// security.</para></summary>
public sealed class EtfAndFundsEndpoints(FmpTransport transport)
{
    /// <summary>Which ETFs hold a given security, from <c>stable/etf/asset-exposure</c>.
    ///
    /// <para><b>This runs the opposite way from the other four <c>etf/*</c> methods.</b> The argument is the
    /// <b>held asset</b>, not the fund: measured 2026-08-30, <c>AAPL</c> answered 3,293 rows each naming a
    /// different ETF. Any asset works, including an ETF — <c>SPY</c> answered 39 rows.</para></summary>
    /// <param name="symbol">The held security. One symbol; a comma-joined list is rejected, because FMP
    /// answers it with an empty array at HTTP 200.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every ETF position in the asset, in FMP's own order. <b>No ordering was found</b> in the
    /// responses measured 2026-08-30. An asset no ETF holds answers an empty list. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<EtfAssetExposure>> GetEtfAssetExposureAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/asset-exposure").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfAssetExposure, ct);
    }

    /// <summary>An ETF's country breakdown, from <c>stable/etf/country-weightings</c>.
    ///
    /// <para><b>The weights arrive as percent-suffixed strings on this path and as bare numbers on
    /// <see cref="GetEtfSectorWeightingsAsync"/></b>, one letter apart in the URL. The SDK reconciles them —
    /// see <see cref="EtfCountryWeighting.WeightPercentage"/>.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The breakdown, in FMP's own order. Measured 2026-08-30 that order is <b>by weight,
    /// descending</b>. A commodity fund still answers a row: GLD and SLV each returned <c>"Other"</c> at
    /// <c>"100%"</c>. The list can be empty — some symbols answer <c>[]</c> at HTTP 200 rather than an
    /// error — but is never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfCountryWeighting>> GetEtfCountryWeightingsAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/country-weightings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfCountryWeighting, ct);
    }

    /// <summary>Everything an ETF holds, from <c>stable/etf/holdings</c>.
    ///
    /// <para><b>This is the method to size before calling.</b> There is no pagination and no way to ask for
    /// less: measured 2026-08-30, <c>BND</c> answered <b>17,252 rows and 4.9 MB</b> and <c>VXUS</c> 8,821 rows
    /// and 2.5 MB, and <c>limit</c> and <c>page</c> changed neither by a byte.
    /// <see cref="EtfInfo.HoldingsCount"/> cannot be used to predict the size — it agreed with this path on
    /// <b>one</b> of 33 ETFs.</para>
    ///
    /// <para>Rows for a bond fund mostly have no ticker: <see cref="EtfHolding.Asset"/> was empty on 51.1% of
    /// 35,185 rows measured. <see cref="EtfHolding.Name"/> was populated on all of them.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holding, in FMP's own order. Measured 2026-08-30 that order is <b>by weight,
    /// descending</b>, and it held over the full 17,252-row BND response. A stock symbol answers an empty
    /// list. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfHolding>> GetEtfHoldingsAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/holdings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfHolding, ct);
    }

    /// <summary>An ETF's fact sheet, from <c>stable/etf/info</c>.
    ///
    /// <para>All 33 responses measured 2026-08-30 were single-element arrays, so this returns one record
    /// rather than a list — the <see cref="CompanyEndpoints.GetProfileAsync"/> precedent. The record carries
    /// the fund's sector breakdown inline: <see cref="EtfInfo.SectorsList"/> measured <b>identical</b> to
    /// <see cref="GetEtfSectorWeightingsAsync"/> on all 13 ETFs cross-checked, so a caller holding this does
    /// not need that call.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The fact sheet, or <see langword="null"/> when FMP answered an empty array — which is what an
    /// unknown symbol and a stock symbol both do, at HTTP 200.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<EtfInfo?> GetEtfInfoAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/etf/info").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfInfo, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>An ETF's sector breakdown, from <c>stable/etf/sector-weightings</c>.
    ///
    /// <para><b>This data is already inside <see cref="GetEtfInfoAsync"/>'s answer.</b> Measured 2026-08-30,
    /// <see cref="EtfInfo.SectorsList"/> agreed with this path on the key set and on every value, with no
    /// rounding difference, on all 13 ETFs cross-checked. Calling both is a wasted request.</para>
    ///
    /// <para>The weights are bare JSON numbers here, unlike
    /// <see cref="GetEtfCountryWeightingsAsync"/>'s.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The breakdown, in FMP's own order. Measured 2026-08-30 that order is <b>alphabetical by
    /// sector</b> — not by weight, unlike <see cref="GetEtfCountryWeightingsAsync"/>. A commodity fund answers
    /// one row, <c>Cash &amp; Others</c>. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<EtfSectorWeighting>> GetEtfSectorWeightingsAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/etf/sector-weightings").With("symbol", symbol),
            FmpJsonContext.Default.ListEtfSectorWeighting, ct);
    }

    /// <summary>A fund's filed portfolio for one quarter, from <c>stable/funds/disclosure</c> — the holding
    /// lines of its SEC Form N-PORT.
    ///
    /// <para><b>This is the fund's own filing, not FMP's cached view.</b> Where
    /// <see cref="GetEtfHoldingsAsync"/> answers "what does FMP think this ETF holds now", this answers "what
    /// did the fund tell the SEC it held on this date", with a real as-of date and an EDGAR acceptance
    /// timestamp.</para>
    ///
    /// <para><b>Find the periods first.</b> A quarter the fund never filed answers an empty list at HTTP 200,
    /// and so does a quarter outside FMP's coverage — measured 2026-08-30, every quarter from 2024 Q1 to
    /// 2026 Q2 answered while 2026 Q3 and Q4 were empty. <see cref="GetFundDisclosureDatesAsync"/> returns the
    /// <c>year</c> and <c>quarter</c> pairs that exist, ready to pass here.</para>
    ///
    /// <para><b>Funds do not all file on calendar quarters.</b> <paramref name="quarter"/> is the
    /// <b>calendar</b> quarter of the fund's fiscal period end, so FXAIX's 2026-05-31 period is Q2 — see
    /// <see cref="FundDisclosureDate"/>.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="year">The calendar year, as <see cref="FundDisclosureDate.Year"/> reports it.
    /// <b>Deliberately unbounded</b>: coverage differs per fund — measured 2026-08-30 it reached back to
    /// 2019-09-30 for SPY, 2019-11-30 for FXAIX and 2020-04-30 for ARKK — and will move, so any bound written
    /// here would be a fabricated fact. A year outside coverage answers an empty list.</param>
    /// <param name="quarter">The calendar quarter, 1 to 4, as <see cref="FundDisclosureDate.Quarter"/> reports
    /// it. Rejected outside that range: measured 2026-08-30, FMP answers <c>quarter=0</c> and
    /// <c>quarter=5</c> with an empty list at HTTP 200 rather than an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holding line in the filing, in FMP's own order. <b>No ordering was found</b> in the
    /// responses measured 2026-08-30. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quarter"/> is not 1 to 4.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundDisclosure>> GetFundDisclosureAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        ThrowIfQuarterOutOfRange(quarter);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListFundDisclosure, ct);
    }

    /// <summary>Which reporting periods a fund has filed, from <c>stable/funds/disclosure-dates</c>.
    ///
    /// <para><b>This is the index for <see cref="GetFundDisclosureAsync"/>.</b> Each row carries the fiscal
    /// period end together with the calendar <c>year</c> and <c>quarter</c> that select it, so a caller pairs
    /// the two calls without doing the arithmetic — which matters because funds do not all file on calendar
    /// quarters.</para></summary>
    /// <param name="symbol">The fund. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every filed period, in FMP's own order. Measured 2026-08-30 that order is <b>newest
    /// first</b>. A symbol with no filings answers an empty list — AAPL did. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundDisclosureDate>> GetFundDisclosureDatesAsync(
        string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-dates").With("symbol", symbol),
            FmpJsonContext.Default.ListFundDisclosureDate, ct);
    }

    /// <summary>Which institutions hold a given security, from
    /// <c>stable/funds/disclosure-holders-latest</c>.
    ///
    /// <para><b>The reverse of <see cref="GetFundDisclosureAsync"/>, and the argument need not be a fund</b> —
    /// measured 2026-08-30, <c>AAPL</c> answered 3,209 rows.</para>
    ///
    /// <para><b>"Latest" is per holder, not per response.</b> One response mixes reporting dates by years:
    /// SPY's 220 rows carried <b>19 distinct dates spanning 2019-09-30 to 2026-06-30</b>. Read
    /// <see cref="FundHolder.DateReported"/> per row before summing or ranking anything — see
    /// <see cref="FundHolder"/>, where the distribution is recorded.</para></summary>
    /// <param name="symbol">The held security. One symbol; a comma-joined list is rejected.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every reported holder, in FMP's own order. <b>No ordering was found</b> in the responses
    /// measured 2026-08-30, and the rows are <b>not a single as-of snapshot</b>. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundHolder>> GetFundHoldersAsync(string symbol, CancellationToken ct = default)
    {
        ThrowIfNotOneSymbol(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-holders-latest").With("symbol", symbol),
            FmpJsonContext.Default.ListFundHolder, ct);
    }

    /// <summary>SEC-registered fund share classes whose registrant name matches a word, from
    /// <c>stable/funds/disclosure-holders-search</c>.
    ///
    /// <para><b>Named for what it returns, not for the path.</b> The rows are not holders: they are
    /// registrant, series and class identifiers plus a filer address — see
    /// <see cref="FundShareClass"/>.</para>
    ///
    /// <para><b>Matching is case-insensitive, whole-word and single-word.</b> Measured 2026-08-30:
    /// <c>Vanguard</c>, <c>vanguard</c> and <c>VANGUARD</c> each returned the same 548 rows; <c>Vangua</c>
    /// returned <b>0</b>; <c>Fid</c> and <c>fidelit</c> returned 0; and <c>Vanguard Group</c> — a two-word
    /// company name, the most likely thing a caller types — returned <b>0</b>. Pass one whole word:
    /// <c>"Vanguard"</c>, <c>"Fidelity"</c>, <c>"Schwab"</c>. The exact tokenisation was not established and
    /// this SDK does not assert one.</para>
    ///
    /// <para><b>This is the largest response in the group and it cannot be narrowed.</b> Measured 2026-08-30,
    /// <c>name=Trust</c> returned <b>66,065 rows and 27.4 MB</b>, and <c>limit</c> and <c>page</c> changed it
    /// by not one byte. A common word is a very expensive query.</para></summary>
    /// <param name="name">One whole word from the registrant's name. Case does not matter; a prefix and a
    /// two-word phrase both match nothing.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every matching share class, in FMP's own order. A word that matches nothing answers an empty
    /// list at HTTP 200. Never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<FundShareClass>> SearchFundsByNameAsync(
        string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return transport.GetListAsync(
            new FmpRequest("stable/funds/disclosure-holders-search").With("name", name),
            FmpJsonContext.Default.ListFundShareClass, ct);
    }

    /// <summary>Rejects a symbol FMP would answer with silence.
    ///
    /// <para>Two failures, one guard. A blank <c>symbol=</c> is an HTTP 400 on every path in this group,
    /// measured 2026-08-30 — an error the caller would see. A comma-joined list is worse: <c>symbol=SPY,QQQ</c>
    /// answers <b><c>[]</c> at HTTP 200</b> on <c>etf/info</c> and <c>etf/sector-weightings</c>, which is
    /// indistinguishable from "this fund has no data", while the plural <c>symbols=</c> is a 400. The
    /// comma-joined form that <see cref="QuoteEndpoints"/>' batch methods take is therefore not merely
    /// unsupported here — it is a silent wrong answer.</para>
    ///
    /// <para><b>Narrow on purpose.</b> This rejects the comma, not "not a known ETF". An unknown symbol
    /// legitimately answers <c>[]</c>, and so does a perfectly valid stock — measured 2026-08-30,
    /// <c>AAPL</c> returned <c>[]</c> on all four ETF-only paths. Those are honest empties and are documented
    /// rather than guarded.</para>
    ///
    /// <para>The parameter is named <c>symbol</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentException.ParamName"/>.</para></summary>
    private static void ThrowIfNotOneSymbol(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (symbol.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "These paths take one symbol. Measured 2026-08-30, a comma-joined list answers an empty "
                + "array with HTTP 200 — a silent wrong answer, not an error. Call once per symbol.",
                nameof(symbol));
        }
    }

    /// <summary>Rejects a quarter FMP would answer with an empty list rather than an error.
    ///
    /// <para>Measured 2026-08-30, <c>quarter=0</c> and <c>quarter=5</c> both return HTTP 200 with <c>[]</c>,
    /// while <c>quarter=Q1</c> is a 400 — so a caller who sends 0 is told "no holdings", not "bad request".
    /// The range is the calendar's and not a measured cap: there is no fifth quarter to measure. This follows
    /// <see cref="InstitutionalOwnershipEndpoints"/>, which guards the same argument for the same
    /// reason.</para>
    ///
    /// <para>The parameter is named <c>quarter</c> so that <c>[CallerArgumentExpression]</c> puts the caller's
    /// own parameter name on <see cref="ArgumentException.ParamName"/>.</para></summary>
    private static void ThrowIfQuarterOutOfRange(int quarter)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quarter, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quarter, 4);
    }
}
