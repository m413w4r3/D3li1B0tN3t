using System.Buffers;

namespace D3B.Network.Framing;

/// <summary>Reads the frames of a connection to Ankama's servers: each a varint length, then that many bytes (D38).</summary>
/// <remarks>
/// <para>
/// The length is a protobuf varint of at most five bytes, seven bits a byte, least significant first, as
/// <c>WriteDelimitedTo</c> writes it. A frame may be empty. A length is read only once it is whole, and a frame only once
/// all its bytes are there, so a reader that returns <see langword="false"/> has consumed nothing: the caller reads more
/// bytes and tries again from the same position.
/// </para>
/// <para>
/// Bytes that cannot be a length, or a length above the maximum, throw <see cref="FrameFormatException"/>: there is no
/// telling where the next frame starts, so the connection cannot be read any further.
/// </para>
/// </remarks>
public static class FrameReader
{
    /// <summary>The longest frame read by default, 16 MiB.</summary>
    /// <remarks>
    /// A bound that only garbage reaches: it keeps a misread length from making a reader wait for, and buffer, gigabytes.
    /// </remarks>
    public const int DefaultMaxLength = 16 * 1024 * 1024;

    /// <summary>The most bytes a length takes: five for 32 bits.</summary>
    public const int MaxLengthSize = 5;

    /// <summary>Reads the next frame, if all its bytes are there.</summary>
    /// <param name="reader">The bytes, read from its position, which moves past the frame when one is read.</param>
    /// <param name="frame">The frame's bytes, without their length, sliced from the reader's sequence.</param>
    /// <param name="maxLength">The longest frame accepted.</param>
    /// <returns><see langword="true"/> when a frame was read; <see langword="false"/> when more bytes are needed.</returns>
    /// <exception cref="FrameFormatException">The bytes are no length, or one above <paramref name="maxLength"/>.</exception>
    public static bool TryReadFrame(ref SequenceReader<byte> reader, out ReadOnlySequence<byte> frame, int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        frame = default;

        if (!TryPeekLength(ref reader, maxLength, out var length, out var lengthSize) || reader.Remaining - lengthSize < length)
            return false;

        reader.Advance(lengthSize);
        frame = reader.UnreadSequence.Slice(0, length);
        reader.Advance(length);

        return true;
    }

    /// <summary>Reads the length of the next frame without consuming it, if the length is whole.</summary>
    /// <param name="reader">The bytes, read from its position, which does not move.</param>
    /// <param name="length">The frame's length, without its own.</param>
    /// <param name="maxLength">The longest frame accepted.</param>
    /// <returns><see langword="true"/> when the length is whole; <see langword="false"/> when more bytes are needed.</returns>
    /// <exception cref="FrameFormatException">The bytes are no length, or one above <paramref name="maxLength"/>.</exception>
    public static bool TryPeekLength(ref SequenceReader<byte> reader, out int length, int maxLength = DefaultMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        return TryPeekLength(ref reader, maxLength, out length, out _);
    }

    private static bool TryPeekLength(ref SequenceReader<byte> reader, int maxLength, out int length, out int size)
    {
        var available = (int)Math.Min(reader.Remaining, MaxLengthSize);
        var unread = reader.UnreadSpan;
        Span<byte> copy = stackalloc byte[MaxLengthSize];

        scoped ReadOnlySpan<byte> head;

        // The length nearly always sits whole in the current segment; it is copied only when it straddles two.
        if (unread.Length >= available)
        {
            head = unread[..available];
        }
        else
        {
            reader.TryCopyTo(copy[..available]);
            head = copy[..available];
        }

        var value = 0u;

        for (size = 0; size < head.Length; size++)
        {
            var current = head[size];

            // The fifth byte carries the top four bits of 32: anything above them would be lost.
            if (size is MaxLengthSize - 1 && current > 0x0F)
                throw new FrameFormatException("A frame's length does not fit in 32 bits.");

            value |= (uint)(current & 0x7F) << (7 * size);

            if (current < 0x80)
            {
                size++;

                if (value > (uint)maxLength)
                    throw new FrameFormatException($"A frame of {value:N0} bytes is longer than the {maxLength:N0} allowed.");

                length = (int)value;

                return true;
            }
        }

        length = 0;

        return false;
    }
}
