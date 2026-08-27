using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Endpoints;

/// <summary>FMP's <c>Directory</c> group — what exists: the securities the API knows about, and the reference
/// vocabularies it classifies them against.
///
/// <para><b>The vocabularies.</b> <see cref="GetSectorsAsync(CancellationToken)"/> and
/// <see cref="GetIndustriesAsync(CancellationToken)"/> answer a flat list of labels. They are the authoritative
/// spelling of the <c>sector</c> and <c>industry</c> values that come back on
/// <see cref="CompanyEndpoints.GetProfileAsync(string, CancellationToken)"/> and on the screener, so a caller
/// building a lookup table or validating user input should take them from here rather than hard-coding a list that
/// silently rots when FMP adds a category.</para>
///
/// <para><b>The universe.</b> <see cref="GetStockListAsync(CancellationToken)"/> and
/// <see cref="GetActivelyTradingAsync(CancellationToken)"/> answer the symbol directory itself. Measured
/// 2026-08-26, the actively-trading list is a strict subset of the stock list — 68,869 of 91,844 symbols, with
/// <b>zero</b> symbols on the trading list absent from the full list — so the difference between them, 22,975
/// symbols, is exactly the set FMP knows but does not consider actively trading.</para>
///
/// <para>All four take no arguments beyond the API key, and the two directories <b>ignore <c>limit</c></b>: asking
/// for five symbols still transfers all 68,869 or 91,844 of them, 5.3 MB and 7.7 MB respectively. There is no
/// sampling call and no paging on these; the alternative when a full download is too much is the screener, which
/// does honour <c>limit</c>.</para></summary>
public sealed class DirectoryEndpoints(FmpTransport transport)
{
    /// <summary>Every sector FMP classifies against, in the order the API returns them.
    ///
    /// <para>Measured on 2026-08-26: <c>stable/available-sectors</c> answers exactly 11 rows, each a
    /// single-property object under the key <c>sector</c>, and they happen to arrive alphabetically. The SDK does
    /// not sort them anyway — see <see cref="GetIndustriesAsync(CancellationToken)"/>, whose sibling endpoint
    /// proves the wire order is meaningful.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
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
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetIndustriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-industries"),
            FmpJsonContext.Default.ListIndustryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Industry);
    }

    /// <summary>Every country FMP classifies an exchange against, as ISO 3166-1 alpha-2 codes — 117 of them
    /// measured 2026-08-27.
    ///
    /// <para><b>Codes, not names.</b> The wire key is <c>country</c> and the values are <c>"FK"</c>, <c>"MT"</c>,
    /// <c>"SG"</c> — two characters on every measured row. A caller rendering these to a user needs a lookup;
    /// <c>GetExchangesAsync</c> carries both spellings of the same fact and is the cheapest join for it.</para>
    ///
    /// <para>Ignores <c>limit</c>, like every list endpoint in this group except <c>cik-list</c> and
    /// <c>symbol-change</c>. Order is the wire order, unsorted — see <see cref="Labels{T}"/>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<string>> GetCountriesAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/available-countries"),
            FmpJsonContext.Default.ListCountryName, ct).ConfigureAwait(false);
        return Labels(rows, static r => r.Country);
    }

    /// <summary>Every ETF symbol FMP carries — 14,567 measured 2026-08-27.
    ///
    /// <para><b>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>.</b> All 14,567 appeared in
    /// that endpoint's 91,845, none outside it — the same relation already measured for
    /// <see cref="GetActivelyTradingAsync(CancellationToken)"/>. So this is a filter of the universe rather than a
    /// separate one, and a caller holding the stock list already has these rows; what this endpoint adds is
    /// knowing <i>which</i> of them are funds, which no field on the stock list says.</para>
    ///
    /// <para><b>The name arrives under <c>name</c>, not <c>companyName</c></b> — the <c>actively-trading-list</c>
    /// spelling rather than the <c>stock-list</c> one, which is why this reuses that endpoint's wire shape. Both
    /// unwrap to <see cref="CompanySymbol"/>; see that type for why the SDK absorbs the inconsistency instead of
    /// publishing it.</para>
    ///
    /// <para>Ignores <c>limit</c>: asking for 5 rows still transfers all 14,567. Order is the wire order.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetEtfListAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/etf-list"),
            FmpJsonContext.Default.ListActivelyTradingRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.Name);
    }

    /// <summary>Every symbol FMP carries, listed or not — 91,844 of them measured on 2026-08-26, 7.7 MB of JSON.
    ///
    /// <para><b>The obvious name for this endpoint 404s.</b> <c>stable/company-symbol-list</c> appears in older
    /// FMP material and reads like the natural spelling; re-probed on 2026-08-26 it answers <b>HTTP 404 with the
    /// body <c>[]</c></b>. That pairing is the trap: a caller who checks only that the body parses as a JSON array
    /// sees an empty universe and concludes FMP has no symbols, rather than that the path is wrong. The working
    /// paths are this one and <c>stable/actively-trading-list</c>. The SDK reads the status first, so through
    /// <see cref="FmpTransport"/> the 404 surfaces as an <see cref="FmpApiException"/> naming the status — but a
    /// caller reaching FMP directly, or re-deriving the path from documentation, will meet the empty array.</para>
    ///
    /// <para><b>This is a superset of <see cref="GetActivelyTradingAsync(CancellationToken)"/>, not a different
    /// list.</b> Every one of that endpoint's 68,869 symbols appeared here; the 22,975 extra rows are the
    /// non-trading remainder. A caller who wants "everything FMP knows" wants this; a caller who wants "what is
    /// currently trading" wants the other, and must not filter this one on a guess about what counts.</para>
    ///
    /// <para><b>The list moves under you.</b> Two calls eight minutes apart on 2026-08-26 answered 91,844 and
    /// 91,845 rows. A diff between two downloads is measuring FMP's churn as much as anything else, which matters
    /// for a caller using set difference as a delisting signal: a single-call disappearance is not evidence.</para>
    ///
    /// <para>Order is the wire order, unsorted — see <see cref="Symbols{T}"/>.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetStockListAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/stock-list"),
            FmpJsonContext.Default.ListStockListRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.CompanyName);
    }

    /// <summary>The symbols FMP considers actively trading — 68,869 of them measured on 2026-08-26, 5.3 MB of
    /// JSON.
    ///
    /// <para>A strict subset of <see cref="GetStockListAsync(CancellationToken)"/>: every symbol here appeared
    /// there, and the company names agreed character for character on all 68,869. The wire spells the name
    /// <c>name</c> here and <c>companyName</c> there; the SDK unwraps both into
    /// <see cref="CompanySymbol"/>.</para>
    ///
    /// <para><b>Absence is a weak signal on its own.</b> Using "symbol dropped off this list" as a delisting alarm
    /// is a reasonable design — it is why the endpoint is here — but the list churns between calls (see
    /// <see cref="GetStockListAsync(CancellationToken)"/>), so a single absence is noise. Confirm across several
    /// days, and note that <see cref="CompanyEndpoints.GetDelistedAsync(int, int, CancellationToken)"/> is the
    /// endpoint that carries an actual delisting <i>date</i> — this one carries only presence.</para>
    ///
    /// <para>Order is the wire order, unsorted, and it is <b>not</b> the same order as the stock list. The two
    /// broadly track each other — 99.9% of adjacent pairs here also move forward there — but they do diverge, so
    /// neither order may be assumed to be a filter of the other. Neither is alphabetical.</para></summary>
    /// <exception cref="FmpPlanRestrictedException">FMP answered 402 or 403. Read
    /// <see cref="FmpPlanRestrictedException.StatusCode"/> before reporting it as a plan limit — 403 points at
    /// the key at least as often as at the plan.</exception>
    public async Task<IReadOnlyList<CompanySymbol>> GetActivelyTradingAsync(CancellationToken ct = default)
    {
        var rows = await transport.GetListAsync(
            new FmpRequest("stable/actively-trading-list"),
            FmpJsonContext.Default.ListActivelyTradingRow, ct).ConfigureAwait(false);
        return Symbols(rows, static r => r.Symbol, static r => r.Name);
    }

    /// <summary>Unwraps the two directory row shapes into <see cref="CompanySymbol"/>. Written once so the pair
    /// cannot drift apart on the judgement calls below, the same way <see cref="Labels{T}"/> serves the
    /// vocabularies.
    ///
    /// <para><b>A blank symbol drops the row; a blank name does not.</b> The symbol is the entry — a row without
    /// one carries no information and would sit in a lookup table matching nothing. A row with a symbol and no
    /// name still tells the caller the symbol exists, and discarding it would quietly shrink a universe that
    /// callers use to decide what is listed. Both are trimmed: a padded ticker is a silent equality miss.</para>
    ///
    /// <para><b>Duplicates are kept and order is preserved</b>, for the reasons set out on
    /// <see cref="Labels{T}"/> — with one addition here. Neither directory is sorted, so preserving the wire order
    /// is not preserving a signal the way it is for industries; it is refusing to spend an O(n log n) sort on
    /// 91,844 rows to impose an order the caller may not want. <c>OrderBy</c> is one call away.</para></summary>
    private static IReadOnlyList<CompanySymbol> Symbols<T>(
        IReadOnlyList<T?> rows, Func<T, string?> symbol, Func<T, string?> name)
        where T : class
    {
        var symbols = new List<CompanySymbol>(rows.Count);
        foreach (var row in rows)
        {
            // As in Labels: a literal null element is legal JSON, and reaching through it here would turn a
            // cosmetic upstream glitch into a NullReferenceException in the caller.
            if (row is null) continue;
            var ticker = symbol(row)?.Trim();
            if (string.IsNullOrEmpty(ticker)) continue;
            var label = name(row)?.Trim();
            symbols.Add(new CompanySymbol { Symbol = ticker, Name = string.IsNullOrEmpty(label) ? null : label });
        }
        return symbols;
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
