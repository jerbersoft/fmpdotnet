using System.Reflection;
using System.Text.Json.Serialization;

namespace FmpDotNet.Tests;

/// <summary>Shared helpers for the tests that prove a captured response still binds.
///
/// <para><see cref="Unbound{T}"/> exists because the failure this slice is guarding against is silent. Five of the
/// models reused here were built for CSV and carry no <c>[JsonPropertyName]</c> attributes; without them JSON
/// binding falls back to the C# property name, which deliberately drops FMP's <c>TTM</c> suffix. Nothing throws —
/// <c>symbol</c> populates and 61 metrics land null. A test that spot-checked two fields could pass with the
/// other 59 empty, so these tests assert the whole record.</para></summary>
internal static class Binding
{
    /// <summary>A captured response, read from the test assembly's output directory.</summary>
    public static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>The names of every wire-bound property on <paramref name="row"/> that came back with nothing in
    /// it — null, blank, or an empty collection.
    ///
    /// <para>Only properties carrying <c>[JsonPropertyName]</c> are considered, which is what makes this precise:
    /// a computed or <c>[JsonIgnore]</c>d property is not something FMP sends and has no business failing a
    /// binding test. Blank counts as unbound because this SDK spells a missing string as <c>""</c>.</para></summary>
    public static IReadOnlyList<string> Unbound<T>(T row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [.. typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
            .Where(p => p.GetValue(row) switch
            {
                null => true,
                string text => text.Trim().Length == 0,
                System.Collections.IEnumerable items => !items.GetEnumerator().MoveNext(),
                _ => false,
            })
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }
}
