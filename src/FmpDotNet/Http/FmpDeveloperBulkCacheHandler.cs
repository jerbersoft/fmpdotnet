using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FmpDotNet.Http;

/// <summary>Replays bulk responses from disk while developing against the <c>*-bulk</c> endpoints. Off unless
/// <see cref="FmpOptions.DeveloperBulkCacheDirectory"/> is set.
///
/// <para><b>This is not a caching layer and must never be treated as one.</b> Entries never expire, nothing is
/// invalidated, nothing is bounded, and a stale entry is served forever. It exists for one job: working on a CSV
/// mapper without re-downloading tens of megabytes on every iteration.</para>
///
/// <para><b>Why the risk is real enough to need this.</b> Bulk is throttled separately and far more tightly than
/// the ordinary endpoints — measured 2026-08-26, a second call moments after the first was already refused — and
/// FMP's own error text warns that "frequent abuse on this API Endpoint may result in restrictions placed on this
/// API Key". Discovering the column names for one endpoint costs one call; iterating on the mapper it feeds costs
/// one more each time, against payloads reaching 69 MB. FMP refreshes bulk data once every few hours, so the
/// repeat calls buy nothing and spend the key's standing.</para>
///
/// <para><b>Placement is outermost, before the throttle</b>, which is the entire point: a replay must not consume
/// a bulk token or a timeout budget. A cache hit never reaches the rate limiter at all.</para>
///
/// <para><b>Memory stays flat.</b> A miss is streamed straight to a temporary file and the response is then served
/// from that file, so a 69 MB body is never held in memory on either path.</para></summary>
public sealed class FmpDeveloperBulkCacheHandler(
    IOptions<FmpOptions> options,
    ILogger<FmpDeveloperBulkCacheHandler> logger) : DelegatingHandler
{
    private static int _announced;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var directory = options.Value.DeveloperBulkCacheDirectory;
        if (string.IsNullOrWhiteSpace(directory) || request.RequestUri is null)
            return await base.SendAsync(request, ct).ConfigureAwait(false);

        Announce(directory);

        var body = Path.Combine(directory, FileName(request.RequestUri));
        var meta = body + ".mediatype";

        if (File.Exists(body))
        {
            logger.LogInformation("Serving {Uri} from the developer bulk cache ({Path}). Delete the file to refetch.",
                UriRedaction.Redact(request.RequestUri), body);
            return Replay(body, MediaTypeOf(meta), request, deleteOnClose: false);
        }

        var response = await base.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return response;

        Directory.CreateDirectory(directory);
        // A unique name per attempt, not a fixed "<name>.partial". Two reasons, one of them found by a test: a
        // rejected payload is served from its own temporary file with DeleteOnClose, which still holds the handle
        // while the caller reads, so a fixed name makes the very next fetch fail with a sharing violation. It also
        // makes two concurrent fetches of the same URL safe instead of racing over one file.
        var partial = $"{body}.{Guid.NewGuid():n}.partial";

        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var destination = File.Create(partial))
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        response.Dispose();

        // FMP answers some failures with HTTP 200 and a JSON error envelope. Caching one would replay the failure
        // forever, and the developer would be debugging a mapper against an error document. Serve it, do not keep
        // it — DeleteOnClose removes the temporary file once the caller has finished reading.
        if (LooksLikeAnErrorEnvelope(partial, mediaType))
        {
            logger.LogWarning("Not caching {Uri}: the response looks like an error payload rather than bulk data.",
                UriRedaction.Redact(request.RequestUri));
            return Replay(partial, mediaType, request, deleteOnClose: true);
        }

        // Move rather than write in place, so an interrupted download cannot leave a truncated file that every
        // later run would replay as if it were complete.
        File.Move(partial, body, overwrite: true);
        if (mediaType is not null) await File.WriteAllTextAsync(meta, mediaType, ct).ConfigureAwait(false);

        logger.LogInformation("Cached {Uri} to {Path}.", UriRedaction.Redact(request.RequestUri), body);
        return Replay(body, mediaType, request, deleteOnClose: false);
    }

    /// <summary>Says once, loudly, that responses are not coming from FMP. A cache that can be on without anyone
    /// noticing is how a stale entry becomes a bug report against the upstream.</summary>
    private void Announce(string directory)
    {
        if (Interlocked.Exchange(ref _announced, 1) == 1) return;
        logger.LogWarning(
            "FMP bulk responses are being served from the DEVELOPER cache at {Directory}. Entries never expire and "
            + "are never invalidated. This is a development aid, not a caching layer — delete the directory to "
            + "refetch, and clear Fmp:DeveloperBulkCacheDirectory to disable it.",
            directory);
    }

    private static HttpResponseMessage Replay(string path, string? mediaType, HttpRequestMessage request, bool deleteOnClose)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024,
            deleteOnClose ? FileOptions.DeleteOnClose | FileOptions.Asynchronous : FileOptions.Asynchronous);

        var content = new StreamContent(stream);
        // The content type is preserved because the CSV pipeline reads it: a bulk body arriving as
        // "application/json" is how the transport recognises an error envelope, so replaying a CSV without its
        // type would change how the response is interpreted.
        if (mediaType is not null)
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = content,
            RequestMessage = request,
        };
    }

    private static string? MediaTypeOf(string metaPath)
        => File.Exists(metaPath) ? File.ReadAllText(metaPath).Trim() : null;

    private static bool LooksLikeAnErrorEnvelope(string path, string? mediaType)
    {
        if (mediaType is "application/json") return true;

        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[8];
        var read = stream.Read(head);
        foreach (var b in head[..read])
        {
            if (b is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t') continue;
            return b == (byte)'{';
        }
        return false;   // empty body: not an envelope, and an empty part is a real terminator for the part walk
    }

    /// <summary>A file name that a developer can recognise and that cannot collide.
    ///
    /// <para>The API key is stripped BEFORE the name is derived. It would otherwise land in a filename on disk and
    /// in every log line quoting the path — the same leak the timeout handler had, arriving by a different
    /// route.</para></summary>
    private static string FileName(Uri uri)
    {
        var safe = UriRedaction.Redact(uri);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(safe)))[..12];
        var leaf = uri.AbsolutePath.TrimEnd('/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];
        if (leaf.Length == 0) leaf = "response";
        return $"{leaf}_{hash}";
    }
}
