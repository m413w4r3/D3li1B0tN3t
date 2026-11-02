namespace D3B.Network.Relay;

/// <summary>Finds the length-delimited values of a protobuf message, at any depth, by the wire format alone.</summary>
/// <remarks>
/// No schema is needed: a value that parses as a message is walked too, and one that does not, such as a text, is a leaf.
/// Bytes that are no protobuf end the walk where they start; what was found before stays.
/// </remarks>
internal static class ProtobufValues
{
    private const int MaxDepth = 8;

    /// <summary>Lists where each length-delimited value of a message lies, outer values before the ones they hold.</summary>
    /// <param name="message">The message's bytes.</param>
    /// <returns>Each value's start and length in <paramref name="message"/>.</returns>
    public static List<(int Start, int Length)> LengthDelimited(ReadOnlySpan<byte> message)
    {
        var values = new List<(int Start, int Length)>();

        Walk(message, 0, 0, values);

        return values;
    }

    private static bool Walk(ReadOnlySpan<byte> message, int offset, int depth, List<(int Start, int Length)> values)
    {
        var position = 0;

        while (position < message.Length)
        {
            if (!TryReadVarint(message, ref position, out var tag) || tag >> 3 is 0)
                return false;

            switch (tag & 7)
            {
                case 0:
                    if (!TryReadVarint(message, ref position, out _))
                        return false;

                    break;
                case 1:
                    position += 8;

                    break;
                case 5:
                    position += 4;

                    break;
                case 2:
                    if (!TryReadVarint(message, ref position, out var length) || length > (ulong)(message.Length - position))
                        return false;

                    var value = message.Slice(position, (int)length);
                    var found = values.Count;

                    values.Add((offset + position, value.Length));

                    // A value that is no message is a leaf: what its walk found inside it is no value at all.
                    if (depth < MaxDepth && !Walk(value, offset + position, depth + 1, values))
                        values.RemoveRange(found + 1, values.Count - found - 1);

                    position += value.Length;

                    break;
                default:
                    return false;
            }
        }

        return position == message.Length;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> bytes, ref int position, out ulong value)
    {
        value = 0;

        for (var shift = 0; shift < 64 && position < bytes.Length; shift += 7)
        {
            var current = bytes[position++];

            value |= (ulong)(current & 0x7F) << shift;

            if (current < 0x80)
                return true;
        }

        return false;
    }
}
