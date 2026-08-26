using FmpDotNet.Serialization;

namespace FmpDotNet.Models;

/// <summary>One company's peer group. From <c>stable/peers-bulk</c> — 82,930 rows and 6.5 MB measured
/// 2026-08-26, the widest symbol coverage of any endpoint the SDK models.
///
/// <para><b><see cref="Peers"/> is a comma-separated list inside a CSV field</b>, which the quoting hides:
/// <c>"3698.HK,600000.SS,600015.SS"</c> is one field, not three. It is split here rather than handed over as a
/// string, because a caller doing that split itself has to know it is CSV-quoted first.</para>
///
/// <para><b>Trailing separators are real and are dropped.</b> Measured rows end with a dangling comma
/// (<c>"...,600383.SS,"</c>), so a naive split yields a phantom empty peer. 965 of the 82,930 rows carry no peers
/// at all; those give an empty list, never a list containing one empty string.</para></summary>
public sealed record BulkPeers
{
    /// <summary>Ticker as FMP spells it.</summary>
    public string Symbol { get; init; } = "";

    /// <summary>The peer tickers, in the order FMP lists them. Empty when FMP names none.</summary>
    public IReadOnlyList<string> Peers { get; init; } = [];

    internal static BulkPeers FromCsv(CsvRow row) => new()
    {
        Symbol = row.GetString("symbol") ?? "",
        Peers = Split(row.GetString("peers")),
    };

    private static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
