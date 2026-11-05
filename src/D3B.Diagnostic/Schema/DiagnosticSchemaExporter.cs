using D3B.Diagnostic.Contract;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Schema;

/// <summary>Projects a build's protobuf descriptors into the versioned diagnostic schema.</summary>
public static class DiagnosticSchemaExporter
{
    private const int MaximumFieldNumber = 536_870_911;

    /// <summary>Exports every message in a linked build schema.</summary>
    /// <param name="clientVersion">The opaque client version associated with the schema.</param>
    /// <param name="schema">The linked build schema.</param>
    /// <returns>A deterministic diagnostic schema projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="DiagnosticSchemaException">The descriptors violate the diagnostic schema contract.</exception>
    public static DiagnosticSchema Export(string clientVersion, ProtocolSchema schema)
    {
        ArgumentNullException.ThrowIfNull(clientVersion);
        ArgumentNullException.ThrowIfNull(schema);

        var descriptors = new List<MessageDescriptor>();
        foreach (var file in schema.Files)
        {
            foreach (var (message, depth) in EnumerateMessages(file.MessageTypes, 1))
            {
                if (depth > DiagnosticProtocol.MaxDepth)
                {
                    throw new DiagnosticSchemaException("The schema message nesting limit was exceeded.");
                }

                descriptors.Add(message);
                if (descriptors.Count > DiagnosticProtocol.MaxSchemaMessages)
                {
                    throw new DiagnosticSchemaException("The schema message count limit was exceeded.");
                }
            }
        }

        var duplicateName = descriptors
            .GroupBy(message => message.FullName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new DiagnosticSchemaException($"Duplicate message name: {duplicateName.Key}.");
        }

        var messages = descriptors
            .Select(CreateMessage)
            .OrderBy(message => message.Name, StringComparer.Ordinal)
            .ToArray();
        var exportedNames = messages.Select(message => message.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var field in messages.SelectMany(message => message.Fields))
        {
            if (field.Kind == DiagnosticFieldKind.Message &&
                (field.MessageType is null || !exportedNames.Contains(field.MessageType)))
            {
                throw new DiagnosticSchemaException($"Message field {field.Number} references a message that is not exported.");
            }
        }

        return new DiagnosticSchema
        {
            ClientVersion = clientVersion,
            SchemaSha256 = Convert.ToHexStringLower(schema.Sha256.ToArray()),
            Messages = messages,
        };
    }

    /// <summary>Exports and validates a synthetic or raw descriptor set before linking it.</summary>
    /// <param name="clientVersion">The opaque client version associated with the schema.</param>
    /// <param name="descriptorSet">The protobuf file descriptor set.</param>
    /// <returns>A deterministic diagnostic schema projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="DiagnosticSchemaException">The descriptor set violates the diagnostic schema contract.</exception>
    public static DiagnosticSchema Export(string clientVersion, FileDescriptorSet descriptorSet)
    {
        ArgumentNullException.ThrowIfNull(clientVersion);
        ArgumentNullException.ThrowIfNull(descriptorSet);

        ValidateDescriptorSet(descriptorSet);

        try
        {
            return Export(clientVersion, ProtocolSchema.Load(descriptorSet.ToByteArray()));
        }
        catch (DiagnosticSchemaException)
        {
            throw;
        }
        catch (Exception exception) when (exception is DescriptorValidationException or InvalidProtocolBufferException)
        {
            throw new DiagnosticSchemaException("The descriptor set could not be linked.");
        }
    }

    private static DiagnosticMessage CreateMessage(MessageDescriptor descriptor)
    {
        var descriptorFields = descriptor.Fields.InDeclarationOrder().ToArray();
        if (descriptorFields.Length > DiagnosticProtocol.MaxFieldsPerMessage)
        {
            throw new DiagnosticSchemaException($"Message {descriptor.FullName} exceeds the field count limit.");
        }

        var duplicateNumber = descriptorFields
            .GroupBy(field => field.FieldNumber)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNumber is not null)
        {
            throw new DiagnosticSchemaException(
                $"Message {descriptor.FullName} has duplicate field number {duplicateNumber.Key}.");
        }

        var fields = descriptorFields
            .OrderBy(field => field.FieldNumber)
            .Select(CreateField)
            .ToArray();
        return new DiagnosticMessage
        {
            Name = descriptor.FullName,
            MapEntry = descriptor.ToProto().Options?.MapEntry is true,
            Fields = fields,
        };
    }

    private static DiagnosticField CreateField(FieldDescriptor descriptor)
    {
        if (descriptor.FieldNumber is < 1 or > MaximumFieldNumber)
        {
            throw new DiagnosticSchemaException($"Message field {descriptor.Name} has an invalid field number.");
        }

        var kind = ToKind(descriptor.FieldType);
        return new DiagnosticField
        {
            Number = descriptor.FieldNumber,
            Name = descriptor.Name,
            Kind = kind,
            Repeated = descriptor.IsRepeated,
            Packed = descriptor.IsPacked,
            MessageType = kind == DiagnosticFieldKind.Message ? descriptor.MessageType.FullName : null,
            EnumType = kind == DiagnosticFieldKind.Enumeration ? descriptor.EnumType.FullName : null,
            Oneof = descriptor.RealContainingOneof?.Name,
            HasPresence = descriptor.HasPresence,
        };
    }

    private static DiagnosticFieldKind ToKind(FieldType fieldType)
    {
        return fieldType switch
        {
            FieldType.Double => DiagnosticFieldKind.DoublePrecision,
            FieldType.Float => DiagnosticFieldKind.SinglePrecision,
            FieldType.Int64 => DiagnosticFieldKind.SignedInteger64,
            FieldType.UInt64 => DiagnosticFieldKind.UnsignedInteger64,
            FieldType.Int32 => DiagnosticFieldKind.SignedInteger32,
            FieldType.Fixed64 => DiagnosticFieldKind.Fixed64,
            FieldType.Fixed32 => DiagnosticFieldKind.Fixed32,
            FieldType.Bool => DiagnosticFieldKind.Boolean,
            FieldType.String => DiagnosticFieldKind.Utf8String,
            FieldType.Group => DiagnosticFieldKind.Group,
            FieldType.Message => DiagnosticFieldKind.Message,
            FieldType.Bytes => DiagnosticFieldKind.Bytes,
            FieldType.UInt32 => DiagnosticFieldKind.UnsignedInteger32,
            FieldType.Enum => DiagnosticFieldKind.Enumeration,
            FieldType.SFixed32 => DiagnosticFieldKind.SignedFixed32,
            FieldType.SFixed64 => DiagnosticFieldKind.SignedFixed64,
            FieldType.SInt32 => DiagnosticFieldKind.SignedZigZag32,
            FieldType.SInt64 => DiagnosticFieldKind.SignedZigZag64,
            _ => throw new DiagnosticSchemaException($"Unknown protobuf field kind: {fieldType}.")
        };
    }

    private static IEnumerable<(MessageDescriptor Message, int Depth)> EnumerateMessages(
        IEnumerable<MessageDescriptor> messages,
        int depth)
    {
        foreach (var message in messages)
        {
            yield return (message, depth);

            foreach (var nested in EnumerateMessages(message.NestedTypes, depth + 1))
            {
                yield return nested;
            }
        }
    }

    private static void ValidateDescriptorSet(FileDescriptorSet descriptorSet)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var messageDefinitions = new List<(string Name, DescriptorProto Descriptor, int Depth)>();
        foreach (var file in descriptorSet.File)
        {
            var prefix = string.IsNullOrEmpty(file.Package) ? string.Empty : file.Package + ".";
            CollectMessages(file.MessageType, prefix, 1, names, messageDefinitions);
        }

        if (messageDefinitions.Count > DiagnosticProtocol.MaxSchemaMessages)
        {
            throw new DiagnosticSchemaException("The schema message count limit was exceeded.");
        }

        foreach (var (name, descriptor, depth) in messageDefinitions)
        {
            if (depth > DiagnosticProtocol.MaxDepth)
            {
                throw new DiagnosticSchemaException("The schema message nesting limit was exceeded.");
            }

            if (descriptor.Field.Count > DiagnosticProtocol.MaxFieldsPerMessage)
            {
                throw new DiagnosticSchemaException($"Message {name} exceeds the field count limit.");
            }

            var fieldNumbers = new HashSet<int>();
            foreach (var field in descriptor.Field)
            {
                if (field.Number is < 1 or > MaximumFieldNumber)
                {
                    throw new DiagnosticSchemaException($"Message {name} has an invalid field number.");
                }

                if (!fieldNumbers.Add(field.Number))
                {
                    throw new DiagnosticSchemaException($"Message {name} has duplicate field number {field.Number}.");
                }
            }
        }

        foreach (var (_, descriptor, _) in messageDefinitions)
        {
            foreach (var field in descriptor.Field.Where(field => field.Type == FieldDescriptorProto.Types.Type.Message))
            {
                var typeName = field.TypeName.TrimStart('.');
                if (typeName.Length == 0 || !names.Contains(typeName))
                {
                    throw new DiagnosticSchemaException("A message field references a message that is not exported.");
                }
            }
        }
    }

    private static void CollectMessages(
        IEnumerable<DescriptorProto> messages,
        string prefix,
        int depth,
        HashSet<string> names,
        ICollection<(string Name, DescriptorProto Descriptor, int Depth)> definitions)
    {
        foreach (var message in messages)
        {
            var name = prefix + message.Name;
            if (!names.Add(name))
            {
                throw new DiagnosticSchemaException($"Duplicate message name: {name}.");
            }

            definitions.Add((name, message, depth));
            CollectMessages(message.NestedType, name + ".", depth + 1, names, definitions);
        }
    }
}
