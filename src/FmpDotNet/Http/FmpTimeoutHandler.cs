using Microsoft.Extensions.Options;
using NodaTime;

namespace FmpDotNet.Http;

/// <summary>Bounds one FMP HTTP attempt, and — the part that matters more than the bound — reports the expiry as a
/// <see cref="TimeoutException"/> rather than as a cancellation.
///
/// <para><b>Why not just set <c>HttpClient.Timeout</c>.</b> A client-timeout expiry surfaces as
/// <see cref="TaskCanceledException"/> — an <see cref="OperationCanceledException"/> — carrying a
/// <see cref="TimeoutException"/> only as its InnerException. Callers routinely treat an
/// <c>OperationCanceledException</c> as "the host is shutting down" and let it escape a per-item handler, where it
/// aborts the enclosing loop and is then swallowed as a shutdown signal without logging. A single slow response
/// then does not merely occupy a worker: it ends the whole run, silently. A genuine cancellation from the caller
/// still surfaces as an <c>OperationCanceledException</c> and still aborts the run, which is correct.</para>
///
/// <para><b>Ordering is part of the contract.</b> This handler is registered INSIDE the rate-limit handler, so its
/// clock starts after the shared token bucket has granted a token. Time spent under back-pressure is not time the
/// upstream is failing to answer, and with a 429 able to hold the bucket for up to
/// <see cref="FmpOptions.MaxRetryAfter"/>, counting it here would turn our own throttle into a burst of abandoned
/// requests.</para></summary>
public abstract class FmpTimeoutHandlerBase(Duration timeout) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // ToTimeSpan at the BCL boundary only — CancelAfter has no NodaTime overload.
        cts.CancelAfter(timeout.ToTimeSpan());
        try
        {
            return await base.SendAsync(request, cts.Token).ConfigureAwait(false);
        }
        // Ours, not the caller's: the linked source fired while `ct` is still un-cancelled. Checking `ct` rather
        // than `cts.Token` is what keeps a real shutdown distinguishable — both cancel the same linked token.
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"FMP request timed out after {timeout.TotalSeconds:0.###}s: {Describe(request)}.",
                ex);
        }
    }

    /// <summary>Renders a request for an exception message with the API key removed.
    ///
    /// <para><b>This is not defensive tidiness; without it this handler leaks the key.</b> The transport puts the
    /// key in the query string, because that is how FMP authenticates, so <c>RequestUri</c> carries it — and an
    /// exception message is the one place a URI reliably escapes into a log, a crash report or an error surfaced
    /// to a user. <see cref="FmpRequest.ToString"/> is key-free for exactly this reason, but by the time a
    /// DelegatingHandler sees the request that structure is gone and only the built URI remains, so the redaction
    /// has to happen again here.</para>
    ///
    /// <para>The rest of the query is kept deliberately: which symbol, which period, which date range is what
    /// makes a timeout diagnosable, and dropping the whole query to be safe would trade the entire diagnostic for
    /// a secret that can be removed on its own.</para></summary>
    private static string Describe(HttpRequestMessage request)
    {
        if (request.RequestUri is not { } uri) return request.Method.ToString();

        var text = uri.ToString();
        const string marker = "apikey=";
        var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return $"{request.Method} {text}";

        var valueStart = start + marker.Length;
        var end = text.IndexOf('&', valueStart);
        var tail = end < 0 ? string.Empty : text[end..];
        return $"{request.Method} {text[..valueStart]}[redacted]{tail}";
    }
}

/// <summary>Applies <see cref="FmpOptions.RequestTimeout"/> to ordinary endpoints.</summary>
public sealed class FmpTimeoutHandler(IOptions<FmpOptions> options)
    : FmpTimeoutHandlerBase(options.Value.RequestTimeout);

/// <summary>Applies <see cref="FmpOptions.BulkRequestTimeout"/> to the <c>*-bulk</c> endpoints, whose payloads run
/// to tens of megabytes and will not fit the ordinary budget.</summary>
public sealed class FmpBulkTimeoutHandler(IOptions<FmpOptions> options)
    : FmpTimeoutHandlerBase(options.Value.BulkRequestTimeout);
