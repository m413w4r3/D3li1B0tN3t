using Google.Protobuf.Reflection;
using FieldType = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type;

namespace D3B.Scripts.Schema;

/// <summary>Writes a file descriptor back as <c>.proto</c> text, to read it: protoc compiles it to an equivalent descriptor.</summary>
/// <remarks>Types are named by their full names, with a leading dot, so that no name depends on where it is written.</remarks>
internal static class ProtoText
{
    public static void Write(TextWriter output, FileDescriptorProto file, string header)
    {
        output.WriteLine($"// {header}");
        output.WriteLine("syntax = \"proto3\";");

        if (file.Dependency.Count > 0)
            output.WriteLine();

        foreach (var dependency in file.Dependency)
            output.WriteLine($"import \"{dependency}\";");

        foreach (var enumeration in file.EnumType)
        {
            output.WriteLine();
            WriteEnum(output, enumeration, 0);
        }

        foreach (var message in file.MessageType)
        {
            output.WriteLine();
            WriteMessage(output, message, 0);
        }
    }

    /// <summary>The type of a field as <c>.proto</c> text writes it: a keyword, or a full name.</summary>
    public static string TypeOf(FieldDescriptorProto field)
    {
        return field.Type switch
        {
            FieldType.Message or FieldType.Enum => field.TypeName,
            FieldType.Double => "double",
            FieldType.Float => "float",
            FieldType.Int64 => "int64",
            FieldType.Uint64 => "uint64",
            FieldType.Int32 => "int32",
            FieldType.Fixed64 => "fixed64",
            FieldType.Fixed32 => "fixed32",
            FieldType.Bool => "bool",
            FieldType.String => "string",
            FieldType.Bytes => "bytes",
            FieldType.Uint32 => "uint32",
            FieldType.Sfixed32 => "sfixed32",
            FieldType.Sfixed64 => "sfixed64",
            FieldType.Sint32 => "sint32",
            FieldType.Sint64 => "sint64",
            _ => throw new InvalidDataException($"Field {field.Name} has type {field.Type}, which proto3 has not."),
        };
    }

    private static void WriteMessage(TextWriter output, DescriptorProto message, int depth)
    {
        var indent = new string(' ', 2 * depth);
        var entries = message.NestedType.Where(nested => nested.Options?.MapEntry is true).ToDictionary(nested => nested.Name, StringComparer.Ordinal);
        var oneofsWritten = new HashSet<int>();

        output.WriteLine($"{indent}message {message.Name} {{");

        foreach (var field in message.Field)
        {
            if (!field.HasOneofIndex || field.Proto3Optional)
            {
                output.WriteLine($"{indent}  {FieldLine(field, entries)}");

                continue;
            }

            // protoc wants a oneof's fields together, which the generated code declares them as.
            if (!oneofsWritten.Add(field.OneofIndex))
                continue;

            output.WriteLine($"{indent}  oneof {message.OneofDecl[field.OneofIndex].Name} {{");

            foreach (var member in message.Field.Where(member => member.HasOneofIndex && member.OneofIndex == field.OneofIndex))
                output.WriteLine($"{indent}    {FieldLine(member, entries)}");

            output.WriteLine($"{indent}  }}");
        }

        foreach (var enumeration in message.EnumType)
            WriteEnum(output, enumeration, depth + 1);

        foreach (var nested in message.NestedType.Where(nested => !entries.ContainsKey(nested.Name)))
            WriteMessage(output, nested, depth + 1);

        output.WriteLine($"{indent}}}");
    }

    private static void WriteEnum(TextWriter output, EnumDescriptorProto enumeration, int depth)
    {
        var indent = new string(' ', 2 * depth);

        output.WriteLine($"{indent}enum {enumeration.Name} {{");

        foreach (var value in enumeration.Value)
            output.WriteLine($"{indent}  {value.Name} = {value.Number};");

        output.WriteLine($"{indent}}}");
    }

    private static string FieldLine(FieldDescriptorProto field, Dictionary<string, DescriptorProto> entries)
    {
        var options = field.Options is { HasPacked: true, Packed: false } ? " [packed = false]" : string.Empty;

        if (field.Label is FieldDescriptorProto.Types.Label.Repeated && entries.TryGetValue(field.TypeName[(field.TypeName.LastIndexOf('.') + 1)..], out var entry))
            return $"map<{TypeOf(entry.Field[0])}, {TypeOf(entry.Field[1])}> {field.Name} = {field.Number};";

        var label = field.Label is FieldDescriptorProto.Types.Label.Repeated ? "repeated " : field.Proto3Optional ? "optional " : string.Empty;

        return $"{label}{TypeOf(field)} {field.Name} = {field.Number}{options};";
    }
}
