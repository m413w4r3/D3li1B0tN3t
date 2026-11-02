using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Runtime;

/// <summary>The client's requests the player sends, told from those the client sends by itself, in a fight too.</summary>
public sealed class PlayerActionsTests
{
    private static readonly CharacterState _state = new() { Key = TestPlay.Key, Name = "Character" };

    public static TheoryData<IMessage> PlayersInAFight => new()
    {
        new FightPlacementPositionRequest { CellId = 300, FighterId = TestPlay.CharacterId },
        new FightReadyRequest { Ready = true },
        new GameActionFightCastRequest { SpellId = 13_286, CellId = 300 },
        new FightTurnFinishRequest(),
        new MapMovementRequest(),
    };

    public static TheoryData<IMessage> TheClientsInAFight => new()
    {
        new GameActionAcknowledgementRequest { ActionId = 3 },
        new FightTurnReadyRequest { Ready = true },
        new FightTurnFinishRequest { Automatic = true },
    };

    [Theory]
    [MemberData(nameof(PlayersInAFight))]
    public void APlacementAReadinessACastAWalkAndATurnsEndAreThePlayers(IMessage request)
    {
        Assert.True(PlayerActions.Starts(_state, request));
    }

    [Theory]
    [MemberData(nameof(TheClientsInAFight))]
    public void TheAcknowledgementsTheReadinessForATurnAndATurnsEndItMarksAreTheClientsOwn(IMessage request)
    {
        Assert.False(PlayerActions.Starts(_state, request));
    }

    [Fact]
    public void LeavingADialogIsThePlayersButADialogWhoseQuestionOffersNoReplyWhichTheClientClosesItself()
    {
        var asked = _state with { Dialog = new NpcDialog(-20_000) { MessageId = 30_638, Replies = [36_977, 36_978] } };
        var farewell = _state with { Dialog = new NpcDialog(-20_000) { MessageId = 30_636 } };

        Assert.True(PlayerActions.Starts(asked, new DialogLeaveRequest()));
        Assert.True(PlayerActions.Starts(_state with { Dialog = new NpcDialog(-20_000) }, new DialogLeaveRequest()));
        Assert.False(PlayerActions.Starts(farewell, new DialogLeaveRequest()));
        Assert.True(PlayerActions.Starts(farewell with { Storage = new OpenStorage(0) }, new DialogLeaveRequest()));
        Assert.False(PlayerActions.Starts(_state, new DialogLeaveRequest()));
    }
}
