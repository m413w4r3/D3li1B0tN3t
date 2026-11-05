using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Resolution;
using D3B.Protocol.Readable;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Resolution;

internal static class ResolverTestData
{
    public const string ClientVersion = "3.7.13.17";
    public const string SchemaHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static ProtocolResolver Resolver(FileDescriptor readableFile, IEnumerable<ReferenceCorpus>? references = null, IEnumerable<PriorMapping>? pins = null)
    {
        return new ProtocolResolver([readableFile], references ?? [], pins ?? []);
    }

    public static DiagnosticSchema Schema(string token, params DiagnosticField[] fields)
    {
        return new DiagnosticSchema
        {
            ClientVersion = ClientVersion,
            SchemaSha256 = SchemaHash,
            Messages =
            [
                new DiagnosticMessage
                {
                    Name = token,
                    Fields = fields
                }
            ]
        };
    }

    public static DiagnosticField Field(int number, DiagnosticFieldKind kind, bool repeated = false, bool packed = false, string? messageType = null)
    {
        return new DiagnosticField { Number = number, Name = "obfuscated", Kind = kind, Repeated = repeated, Packed = packed, MessageType = messageType };
    }

    public static DiagnosticAnalysis Analysis(string token, string direction, DiagnosticStatus status = DiagnosticStatus.Valid)
    {
        return new DiagnosticAnalysis { Index = 0, Direction = direction, CarriedType = token, TypeUrlOk = true, Message = token, Status = status };
    }

    public static FileDescriptor ReadableFile(params DescriptorProto[] messages)
    {
        FileDescriptorProto file = new() { Name = "resolver_fixture.proto", Package = "d3b.game", Syntax = "proto3" };
        file.MessageType.Add(messages);
        return FileDescriptor.BuildFromByteStrings([file.ToByteString()])[0];
    }

    public static DescriptorProto Message(string name, params FieldDescriptorProto[] fields)
    {
        DescriptorProto message = new() { Name = name };
        message.Field.Add(fields);
        return message;
    }

    public static FieldDescriptorProto ReadableField(string name, int number, FieldDescriptorProto.Types.Type type, bool repeated = false, string? messageType = null)
    {
        FieldDescriptorProto field = new()
        {
            Name = name,
            Number = number,
            Type = type,
            Label = repeated ? FieldDescriptorProto.Types.Label.Repeated : FieldDescriptorProto.Types.Label.Optional
        };
        if (messageType is not null)
        {
            field.TypeName = messageType;
        }

        return field;
    }

    public static FileDescriptor PartialReadableFile(string name, int fieldNumber, FieldDescriptorProto.Types.Type type)
    {
        FileDescriptorProto file = new()
        {
            Name = "partial_resolver_fixture.proto",
            Package = "d3b.game",
            Syntax = "proto3",
            Dependency = { OptionsReflection.Descriptor.Name }
        };
        DescriptorProto message = Message(name, ReadableField("value", fieldNumber, type));
        message.Options = new MessageOptions();
        message.Options.SetExtension(OptionsExtensions.Partial, true);
        file.MessageType.Add(message);
        ExtensionRegistry registry = new();
        registry.Add(OptionsExtensions.Partial);
        return FileDescriptor.BuildFromByteStrings(
            [DescriptorReflection.Descriptor.ToProto().ToByteString(), OptionsReflection.Descriptor.ToProto().ToByteString(), file.ToByteString()],
            registry)
            .Single(descriptor => descriptor.Name == file.Name);
    }
}
