using System.Text;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Schema;
using Google.Protobuf.Reflection;

namespace D3B.Diagnostic.Tests.Schema;

public sealed class DiagnosticSchemaExporterTests
{
    [Fact]
    public void ExportsEveryFieldKindAndDescriptorShapeInStableOrder()
    {
        var schema = DiagnosticSchemaExporter.Export("3.7.13.17", DiagnosticTestSchema.CreateProtocolSchema());
        var messages = schema.Messages.ToDictionary(message => message.Name, StringComparer.Ordinal);
        var root = messages["fixture.Root"];
        var fields = root.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);

        Assert.Equal(
            schema.Messages.Select(message => message.Name).Order(StringComparer.Ordinal),
            schema.Messages.Select(message => message.Name));
        Assert.Equal(
            root.Fields.Select(field => field.Number).Order(),
            root.Fields.Select(field => field.Number));
        Assert.Contains(DiagnosticFieldKind.DoublePrecision, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SinglePrecision, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedInteger64, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.UnsignedInteger64, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedInteger32, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Fixed64, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Fixed32, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Boolean, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Utf8String, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Bytes, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.UnsignedInteger32, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Enumeration, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedFixed32, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedFixed64, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedZigZag32, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.SignedZigZag64, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Message, root.Fields.Select(field => field.Kind));
        Assert.Contains(DiagnosticFieldKind.Group, messages["fixture.Grouped"].Fields.Select(field => field.Kind));
        Assert.Equal("fixture.Root.Inner", fields["message_value"].MessageType);
        Assert.Equal("fixture.Root.Mode", fields["enum_value"].EnumType);
        Assert.Equal("choice", fields["choice_value"].Oneof);
        Assert.Null(fields["optional_value"].Oneof);
        Assert.True(fields["optional_value"].HasPresence);
        Assert.True(fields["message_value"].HasPresence);
        Assert.True(fields["packed_values"].Packed);
        Assert.False(fields["unpacked_values"].Packed);
        Assert.Contains("fixture.Root.EntriesEntry", messages.Keys);
        Assert.True(messages["fixture.Root.EntriesEntry"].MapEntry);
        Assert.Contains("fixture.Root.Inner", messages.Keys);
        Assert.DoesNotContain(messages.Keys, name => name.StartsWith("google.protobuf.", StringComparison.Ordinal));
        Assert.Null(messages["fixture.Grouped"].Fields[0].MessageType);
        Assert.Equal(64, schema.SchemaSha256.Length);
        Assert.Equal(schema.SchemaSha256.ToLowerInvariant(), schema.SchemaSha256);
    }

    [Fact]
    public void WritesTheExactCompactSchemaJsonDeterministically()
    {
        var descriptorSet = DiagnosticTestSchema.CreateMinimalDescriptorSet();
        descriptorSet.File[0].MessageType[0].Field.Add(
            new FieldDescriptorProto
            {
                Name = "value",
                Number = 1,
                Type = FieldDescriptorProto.Types.Type.Int32,
                Label = FieldDescriptorProto.Types.Label.Optional,
            });
        var first = DiagnosticSchemaExporter.Export("test", descriptorSet);
        var second = DiagnosticSchemaExporter.Export("test", descriptorSet);
        const string expected = "{\"kind\":\"schema\",\"messages\":[{\"name\":\"fixture.Empty\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"value\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":true,\"messageType\":null,\"enumType\":null,\"oneof\":null,\"hasPresence\":false}]}]}";

        Assert.Equal(expected, first.ToJson());
        Assert.Equal(first.ToJson(), second.ToJson());
        using var output = new MemoryStream();
        first.Write(output);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), output.ToArray());
    }

    [Fact]
    public void RejectsDuplicateFieldNumbers()
    {
        var descriptorSet = DiagnosticTestSchema.CreateDuplicateFieldNumberSet();

        Assert.Throws<DiagnosticSchemaException>(() => DiagnosticSchemaExporter.Export("test", descriptorSet));
    }

    [Fact]
    public void RejectsDanglingMessageTypes()
    {
        var descriptorSet = DiagnosticTestSchema.CreateDanglingMessageTypeSet();

        Assert.Throws<DiagnosticSchemaException>(() => DiagnosticSchemaExporter.Export("test", descriptorSet));
    }
}
