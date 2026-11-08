using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Gui;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;
using D3B.Gui.Windowing;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Gui;

public sealed class BotActorGuiIntentTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly GuiIntentCorrelationFixture _fixture = new();
    private readonly GuiWorldSnapshotAdapter _adapter;

    public BotActorGuiIntentTests()
    {
        _adapter = new GuiWorldSnapshotAdapter(_play.Time);
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task RegistrationAcknowledgementAndClickRunThroughTheInbox()
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);

        var registration = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Registered, registration.Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.True(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task RegistrationRefusesDuplicateAndConcurrentIntentions()
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);

        var registered = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);
        var duplicate = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);
        var busyIntent = CreateIntent(world);
        var busy = await bot.RegisterGuiIntentAsync(busyIntent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Registered, registered.Status);
        Assert.Equal(GuiIntentRegistrationStatus.Duplicate, duplicate.Status);
        Assert.Equal(GuiIntentRegistrationStatus.Busy, busy.Status);
    }

    [Fact]
    public async Task RegistrationRefusesExpiredIntention()
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var now = _play.Time.GetUtcNow();
        var intent = CreateIntent(world) with { CreatedAt = now.AddMinutes(-2), ExpiresAt = now.AddMinutes(-1) };

        var registration = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Expired, registration.Status);
    }

    [Theory]
    [InlineData("decision-revision")]
    [InlineData("decision-epoch")]
    [InlineData("decision-map")]
    [InlineData("intent-revision")]
    [InlineData("intent-epoch")]
    [InlineData("intent-map")]
    public async Task RegistrationRefusesStateDrift(string drift)
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        var decisionWorld = world;

        switch (drift)
        {
            case "decision-revision":
                decisionWorld = world with { StateRevision = world.StateRevision - 1 };
                break;
            case "decision-epoch":
                decisionWorld = world with { SessionEpoch = world.SessionEpoch - 1 };
                break;
            case "decision-map":
                decisionWorld = world with { MapId = world.MapId + 1 };
                break;
            case "intent-revision":
                intent = intent with { StateRevision = intent.StateRevision - 1 };
                break;
            case "intent-epoch":
                intent = intent with { SessionEpoch = intent.SessionEpoch - 1 };
                break;
            case "intent-map":
                intent = intent with { MapId = intent.MapId + 1 };
                break;
        }

        var registration = await bot.RegisterGuiIntentAsync(intent, decisionWorld, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Rejected, registration.Status);
        Assert.False(string.IsNullOrWhiteSpace(registration.Detail));
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("paused")]
    [InlineData("fighting")]
    [InlineData("walking")]
    [InlineData("using")]
    [InlineData("dialog")]
    [InlineData("ambiguous")]
    [InlineData("partial")]
    [InlineData("client-reported")]
    [InlineData("estimated")]
    public async Task RegistrationRefusesUnsafeCurrentWorld(string condition)
    {
        var (connection, bot) = await EnterAsync();

        switch (condition)
        {
            case "disconnected":
                bot.Detach(connection.Session);
                break;
            case "paused":
                bot.Pause();
                break;
            default:
                CharacterState state = SafeState();
                state = condition switch
                {
                    "fighting" => state with { Fight = new FightState() },
                    "walking" => state with { Path = [300, 301] },
                    "using" => state with { Use = new CurrentUse(1, 2) },
                    "dialog" => state with { Dialog = new NpcDialog(1) },
                    "ambiguous" => state with { MovementAnswerAmbiguous = true },
                    "partial" => state with { MapId = null },
                    "client-reported" => state with { Provenance = CellProvenance.ClientReported },
                    "estimated" => state with { Provenance = CellProvenance.Estimated },
                    _ => state,
                };
                bot.Attach(connection.Session, state);
                break;
        }

        await bot.ProcessedAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        var registration = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Rejected, registration.Status);
        Assert.False(string.IsNullOrWhiteSpace(registration.Detail));
    }

    [Fact]
    public async Task RegistrationRefusesClosedTranslationLatch()
    {
        var (connection, bot) = await EnterAsync();
        connection.ServerRaw([0x12, 0x7F, 0x01]);
        await bot.ProcessedAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);

        var registration = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.Equal(ProtocolObservationStatus.TranslationBlocked, world.ProtocolObservationStatus);
        Assert.Equal(GuiIntentRegistrationStatus.Rejected, registration.Status);
    }

    [Theory]
    [InlineData("world")]
    [InlineData("window")]
    [InlineData("actor-state")]
    public async Task AcknowledgementRequiresTheExactRegisteredSnapshot(string mismatch)
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        var registration = await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, registration.Status);

        GuiWorldSnapshot suppliedWorld = mismatch == "world" ? world with { CellId = new LogicalCell(301) } : world;
        GameWindowSnapshot suppliedWindow = mismatch == "window" ? _fixture.Window with { IsFocused = false } : _fixture.Window;

        if (mismatch == "actor-state")
        {
            bot.MarkMovementRequested(TestPlay.MapId);
            await bot.ProcessedAsync();
        }

        Assert.False(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, suppliedWorld, suppliedWindow, TestContext.Current.CancellationToken));
        Assert.NotEqual(GuiIntentCorrelationState.Registered, bot.GuiIntentState);
        Assert.NotEqual(GuiIntentCorrelationState.Acknowledged, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("state")]
    [InlineData("window")]
    public async Task ClickRefusesExpiryOrSnapshotChange(string mismatch)
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);

        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));

        GuiWorldSnapshot suppliedWorld = world;
        GameWindowSnapshot suppliedWindow = _fixture.Window;

        if (mismatch == "expired")
            _play.Time.Advance(TimeSpan.FromMinutes(2));
        else if (mismatch == "state")
        {
            bot.MarkMovementRequested(TestPlay.MapId);
            await bot.ProcessedAsync();
        }
        else
            suppliedWindow = _fixture.Window with { IsFocused = false };

        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, suppliedWorld, suppliedWindow, TestContext.Current.CancellationToken));
        Assert.NotEqual(GuiIntentCorrelationState.Registered, bot.GuiIntentState);
        Assert.NotEqual(GuiIntentCorrelationState.Acknowledged, bot.GuiIntentState);
    }

    [Fact]
    public async Task CancellationBeforeClickIsTerminal()
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        Assert.True(await bot.CancelGuiIntentAsync(intent.IntentId, false, "test cancellation", TestContext.Current.CancellationToken));
        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterClickIsUnconfirmedAndTerminal(bool clickMayHaveBeenEmitted)
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.True(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));

        Assert.True(await bot.CancelGuiIntentAsync(intent.IntentId, clickMayHaveBeenEmitted, "uncertain click", TestContext.Current.CancellationToken));
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("player-start")]
    [InlineData("fight-start")]
    [InlineData("map-change")]
    [InlineData("detach")]
    [InlineData("attach")]
    [InlineData("stop")]
    public async Task ActorTransitionsRevokeIntentionsBeforeAnyClick(string transition)
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);

        switch (transition)
        {
            case "pause":
                bot.Pause();
                break;
            case "player-start":
                connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
                break;
            case "fight-start":
                connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
                break;
            case "map-change":
                connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
                break;
            case "detach":
                bot.Detach(connection.Session);
                break;
            case "attach":
                var replacement = _play.Connect(TestPlay.Account);
                bot.Attach(replacement.Session, SafeState());
                break;
            case "stop":
                await bot.DisposeAsync();
                break;
        }

        if (transition != "stop")
            await bot.ProcessedAsync();

        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ClosingTheTranslationLatchRevokesARegisteredIntention()
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken);

        connection.ServerRaw([0x12, 0x7F, 0x01]);
        await bot.ProcessedAsync();

        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentRegistrationsSerializeOnTheActorLoop()
    {
        var (_, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var first = CreateIntent(world);
        var second = CreateIntent(world);

        var registrations = await Task.WhenAll(
            bot.RegisterGuiIntentAsync(first, world, _fixture.Window, TestContext.Current.CancellationToken),
            bot.RegisterGuiIntentAsync(second, world, _fixture.Window, TestContext.Current.CancellationToken));

        Assert.Single(registrations, result => result.Status == GuiIntentRegistrationStatus.Registered);
        Assert.Single(registrations, result => result.Status == GuiIntentRegistrationStatus.Busy);
    }

    private async Task<(TestConnection Connection, BotActor Bot)> EnterAsync()
    {
        return await _play.EnterAsync(300);
    }

    private GuiActionIntent CreateIntent(GuiWorldSnapshot world)
    {
        DateTimeOffset now = _play.Time.GetUtcNow();

        return _fixture.Intent with
        {
            IntentId = Guid.NewGuid(),
            SessionEpoch = world.SessionEpoch,
            StateRevision = world.StateRevision,
            MapId = world.MapId ?? TestPlay.MapId,
            CreatedAt = now.AddSeconds(-1),
            ExpiresAt = now.AddMinutes(1),
        };
    }

    private static CharacterState SafeState()
    {
        return new CharacterState
        {
            Key = TestPlay.Key,
            Name = "gui-intent",
            MapId = TestPlay.MapId,
            Cell = 300,
            Provenance = CellProvenance.ServerConfirmed,
        };
    }
}
