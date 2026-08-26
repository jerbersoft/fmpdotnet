namespace FmpDotNet.Serialization;

/// <summary>A read-only stream that replays an already-consumed prefix before continuing with the rest.
///
/// <para>Needed because deciding whether a bulk response is CSV or a JSON error means looking at its first bytes,
/// and a network stream cannot be rewound. Buffering the whole response instead is exactly what streaming exists
/// to avoid.</para></summary>
internal sealed class PrefixedStream(byte[] prefix, int prefixLength, Stream rest) : Stream
{
    private int _offset;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_offset >= prefixLength) return rest.Read(buffer);
        var take = Math.Min(buffer.Length, prefixLength - _offset);
        prefix.AsSpan(_offset, take).CopyTo(buffer);
        _offset += take;
        return take;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_offset >= prefixLength) return await rest.ReadAsync(buffer, ct).ConfigureAwait(false);
        var take = Math.Min(buffer.Length, prefixLength - _offset);
        prefix.AsMemory(_offset, take).CopyTo(buffer);
        _offset += take;
        return take;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) rest.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await rest.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
