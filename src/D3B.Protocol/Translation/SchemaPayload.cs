using System.Text.Unicode;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Translation;

/// <summary>Checks that a payload is exactly one message of a build's schema, field by field.</summary>
/// <remarks>
/// The schema's messages are loaded as descriptors, not generated classes, so they have no parser to ask. This walks the
/// wire with the descriptor instead: every tag must name a field of the message, its wire type must be the field's, a
/// nested message is walked the same way, and the last field must end the payload. A field the schema lacks is refused
/// rather than discarded, so a non-position classification cannot exempt a frame carrying a later field.
/// </remarks>
internal static class SchemaPayload
{
    private const int MaxDepth = 64;

    /// <summary>Gets whether the payload is one whole message of the descriptor, with no field it lacks and no trailing byte.</summary>
    /// <param name="descriptor">The schema's message.</param>
    /// <param name="payload">The bytes an <c>Any</c> carries.</param>
    /// <returns>Whether the payload has the schema's shape.</returns>
    public static bool IsValid(MessageDescriptor descriptor, ReadOnlySpan<byte> payload)
    {
        try
        {
            Validate(descriptor, payload, 0);

            return true;
        }
        catch (Exception exception) when (exception is InvalidProtocolBufferException or FormatException)
        {
            return false;
        }
    }

    private static void Validate(MessageDescriptor descriptor, ReadOnlySpan<byte> payload, int depth)
    {
        if (depth > MaxDepth)
            throw new FormatException("The payload nests deeper than the schema allows.");

        var fields = descriptor.Fields.InDeclarationOrder().ToDictionary(field => field.FieldNumber);
        var input = new CodedInputStream(payload.ToArray());
        uint tag;

        while ((tag = input.ReadTag()) is not 0)
        {
            var number = WireFormat.GetTagFieldNumber(tag);
            var wireType = WireFormat.GetTagWireType(tag);

            if (!fields.TryGetValue(number, out var field))
                throw new FormatException($"Field {number} is not in {descriptor.FullName}.");

            if (field.IsRepeated && IsPackable(field.FieldType) && field.IsPacked && wireType is WireFormat.WireType.LengthDelimited)
            {
                ValidatePacked(field, input.ReadBytes().Span);

                continue;
            }

            if (wireType != ExpectedWireType(field.FieldType))
                throw new FormatException($"Field {number} of {descriptor.FullName} has the wrong wire type.");

            if (wireType is WireFormat.WireType.LengthDelimited)
            {
                var value = input.ReadBytes();

                if (field.FieldType is FieldType.String && !Utf8.IsValid(value.Span))
                    throw new FormatException($"Field {number} of {descriptor.FullName} is not a UTF-8 string.");

                if (field.FieldType is FieldType.Message)
                    Validate(field.MessageType, value.Span, depth + 1);

                continue;
            }

            if (wireType is WireFormat.WireType.StartGroup)
                throw new FormatException($"Field {number} of {descriptor.FullName} is a group, which this schema does not support.");

            input.SkipLastField();
        }

        if (!input.IsAtEnd)
            throw new FormatException($"The payload of {descriptor.FullName} has trailing bytes.");
    }

    private static void ValidatePacked(FieldDescriptor field, ReadOnlySpan<byte> payload)
    {
        var input = new CodedInputStream(payload.ToArray());

        while (!input.IsAtEnd)
        {
            switch (ExpectedWireType(field.FieldType))
            {
                case WireFormat.WireType.Varint:
                    input.ReadUInt64();

                    break;

                case WireFormat.WireType.Fixed32:
                    input.ReadFixed32();

                    break;

                case WireFormat.WireType.Fixed64:
                    input.ReadFixed64();

                    break;

                default:
                    throw new FormatException($"Field {field.FieldNumber} of {field.ContainingType.FullName} is not a packed scalar.");
            }
        }
    }

    private static bool IsPackable(FieldType type)
    {
        return type is FieldType.Int32 or FieldType.Int64 or FieldType.UInt32 or FieldType.UInt64
            or FieldType.SInt32 or FieldType.SInt64 or FieldType.Bool or FieldType.Enum
            or FieldType.Fixed32 or FieldType.SFixed32 or FieldType.Float
            or FieldType.Fixed64 or FieldType.SFixed64 or FieldType.Double;
    }

    private static WireFormat.WireType ExpectedWireType(FieldType type)
    {
        return type switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => WireFormat.WireType.Fixed64,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => WireFormat.WireType.Fixed32,
            FieldType.String or FieldType.Bytes or FieldType.Message => WireFormat.WireType.LengthDelimited,
            FieldType.Group => WireFormat.WireType.StartGroup,
            FieldType.Int32 or FieldType.Int64 or FieldType.UInt32 or FieldType.UInt64
                or FieldType.SInt32 or FieldType.SInt64 or FieldType.Bool or FieldType.Enum => WireFormat.WireType.Varint,
            _ => throw new FormatException($"Field type {type} has no wire type."),
        };
    }
}
