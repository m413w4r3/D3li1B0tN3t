using System.Buffers;
using D3B.Bot.Inspection;
using D3B.Bot.Tests.Network;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Bot.Tests.Inspection;

public sealed class FrameFieldsTests
{
    private static readonly ProtocolTranslation _translation = TestGame.Translation();

    [Fact]
    public void TheTicketIsHiddenAndTheOtherFieldsShown()
    {
        var fields = FrameFields.Of(new AuthenticationRequest { Ticket = "0123456789abcdef0123456789abcdef", Language = "fr" });

        Assert.Equal([new FrameField(0, "ticket", FrameFields.Hidden, true), new FrameField(0, "language", "\"fr\"", false)], fields);
        Assert.DoesNotContain(fields, field => field.Value?.Contains("0123", StringComparison.Ordinal) ?? false);
    }

    [Fact]
    public void FieldsAtTheirDefaultAreLeftOutAndListsAndMessagesUnfold()
    {
        var information = new MapInformationEvent
        {
            MapId = 191_104_002,
            Actors = { new Actor { Id = 70_001, Disposition = new ActorDisposition { CellId = 300 } } },
        };

        var fields = FrameFields.Of(information);

        Assert.Equal(new FrameField(0, "actors[0]", null, false), fields[0]);
        Assert.Equal(new FrameField(0, "mapId", "191104002", false), fields[^1]);
        Assert.Contains(new FrameField(1, "id", "70001", false), fields);
        Assert.Contains(new FrameField(1, "disposition", null, false), fields);
        Assert.Contains(new FrameField(2, "cellId", "300", false), fields);
        Assert.DoesNotContain(fields, field => field.Name == "subAreaId");
    }

    [Fact]
    public void AListOfNumbersShowsOnOneLineAndAnEnumerationByItsNameInTheProtoFiles()
    {
        Assert.Equal(new FrameField(0, "keyMovements", "[433, 431]", false), FrameFields.Of(new MapMovementRequest { KeyMovements = { 433, 431 } })[0]);
        Assert.Equal(new FrameField(0, "context", "GAME_CONTEXT_FIGHT", false), FrameFields.Of(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight })[0]);
    }

    [Fact]
    public void AMessageTheReadableProtocolLacksShowsItsShapeNeverItsValues()
    {
        var unknown = new GameMessage
        {
            Event = new GameEvent { Content = new Any { TypeUrl = "type.ankama.com/zzz", Value = new MapCurrentEvent { MapId = 191_104_002 }.ToByteString() } },
        };
        var frame = _translation.DecodeGame(new ReadOnlySequence<byte>(unknown.ToByteArray()));

        Assert.False(frame.IsRead);
        Assert.True(FrameFields.TryShape(frame, out var shape));

        var field = Assert.Single(shape);

        Assert.Equal((1, 0), (field.Number, field.WireType));
    }
}
