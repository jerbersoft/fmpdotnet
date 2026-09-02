using System.Text;
using NodaTime;
using NodaTime.Text;

namespace FmpDotNet;

/// <summary>A path on the FMP API plus its query parameters.
///
/// <para>Callers never build a URL string. The API key is not part of this type — the transport sends it as a
/// request header — so a request can be logged, compared or cached without carrying a credential.</para></summary>
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

    /// <summary>Adds an instant as Unix <b>seconds</b>, which is the unit FMP reads a <c>timestamp</c> in.
    ///
    /// <para>The unit is the whole reason this overload exists rather than an <see cref="long"/> one. Measured
    /// 2026-09-02 on <c>stable/all-exchange-market-hours</c>: the current instant in seconds answered 26 open
    /// exchanges, byte-identical to the unfiltered call, while the same instant in <i>milliseconds</i> answered
    /// <b>0 open and 64 <c>CLOSED</c></b> at HTTP 200 — a date some fifty thousand years out, served as a
    /// well-formed answer. A caller holding an <see cref="Instant"/> cannot make that mistake; a caller holding
    /// a <see cref="long"/> makes it by default. Sub-second precision is truncated, not rounded: the same
    /// second is the same second.</para></summary>
    public FmpRequest With(string name, Instant? value) =>
        With(name, value?.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Renders path and query — exactly the relative URI the transport sends. The API key is never part
    /// of it, so the rendering is safe to log.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder(Path);
        var first = true;
        foreach (var (name, value) in _query)
        {
            sb.Append(first ? '?' : '&').Append(Uri.EscapeDataString(name)).Append('=')
              .Append(Uri.EscapeDataString(value));
            first = false;
        }
        return sb.ToString();
    }
}
