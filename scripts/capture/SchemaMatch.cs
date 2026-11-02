using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Capture;

/// <summary>A protocol schema, a <c>FileDescriptorSet</c> such as <c>scripts/schema.cs</c> writes, matched against frames by structure.</summary>
/// <remarks>
/// A message reads bytes when every field they hold is one of its fields, with a wire type its type is written with,
/// and every nested message, map entry included, reads its own bytes in turn. A field typed as text or bytes reads any
/// length-delimited value, so many messages read a frame: the one reading it as the client wrote it types the most of its
/// fields, at every depth. Values are never looked at.
/// </remarks>
internal sealed class SchemaMatch
{
    private const int MaxDepth = 32;

    /// <summary>Every message of the schema, nested ones included, but for the entries protobuf describes maps with.</summary>
    public IReadOnlyList<MessageDescriptor> Messages { get; }

    private SchemaMatch(IReadOnlyList<MessageDescriptor> messages)
    {
        Messages = messages;
    }

    /// <summary>Loads a schema, linking every type name to its type.</summary>
    /// <exception cref="InvalidProtocolBufferException">The file is not a <c>FileDescriptorSet</c>.</exception>
    /// <exception cref="DescriptorValidationException">The descriptors do not link.</exception>
    public static SchemaMatch Load(string path)
    {
        var set = FileDescriptorSet.Parser.ParseFrom(File.ReadAllBytes(path));
        var files = FileDescriptor.BuildFromByteStrings(set.File.Select(file => file.ToByteString()));

        return new SchemaMatch([.. files.SelectMany(file => All(file.MessageTypes))]);
    }

    /// <summary>Tells whether a message reads bytes, by structure.</summary>
    public static bool Accepts(MessageDescriptor message, ReadOnlySpan<byte> bytes)
    {
        return Typed(message, bytes, 0) >= 0;
    }

    /// <summary>Counts the fields a message types in bytes, at every depth.</summary>
    /// <returns>The count, or null when the message does not read the bytes.</returns>
    public static int? Typed(MessageDescriptor message, ReadOnlySpan<byte> bytes)
    {
        var typed = Typed(message, bytes, 0);

        return typed >= 0 ? typed : null;
    }

    /// <summary>Describes a field as <c>.proto</c> text declares it: its type, then its name.</summary>
    public static string Describe(FieldDescriptor field)
    {
        if (field.IsMap)
            return $"map<{TypeOf(field.MessageType.FindFieldByNumber(1))}, {TypeOf(field.MessageType.FindFieldByNumber(2))}> {field.Name}";

        return $"{(field.IsRepeated ? "repeated " : string.Empty)}{TypeOf(field)} {field.Name}";
    }

    private static IEnumerable<MessageDescriptor> All(IEnumerable<MessageDescriptor> messages)
    {
        foreach (var message in messages)
        {
            if (message.ToProto().Options?.MapEntry is true)
                continue;

            yield return message;

            foreach (var nested in All(message.NestedTypes))
                yield return nested;
        }
    }

    private static int Typed(MessageDescriptor message, ReadOnlySpan<byte> bytes, int depth)
    {
        if (!ProtobufShape.TryFields(bytes, out var fields))
            return -1;

        var typed = 0;

        foreach (var (number, wireType, start, length) in fields)
        {
            var field = message.FindFieldByNumber(number);
            var value = bytes.Slice(start, length);

            if (field is null)
                return -1;

            if (field.FieldType is FieldType.Message)
            {
                var nested = wireType is 2 && depth < MaxDepth ? Typed(field.MessageType, value, depth + 1) : -1;

                if (nested < 0)
                    return -1;

                typed += 1 + nested;
            }
            else if (Fits(field, wireType, value))
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

        var scalar = WireTypeOf(field.FieldType);

        // A list of scalars may come packed, as proto3 writes it, or one value a field, and a reader takes both.
        return wireType == scalar || (field.IsRepeated && wireType is 2 && IsPacked(value, scalar));
    }

    private static int WireTypeOf(FieldType type)
    {
        return type switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => 1,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => 5,
            _ => 0,
        };
    }

    private static bool IsPacked(ReadOnlySpan<byte> value, int wireType)
    {
        if (wireType is 1 or 5)
            return value.Length % (wireType is 1 ? 8 : 4) is 0;

        // Varints: every byte but each value's last has its high bit set, so the run must end on a byte without it.
        return value.Length is 0 || value[^1] < 0x80;
    }

    private static string TypeOf(FieldDescriptor field)
    {
        return field.FieldType switch
        {
            FieldType.Message => "." + field.MessageType.FullName,
            FieldType.Enum => "." + field.EnumType.FullName,
            FieldType.Double => "double",
            FieldType.Float => "float",
            FieldType.Int64 => "int64",
            FieldType.UInt64 => "uint64",
            FieldType.Int32 => "int32",
            FieldType.Fixed64 => "fixed64",
            FieldType.Fixed32 => "fixed32",
            FieldType.Bool => "bool",
            FieldType.String => "string",
            FieldType.Bytes => "bytes",
            FieldType.UInt32 => "uint32",
            FieldType.SFixed32 => "sfixed32",
            FieldType.SFixed64 => "sfixed64",
            FieldType.SInt32 => "sint32",
            FieldType.SInt64 => "sint64",
            _ => "group",
        };
    }

    /// <summary>The messages of the schema reading bytes.</summary>
    public List<MessageDescriptor> Accepting(ReadOnlySpan<byte> bytes)
    {
        var accepting = new List<MessageDescriptor>();

        foreach (var message in Messages)
        {
            if (Accepts(message, bytes))
                accepting.Add(message);
        }

        return accepting;
    }
}
