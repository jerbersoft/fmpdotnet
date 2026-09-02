using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>Earnings call transcripts — one call in full, the index of calls for a symbol, and the
/// whole-market feed of what was just published.
///
/// <para><b>Which symbols have transcripts at all is answered elsewhere.</b>
/// <see cref="DirectoryEndpoints.GetTranscriptSymbolsAsync"/> serves
/// <c>stable/earnings-transcript-list</c> — 11,178 symbols measured 2026-08-27 — and stays on
/// <see cref="DirectoryEndpoints"/> because it is a universe list rather than a transcript.</para>
///
/// <para><b>The three paths spell the same two facts three different ways</b>, and this SDK reproduces each
/// exactly rather than normalising. See <see cref="EarningsTranscript"/>.</para>
///
/// <para><b>Plan tier — Ultimate, second-hand.</b> fmpsdk 20260824.0, the independent client this SDK is
/// cross-checked against, recorded every path in this class as 402 on free, Starter and Premium and working on
/// Ultimate on 2026-08-24. Not verified here: every path answered 200 on the Ultimate key this SDK is measured with
/// (2026-09-02), which says nothing about the plans below it. A dated observation, not a contract — catch
/// <see cref="FmpPlanRestrictedException"/> rather than gating on it.</para></summary>
public sealed class TranscriptsEndpoints(FmpTransport transport)
{
    /// <summary>The largest page <see cref="GetLatestAsync"/> will serve, measured rather than documented.
    ///
    /// <para>A <b>cap, not a page size</b>. Measured 2026-08-29, <c>?limit=500</c> answered exactly 100 rows
    /// at HTTP 200 with nothing in the body saying the request had been trimmed — byte-identical to the bare
    /// call. <c>?limit=10</c> answered 10, so the parameter works below the cap.</para></summary>
    public const int MaxLatestTranscriptPageSize = 100;

    /// <summary>One earnings call in full — <c>stable/earning-call-transcript</c>.
    ///
    /// <para><b>Queried with <c>quarter=3</c>, answers <c>period: "Q3"</c>.</b> The request vocabulary and
    /// the response vocabulary disagree on this one endpoint. Renaming
    /// <paramref name="quarter"/> to match what comes back gets HTTP 400.</para>
    ///
    /// <para>All three parameters are required — measured 2026-08-29 by removing them one at a time, each
    /// omission answering HTTP 400 naming the missing one. The quarters a symbol actually has are listed by
    /// <see cref="GetDatesAsync"/>.</para>
    ///
    /// <para><see cref="EarningsTranscript.Content"/> is the whole transcript as one string — 46,487
    /// characters for AAPL 2025 Q3, measured 2026-08-29. This is not a small response.</para>
    ///
    /// <para><b>No <c>limit</c>, because with all three parameters required the path answers exactly one
    /// transcript and there is nothing for a limit to bound.</b> Recorded because <c>fmpsdk</c> sends one and a
    /// later parameter diff will raise it again: measured 2026-09-01 (#46), <c>limit=1</c> answered a body
    /// byte-identical to the request without it. Decorative here by the shape of the endpoint rather than by FMP
    /// discarding it — the same verdict, and the same caveat, as
    /// <see cref="SearchEndpoints.FindByCikAsync"/>.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="year">The fiscal year, as <see cref="TranscriptDate.FiscalYear"/> reports it.</param>
    /// <param name="quarter">The fiscal quarter as an integer, 1 to 4 — as
    /// <see cref="TranscriptDate.Quarter"/> reports it, and <b>not</b> the <c>Q3</c> form the response
    /// carries.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The transcript, or <see langword="null"/> when FMP has none for that symbol and period.
    /// A miss is an empty array rather than an error, so it arrives here as null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public async Task<EarningsTranscript?> GetTranscriptAsync(
        string symbol, int year, int quarter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return await transport.GetSingleAsync(
            new FmpRequest("stable/earning-call-transcript")
                .With("symbol", symbol).With("year", year).With("quarter", quarter),
            FmpJsonContext.Default.ListEarningsTranscript, ct).ConfigureAwait(false);
    }

    /// <summary>Every quarter one symbol has a transcript for, newest first —
    /// <c>stable/earning-call-transcript-dates</c>.
    ///
    /// <para>The index into <see cref="GetTranscriptAsync"/>. Measured 2026-08-29, <c>?symbol=AAPL</c>
    /// answered <b>84 rows</b> spanning 2026-07-30 back to 2005-10-13 — full history, with no cap
    /// observed and no paging parameter offered.</para></summary>
    /// <param name="symbol">The ticker, as FMP spells it.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Every quarter with a transcript, newest first. Never <see langword="null"/>; empty for a
    /// symbol with none, not an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or blank.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<TranscriptDate>> GetDatesAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return transport.GetListAsync(
            new FmpRequest("stable/earning-call-transcript-dates").With("symbol", symbol),
            FmpJsonContext.Default.ListTranscriptDate, ct);
    }

    /// <summary>Transcripts as they are published, across every market —
    /// <c>stable/earning-call-transcript-latest</c>.
    ///
    /// <para><b><paramref name="page"/> works, but it does not mean what its name implies.</b> Measured
    /// 2026-08-29: two <c>page=0</c> calls in one burst returned identical sets, and pages two apart are
    /// disjoint — so paging is real. But <b>adjacent pages overlap</b>: page 0 against page 1 shared
    /// <b>28 of 100</b> rows and page 1 against page 2 shared <b>21</b>. The stride is roughly 72–79 rows
    /// against a page size of 100, and the union of pages 0, 1 and 2 was <b>251 distinct rows of
    /// 300</b>.</para>
    ///
    /// <para>So a caller enumerating this feed must <b>de-duplicate</b>, on
    /// <c>(Symbol, FiscalYear, Period, Date)</c> — the tuple measured unique within all four pages taken. The
    /// SDK does not do it: hiding the overlap would mean buffering pages and guessing when to stop.</para>
    ///
    /// <para><b>The bare call is not <c>page=0</c>.</b> Issued at the same instant on 2026-08-29 they shared
    /// 71 of 100 rows. Omitting <paramref name="page"/> is its own query rather than a synonym for
    /// zero.</para>
    ///
    /// <para><b>The feed churns on a timescale of tens of minutes</b> — two bare calls twenty minutes apart
    /// shared 90 of 100 rows. That, and not the page overlap, is why nothing may be asserted by
    /// index against this endpoint.</para>
    ///
    /// <para>The response is global: measured 2026-08-29 the first page carried Tokyo, Shanghai and Oslo
    /// tickers, and was not sorted by date.</para></summary>
    /// <param name="limit">Rows per page. Omit for FMP's own default of 100. Values above
    /// <see cref="MaxLatestTranscriptPageSize"/> are clamped by FMP without saying so, which is why this
    /// method rejects them instead.</param>
    /// <param name="page">Zero-based page index — with the overlap described above. A page past the end
    /// answers an empty list, not an error.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The page's transcripts, unsorted and possibly overlapping the adjacent page. Never
    /// <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="page"/> is negative, or
    /// <paramref name="limit"/> is outside 1 to <see cref="MaxLatestTranscriptPageSize"/>.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403.</exception>
    public Task<IReadOnlyList<LatestTranscript>> GetLatestAsync(
        int? limit = null, int? page = null, CancellationToken ct = default)
    {
        if (page is { } p) ArgumentOutOfRangeException.ThrowIfNegative(p, nameof(page));
        if (limit is { } l)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(l, nameof(limit));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(l, MaxLatestTranscriptPageSize, nameof(limit));
        }

        return transport.GetListAsync(
            new FmpRequest("stable/earning-call-transcript-latest").With("limit", limit).With("page", page),
            FmpJsonContext.Default.ListLatestTranscript, ct);
    }
}
