using System.Runtime.CompilerServices;
using System.Text;

namespace FmpDotNet.Serialization;

/// <summary>Streaming RFC 4180 reader for FMP's bulk CSV.
///
/// <para>Streaming is not an optimisation here, it is the requirement: measured 2026-08-26,
/// <c>ratios-ttm-bulk</c> answers 69 MB and <c>key-metrics-ttm-bulk</c> 44 MB in one response, and three bulk
/// endpoints send no <c>Content-Length</c> at all, so nothing can pre-size a buffer. Reading a record at a time
/// keeps the working set flat regardless of payload size.</para>
///
/// <para>FMP mixes quoted and bare fields in the same record — <c>"TJSEUR","2025-10-22",0.09346581</c> is a
/// measured line — so both forms are handled.</para></summary>
public static class CsvStreamReader
{
    /// <summary>Reads <paramref name="stream"/> as CSV, taking the first record as the header and yielding one
    /// <see cref="CsvRow"/> per record after it. The row's backing array is reused between records; map it before
    /// advancing.</summary>
    public static async IAsyncEnumerable<CsvRow> ReadAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024, leaveOpen: true);

        var parser = new RecordParser(reader);

        var header = await parser.NextRecordAsync(ct).ConfigureAwait(false);
        if (header is null) yield break;

        var columns = new Dictionary<string, int>(header.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++) columns.TryAdd(header[i], i);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var record = await parser.NextRecordAsync(ct).ConfigureAwait(false);
            if (record is null) yield break;
            // A trailing newline yields one empty field; that is a file terminator, not a record.
            if (record.Count == 1 && record[0].Length == 0) continue;
            yield return new CsvRow(columns, parser.Fields, record.Count);
        }
    }

    /// <summary>Pulls one record at a time from a <see cref="TextReader"/>, reusing the field list between records.
    /// The state machine is explicit rather than regex-driven because a quoted field may contain commas, escaped
    /// quotes (<c>""</c>) and newlines, none of which a line-oriented reader survives.</summary>
    private sealed class RecordParser(TextReader reader)
    {
        private readonly char[] _buffer = new char[64 * 1024];
        private readonly StringBuilder _field = new(64);
        private readonly List<string> _record = [];
        private string[] _fields = new string[32];
        private int _length;
        private int _position;

        /// <summary>Field values for the record most recently returned.</summary>
        public string[] Fields => _fields;

        public async Task<List<string>?> NextRecordAsync(CancellationToken ct)
        {
            _record.Clear();
            _field.Clear();

            var inQuotes = false;
            var sawAny = false;

            while (true)
            {
                if (_position >= _length)
                {
                    _length = await reader.ReadAsync(_buffer.AsMemory(), ct).ConfigureAwait(false);
                    _position = 0;
                    if (_length == 0)
                    {
                        // End of stream. Emit whatever is buffered, unless nothing at all was seen.
                        if (!sawAny && _field.Length == 0 && _record.Count == 0) return null;
                        Commit();
                        return Materialise();
                    }
                }

                var c = _buffer[_position++];
                sawAny = true;

                if (inQuotes)
                {
                    if (c != '"') { _field.Append(c); continue; }

                    // A quote inside a quoted field either escapes a literal quote ("") or closes the field.
                    if (_position >= _length)
                    {
                        _length = await reader.ReadAsync(_buffer.AsMemory(), ct).ConfigureAwait(false);
                        _position = 0;
                        if (_length == 0) { inQuotes = false; continue; }
                    }
                    if (_buffer[_position] == '"') { _field.Append('"'); _position++; }
                    else inQuotes = false;
                    continue;
                }

                switch (c)
                {
                    case '"' when _field.Length == 0:
                        inQuotes = true;
                        break;
                    case ',':
                        Commit();
                        break;
                    case '\r':
                        break; // CRLF and lone CR both terminate on the LF or the next char; swallow the CR.
                    case '\n':
                        Commit();
                        return Materialise();
                    default:
                        _field.Append(c);
                        break;
                }
            }
        }

        private void Commit()
        {
            _record.Add(_field.ToString());
            _field.Clear();
        }

        private List<string> Materialise()
        {
            if (_fields.Length < _record.Count) Array.Resize(ref _fields, _record.Count);
            for (var i = 0; i < _record.Count; i++) _fields[i] = _record[i];
            return _record;
        }
    }
}
