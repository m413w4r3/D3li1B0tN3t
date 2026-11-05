namespace D3B.Diagnostic.Tests.Ipc;

internal sealed class FramedMemoryStream : Stream
{
    private readonly MemoryStream _input;

    public FramedMemoryStream(byte[] input)
    {
        _input = new MemoryStream(input);
        Written = new MemoryStream();
    }

    public MemoryStream Written { get; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush()
    {
        Written.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _input.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _input.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Written.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Written.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _input.Dispose();
            Written.Dispose();
        }

        base.Dispose(disposing);
    }
}
