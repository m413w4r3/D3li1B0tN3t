using System.Buffers;

namespace D3B.Network.Framing;

/// <summary>Writes frames as <see cref="FrameReader"/> reads them: a varint length, then the bytes.</summary>
public static class FrameWriter
{
    /// <summary>Writes a frame.</summary>
    /// <param name="writer">Where to write it.</param>
    /// <param name="payload">The frame's bytes, without their length.</param>
    public static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var lengthSize = LengthSize(payload.Length);
        var span = writer.GetSpan(lengthSize + payload.Length);
        var value = (uint)payload.Length;
        var index = 0;

        while (value >= 0x80)
        {
            span[index++] = (byte)(value | 0x80);
            value >>= 7;
        }

        span[index] = (byte)value;
        payload.CopyTo(span[lengthSize..]);
        writer.Advance(lengthSize + payload.Length);
    }

    /// <summary>Gets how many bytes the length of a frame takes.</summary>
    /// <param name="length">The frame's length, without its own.</param>
    /// <returns>From 1 to <see cref="FrameReader.MaxLengthSize"/>.</returns>
    public static int LengthSize(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var size = 1;

        for (var value = (uint)length; value >= 0x80; value >>= 7)
            size++;

        return size;
    }
}
