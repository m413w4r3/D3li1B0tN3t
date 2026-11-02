using D3B.Protocol.Connection;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Fixtures;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Mapping;

public sealed class ContractAuditTests
{
    // The readable protocol's size, which the audit checks whole: messages, fields, enums and enum values.
    private const int ReadableMessages = 222;
    private const int ReadableFields = 492;
    private const int ReadableEnums = 29;
    private const int ReadableValues = 206;

    /// <summary>Maps the readable protocol against its obfuscation, then audits the mapping against the schema as a test changed it.</summary>
    private static AuditReport Audit(Action<Obfuscation>? change = null, Action<ProtocolMapping>? edit = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var schema = obfuscation.Schema();
        var mapping = new ProtocolMatcher(ReadableProtocol.Files, schema, ReadableProtocol.Scopes).Match(obfuscation.Pins(Obfuscation.GamePayloads()), [])
            .ToMapping("3.7.12.12", schema.Sha256, ReadableProtocol.Digest, "test");

        edit?.Invoke(mapping);

        if (change is null)
            return ContractAudit.Audit(ReadableProtocol.Files, schema, mapping);

        change(obfuscation);

        var changed = obfuscation.Schema();

        mapping.SchemaSha256 = ByteString.CopyFrom([.. changed.Sha256]);

        return ContractAudit.Audit(ReadableProtocol.Files, changed, mapping);
    }

    private static FieldDescriptorProto Field(Obfuscation obfuscation, MessageDescriptor message, int number)
    {
        return obfuscation.Message(message.FullName).Field.Single(field => field.Number == number);
    }

    [Fact]
    public void TheMappingOfTheReadableProtocolHolds()
    {
        var report = Audit();

        Assert.True(report.Passed, string.Join('\n', report.Failures));
        Assert.Empty(report.Notes);
        Assert.Empty(report.Unread);
        Assert.Equal((ReadableMessages, ReadableFields, ReadableEnums, ReadableValues), (report.Messages, report.Fields, report.Enums, report.Values));
    }

    [Fact]
    public void WhatAPartialMessageLeavesOutIsCountedApartFromWhatTheBuildAdds()
    {
        var report = Audit(obfuscation =>
        {
            obfuscation.Message(MapInformationEvent.Descriptor.FullName).Field.Add(new FieldDescriptorProto
            {
                Name = "left",
                Number = 20,
                Type = FieldDescriptorProto.Types.Type.Bool,
                Label = FieldDescriptorProto.Types.Label.Optional,
            });
            obfuscation.Message(MapCurrentEvent.Descriptor.FullName).Field.Add(new FieldDescriptorProto
            {
                Name = "added",
                Number = 2,
                Type = FieldDescriptorProto.Types.Type.Bool,
                Label = FieldDescriptorProto.Types.Label.Optional,
            });
        });

        Assert.True(report.Passed, string.Join('\n', report.Failures));
        Assert.StartsWith("d3b.game.MapInformationEvent: 1 of the 7 fields", Assert.Single(report.Unread), StringComparison.Ordinal);
        Assert.StartsWith("d3b.game.MapCurrentEvent: 1 of the 2 fields", Assert.Single(report.Notes), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnyFieldFacingAnotherTypeFails()
    {
        var report = Audit(obfuscation => Field(obfuscation, GameEvent.Descriptor, GameEvent.ContentFieldNumber).TypeName = "." + obfuscation.Of(GameRequest.Descriptor.FullName));

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 1 of d3b.game.GameEvent", StringComparison.Ordinal) && failure.Contains("does not have the type", StringComparison.Ordinal));
    }

    [Fact]
    public void AReadableMessageWithoutCounterpartFails()
    {
        var report = Audit(edit: mapping => mapping.Messages.Remove(mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName)));

        Assert.Contains("d3b.connection.Ping has no counterpart.", report.Failures);
    }

    [Fact]
    public void AReadableFieldWithoutCounterpartFails()
    {
        var report = Audit(edit: mapping =>
        {
            var pair = mapping.Messages.Single(pair => pair.Readable == ServerSelectionRequest.Descriptor.FullName);

            pair.Fields.Clear();
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 1 of d3b.connection.ServerSelectionRequest", StringComparison.Ordinal) && failure.Contains("has no counterpart", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldOfAnotherTypeFails()
    {
        var report = Audit(obfuscation => Field(obfuscation, ServerSelectionRequest.Descriptor, 1).Type = FieldDescriptorProto.Types.Type.Int64);

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 1 of d3b.connection.ServerSelectionRequest", StringComparison.Ordinal) && failure.Contains("does not have the type", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldOfAnotherMessageTypeFails()
    {
        var report = Audit(obfuscation =>
        {
            var field = Field(obfuscation, ServerUpdate.Descriptor, 1);

            field.TypeName = "." + obfuscation.Of(GameServer.Descriptor.FullName);
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 1 of d3b.connection.ServerUpdate", StringComparison.Ordinal) && failure.Contains("does not have the type", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldOfAnotherLabelFails()
    {
        var report = Audit(obfuscation => Field(obfuscation, ServerSelectionResponse.Types.Success.Descriptor, 3).Label = FieldDescriptorProto.Types.Label.Optional);

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 3 of d3b.connection.ServerSelectionResponse.Success", StringComparison.Ordinal) && failure.Contains("does not have the label", StringComparison.Ordinal));
    }

    [Fact]
    public void AMapFacingAListOfMessagesFails()
    {
        var entry = ObjectUseMultipleRequest.Types.Unmeasured.Descriptor.FindFieldByNumber(1).MessageType;
        var report = Audit(obfuscation => obfuscation.Message(entry.FullName).Options = null);

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 1 of d3b.game.ObjectUseMultipleRequest.Unmeasured", StringComparison.Ordinal) && failure.Contains("does not have the label", StringComparison.Ordinal));
    }

    [Fact]
    public void AMapOfAnotherValueTypeFails()
    {
        var entry = ObjectUseMultipleRequest.Types.Unmeasured.Descriptor.FindFieldByNumber(1).MessageType;
        var report = Audit(obfuscation => Field(obfuscation, entry, 2).Type = FieldDescriptorProto.Types.Type.Int32);

        Assert.Contains(report.Failures, failure => failure.StartsWith($"Field 2 of {entry.FullName}", StringComparison.Ordinal) && failure.Contains("does not have the type", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldOfAnotherPresenceFails()
    {
        var report = Audit(obfuscation =>
        {
            var message = obfuscation.Message(IdentificationResponse.Types.Success.Descriptor.FullName);
            var field = message.Field.Single(field => field.Number == 7);

            message.OneofDecl.RemoveAt(field.OneofIndex);
            field.ClearOneofIndex();
            field.ClearProto3Optional();
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 7 of d3b.connection.IdentificationResponse.Success", StringComparison.Ordinal) && failure.Contains("does not have the presence", StringComparison.Ordinal));
    }

    [Fact]
    public void AFieldOutOfItsOneofFails()
    {
        var report = Audit(obfuscation => Field(obfuscation, ConnectionRequest.Descriptor, 8).ClearOneofIndex());

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 8 of d3b.connection.ConnectionRequest", StringComparison.Ordinal) && failure.Contains("in a oneof", StringComparison.Ordinal));
    }

    [Fact]
    public void FieldsOfOneOneofInTwoFail()
    {
        var report = Audit(obfuscation =>
        {
            var message = obfuscation.Message(ConnectionRequest.Descriptor.FullName);

            message.OneofDecl.Add(new OneofDescriptorProto { Name = "split" });
            message.Field.Single(field => field.Number == 8).OneofIndex = message.OneofDecl.Count - 1;
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 8 of d3b.connection.ConnectionRequest", StringComparison.Ordinal) && failure.Contains("another oneof", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissingEnumValueFails()
    {
        var report = Audit(obfuscation =>
        {
            var type = obfuscation.Enum("d3b.connection.ServerInformation.Accessibility");

            type.Value.RemoveAt(type.Value.Count - 1);
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Value 3 of d3b.connection.ServerInformation.Accessibility", StringComparison.Ordinal));
    }

    [Fact]
    public void WhatTheBuildAddsIsNotedNotFailed()
    {
        var report = Audit(obfuscation =>
        {
            obfuscation.Message(GameServer.Descriptor.FullName).Field.Add(new FieldDescriptorProto
            {
                Name = "added",
                Number = 9,
                Type = FieldDescriptorProto.Types.Type.Bool,
                Label = FieldDescriptorProto.Types.Label.Optional,
            });
            obfuscation.Enum("d3b.connection.ServerType").Value.Add(new EnumValueDescriptorProto { Name = "added", Number = 7 });
        });

        Assert.True(report.Passed, string.Join('\n', report.Failures));
        Assert.Equal(2, report.Notes.Count);
    }

    [Fact]
    public void DuplicatedOrStrayNumberPairsFail()
    {
        var report = Audit(edit: mapping =>
        {
            var server = mapping.Messages.Single(pair => pair.Readable == GameServer.Descriptor.FullName);
            var type = mapping.Enums.Single(pair => pair.Readable == "d3b.connection.ServerType");

            server.Fields.Add(new NumberPair { Readable = 1, Obfuscated = 1 });
            server.Fields.Add(new NumberPair { Readable = 99, Obfuscated = 1 });
            type.Values.Add(new NumberPair { Readable = 2, Obfuscated = 2 });
            type.Values.Add(new NumberPair { Readable = 99, Obfuscated = 0 });
        });

        Assert.Contains("The mapping pairs field of d3b.connection.GameServer 1 twice.", report.Failures);
        Assert.Contains("The mapping pairs field of d3b.connection.GameServer 99, which the readable protocol lacks.", report.Failures);
        Assert.Contains("The mapping pairs value of d3b.connection.ServerType 2 twice.", report.Failures);
        Assert.Contains("The mapping pairs value of d3b.connection.ServerType 99, which the readable protocol lacks.", report.Failures);
    }

    [Fact]
    public void TwoReadableFieldsOnOneObfuscatedFieldFail()
    {
        var report = Audit(edit: mapping =>
        {
            var pair = mapping.Messages.Single(pair => pair.Readable == ServerSelectionResponse.Types.Success.Descriptor.FullName);

            pair.Fields.Single(field => field.Readable == 2).Obfuscated = 1;
        });

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 2 of d3b.connection.ServerSelectionResponse.Success", StringComparison.Ordinal) && failure.Contains("already the counterpart of field 1", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoReadableOneofsInOneObfuscatedOneofFail()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var schema = obfuscation.Schema();
        var scopes = new Dictionary<string, string>(StringComparer.Ordinal) { ["d3b.fixture"] = ReadableProtocol.ConnectionSchemaFile };
        var mapping = new ProtocolMatcher([FixtureReflection.Descriptor], schema, scopes).Match(null, [])
            .ToMapping("3.7.12.12", schema.Sha256, ProtocolDigest.Of([FixtureReflection.Descriptor]), "test");
        var root = obfuscation.Message(Root.Descriptor.FullName);

        root.Field.Single(field => field.Number == 3).OneofIndex = root.Field.Single(field => field.Number == 1).OneofIndex;

        var changed = obfuscation.Schema();

        mapping.SchemaSha256 = ByteString.CopyFrom([.. changed.Sha256]);

        var report = ContractAudit.Audit([FixtureReflection.Descriptor], changed, mapping);

        Assert.Contains(report.Failures, failure => failure.StartsWith("Field 3 of d3b.fixture.Root", StringComparison.Ordinal) && failure.Contains("oneof content's fields are in", StringComparison.Ordinal));
    }

    [Fact]
    public void AMappingOfAnotherReadableProtocolOrSchemaFails()
    {
        var report = Audit(edit: mapping =>
        {
            mapping.ReadableDigest = ByteString.CopyFrom(new byte[32]);
            mapping.SchemaSha256 = ByteString.CopyFrom(new byte[32]);
            mapping.FormatVersion = MappingFile.FormatVersion + 1;
        });

        Assert.Contains("The mapping was matched against another readable protocol.", report.Failures);
        Assert.Contains("The mapping was matched against another schema.", report.Failures);
        Assert.Contains(report.Failures, failure => failure.StartsWith("The mapping is of format", StringComparison.Ordinal));
    }

    [Fact]
    public void AnObfuscatedMessageCounterpartOfTwoFails()
    {
        var report = Audit(edit: mapping =>
        {
            var ping = mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName);

            mapping.Messages.Single(pair => pair.Readable == Pong.Descriptor.FullName).Obfuscated = ping.Obfuscated;
        });

        Assert.Contains(report.Failures, failure => failure.Contains("is the counterpart of both", StringComparison.Ordinal));
    }
}
