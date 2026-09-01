using System.Text;

namespace FmpDotNet.Http;

/// <summary>Removes any <c>apikey</c> query parameter from a request URI.
///
/// <para><b>The SDK no longer puts the key there.</b> The transport sends it as a request header (issue #59,
/// measured 2026-09-01), so a URI the SDK built has nothing to redact. This exists for the URI the SDK did not
/// build: <see cref="FmpRequest"/> takes its path as a bare string precisely so an endpoint the SDK has not
/// modelled yet is still reachable, and every example URL in FMP's documentation ends in <c>&amp;apikey=</c>. A
/// caller who pastes one in has put a key on the URI, and three handlers render that URI: the retry handler into
/// a log line, the timeout handler into an exception message, and the developer bulk cache into a filename and a
/// log line. When the key was on every URI the first of those shipped leaking it. One implementation, tested
/// once, keeps the caller-supplied case from repeating that.</para></summary>
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
    /// reachable. So a plain substring search finds <c>apikey=</c> inside a preceding <c>xapikey=</c> and redacts
    /// the decoy, while a search that stopped at the first match would leave a second <c>apikey=</c> in place.
    /// Either way the credential survives into whatever log, filename or exception message the caller was told
    /// was safe.</para></summary>
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
