using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace FinancialModelingPrep.Serialization;

/// <summary>One parsed CSV record, addressed by column name.
///
/// <para><b>Lifetime.</b> The backing field array is reused across records for the length of a read, so a row is
/// valid only for the duration of the call that receives it. The SDK maps each row to a domain object before
/// advancing, so this never escapes; do not store a <see cref="CsvRow"/> or the strings-array identity.</para>
///
/// <para>Every accessor returns null for an absent column and for an empty field, so a missing value and a blank
/// one are treated alike — which is what FMP's bulk CSV means by them.</para></summary>
public readonly struct CsvRow
{
    private readonly IReadOnlyDictionary<string, int> _columns;
    private readonly string[] _fields;
    private readonly int _count;

    internal CsvRow(IReadOnlyDictionary<string, int> columns, string[] fields, int count)
    {
        _columns = columns;
        _fields = fields;
        _count = count;
    }

    /// <summary>Column names in file order.</summary>
    public IReadOnlyDictionary<string, int> Columns => _columns;

    /// <summary>Number of fields this record actually carried, which a ragged row may make differ from
    /// <see cref="Columns"/>.</summary>
    public int FieldCount => _count;

    /// <summary>The raw field, or null when the column is absent or the value is empty.</summary>
    public string? GetString(string column)
    {
        if (_columns is null || !_columns.TryGetValue(column, out var i) || i >= _count) return null;
        var value = _fields[i];
        return value.Length == 0 ? null : value;
    }

    /// <summary>The field as a <see cref="decimal"/>, or null when absent, empty, or unparseable.</summary>
    public decimal? GetDecimal(string column) =>
        decimal.TryParse(GetString(column), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The field as a <see cref="double"/>. Bulk CSV uses exponent notation for small prices
    /// (<c>1.8646e-8</c>), which <see cref="decimal"/> parses but cannot always represent faithfully.</summary>
    public double? GetDouble(string column) =>
        double.TryParse(GetString(column), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The field as a <see cref="long"/>, or null when absent, empty, or unparseable.</summary>
    public long? GetInt64(string column) =>
        long.TryParse(GetString(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The field as an <see cref="int"/>, or null when absent, empty, or unparseable.</summary>
    public int? GetInt32(string column) =>
        int.TryParse(GetString(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The field as a <see cref="LocalDate"/>. Accepts a bare <c>yyyy-MM-dd</c> and the
    /// <c>"yyyy-MM-dd HH:mm:ss"</c> form FMP uses where a timestamp is carried in a date column, taking the date
    /// part of the latter.</summary>
    public LocalDate? GetDate(string column)
    {
        var raw = GetString(column);
        if (raw is null) return null;
        var space = raw.IndexOf(' ');
        var parsed = LocalDatePattern.Iso.Parse(space < 0 ? raw : raw[..space]);
        return parsed.Success ? parsed.Value : null;
    }

    /// <summary>The field as an <see cref="Instant"/>, reading FMP's space-separated
    /// <c>"yyyy-MM-dd HH:mm:ss"</c> timestamps as UTC — see
    /// <see cref="NullableFmpInstantJsonConverter"/> for why UTC is established rather than assumed.</summary>
    public Instant? GetInstant(string column)
    {
        var raw = GetString(column);
        if (raw is null) return null;
        var parsed = FmpTimestampPattern.Parse(raw);
        return parsed.Success ? parsed.Value.InUtc().ToInstant() : null;
    }

    private static readonly LocalDateTimePattern FmpTimestampPattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd HH:mm:ss");

    /// <summary>The field as a <see cref="bool"/>, accepting <c>true</c>/<c>false</c> and <c>1</c>/<c>0</c>.</summary>
    public bool? GetBoolean(string column) => GetString(column) switch
    {
        null => null,
        "1" => true,
        "0" => false,
        var s when bool.TryParse(s, out var b) => b,
        _ => null,
    };
}
