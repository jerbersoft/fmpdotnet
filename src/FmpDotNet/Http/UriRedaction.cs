using System.Text;

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

    private const string Redacted = "[redacted]";

    /// <summary>The redacted rendering of <paramref name="uri"/>.
    ///
    /// <para>Only the key's value is replaced. The rest of the query — which symbol, which period, which year — is
    /// what makes a log line or an exception useful, and dropping all of it to be safe would trade the whole
    /// diagnostic for a secret that can be removed on its own.</para>
    ///
    /// <para><b>The match is anchored to a parameter boundary, and every occurrence is replaced. Both of those are
    /// load-bearing, and the earlier version of this method did neither.</b> Parameter names are caller-controlled
    /// — <see cref="FmpRequest"/> is public precisely so an endpoint the SDK has not modelled yet is still
    /// reachable — and the transport appends the key LAST. So a plain substring search finds <c>apikey=</c> inside
    /// a preceding <c>xapikey=</c> and redacts the decoy, while a search that stopped at the first match would
    /// redact a caller's own <c>apikey</c> parameter and leave the appended one. Either way the credential
    /// survives into whatever log, filename or exception message the caller was told was safe.</para></summary>
    internal static string Redact(Uri? uri)
    {
        if (uri is null) return string.Empty;

        var text = uri.ToString();
        var builder = new StringBuilder(text.Length);
        var copied = 0;

        for (var search = 0; search < text.Length;)
        {
            var start = text.IndexOf(Marker, search, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            search = start + Marker.Length;

            // A query parameter starts after '?' or '&'. At index 0 there is no query at all, so a marker there
            // is part of the path rather than a credential.
            if (start == 0 || (text[start - 1] != '?' && text[start - 1] != '&')) continue;

            var valueStart = start + Marker.Length;
            var valueEnd = text.IndexOf('&', valueStart);
            if (valueEnd < 0) valueEnd = text.Length;

            builder.Append(text, copied, valueStart - copied).Append(Redacted);
            copied = search = valueEnd;
        }

        return copied == 0 ? text : builder.Append(text, copied, text.Length - copied).ToString();
    }
}
