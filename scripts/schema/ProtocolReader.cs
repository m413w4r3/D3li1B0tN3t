using System.Globalization;
using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using D3B.Scripts.Unity;
using Google.Protobuf.Reflection;
using FieldType = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type;

namespace D3B.Scripts.Schema;

/// <summary>Reads a protocol assembly of the client into a proto3 file descriptor, with its obfuscated names.</summary>
/// <remarks>
/// Messages and enums keep the order of the client's type table, fields the order the generated code declares them in.
/// The protocol's types have no namespace, so the file has no package. A map becomes the nested entry message protobuf
/// describes it with, and a proto3 <c>optional</c> field the synthetic oneof protoc gives it.
/// </remarks>
internal static class ProtocolReader
{
    /// <summary>Reads every message and enum of an assembly.</summary>
    /// <exception cref="InvalidDataException">A type does not read as protobuf's generated code.</exception>
    public static FileDescriptorProto Read(AssemblyAnalysisContext assembly, string fileName, ClientPlatform platform)
    {
        var file = new FileDescriptorProto { Name = fileName, Syntax = "proto3" };

        foreach (var type in assembly.Types.Where(type => type.DeclaringType is null))
        {
            if (ProtocolTypes.IsMessage(type))
                file.MessageType.Add(ReadMessage(type, platform));
            else if (type.IsEnumType)
                file.EnumType.Add(ReadEnum(type));
        }

        if (file.MessageType.Any(UsesAny))
            file.Dependency.Add(ProtocolTypes.AnyFile);

        return file;
    }

    private static DescriptorProto ReadMessage(TypeAnalysisContext type, ClientPlatform platform)
    {
        var layout = MessageLayout.Read(type);
        var encodings = WireEncodings.Read(type, layout, platform);
        var message = new DescriptorProto { Name = type.Name };

        foreach (var nested in layout.Types?.NestedTypes ?? [])
        {
            if (ProtocolTypes.IsMessage(nested))
                message.NestedType.Add(ReadMessage(nested, platform));
            else if (nested.IsEnumType)
                message.EnumType.Add(ReadEnum(nested));
            else if (!ProtocolTypes.IsCompilerGenerated(nested))
                throw new InvalidDataException($"{nested.FullName} is neither a message nor an enum.");
        }

        foreach (var oneof in layout.Oneofs)
            message.OneofDecl.Add(new OneofDescriptorProto { Name = oneof.Name });

        foreach (var field in layout.Fields)
            message.Field.Add(ReadField(type, message, field, encodings));

        return message;
    }

    private static FieldDescriptorProto ReadField(TypeAnalysisContext type, DescriptorProto message, LayoutField layout, WireEncodings encodings)
    {
        var field = new FieldDescriptorProto { Name = layout.Name, Number = layout.Number, Label = FieldDescriptorProto.Types.Label.Optional };

        switch (layout.Kind)
        {
            case FieldKind.Singular:
                SetType(field, layout.ValueTypes[0], encodings.ValueOf(layout.Number));

                break;
            case FieldKind.Repeated:
                field.Label = FieldDescriptorProto.Types.Label.Repeated;
                SetType(field, layout.ValueTypes[0], encodings.ValueOf(layout.Number));

                if (encodings.IsUnpacked(layout.Number))
                    field.Options = new FieldOptions { Packed = false };

                break;
            case FieldKind.Map:
                var (key, value) = encodings.MapOf(layout.Number);
                var entry = new DescriptorProto { Name = EntryNameOf(layout.Name), Options = new MessageOptions { MapEntry = true } };
                var keyField = new FieldDescriptorProto { Name = "key", Number = 1, Label = FieldDescriptorProto.Types.Label.Optional };
                var valueField = new FieldDescriptorProto { Name = "value", Number = 2, Label = FieldDescriptorProto.Types.Label.Optional };

                SetType(keyField, layout.ValueTypes[0], key);
                SetType(valueField, layout.ValueTypes[1], value);
                entry.Field.Add(keyField);
                entry.Field.Add(valueField);
                message.NestedType.Add(entry);

                field.Label = FieldDescriptorProto.Types.Label.Repeated;
                field.Type = FieldType.Message;
                field.TypeName = $"{ProtocolTypes.FullNameOf(type)}.{entry.Name}";

                break;
        }

        if (layout.OneofIndex >= 0)
        {
            field.OneofIndex = layout.OneofIndex;
        }
        else if (layout.HasPresence)
        {
            // protoc gives a proto3 optional field a oneof of its own, after the real ones, named after the field.
            field.Proto3Optional = true;
            field.OneofIndex = message.OneofDecl.Count;
            message.OneofDecl.Add(new OneofDescriptorProto { Name = "_" + layout.Name });
        }

        return field;
    }

    private static void SetType(FieldDescriptorProto field, TypeAnalysisContext type, FieldEncoding? encoding)
    {
        if (ProtocolTypes.IsMessage(type) || ProtocolTypes.IsAny(type))
        {
            field.Type = FieldType.Message;
            field.TypeName = ProtocolTypes.FullNameOf(type);
        }
        else if (type.IsEnumType)
        {
            field.Type = FieldType.Enum;
            field.TypeName = ProtocolTypes.FullNameOf(type);
        }
        else
        {
            field.Type = (encoding ?? FieldEncoding.DefaultOf(type)).Type;
        }
    }

    private static EnumDescriptorProto ReadEnum(TypeAnalysisContext type)
    {
        var values = type.Fields.Where(field => field.IsStatic && field.Attributes.HasFlag(FieldAttributes.Literal)).ToList();
        var numbers = MessageLayout.EnumValues(type).ToList();
        var descriptor = new EnumDescriptorProto { Name = type.Name };

        if (numbers.Count is 0 || numbers[0] is not 0)
            throw new InvalidDataException($"{type.FullName} does not start with zero, as every proto3 enum does.");

        for (var index = 0; index < numbers.Count; index++)
            descriptor.Value.Add(new EnumValueDescriptorProto { Name = values[index].Name, Number = numbers[index] });

        return descriptor;
    }

    private static string EntryNameOf(string fieldName)
    {
        // protoc names a map's entry after its field, in camel case with a capital first letter, then "Entry".
        return string.Concat(fieldName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpper(part[0], CultureInfo.InvariantCulture) + part[1..])) + "Entry";
    }

    private static bool UsesAny(DescriptorProto message)
    {
        return message.Field.Any(field => field.TypeName == ProtocolTypes.AnyName) || message.NestedType.Any(UsesAny);
    }
}
