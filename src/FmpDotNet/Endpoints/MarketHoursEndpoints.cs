using FmpDotNet.Models;
using FmpDotNet.Serialization;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>When exchanges trade — opening and closing bells for 81 exchanges, and the holiday calendar
/// behind them.
///
/// <para><b>Three things hold across all three paths, measured 2026-08-30.</b></para>
///
/// <list type="number">
///   <item><description><b>The exchange code is case-insensitive and the exchange NAME is not accepted.</b>
///     <c>exchange=nasdaq</c> returned a byte-identical response to <c>exchange=NASDAQ</c> on both
///     single-exchange paths; <c>exchange=NASDAQ%20Global%20Market</c> is an HTTP 400. Codes come from
///     <see cref="DirectoryEndpoints.GetExchangesAsync"/> — all <b>63</b> codes it returned appear in
///     <see cref="GetAllExchangesAsync"/>, which carries 18 more.</description></item>
///   <item><description><b>An unknown exchange is an error, not an empty list.</b> <c>exchange=ZZZZ</c> and
///     <c>exchange=NASDAQ,NYSE</c> are both HTTP 400 <c>Invalid Exchange Provided.</c> This SDK does not
///     validate the code itself: the vocabulary is 81 entries that will change, and a client-side list
///     would go stale.</description></item>
///   <item><description><b>Nothing paginates.</b> <c>limit</c> and <c>page</c> were ignored on all three
///     paths — byte-identical responses.</description></item>
/// </list>
///
/// <para>Index membership is a separate facade — <see cref="IndexesEndpoints"/> — because the two groups
/// share no path prefix, no parameter, no record and no concept.</para></summary>
public sealed class MarketHoursEndpoints(FmpTransport transport)
{
    /// <summary>Trading hours for every exchange FMP knows, from <c>stable/all-exchange-market-hours</c>.
    ///
    /// <para>81 rows measured 2026-08-30 — 18 more than
    /// <see cref="DirectoryEndpoints.GetExchangesAsync"/> returns. Read
    /// <see cref="ExchangeMarketHours.OpeningAdditionalText"/> before building anything on the first row:
    /// seven of these exchanges break for lunch and carry two extra keys that 74 rows lack.</para></summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every exchange, in FMP's own order. Never <see langword="null"/>.</returns>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points
    /// at the key at least as often as at the plan.</exception>
    public Task<IReadOnlyList<ExchangeMarketHours>> GetAllExchangesAsync(CancellationToken ct = default) =>
        transport.GetListAsync(
            new FmpRequest("stable/all-exchange-market-hours"),
            FmpJsonContext.Default.ListExchangeMarketHours, ct);

    /// <summary>Trading hours for one exchange, from <c>stable/exchange-market-hours</c>.
    ///
    /// <para><b>The row is the same row <see cref="GetAllExchangesAsync"/> carries.</b> For each of seven
    /// exchanges cross-checked 2026-08-30, this path's single row compared <b>equal, key for key and value
    /// for value</b>, to that exchange's row in the 81-row response. Call this when you want one exchange
    /// and that one when you want them all.</para>
    ///
    /// <para><b><see langword="null"/> was never observed and probably cannot happen.</b> Every measured
    /// response was a single-element array, and an unknown exchange is an HTTP 400 rather than an empty
    /// array — so the emptiness that would produce <see langword="null"/> here has no measured cause. The
    /// nullable return is honesty about what the deserialiser can promise, not a hint that emptiness is
    /// expected.</para>
    ///
    /// <para>The code is sent exactly as given: <c>nasdaq</c> and <c>NASDAQ</c> answered byte-identically
    /// on 2026-08-30, so there is nothing to normalise and normalising would rewrite the caller's
    /// identifier.</para></summary>
    /// <param name="exchange">The exchange code — <c>"NASDAQ"</c>, <c>"JPX"</c>. One exchange; a
    /// comma-joined list is rejected. Case-insensitive upstream. The exchange's full <i>name</i> is not
    /// accepted.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The exchange's hours, or <see langword="null"/> on an empty array.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is blank or contains a comma.</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status — including <b>400</b> for an
    /// exchange it does not know.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<ExchangeMarketHours?> GetExchangeAsync(string exchange, CancellationToken ct = default)
    {
        ThrowIfNotOneExchange(exchange);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/exchange-market-hours").With("exchange", exchange),
            FmpJsonContext.Default.ListExchangeMarketHours, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>The days an exchange is closed or closes early, over a required date range, from
    /// <c>stable/holidays-by-exchange</c>.
    ///
    /// <para><b>The range is required because the default answer hides the future.</b> Measured 2026-08-30
    /// across five exchanges, the bare call returned <b>67 rows, every one dated between 2025-08-30 and that
    /// day, and not one dated after it</b> — while <c>from=1990-01-01&amp;to=2035-12-31</c> returned 446 rows
    /// reaching <b>2032-12-31</b>. The most natural question a caller has for this endpoint — <i>when is the
    /// market next closed?</i> — is the one question its default answer can never answer. Making the range
    /// required costs the caller one obvious line and removes a wrong answer that arrives at HTTP 200 with
    /// no warning.</para>
    ///
    /// <para><b>The window is half-open — <c>(from, to]</c> — and this SDK does not compensate for it.</b>
    /// Measured 2026-08-30 against NASDAQ's 2026-07-03 holiday: <c>from=2026-07-03&amp;to=2026-07-03</c>
    /// returns <c>[]</c>, <c>from=2026-07-03&amp;to=2026-07-04</c> returns <c>[]</c>, and
    /// <c>from=2026-07-02&amp;to=2026-07-03</c> returns the row. <c>to</c> is inclusive, <c>from</c> is not,
    /// and <b>a single-day range therefore always answers an empty list</b> no matter what falls on that
    /// day. Pass a <paramref name="from"/> one day before the earliest date you care about.</para>
    ///
    /// <para>Sending <c>from.PlusDays(-1)</c> upstream on the caller's behalf would make this signature
    /// behave the way a date range is expected to, and is deliberately <b>not</b> done: the request would
    /// then not match the arguments passed, which turns every debugging session into a puzzle.</para>
    ///
    /// <para><b>These are the only two date parameters honoured in this group.</b> On the three
    /// <c>historical-*-constituent</c> paths <c>from</c> and <c>to</c> are accepted and discarded, which is
    /// why <see cref="IndexesEndpoints"/>'s methods do not offer them.</para></summary>
    /// <param name="exchange">The exchange code. One exchange; a comma-joined list is rejected.</param>
    /// <param name="from">The day <b>before</b> the earliest date wanted — the bound is exclusive
    /// upstream.</param>
    /// <param name="to">The latest date wanted; this bound is inclusive.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every holiday in the window, in FMP's own order. Measured 2026-08-30 that order is <b>by
    /// date, descending</b>. Never <see langword="null"/>; an empty list means either no holidays or a range
    /// one day wide.</returns>
    /// <exception cref="ArgumentException"><paramref name="exchange"/> is blank or contains a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is earlier than
    /// <paramref name="from"/>. FMP answers a reversed range with an empty list at HTTP 200, which reads as
    /// "no holidays".</exception>
    /// <exception cref="FmpApiException">FMP answered a failure status.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<ExchangeHoliday>> GetHolidaysAsync(
        string exchange, LocalDate from, LocalDate to, CancellationToken ct = default)
    {
        ThrowIfNotOneExchange(exchange);
        DateRange.ThrowIfBackwards(from, to);
        return transport.GetListAsync(
            new FmpRequest("stable/holidays-by-exchange")
                .With("exchange", exchange).With("from", from).With("to", to),
            FmpJsonContext.Default.ListExchangeHoliday, ct);
    }

    /// <summary>Rejects an exchange argument FMP would answer with a 400.
    ///
    /// <para>Measured 2026-08-30, <c>exchange=NASDAQ,NYSE</c> answers <b>HTTP 400</b>
    /// <c>Invalid Exchange Provided.</c> — so unlike the comma case on the ETF paths, this is already an
    /// error rather than a silent empty list. The guard is still worth having: it turns a wasted call
    /// against the key's quota into an <see cref="ArgumentException"/> that names the fix, and it matches
    /// <c>ThrowIfNotOneSymbol</c>'s established shape. It does <b>not</b> validate the code's spelling —
    /// the vocabulary is upstream's and will change.</para></summary>
    private static void ThrowIfNotOneExchange(string exchange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (exchange.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "These paths take one exchange. Measured 2026-08-30, a comma-joined list answers HTTP 400 "
                + "'Invalid Exchange Provided.' — a wasted call against the key's quota. Call once per "
                + "exchange, or use GetAllExchangesAsync.",
                nameof(exchange));
        }
    }
}
