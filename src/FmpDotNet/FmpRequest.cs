using System.Text;
using NodaTime;
using NodaTime.Text;

namespace FmpDotNet;

/// <summary>A path on the FMP API plus its query parameters.
///
/// <para>Callers never build a URL string. The API key is not part of this type — the transport appends it — so a
/// request can be logged, compared or cached without carrying a credential.</para></summary>
public sealed class FmpRequest
{
    private readonly List<KeyValuePair<string, string>> _query = [];

    /// <summary>Creates a request for a path such as <c>stable/profile</c>. A leading slash is accepted and
    /// trimmed, because the base address is the bare host and the <c>/stable/</c> segment belongs to the path.</summary>
    public FmpRequest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path.TrimStart('/');
    }

    /// <summary>The path, without a leading slash.</summary>
    public string Path { get; }

    /// <summary>Adds a query parameter. A null value is dropped, so optional arguments need no branching at the
    /// call site.</summary>
    public FmpRequest With(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) _query.Add(new(name, value));
        return this;
    }

    /// <summary>Adds an integer query parameter.</summary>
    public FmpRequest With(string name, int? value) =>
        With(name, value?.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Adds a date query parameter in FMP's <c>yyyy-MM-dd</c> form.</summary>
    public FmpRequest With(string name, LocalDate? value) =>
        With(name, value is null ? null : LocalDatePattern.Iso.Format(value.Value));

    /// <summary>Adds a boolean query parameter as lowercase <c>true</c>/<c>false</c>.</summary>
    public FmpRequest With(string name, bool? value) => With(name, value is null ? null : value.Value ? "true" : "false");

    /// <summary>Renders path and query without the API key — safe to log.</summary>
    public override string ToString() => Build(apiKey: null);

    internal string Build(string? apiKey)
    {
        var sb = new StringBuilder(Path);
        var first = true;
        foreach (var (name, value) in _query)
        {
            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(name)).Append('=')
              .Append(Uri.EscapeDataString(value));
            first = false;
        }
        if (apiKey is not null)
            sb.Append(first ? '?' : '&').Append("apikey=").Append(Uri.EscapeDataString(apiKey));
        return sb.ToString();
    }
}
