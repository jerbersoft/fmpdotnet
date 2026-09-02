namespace FmpDotNet;

/// <summary>What to screen for. Every property is optional; the ones left unset are not sent, so an empty
/// <see cref="ScreenerCriteria"/> is a valid request for the whole universe by descending market cap.
///
/// <para><b>This type exists because the screener does not reject bad input — it answers it.</b> Measured on
/// 2026-08-26 against the live API:</para>
/// <list type="bullet">
///   <item><description>an <b>unrecognised parameter name</b> is silently ignored. <c>bogusParam=1&amp;limit=3</c>
///     returned the same three rows as <c>limit=3</c> alone, with HTTP 200. A hand-built query with a typo in a
///     filter name therefore screens on nothing and returns a universe that is too broad, looking exactly like a
///     query that worked;</description></item>
///   <item><description>an <b>unrecognised parameter value</b> answers an empty list.
///     <c>sector=Nonsense</c> returned <c>[]</c> with HTTP 200 — indistinguishable from a real filter that matched
///     nothing.</description></item>
/// </list>
/// <para>The first failure is closed by this type: a misspelled filter name will not compile. The second cannot be
/// closed here without freezing a vocabulary FMP grows, so the string-valued filters below each name the endpoint
/// that returns their valid values.</para>
///
/// <para><b>Every numeric bound is inclusive, despite what the names say.</b> FMP calls these parameters
/// <c>…MoreThan</c> and <c>…LowerThan</c> and the SDK keeps those names so they map onto FMP's own documentation,
/// but both were measured as <c>&gt;=</c> and <c>&lt;=</c>: <c>priceLowerThan=1</c> returns securities priced at
/// exactly <c>1</c>, <c>betaLowerThan=0</c> returns beta exactly <c>0</c>, and <c>betaMoreThan=2.215</c> returns
/// the security whose beta is exactly <c>2.215</c>; and on 2026-09-02 (#58) <c>avgVolumeMoreThan=5006432</c>
/// kept the row whose average is exactly <c>5006432</c> while <c>5006433</c> dropped it. Two adjacent ranges built
/// as <c>LowerThan = x</c> and <c>MoreThan = x</c> therefore overlap on the boundary rather than
/// partitioning.</para></summary>
public sealed record ScreenerCriteria
{
    /// <summary>Smallest market capitalisation to include. Inclusive — see the note on the type.</summary>
    public decimal? MarketCapMoreThan { get; init; }

    /// <summary>Largest market capitalisation to include. Inclusive.</summary>
    public decimal? MarketCapLowerThan { get; init; }

    /// <summary>Lowest price to include. Inclusive.</summary>
    public decimal? PriceMoreThan { get; init; }

    /// <summary>Highest price to include. Inclusive — <c>PriceLowerThan = 1</c> matched securities priced at
    /// exactly 1.</summary>
    public decimal? PriceLowerThan { get; init; }

    /// <summary>Lowest beta to include. Inclusive.</summary>
    public decimal? BetaMoreThan { get; init; }

    /// <summary>Highest beta to include. Inclusive, and worth care at zero: a beta of <c>0</c> appears to mean "not
    /// computed" rather than "market-neutral", so <c>BetaLowerThan = 0</c> sweeps in ETNs and preferred shares
    /// along with the genuinely negative betas. See <see cref="Models.ScreenerResult.Beta"/>.</summary>
    public decimal? BetaLowerThan { get; init; }

    /// <summary>Lowest volume to include. Inclusive.</summary>
    public long? VolumeMoreThan { get; init; }

    /// <summary>Highest volume to include. Inclusive.</summary>
    public long? VolumeLowerThan { get; init; }

    /// <summary>Lowest average daily volume to include. Inclusive, measured 2026-09-02 (#58) at the boundary:
    /// <c>avgVolumeMoreThan=5006432</c> kept the row whose average is exactly <c>5006432</c>, and <c>5006433</c>
    /// dropped it.
    ///
    /// <para><c>0</c> is inert here — every row satisfies it — and <b><c>1</c> is the useful floor</b>: it drops
    /// every zero-average row, and all but a handful of those are the mutual funds. See
    /// <see cref="Models.ScreenerResult.AvgVolume"/>.</para></summary>
    public long? AvgVolumeMoreThan { get; init; }

    /// <summary>Highest average daily volume to include. Inclusive, measured 2026-09-02 (#58):
    /// <c>avgVolumeLowerThan=748</c> kept the row whose average is exactly <c>748</c>, and <c>747</c> dropped it.
    ///
    /// <para>Any low bound sweeps in the zero-average rows, and there are more than a page of them:
    /// <c>avgVolumeLowerThan=0</c> on NASDAQ filled the 1,000-row default page with zeros alone. Pair with
    /// <see cref="IsFund"/> = <see langword="false"/> or <see cref="AvgVolumeMoreThan"/> = <c>1</c> to keep only
    /// the lines that trade.</para></summary>
    public long? AvgVolumeLowerThan { get; init; }

    /// <summary>Smallest last-annual-dividend to include, as an amount per share rather than a yield. Inclusive.
    /// Note that a security paying nothing reports <c>0</c> here, so <c>DividendMoreThan = 0</c> does not exclude
    /// non-payers.</summary>
    public decimal? DividendMoreThan { get; init; }

    /// <summary>Largest last-annual-dividend to include. Inclusive.</summary>
    public decimal? DividendLowerThan { get; init; }

    /// <summary>Sector to restrict to. <b>Take the spelling from
    /// <see cref="Endpoints.DirectoryEndpoints.GetSectorsAsync(CancellationToken)"/></b> — there are 11, and one
    /// FMP does not recognise returns an empty result rather than an error. Matching is case-insensitive
    /// (<c>technology</c> matched), so case is not the risk here; a wrong or invented label is.</summary>
    public string? Sector { get; init; }

    /// <summary>Industry to restrict to. Take the spelling from
    /// <see cref="Endpoints.DirectoryEndpoints.GetIndustriesAsync(CancellationToken)"/> — there are 159, which is
    /// more than anyone reliably remembers, and a wrong one is silently empty.</summary>
    public string? Industry { get; init; }

    /// <summary>Two-letter country code of the <b>company</b>. This is not a market filter: <c>CA</c> returns
    /// Canadian companies listed in Hong Kong and London as well as Toronto. Use <see cref="Exchange"/> to narrow
    /// to a venue.</summary>
    public string? Country { get; init; }

    /// <summary>Exchange to restrict to, as the <b>short code</b> — <c>NASDAQ</c>, <c>NYSE</c>, <c>AMEX</c>,
    /// <c>TSX</c>.
    ///
    /// <para><b>Not the long name that comes back on the result.</b> <see cref="Models.ScreenerResult.Exchange"/>
    /// carries <c>NASDAQ Global Select</c>, and sending that here answers an empty list with HTTP 200 — measured.
    /// The field to round-trip is <see cref="Models.ScreenerResult.ExchangeShortName"/>.</para></summary>
    public string? Exchange { get; init; }

    /// <summary>Restrict to, or exclude, exchange-traded funds. Disjoint from <see cref="IsFund"/>.</summary>
    public bool? IsEtf { get; init; }

    /// <summary>Restrict to, or exclude, mutual and money-market funds. A row matching this carries
    /// <c>isEtf=false</c>, so the two flags do not overlap.</summary>
    public bool? IsFund { get; init; }

    /// <summary>Restrict to, or exclude, securities FMP considers actively trading — the same judgement behind
    /// <see cref="Endpoints.DirectoryEndpoints.GetActivelyTradingAsync(CancellationToken)"/>.</summary>
    public bool? IsActivelyTrading { get; init; }

    /// <summary>Include non-common share classes — preferred shares and the like.
    ///
    /// <para>Measured and real, not cosmetic: over a 1,000-row response this swapped out 116 symbols, bringing in
    /// <c>AIG-PA</c>, <c>BAC-PB</c>, <c>ALL-PH</c> and their kind, and pushing 116 common lines off the end. Note
    /// that multiple <i>common</i> classes are already returned by default — <c>GOOG</c> and <c>GOOGL</c> both
    /// appear without this — so this is about preferred lines rather than dual-class common
    /// stock.</para></summary>
    public bool? IncludeAllShareClasses { get; init; }

    /// <summary>Zero-based page index. Measured as honoured: <c>page=1&amp;limit=5</c> returned rows 6 to 10, the
    /// five immediately after <c>page=0</c>'s. Left unset, FMP serves the first page.</summary>
    public int? Page { get; init; }

    /// <summary>How many rows to return. Left unset, FMP returns <b>1,000</b> — measured, and worth knowing before
    /// treating an unset limit as "everything".
    ///
    /// <para>There is no low cap here, unlike
    /// <see cref="Endpoints.CompanyEndpoints.GetDelistedAsync(int, int, CancellationToken)"/>'s hard 100:
    /// <c>limit=3000</c> and <c>limit=10000</c> were both honoured exactly, the latter transferring 4.4 MB. Since
    /// rows come back ordered by market cap descending, this is a "top N" control.</para></summary>
    public int? Limit { get; init; }

    /// <summary>Renders the criteria onto a request, dropping everything unset.
    ///
    /// <para><see cref="FmpRequest.With(string, string?)"/> already drops nulls, so the absent properties never
    /// reach the query string — which is what makes an empty <see cref="ScreenerCriteria"/> an unfiltered
    /// request rather than a request for nothing.</para></summary>
    internal FmpRequest ToRequest() =>
        new FmpRequest("stable/company-screener")
            .With("marketCapMoreThan", Number(MarketCapMoreThan))
            .With("marketCapLowerThan", Number(MarketCapLowerThan))
            .With("priceMoreThan", Number(PriceMoreThan))
            .With("priceLowerThan", Number(PriceLowerThan))
            .With("betaMoreThan", Number(BetaMoreThan))
            .With("betaLowerThan", Number(BetaLowerThan))
            .With("volumeMoreThan", Number(VolumeMoreThan))
            .With("volumeLowerThan", Number(VolumeLowerThan))
            .With("avgVolumeMoreThan", Number(AvgVolumeMoreThan))
            .With("avgVolumeLowerThan", Number(AvgVolumeLowerThan))
            .With("dividendMoreThan", Number(DividendMoreThan))
            .With("dividendLowerThan", Number(DividendLowerThan))
            .With("sector", Sector)
            .With("industry", Industry)
            .With("country", Country)
            .With("exchange", Exchange)
            .With("isEtf", IsEtf)
            .With("isFund", IsFund)
            .With("isActivelyTrading", IsActivelyTrading)
            .With("includeAllShareClasses", IncludeAllShareClasses)
            .With("page", Page)
            .With("limit", Limit);

    /// <summary>Formats a numeric bound invariantly.
    ///
    /// <para>The culture is the point. A market-cap bound formatted under a comma-decimal culture becomes
    /// <c>1000000000,5</c> in the query string, and FMP does not reject it — an unparseable value is treated like
    /// an unrecognised one, which on this endpoint means a silent empty result on a German or French host and a
    /// correct one everywhere else. <see cref="decimal"/> is used rather than <see cref="double"/> so a bound the
    /// caller wrote exactly is sent exactly.</para></summary>
    private static string? Number<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
}
