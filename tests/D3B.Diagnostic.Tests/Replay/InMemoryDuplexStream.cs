namespace D3B.Diagnostic.Tests.Replay;

internal sealed class InMemoryDuplexStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();

    public InMemoryDuplexStream(string inputText)
    {
        _input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(inputText));
    }

    public string WrittenText
    {
        get
        {
            return System.Text.Encoding.UTF8.GetString(_output.ToArray());
        }
    }

    public override bool CanRead
    {
        get
        {
            return true;
        }
    }

    public override bool CanSeek
    {
        get
        {
            return false;
        }
    }

    public override bool CanWrite
    {
        get
        {
            return true;
        }
    }

    public override long Length
    {
        get
        {
            throw new NotSupportedException();
        }
    }

    public override long Position
    {
        get
        {
            throw new NotSupportedException();
        }
        set
        {
            throw new NotSupportedException();
        }
    }

    public override void Flush()
    {
        _output.Flush();
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
        _output.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _output.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _input.Dispose();
            _output.Dispose();
        }

        base.Dispose(disposing);
    }
}
