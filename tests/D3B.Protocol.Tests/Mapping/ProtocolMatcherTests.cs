using D3B.Protocol.Connection;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Fixtures;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Mapping;

public sealed class ProtocolMatcherTests
{
    private static readonly Dictionary<string, string> _fixtureScopes = new(StringComparer.Ordinal)
    {
        ["d3b.fixture"] = ReadableProtocol.ConnectionSchemaFile,
    };

    private static readonly IReadOnlyList<FileDescriptor> _connection = [.. ReadableProtocol.Files.Where(file => file.Package == ReadableProtocol.ConnectionPackage)];

    private static MatchResult MatchConnection(Obfuscation obfuscation)
    {
        return new ProtocolMatcher(_connection, obfuscation.Schema(), ReadableProtocol.Scopes).Match(null, []);
    }

    private static MatchResult MatchReadable(Obfuscation obfuscation, PinnedPairs? pins = null)
    {
        return new ProtocolMatcher(ReadableProtocol.Files, obfuscation.Schema(), ReadableProtocol.Scopes).Match(pins, []);
    }

    private static MatchResult MatchFixture(Obfuscation obfuscation, PinnedPairs? pins = null)
    {
        return new ProtocolMatcher([FixtureReflection.Descriptor], obfuscation.Schema(), _fixtureScopes).Match(pins, []);
    }

    private static MessageMatch Find(MatchResult result, MessageDescriptor readable)
    {
        return result.Messages.Single(match => match.Readable == readable);
    }

    [Fact]
    public void TheConnectionPhaseMatchesItsObfuscationFromTheStructureAlone()
    {
        var obfuscation = new Obfuscation(_connection);
        var result = MatchConnection(obfuscation);

        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Unpaired);
        Assert.Equal(37, result.Messages.Count);
        Assert.Equal(9, result.Enums.Count);
        Assert.All(result.Messages, match => Assert.Equal(obfuscation.Of(match.Readable.FullName), match.Obfuscated.FullName));
        Assert.All(result.Enums, match => Assert.Equal(obfuscation.Of(match.Readable.FullName), match.Obfuscated.FullName));
        Assert.All(result.Messages, match => Assert.All(match.Fields, field => Assert.Equal(field.Readable, field.Obfuscated)));
    }

    [Fact]
    public void TheGameMatchesItsObfuscationOnceTheMessagesItCarriesArePinned()
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var result = MatchReadable(obfuscation, obfuscation.Pins(Obfuscation.GamePayloads()));

        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Unpaired);
        Assert.All(result.Messages, match => Assert.Equal(obfuscation.Of(match.Readable.FullName), match.Obfuscated.FullName));
        Assert.Equal(MatchEvidence.Pin, Find(result, MapInformationEvent.Descriptor).Evidence);
        Assert.Equal(MatchEvidence.Field, Find(result, Actor.Descriptor).Evidence);
    }

    [Fact]
    public void TheGameLeavesTheMessagesItCarriesOpenWithoutPins()
    {
        var result = MatchReadable(new Obfuscation(ReadableProtocol.Files));

        Assert.Empty(result.Conflicts);
        Assert.Contains(ContextDestroyEvent.Descriptor.FullName, result.Unpaired);
        Assert.Contains(CharacterListRequest.Descriptor.FullName, result.Unpaired);
    }

    [Fact]
    public void AWellKnownTypeIsItsOwnCounterpartAndNoPair()
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var result = MatchReadable(obfuscation, obfuscation.Pins(Obfuscation.GamePayloads()));

        Assert.Equal([(1, 1), (2, 2)], Find(result, GameRequest.Descriptor).Fields);
        Assert.DoesNotContain(result.Messages, match => match.Readable.File.Package == "google.protobuf");
    }

    [Fact]
    public void AWellKnownTypeFacingAnotherTypeIsAConflict()
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var content = obfuscation.Message(GameRequest.Descriptor.FullName).Field.Single(field => field.Number == GameRequest.ContentFieldNumber);

        content.TypeName = "." + obfuscation.Of(GameEvent.Descriptor.FullName);

        var result = MatchReadable(obfuscation, obfuscation.Pins(Obfuscation.GamePayloads()));

        Assert.Contains(result.Conflicts, conflict => conflict.Contains("google.protobuf.Any", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyMessagesArePairedOnlyThroughTheFieldsUsingThem()
    {
        var result = MatchConnection(new Obfuscation(_connection));

        Assert.Equal(MatchEvidence.Field, Find(result, Ping.Descriptor).Evidence);
        Assert.Equal(MatchEvidence.Field, Find(result, Pong.Descriptor).Evidence);
        Assert.Equal(MatchEvidence.Field, Find(result, ReleaseAccountRequest.Descriptor).Evidence);
    }

    [Fact]
    public void AnchorsComeBeforeFieldsAndScores()
    {
        var obfuscation = new Obfuscation(_connection);
        var schema = obfuscation.Schema();
        var root = schema.FindMessage(obfuscation.Of(ConnectionMessage.Descriptor.FullName))!;
        var anchors = new[] { new Anchor(ConnectionMessage.Descriptor, root, [(1, 1), (2, 2)]) };
        var result = new ProtocolMatcher(_connection, schema, ReadableProtocol.Scopes).Match(null, anchors);

        Assert.Empty(result.Unpaired);
        Assert.Equal(MatchEvidence.Capture, Find(result, ConnectionMessage.Descriptor).Evidence);
        Assert.All(result.Messages.Where(match => match.Readable != ConnectionMessage.Descriptor), match => Assert.Equal(MatchEvidence.Field, match.Evidence));
    }

    [Fact]
    public void AnAnchorContradictingTheFieldsIsAConflict()
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);
        var schema = obfuscation.Schema();
        var anchors = new[]
        {
            new Anchor(ConnectionMessage.Descriptor, schema.FindMessage(obfuscation.Of(ConnectionMessage.Descriptor.FullName))!, []),
            new Anchor(ServerSelectionRequest.Descriptor, schema.FindMessage(obfuscation.Of(ForceAccountRequest.Descriptor.FullName))!, []),
        };
        var result = new ProtocolMatcher(ReadableProtocol.Files, schema, ReadableProtocol.Scopes).Match(null, anchors);

        Assert.NotEmpty(result.Conflicts);
    }

    [Fact]
    public void MessagesNoFieldReachesAndAlikeInShapeAreLeftUnpaired()
    {
        var result = MatchFixture(new Obfuscation([FixtureReflection.Descriptor]));

        Assert.Empty(result.Conflicts);
        Assert.Equal(["d3b.fixture.LoneFirst", "d3b.fixture.LoneSecond"], result.Unpaired);
    }

    [Fact]
    public void APinSettlesWhatTheStructureLeavesOpen()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var pins = new PinnedPairs { Messages = { new MessagePin { Readable = "d3b.fixture.LoneFirst", Obfuscated = obfuscation.Of("d3b.fixture.LoneFirst") } } };
        var result = MatchFixture(obfuscation, pins);

        Assert.Empty(result.Unpaired);
        Assert.Equal(MatchEvidence.Pin, Find(result, LoneFirst.Descriptor).Evidence);
        Assert.Equal(obfuscation.Of("d3b.fixture.LoneSecond"), Find(result, LoneSecond.Descriptor).Obfuscated.FullName);
    }

    [Fact]
    public void ARowClearOfItsRivalsButNotOfItsColumnIsLeftUnpaired()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);

        obfuscation.Message("d3b.fixture.LoneFirst").Field.Add(new FieldDescriptorProto
        {
            Name = "added",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Bool,
            Label = FieldDescriptorProto.Types.Label.Optional,
        });

        // LoneFirst now scores 1 against the obfuscated LoneSecond only, but so does LoneSecond: that column is a tie.
        Assert.Equal(["d3b.fixture.LoneFirst", "d3b.fixture.LoneSecond"], MatchFixture(obfuscation).Unpaired);
    }

    [Fact]
    public void AScoreBelowTheMinimumIsNotAccepted()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var pins = new PinnedPairs { Messages = { new MessagePin { Readable = "d3b.fixture.LoneSecond", Obfuscated = obfuscation.Of("d3b.fixture.LoneSecond") } } };

        obfuscation.Message("d3b.fixture.LoneFirst").Field.Add(new FieldDescriptorProto
        {
            Name = "added",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Bool,
            Label = FieldDescriptorProto.Types.Label.Optional,
        });

        Assert.Equal(["d3b.fixture.LoneFirst"], MatchFixture(obfuscation, pins).Unpaired);
    }

    [Fact]
    public void AMarginOfExactlyOneTenthClears()
    {
        Assert.True(ProtocolMatcher.Clears(1.0, 0.9));
        Assert.True(ProtocolMatcher.Clears(0.9, 0.8));
        Assert.False(ProtocolMatcher.Clears(1.0, 0.95));
        Assert.False(ProtocolMatcher.Clears(0.75, 0));
    }

    [Fact]
    public void APinnedFieldPairsFieldsWhoseNumbersDiffer()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var right = obfuscation.Message("d3b.fixture.Right");

        right.Field.Single(field => field.Number == 2).Number = 7;

        var pins = new PinnedPairs
        {
            Messages = { new MessagePin { Readable = "d3b.fixture.Right", Obfuscated = obfuscation.Of("d3b.fixture.Right"), Fields = { new NumberPair { Readable = 2, Obfuscated = 7 } } } },
        };
        var result = MatchFixture(obfuscation, pins);

        Assert.Equal([(1, 1), (2, 7)], Find(result, Right.Descriptor).Fields);
    }

    [Fact]
    public void APinOfAFieldTheReadableMessageLacksIsAConflict()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var pins = new PinnedPairs
        {
            Messages = { new MessagePin { Readable = "d3b.fixture.Right", Obfuscated = obfuscation.Of("d3b.fixture.Right"), Fields = { new NumberPair { Readable = 20, Obfuscated = 2 } } } },
        };

        Assert.Contains(MatchFixture(obfuscation, pins).Conflicts, conflict => conflict.Contains("has no field 20", StringComparison.Ordinal));
    }

    [Fact]
    public void APinNamingWhatIsMissingIsAConflict()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var pins = new PinnedPairs { Messages = { new MessagePin { Readable = "d3b.fixture.Missing", Obfuscated = obfuscation.Of("d3b.fixture.Left") } } };

        Assert.Contains("d3b.fixture.Missing", Assert.Single(MatchFixture(obfuscation, pins).Conflicts));
    }

    [Fact]
    public void AFieldOfAnotherShapeIsNotPairedNorPairsItsType()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);

        obfuscation.Message("d3b.fixture.Left").Field.Single(field => field.Number == 2).Type = FieldDescriptorProto.Types.Type.Int32;
        obfuscation.Message("d3b.fixture.Left").Field.Single(field => field.Number == 2).ClearTypeName();

        var result = MatchFixture(obfuscation);

        Assert.DoesNotContain(Find(result, Left.Descriptor).Fields, field => field.Readable == 2);
        Assert.Equal(MatchEvidence.Score, result.Enums.Single(match => match.Readable.FullName == "d3b.fixture.Color").Evidence);
    }

    [Fact]
    public void AReadablePackageWithoutScopeIsAConflict()
    {
        var obfuscation = new Obfuscation([FixtureReflection.Descriptor]);
        var result = new ProtocolMatcher([FixtureReflection.Descriptor], obfuscation.Schema(), ReadableProtocol.Scopes).Match(null, []);

        Assert.Contains(result.Conflicts, conflict => conflict.Contains("d3b.fixture", StringComparison.Ordinal));
    }
}
