using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Directory</c> group — the reference vocabularies the rest of the API classifies against.
///
/// <para>Both endpoints below take no arguments beyond the API key and answer a flat list of labels. They are the
/// authoritative spelling of the <c>sector</c> and <c>industry</c> values that come back on
/// <see cref="CompanyEndpoints.GetProfileAsync(string, CancellationToken)"/> and on the screener, so a caller
/// building a lookup table or validating user input should take them from here rather than hard-coding a list that
/// silently rots when FMP adds a category.</para></summary>
public sealed class DirectoryEndpoints(FmpTransport transport)
{
    /// <summary>Every sector FMP classifies against, in the order the API returns them.
    ///
    /// <para>Measured on 2026-08-26: <c>stable/available-sectors</c> answers exactly 11 rows, each a
    /// single-property object under the key <c>sector</c>, and they happen to arrive alphabetically. The SDK does
    /// not sort them anyway — see <see cref="GetIndustriesAsync(CancellationToken)"/>, whose sibling endpoint
    /// proves the wire order is meaningful.</para></summary>
    public async Task<IReadOnlyList<string>> GetSectorsAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-sectors"),
            FmpJsonContext.Default.ListSectorName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Sector);
    }

    /// <summary>Every industry FMP classifies against, in the order the API returns them.
    ///
    /// <para>Measured on 2026-08-26: <c>stable/available-industries</c> answers exactly 159 rows under the key
    /// <c>industry</c>, and they are <b>not</b> alphabetical — they are grouped by sector, running
    /// <c>Steel, Silver, Other Precious Metals, Gold, Copper…</c> through to
    /// <c>…Diversified Utilities, General Utilities</c>. That grouping is the only signal in the response that says
    /// which sector an industry belongs to, since no row carries a sector field, so the order is data and the SDK
    /// preserves it.</para></summary>
    public async Task<IReadOnlyList<string>> GetIndustriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-industries"),
            FmpJsonContext.Default.ListIndustryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Industry);
    }

    /// <summary>Unwraps the single-property rows into their labels. Written once so the two endpoints cannot drift
    /// apart on the three judgement calls below.
    ///
    /// <para><b>Blanks are dropped.</b> Nothing in either measured payload was null, empty or padded, but a label
    /// is a key: a caller cannot see the payload, and an empty string entering a lookup table becomes a phantom
    /// category that matches nothing and is invisible in a diff. Whitespace is trimmed for the same reason — a
    /// trailing space turns an equality test against <c>"Technology"</c> into a silent miss.</para>
    ///
    /// <para><b>Duplicates are kept.</b> Deliberate, and the opposite of the blank rule: a blank label carries no
    /// information, whereas a repeated one carries the fact that upstream now repeats it. De-duplicating would
    /// change the cardinality of a directory response without saying so, hiding an upstream change behind an SDK
    /// that looks correct. Which duplicates mean — a data fault to report, or two spellings to merge — is the
    /// caller's policy, and <c>Distinct()</c> is one call away for callers who want it.</para>
    ///
    /// <para><b>Order is preserved.</b> See <see cref="GetIndustriesAsync(CancellationToken)"/>.</para></summary>
    private static IReadOnlyList<string> Labels<T>(IReadOnlyList<T?> rows, Func<T, string?> label)
        where T : class
    {
        var names = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            // A literal null element is possible in JSON even though neither capture contained one, and reaching
            // through it here would turn a cosmetic upstream glitch into a NullReferenceException in the caller.
            if (row is null) continue;
            var name = label(row)?.Trim();
            if (!string.IsNullOrEmpty(name)) names.Add(name);
        }
        return names;
    }
}
