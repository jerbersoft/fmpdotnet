using System.Net;
using System.Runtime.CompilerServices;
using FmpDotNet.Models;
using NodaTime;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Bulk</c> group — whole-universe CSV downloads.
///
/// <para>Every method here streams. Measured 2026-08-26, one bulk response reaches 69 MB and three of them send no
/// <c>Content-Length</c>, so there is no size at which buffering is safe.</para>
///
/// <para>These endpoints are throttled separately from the account's per-minute cap and much more tightly — a
/// second call moments after the first was already refused, and FMP's own error text warns that frequent use may
/// get the key restricted. The data behind them refreshes only once every few hours, so treat a successful
/// download as something to cache, not something to repeat.</para>
///
/// <para><b>Plan gating on this surface is not settled.</b> <c>profile-bulk</c> was recorded as 402-on-Premium by
/// the application this SDK replaces and answered 200 when re-probed on 2026-08-26. A 402 or 403 arrives as
/// <see cref="FmpPlanRestrictedException"/> on the first <c>MoveNextAsync</c> rather than as an empty stream, and
/// that asymmetry with <c>CompanyEndpoints.TryGetAllSharesFloatAsync</c> — which returns null for the same status —
/// is deliberate. An empty stream would be indistinguishable from a genuinely empty universe, and "a paywalled
/// endpoint reading as an empty result" is the exact defect the caller-side history records. Catch the exception to
/// fall back to the per-symbol path; do not infer entitlement from a row count.</para></summary>
public sealed class BulkEndpoints(FmpBulkTransport transport)
{
    /// <summary>Streams end-of-day bars for every symbol FMP covers on <paramref name="date"/>.</summary>
    /// <exception cref="FmpApiException">The bulk throttle refused the call — which arrives as HTTP 200 carrying a
    /// JSON error body, not as a 429.</exception>
    public IAsyncEnumerable<BulkEndOfDayPrice> StreamEndOfDayAsync(LocalDate date, CancellationToken ct = default) =>
        transport.StreamCsvAsync(
            new FmpRequest("stable/eod-bulk").With("date", date),
            BulkEndOfDayPrice.FromCsv, ct);

    /// <summary>Streams one <paramref name="part"/> of <c>stable/profile-bulk</c> — the whole-universe company
    /// profile download, in CSV.
    ///
    /// <para><b>Why this streams and cannot be buffered.</b> Measured 2026-08-26, <c>part=0</c> answered
    /// <b>30,467,596 bytes</b> across 22,857 lines with <b>no <c>Content-Length</c></b> — the response is chunked,
    /// so nothing can pre-size a buffer and no threshold exists at which "small enough to buffer" could be decided.
    /// The 36 columns are mostly one field: <see cref="BulkCompanyProfile.Description"/> runs past 1,500 characters
    /// per row. A caller that wants only the classification fields still pays to stream the descriptions past,
    /// which is why the per-symbol <c>stable/profile</c> remains the right call for a handful of
    /// symbols.</para>
    ///
    /// <para><b>The caller supplies the part.</b> There is no total to ask for: FMP publishes no part count, no
    /// <c>Link</c> header and no terminator row, and a part that does not exist answers a 400 rather than an empty
    /// body — see <see cref="StreamAllProfilesAsync"/>, which walks the parts on a documented heuristic. This method
    /// makes no guess: it fetches exactly the part named and lets every failure through unchanged, so it is the
    /// place to go when you need to see why a part was refused.</para>
    ///
    /// <para><b>Rows are not in symbol order.</b> Measured <c>part=0</c> opens <c>PRTA, PRDO, MRV.TO</c> — a
    /// Nasdaq biotech, a Nasdaq education company and a Toronto listing — so a part is a shard of the universe with
    /// no ordering a caller can exploit, and part 0 is not "the first symbols alphabetically". Compare
    /// <c>shares-float-all</c>, whose pages <i>are</i> symbol-ordered and whose page 0 is therefore all Shenzhen.
    /// Neither is a sample of the universe; both need every page read.</para></summary>
    /// <param name="part">Zero-based part index. Parts 0 and 1 were measured to exist on 2026-08-26; 99 did
    /// not.</param>
    /// <param name="ct">Cancels the download mid-stream.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="part"/> is negative — checked before a request
    /// is spent, since the bulk throttle makes a wasted call expensive.</exception>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403 — see the note on the type about why
    /// this is an exception here and a null elsewhere.</exception>
    /// <exception cref="FmpApiException">FMP refused the request. Two distinguishable cases, told apart by
    /// <see cref="FmpApiException.StatusCode"/>: a <see cref="HttpStatusCode.BadRequest"/> carrying
    /// <c>Query Error: Invalid or missing query parameter - part</c> means the part index is out of range, while a
    /// null status means the bulk throttle refused the call — which arrives as HTTP 200 with a JSON error body, not
    /// as a 429.</exception>
    public IAsyncEnumerable<BulkCompanyProfile> StreamProfilesAsync(int part, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(part);
        return transport.StreamCsvAsync(
            new FmpRequest("stable/profile-bulk").With("part", part),
            BulkCompanyProfile.FromCsv, ct);
    }

    /// <summary>Walks <c>stable/profile-bulk</c> from part 0 upwards and streams every row of every part as one
    /// sequence.
    ///
    /// <para><b>The termination rule is a heuristic, and this is the paragraph that says so.</b> FMP gives no way
    /// to ask how many parts there are and no empty-response terminator: measured 2026-08-26, <c>part=0</c> and
    /// <c>part=1</c> both answered HTTP 200 with data, and <c>part=99</c> answered <b>HTTP 400</b> with the
    /// plain-text body <c>Query Error: Invalid or missing query parameter - part</c>. So the only signal that the
    /// parts have run out is an error status — and a 400 saying "invalid or missing query parameter" could equally
    /// mean the parameter was malformed. This walk is entitled to read it as "past the last part" for one reason
    /// only: <b>the SDK controls the value it sent.</b> <paramref name="ct"/> aside, the sole query parameter is a
    /// non-negative integer this method generated, so "malformed" is not a live possibility for the request it
    /// actually made. If FMP ever changes <c>part</c>'s spelling or adds a required companion parameter, that
    /// reasoning fails and this method will report an empty universe instead of an error. That is the risk being
    /// accepted, and it is why <see cref="StreamProfilesAsync"/> exists alongside it.</para>
    ///
    /// <para>Two guards narrow it:</para>
    /// <list type="bullet">
    /// <item><description>A 400 on <b>part 0</b> is <i>not</i> swallowed — it is rethrown. Part 0 was measured to
    /// exist, so a 400 there cannot mean "past the last part" and almost certainly means the request shape changed.
    /// Only a 400 on a part after at least one has been read ends the walk.</description></item>
    /// <item><description>A part that yields <b>zero data rows</b> also ends the walk. Nothing measured behaves that
    /// way; it is there so that an upstream that starts answering 200-with-header-only cannot spin this into an
    /// unbounded loop against an endpoint whose throttle is measured in calls per hour.</description></item>
    /// </list>
    ///
    /// <para><b>Every other failure propagates.</b> Plan gating (402/403), rate limiting (429) and the bulk
    /// throttle's HTTP-200-with-a-JSON-body all surface as their own exceptions mid-walk, so a partial result is
    /// never silently returned as a complete one. Because the bulk throttle refuses calls made moments apart, a
    /// caller pacing this walk itself — rather than letting it run flat out — is the difference between finishing
    /// and being refused on part 1.</para></summary>
    /// <param name="ct">Cancels the walk between parts as well as mid-part.</param>
    public async IAsyncEnumerable<BulkCompanyProfile> StreamAllProfilesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var part = 0; ; part++)
        {
            var rows = 0;
            var exhausted = false;
            var enumerator = StreamProfilesAsync(part, ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (FmpApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && part > 0)
                    {
                        exhausted = true;
                        break;
                    }
                    if (!moved) break;
                    rows++;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (exhausted || rows == 0) yield break;
        }
    }
}
