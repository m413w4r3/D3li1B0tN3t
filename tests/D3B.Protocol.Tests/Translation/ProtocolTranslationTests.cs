using System.Buffers;
using D3B.Network.Tests;
using D3B.Protocol.Connection;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Protocol.Tests.Translation;

public sealed class ProtocolTranslationTests
{
    private const string Version = "3.7.12.12";

    /// <summary>Maps the readable protocol against its obfuscation, as a test changed the schema and the mapping, then translates it.</summary>
    private static ProtocolTranslation Translate(Action<Obfuscation>? change = null, Action<Obfuscation, ProtocolMapping>? edit = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var mapping = new ProtocolMatcher(ReadableProtocol.Files, obfuscation.Schema(), ReadableProtocol.Scopes).Match(obfuscation.Pins(Obfuscation.GamePayloads()), [])
            .ToMapping(Version, obfuscation.Schema().Sha256, ReadableProtocol.Digest, "test");

        change?.Invoke(obfuscation);
        edit?.Invoke(obfuscation, mapping);

        var schema = obfuscation.Schema();

        mapping.SchemaSha256 = ByteString.CopyFrom([.. schema.Sha256]);

        return ProtocolTranslation.Create(schema, mapping);
    }

    private static int Count(Google.Protobuf.Reflection.MessageDescriptor message)
    {
        return 1 + message.NestedTypes.Sum(Count);
    }

    /// <summary>A game frame carrying a message as the build's do: its type URL names the obfuscated message, its value is extra bytes appended.</summary>
    private static ReadOnlySequence<byte> GameEventFrame(IMessage content, string? typeName = null, byte[]? extra = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var any = new Any
        {
            TypeUrl = "type.ankama.com/" + (typeName ?? obfuscation.Of(content.Descriptor.FullName)),
            Value = ByteString.CopyFrom([.. content.ToByteArray(), .. extra ?? []]),
        };

        return new ReadOnlySequence<byte>(new GameMessage { Event = new GameEvent { Content = any } }.ToByteArray());
    }

    private static ConnectionMessage ServerSelection()
    {
        return new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "b",
                ServerSelection = new ServerSelectionResponse
                {
                    Success = new ServerSelectionResponse.Types.Success { Ticket = new string('t', 32), Host = "game.test", Ports = { 5555, 443 } },
                },
            },
        };
    }

    [Fact]
    public void AMappingThatHoldsIsTranslated()
    {
        var translation = Translate();

        Assert.Equal(Version, translation.ClientVersion);
        Assert.True(translation.Audit.Passed);
        Assert.Equal(ReadableProtocol.Files.Sum(file => file.MessageTypes.Sum(Count)), translation.Audit.Messages);
    }

    [Fact]
    public void AMappingThatFailsTheAuditIsRefused()
    {
        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(edit: (_, mapping) => mapping.Messages.Remove(mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName))));

        Assert.Contains("d3b.connection.Ping has no counterpart.", exception.Failures);
        Assert.StartsWith($"The mapping of client {Version} does not hold: {exception.Failures.Count} failures, the first: ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildRenumberingAFieldIsRefused()
    {
        // The audit holds: the counterpart's field 7 has the shape of the readable field 1. Only its number differs.
        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(
            obfuscation => obfuscation.Message(ServerSelectionRequest.Descriptor.FullName).Field.Single(field => field.Number == 1).Number = 7,
            (_, mapping) => mapping.Messages.Single(pair => pair.Readable == ServerSelectionRequest.Descriptor.FullName).Fields.Single().Obfuscated = 7));

        var failure = Assert.Single(exception.Failures);

        Assert.StartsWith("d3b.connection.ServerSelectionRequest field 1 is field 7 of .", failure, StringComparison.Ordinal);
        Assert.EndsWith("the build renumbers it.", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildRenumberingAnEnumValueIsRefused()
    {
        const string accessibility = "d3b.connection.ServerInformation.Accessibility";

        var exception = Assert.Throws<ProtocolMismatchException>(() => Translate(
            obfuscation => obfuscation.Enum(accessibility).Value.Single(value => value.Number == 3).Number = 9,
            (_, mapping) => mapping.Enums.Single(pair => pair.Readable == accessibility).Values.Single(value => value.Readable == 3).Obfuscated = 9));

        var failure = Assert.Single(exception.Failures);

        Assert.StartsWith($"{accessibility} value 3 is value 9 of .", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrameOfTheConnectionIsUnderstoodWholeWhereverTheNetworkCutIt()
    {
        var bytes = ServerSelection().ToByteArray();
        var decoded = Translate().DecodeConnection(TestSequences.Segments(bytes, 7));

        Assert.True(decoded.IsUnderstood, decoded.Problem);
        Assert.Equal(ServerSelection(), decoded.Message);
    }

    [Fact]
    public void AFrameHoldingWhatTheReadableProtocolLacksIsNotUnderstood()
    {
        // Field 15 of the root, a varint: no readable message has it.
        byte[] bytes = [.. ServerSelection().ToByteArray(), 15 << 3, 1];
        var decoded = Translate().DecodeConnection(new ReadOnlySequence<byte>(bytes));

        Assert.False(decoded.IsUnderstood);
        Assert.Equal(ServerSelection(), decoded.Message);
        Assert.Contains($"encodes back to {bytes.Length - 2} of its {bytes.Length} bytes", decoded.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesThatAreNoMessageHaveNone()
    {
        // A length-delimited field 2 declaring more bytes than the frame holds.
        var decoded = Translate().DecodeConnection(new ReadOnlySequence<byte>([0x12, 0x7F, 0x01]));

        Assert.False(decoded.IsUnderstood);
        Assert.Null(decoded.Message);
        Assert.StartsWith("it is no connection message: ", decoded.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AGameFrameIsReadThroughTheMessageItsTypeUrlNames()
    {
        var current = new MapCurrentEvent { MapId = 188 };
        var decoded = Translate().DecodeGame(GameEventFrame(current));

        Assert.True(decoded.IsUnderstood, decoded.Problem);
        Assert.True(decoded.IsRead);
        Assert.Equal(MapCurrentEvent.Descriptor, decoded.ReadableType);
        Assert.Equal(current, decoded.Content);
    }

    [Fact]
    public void AFrameOfAPartialMessageHoldingWhatItLeavesOutIsReadNotUnderstood()
    {
        // Field 20 of the map's information, a varint of 1, its tag in two bytes: the readable message leaves it out on purpose.
        var information = new MapInformationEvent { MapId = 188, SubAreaId = 5 };
        var decoded = Translate().DecodeGame(GameEventFrame(information, extra: [0xA0, 0x01, 0x01]));

        Assert.True(decoded.IsRead);
        Assert.False(decoded.IsUnderstood);
        Assert.Equal(information, decoded.Content);
        Assert.Contains("encodes back to", decoded.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AGameFrameCarryingAMessageTheReadableProtocolLacksIsNotRead()
    {
        var decoded = Translate().DecodeGame(GameEventFrame(new MapCurrentEvent { MapId = 1 }, "unknown"));

        Assert.False(decoded.IsRead);
        Assert.False(decoded.IsUnderstood);
        Assert.NotNull(decoded.Envelope);
        Assert.Equal("unknown", decoded.CarriedType);
        Assert.Null(decoded.ReadableType);
        Assert.Equal("it carries .unknown, which the readable protocol lacks.", decoded.Problem);
    }

    [Fact]
    public void AGameRequestCarryingNothingIsUnderstood()
    {
        var bytes = new GameMessage { Request = new GameRequest { Id = -1 } }.ToByteArray();
        var decoded = Translate().DecodeGame(new ReadOnlySequence<byte>(bytes));

        Assert.True(decoded.IsUnderstood, decoded.Problem);
        Assert.False(decoded.IsRead);
        Assert.Null(decoded.CarriedType);
    }

    [Fact]
    public void BytesThatAreNoGameMessageHaveNoEnvelope()
    {
        var decoded = Translate().DecodeGame(new ReadOnlySequence<byte>([0x12, 0x7F, 0x01]));

        Assert.False(decoded.IsRead);
        Assert.Null(decoded.Envelope);
        Assert.StartsWith("it is no game message: ", decoded.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ARequestIsEncodedAsTheClientSendsOne()
    {
        var request = new MapMovementRequest { KeyMovements = { 398, 4495, 457, 461 }, MapId = 188744706 };
        var translation = Translate();
        var frame = translation.EncodeGameRequest(request);
        var envelope = GameMessage.Parser.ParseFrom(frame);
        var decoded = translation.DecodeGame(new ReadOnlySequence<byte>(frame));

        Assert.Equal(-1, envelope.Request.Id);
        Assert.Equal("type.ankama.com/" + new Obfuscation(ReadableProtocol.Files).Of(MapMovementRequest.Descriptor.FullName), envelope.Request.Content.TypeUrl);
        Assert.True(decoded.IsUnderstood, decoded.Problem);
        Assert.Equal(request, decoded.Content);
    }

    [Fact]
    public void UsingObjectsAndMovingAStackOutOfAStorageAreEncodedAsTheClientSendsThem()
    {
        var translation = Translate();
        IMessage[] requests =
        [
            new ObjectUseRequest { Uid = 2752363 },
            new ObjectUseMultipleRequest { Uid = 2752342, Quantity = 266 },
            new ObjectUseMultipleRequest
            {
                Uid = 2752342,
                Message2 = new ObjectUseMultipleRequest.Types.Unmeasured { Map1 = { [true] = "a" }, Numbers2 = { 3 } },
                Quantity = 266,
            },
            new ExchangeObjectMoveRequest { Uid = 1443864, Quantity = -15 },
        ];

        foreach (var request in requests)
        {
            var decoded = translation.DecodeGame(new ReadOnlySequence<byte>(translation.EncodeGameRequest(request)));

            Assert.True(decoded.IsUnderstood, decoded.Problem);
            Assert.Equal(request, decoded.Content);
        }
    }

    [Fact]
    public void TheLifeIsReadFromTheCharacteristicsLeavingTheirOtherFieldsOut()
    {
        var stats = new CharacterStats
        {
            Characteristics =
            {
                new Characteristic { Id = 0, Detailed = new DetailedCharacteristic { Number7 = 505 } },
                new Characteristic { Id = 11, Detailed = new DetailedCharacteristic { Number5 = 1016 } },
                new Characteristic { Id = 97, Detailed = new DetailedCharacteristic { Number7 = -760 } },
            },
        };

        // Field 2 of the characteristics, a varint, as the build sets: the readable message leaves it out on purpose.
        byte[] inner = [.. stats.ToByteArray(), 0x10, 0x05];
        var decoded = Translate().DecodeGame(GameEventFrame(new CharacterStatsEvent(), extra: [0x0A, (byte)inner.Length, .. inner]));

        Assert.True(decoded.IsRead);
        Assert.False(decoded.IsUnderstood);
        Assert.Equal(new CharacterStatsEvent { Stats = stats }, decoded.Content);
    }

    [Fact]
    public void TheGuildChestsOpeningIsReadByItsType()
    {
        // Fields 2 and 3, varints of 1 and 100 as the build sets them, are not measured: the readable message leaves them out.
        var decoded = Translate().DecodeGame(GameEventFrame(new ExchangeStartedWithTabbedStorageEvent { Type = ExchangeType.GuildChest }, extra: [0x10, 0x01, 0x18, 0x64]));

        Assert.True(decoded.IsRead);
        Assert.Equal(ExchangeType.GuildChest, Assert.IsType<ExchangeStartedWithTabbedStorageEvent>(decoded.Content).Type);
    }

    [Fact]
    public void APartialMessageIsNeverEncodedAsARequest()
    {
        var exception = Assert.Throws<ArgumentException>(() => Translate().EncodeGameRequest(new MapInformationEvent()));

        Assert.Contains("partial", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAMessageTheGamesFramesCarryIsEncoded()
    {
        var translation = Translate();

        Assert.Throws<ArgumentException>(() => translation.EncodeGameRequest(new GameRequest()));
        Assert.Throws<ArgumentException>(() => translation.EncodeGameRequest(new Ping()));
        Assert.Throws<ArgumentException>(() => translation.EncodeGameRequest(new ExchangeKamasRequest.Types.Unmeasured()));
    }
}
