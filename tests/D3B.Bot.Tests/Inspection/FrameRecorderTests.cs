using D3B.Bot.Inspection;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Inspection;

public sealed class FrameRecorderTests
{
    private readonly FrameRecorder _recorder = new(TimeProvider.System);

    [Fact]
    public async Task ABotsFramesAreRecordedFromItsConnectionsFirstFrameBothSidesAndD3BsOwn()
    {
        var sentFrames = new TestFrameObserver();
        await using var play = new TestPlay([_recorder, sentFrames]);
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(play, connection, 1, afterRequestRecorded: () => sentFrames.SentAsync(nameof(MapMovementRequest)));
        await walk;

        Assert.True(_recorder.TryGet(TestPlay.Key, out var frames));

        var recorded = frames.All();

        // The ticket's request and the choice came before the connection was the bot's: they join its frames.
        Assert.Equal(
            [
                (FrameSource.Client, nameof(AuthenticationRequest)),
                (FrameSource.Server, nameof(CharacterSelectionEvent)),
                (FrameSource.Server, nameof(MapCurrentEvent)),
                (FrameSource.Server, nameof(MapInformationEvent)),
                (FrameSource.D3B, nameof(MapMovementRequest)),
                (FrameSource.Server, nameof(MapMovementEvent)),
                (FrameSource.Client, nameof(MapMovementConfirmRequest)),
                (FrameSource.Server, nameof(MapMovementConfirmResponse)),
            ],
            recorded.Select(frame => (frame.Source, frame.Frame.ReadableType!.Name)));
        Assert.All(recorded, frame => Assert.Equal("g1", frame.ConnectionId));
    }

    [Fact]
    public async Task AConnectionWhoseAccountIsUnknownIsNoBotsAndKeepsNothing()
    {
        await using var play = new TestPlay([_recorder]);
        var connection = play.Connect(null);

        connection.Server(TestPlay.Chosen());

        Assert.False(_recorder.TryGet(TestPlay.Key, out _));
    }

    [Fact]
    public async Task ANewConnectionsFramesFollowThoseOfTheOneBefore()
    {
        await using var play = new TestPlay([_recorder]);
        var (first, _) = await play.EnterAsync(433);

        first.Close();

        var second = play.Connect(TestPlay.Account);

        second.Server(TestPlay.Chosen());

        Assert.True(_recorder.TryGet(TestPlay.Key, out var frames));
        Assert.Equal(["g1", "g2"], frames.All().Select(frame => frame.ConnectionId).Distinct());
    }
}
