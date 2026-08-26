using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialModelingPrep.Serialization;

/// <summary>The one <see cref="JsonSerializerOptions"/> every JSON endpoint deserialises through.</summary>
public static class FmpJson
{
    /// <summary>FMP's <c>stable</c> surface quotes some numeric fields — <c>"fiscalYear":"2026"</c> is the measured
    /// case. Without <see cref="JsonNumberHandling.AllowReadingFromString"/> the first quoted number throws and the
    /// whole response is lost, not just that field. Case-insensitive matching is belt-and-braces on top of the
    /// <see cref="JsonPropertyNameAttribute"/> each model carries.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };
}
