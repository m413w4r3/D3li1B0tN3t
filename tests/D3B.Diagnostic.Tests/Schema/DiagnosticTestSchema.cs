using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Schema;

internal static class DiagnosticTestSchema
{
    public static ProtocolSchema CreateProtocolSchema()
    {
        return ProtocolSchema.Load(CreateDescriptorSet().ToByteArray());
    }

    public static FileDescriptorSet CreateDescriptorSet()
    {
        var set = new FileDescriptorSet();
        set.File.Add(CreateGoogleFile());
        set.File.Add(CreateApplicationFile());
        set.File.Add(CreateGroupFile());
        return set;
    }

    public static FileDescriptorSet CreateMinimalDescriptorSet()
    {
        var set = new FileDescriptorSet();
        var file = new FileDescriptorProto { Name = "empty.proto", Package = "fixture", Syntax = "proto3" };
        file.MessageType.Add(new DescriptorProto { Name = "Empty" });
        set.File.Add(file);
        return set;
    }

    public static FileDescriptorSet CreateDuplicateFieldNumberSet()
    {
        var set = CreateMinimalDescriptorSet();
        var message = set.File[0].MessageType[0];
        message.Field.Add(CreateField("first", 1, FieldDescriptorProto.Types.Type.Int32));
        message.Field.Add(CreateField("second", 1, FieldDescriptorProto.Types.Type.String));
        return set;
    }

    public static FileDescriptorSet CreateDanglingMessageTypeSet()
    {
        var set = CreateMinimalDescriptorSet();
        set.File[0].MessageType[0].Field.Add(
            CreateField("missing", 1, FieldDescriptorProto.Types.Type.Message, ".fixture.Missing"));
        return set;
    }

    private static FileDescriptorProto CreateApplicationFile()
    {
        var file = new FileDescriptorProto { Name = "fixture.proto", Package = "fixture", Syntax = "proto3" };
        file.Dependency.Add("google/protobuf/any.proto");
        var root = new DescriptorProto { Name = "Root" };
        root.NestedType.Add(new DescriptorProto
        {
            Name = "Inner",
            Field = { CreateField("text", 1, FieldDescriptorProto.Types.Type.String) },
        });

        var entry = new DescriptorProto { Name = "EntriesEntry", Options = new MessageOptions { MapEntry = true } };
        entry.Field.Add(CreateField("key", 1, FieldDescriptorProto.Types.Type.String));
        entry.Field.Add(CreateField("value", 2, FieldDescriptorProto.Types.Type.Message, ".fixture.Root.Inner"));
        root.NestedType.Add(entry);
        root.EnumType.Add(new EnumDescriptorProto
        {
            Name = "Mode",
            Value = { new EnumValueDescriptorProto { Name = "MODE_UNSPECIFIED", Number = 0 } },
        });
        root.OneofDecl.Add(new OneofDescriptorProto { Name = "choice" });
        root.OneofDecl.Add(new OneofDescriptorProto { Name = "_optional" });

        root.Field.Add(CreateField("double_value", 1, FieldDescriptorProto.Types.Type.Double));
        root.Field.Add(CreateField("float_value", 2, FieldDescriptorProto.Types.Type.Float));
        root.Field.Add(CreateField("int64_value", 3, FieldDescriptorProto.Types.Type.Int64));
        root.Field.Add(CreateField("uint64_value", 4, FieldDescriptorProto.Types.Type.Uint64));
        root.Field.Add(CreateField("int32_value", 5, FieldDescriptorProto.Types.Type.Int32));
        root.Field.Add(CreateField("fixed64_value", 6, FieldDescriptorProto.Types.Type.Fixed64));
        root.Field.Add(CreateField("fixed32_value", 7, FieldDescriptorProto.Types.Type.Fixed32));
        root.Field.Add(CreateField("bool_value", 8, FieldDescriptorProto.Types.Type.Bool));
        root.Field.Add(CreateField("string_value", 9, FieldDescriptorProto.Types.Type.String));
        root.Field.Add(CreateField("bytes_value", 10, FieldDescriptorProto.Types.Type.Bytes));
        root.Field.Add(CreateField("uint32_value", 11, FieldDescriptorProto.Types.Type.Uint32));
        root.Field.Add(CreateField("enum_value", 12, FieldDescriptorProto.Types.Type.Enum, ".fixture.Root.Mode"));
        root.Field.Add(CreateField("sfixed32_value", 13, FieldDescriptorProto.Types.Type.Sfixed32));
        root.Field.Add(CreateField("sfixed64_value", 14, FieldDescriptorProto.Types.Type.Sfixed64));
        root.Field.Add(CreateField("sint32_value", 15, FieldDescriptorProto.Types.Type.Sint32));
        root.Field.Add(CreateField("sint64_value", 16, FieldDescriptorProto.Types.Type.Sint64));
        root.Field.Add(CreateField("message_value", 17, FieldDescriptorProto.Types.Type.Message, ".fixture.Root.Inner"));
        root.Field.Add(CreateField("choice_value", 18, FieldDescriptorProto.Types.Type.String, oneofIndex: 0));
        root.Field.Add(CreateField("optional_value", 19, FieldDescriptorProto.Types.Type.Int32, oneofIndex: 1, proto3Optional: true));
        root.Field.Add(CreateField("entries", 20, FieldDescriptorProto.Types.Type.Message, ".fixture.Root.EntriesEntry", FieldDescriptorProto.Types.Label.Repeated));
        root.Field.Add(CreateField("packed_values", 21, FieldDescriptorProto.Types.Type.Int32, label: FieldDescriptorProto.Types.Label.Repeated, packed: true));
        root.Field.Add(CreateField("unpacked_values", 22, FieldDescriptorProto.Types.Type.Int32, label: FieldDescriptorProto.Types.Label.Repeated, packed: false));
        root.Field.Add(CreateField("any_value", 23, FieldDescriptorProto.Types.Type.Message, ".google.protobuf.Any"));
        file.MessageType.Add(root);
        return file;
    }

    private static FileDescriptorProto CreateGroupFile()
    {
        var file = new FileDescriptorProto { Name = "groups.proto", Package = "fixture", Syntax = "proto2" };
        var group = new DescriptorProto
        {
            Name = "Grouped",
            NestedType = { new DescriptorProto { Name = "LegacyGroup" } },
            Field =
            {
                CreateField("legacygroup", 1, FieldDescriptorProto.Types.Type.Group, ".fixture.Grouped.LegacyGroup"),
            },
        };
        file.MessageType.Add(group);
        return file;
    }

    private static FileDescriptorProto CreateGoogleFile()
    {
        var file = new FileDescriptorProto
        {
            Name = "google/protobuf/any.proto",
            Package = "google.protobuf",
            Syntax = "proto3",
        };
        var any = new DescriptorProto { Name = "Any" };
        any.Field.Add(CreateField("type_url", 1, FieldDescriptorProto.Types.Type.String));
        any.Field.Add(CreateField("value", 2, FieldDescriptorProto.Types.Type.Bytes));
        file.MessageType.Add(any);
        return file;
    }

    private static FieldDescriptorProto CreateField(
        string name,
        int number,
        FieldDescriptorProto.Types.Type type,
        string? typeName = null,
        FieldDescriptorProto.Types.Label label = FieldDescriptorProto.Types.Label.Optional,
        bool? packed = null,
        int? oneofIndex = null,
        bool proto3Optional = false)
    {
        var field = new FieldDescriptorProto { Name = name, Number = number, Type = type, Label = label };
        if (typeName is not null)
        {
            field.TypeName = typeName;
        }

        if (packed is not null)
        {
            field.Options = new FieldOptions { Packed = packed.Value };
        }

        if (oneofIndex is not null)
        {
            field.OneofIndex = oneofIndex.Value;
        }

        if (proto3Optional)
        {
            field.Proto3Optional = true;
        }

        return field;
    }
}
