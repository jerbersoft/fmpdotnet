namespace FmpDotNet.Http;

/// <summary>Removes the API key from a request URI.
///
/// <para><b>Why this is shared rather than written where it is needed.</b> FMP authenticates by query string, so
/// every built URI carries the key. <see cref="FmpRequest.ToString"/> renders without it precisely so a request
/// can be logged safely — but that structure is gone by the time a <see cref="DelegatingHandler"/> sees the
/// request, and only the built URI remains. Two handlers have now needed to undo that independently: the timeout
/// handler, which puts the URI in an exception message, and the developer bulk cache, which puts it in a filename
/// and a log line. The first of those shipped leaking the key. One implementation, tested once, is what stops the
/// third one repeating it.</para></summary>
internal static class UriRedaction
{
    private const string Marker = "apikey=";

    /// <summary>The redacted rendering of <paramref name="uri"/>.
    ///
    /// <para>Only the key's value is replaced. The rest of the query — which symbol, which period, which year — is
    /// what makes a log line or an exception useful, and dropping all of it to be safe would trade the whole
    /// diagnostic for a secret that can be removed on its own.</para></summary>
    internal static string Redact(Uri? uri)
    {
        if (uri is null) return string.Empty;

        var text = uri.ToString();
        var start = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return text;

        var valueStart = start + Marker.Length;
        var end = text.IndexOf('&', valueStart);
        return text[..valueStart] + "[redacted]" + (end < 0 ? string.Empty : text[end..]);
    }
}
