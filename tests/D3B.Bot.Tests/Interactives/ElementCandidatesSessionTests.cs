using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Interactives;

public sealed class ElementCandidatesSessionTests
{
    [Fact]
    public async Task CandidatesWithoutAConnectedBotReportNoConnectedBot()
    {
        await using var play = new TestPlay();
        var session = new SupervisedElementUseSession(play.Bots, play.Tables);

        var report = session.Candidates(actionEnabled: false);

        Assert.Equal("NO_CONNECTED_BOT", report.Reason);
        Assert.Empty(report.Candidates);
        Assert.Equal(ElementCandidateTargetVerdict.Unknown, report.Target);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, report.Action);
        Assert.Equal("NO_CONNECTED_BOT", report.ActionReason);
    }

    [Fact]
    public async Task CandidatesWithTwoConnectedBotsReportAmbiguousBot()
    {
        await using var play = new TestPlay();
        var (firstConnection, _) = await play.EnterAsync(190);
        var secondCharacterId = TestPlay.CharacterId + 1;
        var secondAccount = TestPlay.Account with { Id = TestPlay.AccountId + 1 };
        var secondConnection = play.Connect(secondAccount);
        secondConnection.Server(TestPlay.Chosen(secondCharacterId));
        var information = TestPlay.Information(190);
        information.Actors.Single(actor => actor.Id == TestPlay.CharacterId).Id = secondCharacterId;
        secondConnection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        secondConnection.Server(information);
        Assert.True(play.Bots.TryGet(new CharacterKey(secondAccount.Id, secondCharacterId), out var secondBot));
        await secondBot.ProcessedAsync();
        var session = new SupervisedElementUseSession(play.Bots, play.Tables);

        var report = session.Candidates(actionEnabled: false);

        Assert.Equal("AMBIGUOUS_BOT", report.Reason);
        Assert.Null(report.Snapshot);
        Assert.Empty(report.Candidates);
        Assert.Equal(ElementCandidateTargetVerdict.Unknown, report.Target);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, report.Action);
        Assert.Equal("AMBIGUOUS_BOT", report.ActionReason);
        Assert.Empty(firstConnection.Sent);
        Assert.Empty(secondConnection.Sent);
    }
}
