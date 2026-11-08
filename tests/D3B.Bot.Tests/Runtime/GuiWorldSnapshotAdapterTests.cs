using System.Collections.Concurrent;
using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Gui;
using D3B.Bot.Runtime;
using D3B.Gui.Safety;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Runtime;

public sealed class GuiWorldSnapshotAdapterTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly GuiWorldSnapshotAdapter _adapter;

    public GuiWorldSnapshotAdapterTests()
    {
        _adapter = new GuiWorldSnapshotAdapter(_play.Time);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task SnapshotMapsCharacterStateAndElements()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var element = new MapElement(10, 30, [new ElementSkill(40, 50)], []) { Cell = 60, State = 1 };
        var state = new CharacterState
        {
            Key = TestPlay.Key,
            Name = "snapshot",
            MapId = TestPlay.MapId,
            Cell = 70,
            Provenance = CellProvenance.ServerConfirmed,
            Fight = new FightState(),
            Path = [70, 71],
            IsWalkHeld = true,
            Use = new CurrentUse(10, 40),
            Dialog = new NpcDialog(80),
            Elements = ImmutableSortedDictionary<int, MapElement>.Empty.Add(element.ElementId, element),
        };

        bot.Attach(connection.Session, state);
        await bot.ProcessedAsync();
        var snapshot = _adapter.Capture(bot);

        Assert.Equal(TestPlay.MapId, snapshot.MapId);
        Assert.Equal(70, snapshot.CellId?.Value);
        Assert.Equal(GuiCellProvenance.ServerConfirmed, snapshot.CellProvenance);
        Assert.True(snapshot.IsConnected);
        Assert.True(snapshot.IsFighting);
        Assert.True(snapshot.IsWalking);
        Assert.True(snapshot.IsUsing);
        Assert.True(snapshot.IsInDialog);
        Assert.True(snapshot.HasAmbiguousMovement);
        Assert.False(snapshot.IsUserInterrupted);
        Assert.Equal(_play.Time.GetUtcNow(), snapshot.CapturedAt);
        Assert.Equal(ProtocolObservationStatus.Complete, snapshot.ProtocolObservationStatus);
        var knownElement = Assert.Single(snapshot.KnownElements);
        Assert.Equal(10, knownElement.ElementId);
        Assert.Equal(60, knownElement.Cell?.Value);
        Assert.Equal(30, knownElement.InteractiveId);
        Assert.Equal(1, knownElement.State);
        Assert.Equal([40], knownElement.EnabledSkillIds);
    }

    [Fact]
    public async Task UnknownProvenanceMapsToNull()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var state = State() with { Provenance = CellProvenance.Unknown };

        bot.Attach(connection.Session, state);
        await bot.ProcessedAsync();

        Assert.Null(_adapter.Capture(bot).CellProvenance);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MissingMapOrCellMakesTheObservationPartial(bool missingMap, bool missingCell)
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var state = State() with
        {
            MapId = missingMap ? null : TestPlay.MapId,
            Cell = missingCell ? null : 300,
        };

        bot.Attach(connection.Session, state);
        await bot.ProcessedAsync();

        Assert.Equal(ProtocolObservationStatus.Partial, _adapter.Capture(bot).ProtocolObservationStatus);
    }

    [Fact]
    public async Task InvalidCellIsNotRepresentedAsALogicalCell()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        bot.Attach(connection.Session, State() with { Cell = 560 });
        await bot.ProcessedAsync();

        var snapshot = _adapter.Capture(bot);

        Assert.Null(snapshot.CellId);
        Assert.Equal(ProtocolObservationStatus.Partial, snapshot.ProtocolObservationStatus);
    }

    [Fact]
    public async Task AmbiguousMovementMakesTheObservationContradictory()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        bot.Attach(connection.Session, State() with { MovementAnswerAmbiguous = true });
        await bot.ProcessedAsync();

        Assert.Equal(ProtocolObservationStatus.Contradictory, _adapter.Capture(bot).ProtocolObservationStatus);
    }

    [Fact]
    public async Task ClosedTranslationLatchMakesTheObservationTranslationBlocked()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.ServerRaw([0x12, 0x7F, 0x01]);
        await bot.ProcessedAsync();

        Assert.Equal(ProtocolObservationStatus.TranslationBlocked, _adapter.Capture(bot).ProtocolObservationStatus);
    }

    [Fact]
    public async Task DisconnectedCharacterHasUnknownObservationStatus()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        bot.Detach(connection.Session);
        await bot.ProcessedAsync();

        var snapshot = _adapter.Capture(bot);

        Assert.False(snapshot.IsConnected);
        Assert.Equal(ProtocolObservationStatus.Unknown, snapshot.ProtocolObservationStatus);
    }

    [Fact]
    public async Task EachAttachedSessionAdvancesTheEpoch()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var initialEpoch = bot.View.SessionEpoch;
        var firstConnection = _play.Connect(TestPlay.Account);
        bot.Attach(firstConnection.Session, State());
        await bot.ProcessedAsync();
        var firstEpoch = bot.View.SessionEpoch;
        var secondConnection = _play.Connect(TestPlay.Account);
        bot.Attach(secondConnection.Session, State());
        await bot.ProcessedAsync();

        Assert.Equal(initialEpoch + 1, firstEpoch);
        Assert.Equal(firstEpoch + 1, bot.View.SessionEpoch);
    }

    [Fact]
    public async Task RevisionsAdvanceForSeenSettledRequestedAndDetachedState()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var revision = bot.View.Revision;

        bot.MarkMovementRequested(TestPlay.MapId);
        await bot.ProcessedAsync();
        Assert.True(bot.View.Revision > revision);
        revision = bot.View.Revision;

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        await bot.ProcessedAsync();
        Assert.True(bot.View.Revision > revision);
        revision = bot.View.Revision;

        await bot.SettleWalkAsync();
        Assert.True(bot.View.Revision > revision);
        revision = bot.View.Revision;

        bot.Detach(connection.Session);
        await bot.ProcessedAsync();
        Assert.True(bot.View.Revision > revision);
        Assert.False(bot.View.IsConnected);
    }

    [Fact]
    public async Task StateRevisionAndConnectionArePublishedAsOneView()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var expected = new ConcurrentDictionary<(string Name, long Revision, long Epoch, bool Connected), byte>();
        var initial = bot.View;
        expected.TryAdd((initial.State.Name, initial.Revision, initial.SessionEpoch, initial.IsConnected), 0);
        var mismatches = new ConcurrentQueue<(string Name, long Revision, long Epoch, bool Connected)>();
        using var stopReader = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stopReader.IsCancellationRequested)
            {
                var view = bot.View;
                var observed = (view.State.Name, view.Revision, view.SessionEpoch, view.IsConnected);

                if (!expected.ContainsKey(observed))
                    mismatches.Enqueue(observed);
            }
        }, TestContext.Current.CancellationToken);

        for (var index = 0; index < 20; index++)
        {
            var current = bot.View;
            var name = $"snapshot-{index}";
            expected.TryAdd((name, current.Revision + 1, current.SessionEpoch + 1, true), 0);
            bot.Attach(connection.Session, State() with { Name = name });
            await bot.ProcessedAsync();

            current = bot.View;
            expected.TryAdd((current.State.Name, current.Revision + 1, current.SessionEpoch, false), 0);
            bot.Detach(connection.Session);
            await bot.ProcessedAsync();
        }

        stopReader.Cancel();
        await reader;

        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task ReadingSnapshotsDuringAnInboxBurstDoesNotBlockTheInbox()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var readCount = 0;
        var reader = Task.Run(() =>
        {
            for (var index = 0; index < 10_000; index++)
            {
                _adapter.Capture(bot);
                Interlocked.Increment(ref readCount);
            }
        }, TestContext.Current.CancellationToken);

        for (var index = 0; index < 500; index++)
            connection.Server(new KamasUpdateEvent { Kamas = index });

        await bot.ProcessedAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await reader;

        Assert.True(readCount > 0);
        Assert.Equal(499, bot.State.Kamas);
    }

    [Fact]
    public async Task SnapshotReportsUserInterruption()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var behavior = bot.RunAsync(async (_, token) =>
        {
            started.TrySetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();

            return true;
        }, TestContext.Current.CancellationToken);
        await started.Task;
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await bot.ProcessedAsync();
        release.TrySetResult();

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => behavior);
        await bot.ProcessedAsync();

        Assert.Equal(Interruption.Player, interrupted.Reason);
        Assert.True(_adapter.Capture(bot).IsUserInterrupted);
    }

    private static CharacterState State()
    {
        return new CharacterState
        {
            Key = TestPlay.Key,
            Name = "snapshot",
            MapId = TestPlay.MapId,
            Cell = 300,
            Provenance = CellProvenance.ServerPlaced,
        };
    }
}
