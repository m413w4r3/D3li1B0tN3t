using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Translation;

public sealed class SchemaPayloadTests
{
    private static readonly FileDescriptor _file = FileDescriptor.BuildFromByteStrings([File().ToByteString()])[0];

    private static FileDescriptorProto File()
    {
        var file = new FileDescriptorProto { Name = "schema_payload.proto", Syntax = "proto3" };

        file.MessageType.Add(new DescriptorProto
        {
            Name = "scalar",
            Field = { new FieldDescriptorProto { Name = "value", Number = 1, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Optional } },
        });
        file.MessageType.Add(new DescriptorProto
        {
            Name = "nested",
            Field = { new FieldDescriptorProto { Name = "inner", Number = 1, Type = FieldDescriptorProto.Types.Type.Message, TypeName = ".scalar", Label = FieldDescriptorProto.Types.Label.Optional } },
        });
        file.MessageType.Add(new DescriptorProto
        {
            Name = "packed",
            Field = { new FieldDescriptorProto { Name = "values", Number = 1, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Repeated } },
        });
        file.MessageType.Add(new DescriptorProto
        {
            Name = "repeated_message",
            Field = { new FieldDescriptorProto { Name = "values", Number = 1, Type = FieldDescriptorProto.Types.Type.Message, TypeName = ".scalar", Label = FieldDescriptorProto.Types.Label.Repeated } },
        });

        var entry = new DescriptorProto { Name = "values_entry", Options = new MessageOptions { MapEntry = true } };
        entry.Field.Add(new FieldDescriptorProto { Name = "key", Number = 1, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Optional });
        entry.Field.Add(new FieldDescriptorProto { Name = "value", Number = 2, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Optional });

        var map = new DescriptorProto { Name = "map_message" };
        map.NestedType.Add(entry);
        map.Field.Add(new FieldDescriptorProto { Name = "values", Number = 1, Type = FieldDescriptorProto.Types.Type.Message, TypeName = ".map_message.values_entry", Label = FieldDescriptorProto.Types.Label.Repeated });
        file.MessageType.Add(map);

        file.MessageType.Add(new DescriptorProto
        {
            Name = "string_message",
            Field = { new FieldDescriptorProto { Name = "value", Number = 1, Type = FieldDescriptorProto.Types.Type.String, Label = FieldDescriptorProto.Types.Label.Optional } },
        });
        file.MessageType.Add(new DescriptorProto
        {
            Name = "fixed",
            Field = { new FieldDescriptorProto { Name = "value", Number = 1, Type = FieldDescriptorProto.Types.Type.Fixed64, Label = FieldDescriptorProto.Types.Label.Optional } },
        });
        file.MessageType.Add(new DescriptorProto { Name = "empty" });

        return file;
    }

    private static MessageDescriptor Message(string name)
    {
        return _file.MessageTypes.Single(message => message.Name == name);
    }

    private static bool IsValid(string name, params byte[] payload)
    {
        return SchemaPayload.IsValid(Message(name), payload);
    }

    [Fact]
    public void AScalarPayloadMustHoldOnlyItsFieldAndEndOnIt()
    {
        Assert.True(IsValid("scalar", 0x08, 0x01));
        Assert.True(IsValid("scalar"));
        Assert.False(IsValid("scalar", 0x10, 0x01));
        Assert.False(IsValid("scalar", 0x08, 0x01, 0x00));
        Assert.False(IsValid("scalar", 0x0D, 0x01, 0x00, 0x00, 0x00));
    }

    [Fact]
    public void ANestedPayloadIsWalkedTheSameWay()
    {
        Assert.True(IsValid("nested", 0x0A, 0x02, 0x08, 0x01));
        Assert.False(IsValid("nested", 0x0A, 0x02, 0x10, 0x01));
        Assert.False(IsValid("nested", 0x0A, 0x03, 0x08, 0x01));
    }

    [Fact]
    public void ARepeatedMessageIsNotAPackedScalar()
    {
        Assert.True(IsValid("repeated_message", 0x0A, 0x02, 0x08, 0x01));
        Assert.True(IsValid("repeated_message", 0x0A, 0x02, 0x08, 0x01, 0x0A, 0x02, 0x08, 0x02));
        Assert.False(IsValid("repeated_message", 0x0A, 0x02, 0x10, 0x01));
    }

    [Fact]
    public void AMapPayloadIsWalkedThroughItsEntry()
    {
        Assert.True(IsValid("map_message", 0x0A, 0x04, 0x08, 0x01, 0x10, 0x02));
        Assert.False(IsValid("map_message", 0x0A, 0x02, 0x18, 0x01));
        Assert.False(IsValid("map_message", 0x0A, 0x05, 0x08, 0x01));
    }

    [Fact]
    public void AStringPayloadMustBeUtf8()
    {
        Assert.True(IsValid("string_message", 0x0A, 0x01, 0x61));
        Assert.False(IsValid("string_message", 0x0A, 0x01, 0xFF));
    }

    [Fact]
    public void APackedPayloadMayBePackedOrUnpacked()
    {
        Assert.True(IsValid("packed", 0x08, 0x01, 0x08, 0x02));
        Assert.True(IsValid("packed", 0x0A, 0x02, 0x08, 0x01));
        Assert.False(IsValid("packed", 0x0A, 0x02, 0x08));
    }

    [Fact]
    public void AFixedPayloadNeedsItsWholeEightBytes()
    {
        Assert.True(IsValid("fixed", 0x09, 1, 2, 3, 4, 5, 6, 7, 8));
        Assert.False(IsValid("fixed", 0x09, 1, 2, 3));
        Assert.False(IsValid("fixed", 0x08, 0x01));
    }

    [Fact]
    public void AnEmptyPayloadHoldsNoByte()
    {
        Assert.True(IsValid("empty"));
        Assert.False(IsValid("empty", 0x08, 0x01));
    }
}
