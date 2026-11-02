using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>Reads protobuf bytes by their structure alone: field numbers, wire types and lengths, never values.</summary>
/// <remarks>
/// A message reads bytes when every field they hold is one of its fields, with a wire type its type is written with, and
/// every nested message reads its own bytes in turn. A field typed as text reads any length-delimited value, so many
/// messages may read the same bytes: the one they were written as types the most of their fields, at every depth.
/// </remarks>
public static class WireReading
{
    private const int MaxDepth = 32;

    /// <summary>Reads a message's fields, in the order the bytes hold them.</summary>
    /// <param name="bytes">The message's bytes.</param>
    /// <param name="fields">Its fields, as far as they parse.</param>
    /// <returns>Whether the bytes parse whole as fields.</returns>
    public static bool TryFields(ReadOnlySpan<byte> bytes, out List<WireField> fields)
    {
        fields = [];

        var position = 0;

        while (position < bytes.Length)
        {
            if (!TryVarint(bytes, ref position, out var key) || key >> 3 is 0 or > 536_870_911)
                return false;

            var number = (int)(key >> 3);
            var wireType = (int)(key & 7);
            var start = position;
            int length;

            switch (wireType)
            {
                case 0:
                    if (!TryVarint(bytes, ref position, out _))
                        return false;

                    length = position - start;

                    break;
                case 1:
                    length = 8;

                    break;
                case 2:
                    if (!TryVarint(bytes, ref position, out var declared) || declared > (ulong)(bytes.Length - position))
                        return false;

                    start = position;
                    length = (int)declared;

                    break;
                case 5:
                    length = 4;

                    break;
                default:
                    return false;
            }

            if (start + length > bytes.Length)
                return false;

            position = start + length;
            fields.Add(new WireField(number, wireType, start, length));
        }

        return true;
    }

    /// <summary>Counts the fields a message types in bytes, at every depth.</summary>
    /// <returns>The count, or −1 when the message does not read the bytes.</returns>
    internal static int Typed(MessageDescriptor message, ReadOnlySpan<byte> bytes)
    {
        return Typed(message, bytes, 0);
    }

    private static int Typed(MessageDescriptor message, ReadOnlySpan<byte> bytes, int depth)
    {
        if (!TryFields(bytes, out var fields))
            return -1;

        var typed = 0;

        foreach (var wire in fields)
        {
            var field = message.FindFieldByNumber(wire.Number);
            var value = bytes.Slice(wire.Start, wire.Length);

            if (field is null)
                return -1;

            if (field.FieldType is FieldType.Message)
            {
                var nested = wire.WireType is 2 && depth < MaxDepth ? Typed(field.MessageType, value, depth + 1) : -1;

                if (nested < 0)
                    return -1;

                typed += 1 + nested;
            }
            else if (Fits(field, wire.WireType, value))
            {
                typed++;
            }
            else
            {
                return -1;
            }
        }

        return typed;
    }

    private static bool Fits(FieldDescriptor field, int wireType, ReadOnlySpan<byte> value)
    {
        if (field.FieldType is FieldType.String or FieldType.Bytes)
            return wireType is 2;

        if (field.FieldType is FieldType.Group)
            return false;

        var scalar = field.FieldType switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => 1,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => 5,
            _ => 0,
        };

        // A list of scalars may come packed, as proto3 writes it, or one value a field, and a reader takes both.
        return wireType == scalar || (field.IsRepeated && wireType is 2 && IsPacked(value, scalar));
    }

    private static bool IsPacked(ReadOnlySpan<byte> value, int wireType)
    {
        if (wireType is 1 or 5)
            return value.Length % (wireType is 1 ? 8 : 4) is 0;

        // Varints: every byte but each value's last has its high bit set, so the run must end on a byte without it.
        return value.Length is 0 || value[^1] < 0x80;
    }

    private static bool TryVarint(ReadOnlySpan<byte> bytes, ref int position, out ulong value)
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
