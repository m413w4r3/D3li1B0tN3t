namespace D3B.Network.Tests.Captures;

/// <summary>A stream that takes so many bytes, then fails every write, as a full disk does.</summary>
internal sealed class FailingStream : MemoryStream
{
    private readonly int _capacity;

    public FailingStream(int capacity)
    {
        _capacity = capacity;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Take(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        // MemoryStream hands a span to a derived class's array overload: calling it directly keeps one path.
        Take(buffer.Length);
        base.Write(buffer.ToArray(), 0, buffer.Length);
    }

    public override void WriteByte(byte value)
    {
        Take(1);
        base.WriteByte(value);
    }

    private void Take(int count)
    {
        if (Length + count > _capacity)
            throw new IOException("The disk is full.");
    }
}
