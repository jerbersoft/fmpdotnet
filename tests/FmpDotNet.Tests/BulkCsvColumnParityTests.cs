using System.Collections;
using System.Text;
using FmpDotNet.Models;
using FmpDotNet.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Holds each bulk model to the CSV header it is read from (#55).
///
/// <para><b>This turns a hand-comparison into an assertion.</b> <c>BulkStatementFamilyTests</c> records the
/// reasoning for sharing one model between the JSON and CSV paths — "the two carry exactly the same 39 field
/// names, compared header against <c>[JsonPropertyName]</c> on 2026-08-26, so duplicating the model for the CSV
/// path would be 39 chances to drift for no gain". The reasoning is right and the sharing is the correct call.
/// Nothing enforced it: the CSV column names are string literals inside <c>FromCsv</c> method bodies, invisible
/// to reflection, so the comparison was a date and a claim.</para>
///
/// <para><b>What drift costs, and why it is silent.</b> <see cref="CsvRow"/> says it outright — "every accessor
/// returns null for an absent column and for an empty field, so a missing value and a blank one are treated
/// alike". So a column FMP renames does not throw, does not warn, and does not fail a spot-check that asserts
/// seven fields out of thirty-nine. It arrives as null on every row of a 41,000-row download, which reads as
/// "FMP does not report this" — the silent-wrong-answer shape this repository exists to catch.</para>
///
/// <para><b>The columns a model reads are recovered by watching it read them.</b> A <c>FromCsv</c> body cannot be
/// reflected over, but every accessor on <see cref="CsvRow"/> resolves its column through the
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> the row was constructed with. <see cref="RecordingColumns"/>
/// wraps the real header dictionary and notes every name looked up, hit or miss, so driving the model over one
/// real captured row yields exactly the set of columns it asks for. No production code is instrumented and
/// nothing about the parse changes.</para>
///
/// <para>Both directions are failures and they fail differently. A column in the header that the model never
/// reads is data FMP sends and this SDK discards. A column the model reads that is not in the header is a field
/// that binds to null on every row.</para></summary>
public class BulkCsvColumnParityTests
{
    /// <summary>An <see cref="IReadOnlyDictionary{TKey, TValue}"/> that answers exactly as the real header does
    /// and remembers what was asked for.
    ///
    /// <para>Only <see cref="TryGetValue"/> records, because that is the single entry point every
    /// <see cref="CsvRow"/> accessor funnels through — <c>GetDecimal</c>, <c>GetDate</c> and the rest all call
    /// <c>GetString</c> first. A miss is recorded as readily as a hit; a lookup for a column that is not there is
    /// the more interesting of the two.</para></summary>
    private sealed class RecordingColumns(IReadOnlyDictionary<string, int> inner)
        : IReadOnlyDictionary<string, int>
    {
        public HashSet<string> Requested { get; } = new(StringComparer.Ordinal);

        public bool TryGetValue(string key, out int value)
        {
            Requested.Add(key);
            return inner.TryGetValue(key, out value);
        }

        public int this[string key] => inner[key];
        public IEnumerable<string> Keys => inner.Keys;
        public IEnumerable<int> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Every bulk path's captured header, paired with the model the endpoint maps it through.
    ///
    /// <para>The pairs are the ones <c>BulkEndpoints</c> actually wires up. Nine of these models serve the JSON
    /// path too and carry <c>[JsonPropertyName]</c>; nine are CSV-only and carry none. That difference is why the
    /// check is written against the reading behaviour rather than against the attributes — it covers all
    /// eighteen either way.</para></summary>
    public static TheoryData<string, Func<CsvRow, object>> BulkFixtures => new()
    {
        { "balance-sheet-statement-bulk.head.csv", row => BalanceSheetStatement.FromCsv(row) },
        { "balance-sheet-statement-growth-bulk.head.csv", row => BalanceSheetGrowth.FromCsv(row) },
        { "cash-flow-statement-bulk.head.csv", row => CashFlowStatement.FromCsv(row) },
        { "cash-flow-statement-growth-bulk.head.csv", row => CashFlowGrowth.FromCsv(row) },
        { "dcf-bulk.head.csv", row => BulkDiscountedCashFlow.FromCsv(row) },
        { "earnings-surprises-bulk.head.csv", row => BulkEarningsSurprise.FromCsv(row) },
        { "eod-bulk.head.csv", row => BulkEndOfDayPrice.FromCsv(row) },
        { "etf-holder-bulk.head.csv", row => BulkEtfHolding.FromCsv(row) },
        { "income-statement-bulk.head.csv", row => IncomeStatement.FromCsv(row) },
        { "income-statement-growth-bulk.head.csv", row => IncomeStatementGrowth.FromCsv(row) },
        { "key-metrics-ttm-bulk.head.csv", row => KeyMetricsTtm.FromCsv(row) },
        { "peers-bulk.head.csv", row => BulkPeers.FromCsv(row) },
        { "price-target-summary-bulk.head.csv", row => BulkPriceTargetSummary.FromCsv(row) },
        { "profile-bulk.part0.head.csv", row => BulkCompanyProfile.FromCsv(row) },
        { "rating-bulk.head.csv", row => BulkCompanyRating.FromCsv(row) },
        { "ratios-ttm-bulk.head.csv", row => RatiosTtm.FromCsv(row) },
        { "scores-bulk.head.csv", row => FinancialScores.FromCsv(row) },
        { "upgrades-downgrades-consensus-bulk.head.csv", row => BulkAnalystConsensus.FromCsv(row) },
    };

    [Theory]
    [MemberData(nameof(BulkFixtures))]
    public async Task Every_captured_column_is_read_and_every_column_read_was_captured(
        string fixture, Func<CsvRow, object> map)
    {
        var (columns, fields) = await FirstRecordAsync(fixture);
        var recording = new RecordingColumns(columns);

        map(new CsvRow(recording, fields, fields.Length));

        var header = columns.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            string.Join(", ", header.Except(recording.Requested).OrderBy(c => c, StringComparer.Ordinal)),
            string.Join(", ", recording.Requested.Except(header).OrderBy(c => c, StringComparer.Ordinal)));
        Assert.Equal(header, recording.Requested);
    }

    /// <summary>The first data record of a fixture, as the column map plus the fields in column order.
    ///
    /// <para>The values are reconstructed from the parsed row rather than re-split, so quoting, embedded commas
    /// and doubled quotes are handled by the reader under test everywhere else in this suite. An absent value
    /// comes back as the empty string, which is what the reader itself stores.</para></summary>
    private static async Task<(IReadOnlyDictionary<string, int> Columns, string[] Fields)> FirstRecordAsync(
        string fixture)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Binding.Fixture(fixture)));
        await foreach (var row in CsvStreamReader.ReadAsync(stream))
        {
            var fields = new string[row.Columns.Count];
            foreach (var (name, index) in row.Columns) fields[index] = row.GetString(name) ?? "";
            return (row.Columns.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal), fields);
        }

        throw new Xunit.Sdk.XunitException($"{fixture} carried a header and no data record.");
    }
}
