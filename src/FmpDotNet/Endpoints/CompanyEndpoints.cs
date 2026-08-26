using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Company</c> group — profiles and the identifiers hanging off them.</summary>
public sealed class CompanyEndpoints(FmpTransport transport)
{
    /// <summary>Company profile for one symbol, or null when FMP knows no such symbol.
    ///
    /// <para><c>stable/profile</c> answers a single-element array rather than an object, and an unknown symbol
    /// answers an empty array rather than a 404 — so "not found" is a shape, not a status code.</para></summary>
    public async Task<CompanyProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/profile").With("symbol", symbol),
            FmpJsonContext.Default.ListCompanyProfile, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>Public float and shares outstanding for one symbol, or null when FMP knows no such symbol.
    ///
    /// <para>Single object rather than a list, and no <c>limit</c> parameter, because <c>stable/shares-float</c>
    /// holds no history: measured 2026-08-26, it answers exactly one row and silently ignores <c>limit</c>. As with
    /// <see cref="GetProfileAsync"/>, an unknown symbol answers an empty array with HTTP 200 rather than a 404 — and
    /// so does a class-share ticker spelled with a dot, which FMP wants hyphenated (<c>BRK-B</c>, not
    /// <c>BRK.B</c>).</para></summary>
    public async Task<SharesFloat?> GetSharesFloatAsync(string symbol, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/shares-float").With("symbol", symbol),
            FmpJsonContext.Default.ListSharesFloat, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
}
