using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Gui;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
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
    public async Task ASuccessfulClickAttemptPermitsOneIntentPerSessionEpoch()
    {
        var (_, bot) = await EnterAsync();
        GuiWorldSnapshot world = _adapter.Capture(bot);
        GuiActionIntent intent = CreateIntent(world);

        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.True(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));

        GuiActionIntent secondIntent = CreateIntent(world);
        GuiIntentRegistration refused = await bot.RegisterGuiIntentAsync(
            secondIntent,
            world,
            _fixture.Window,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Rejected, refused.Status);
        Assert.Equal("This session already made its one permitted GUI click attempt.", refused.Detail);

        var replacement = _play.Connect(TestPlay.Account);
        bot.Attach(replacement.Session, SafeState());
        await bot.ProcessedAsync();

        GuiWorldSnapshot replacementWorld = _adapter.Capture(bot);
        GuiActionIntent replacementIntent = CreateIntent(replacementWorld);
        GuiIntentRegistration afterReconnect = await bot.RegisterGuiIntentAsync(
            replacementIntent,
            replacementWorld,
            _fixture.Window,
            TestContext.Current.CancellationToken);

        Assert.NotEqual(world.SessionEpoch, replacementWorld.SessionEpoch);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, afterReconnect.Status);
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
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        Assert.True(await bot.CancelGuiIntentAsync(intent.IntentId, false, "test cancellation", TestContext.Current.CancellationToken));
        Assert.Null(await outcome);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));

        GuiActionIntent replacement = CreateIntent(world);
        GuiIntentRegistration afterCancellation = await bot.RegisterGuiIntentAsync(
            replacement,
            world,
            _fixture.Window,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiIntentRegistrationStatus.Registered, afterCancellation.Status);
    }

    [Fact]
    public async Task CancellationOfAcknowledgedIntentIsPreClickEvenWhenClickMayHaveBeenEmitted()
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        Assert.True(await bot.CancelGuiIntentAsync(intent.IntentId, true, "uncertain click", TestContext.Current.CancellationToken));

        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.Null(await outcome);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
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
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        Assert.True(await bot.CancelGuiIntentAsync(intent.IntentId, clickMayHaveBeenEmitted, "uncertain click", TestContext.Current.CancellationToken));
        Assert.Null(await outcome);
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
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);
        Assert.False(clickCancellationToken.IsCancellationRequested);
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

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

        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.Null(await outcome);
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
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        connection.ServerRaw([0x12, 0x7F, 0x01]);
        await bot.ProcessedAsync();

        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, bot.GuiIntentState);
        Assert.Null(await outcome);
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

    [Fact]
    public async Task MatchingWalkRequestMovementAndConfirmationCompleteAndConsumeOnce()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        MapMovementRequest request = ReachableWalkRequest(out int[] cells);

        connection.Client(request);
        await bot.ProcessedAsync();
        Assert.True(bot.GuiIntentState is GuiIntentCorrelationState.ClientRequestSeen, bot.GuiIntentDetail);
        var movement = new MapMovementEvent { ActorId = TestPlay.Key.CharacterId };
        movement.Cells.AddRange(cells);
        connection.Server(movement);
        await bot.ProcessedAsync();
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        Assert.Equal(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
        Assert.False(clickCancellationToken.IsCancellationRequested);
        Assert.Equal(GuiServerOutcome.MovementConfirmed, await outcome);
        Assert.Equal(TargetCell, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, bot.State.Provenance);
        Assert.True(await bot.ConsumeGuiIntentAsync(intent.IntentId, TestContext.Current.CancellationToken));
        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.False(await bot.ConsumeGuiIntentAsync(intent.IntentId, TestContext.Current.CancellationToken));
        Assert.Null(await bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken));
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, _adapter.Capture(bot), _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("wrong-map")]
    [InlineData("wrong-cell")]
    [InlineData("empty-path")]
    [InlineData("bad-direction")]
    [InlineData("bad-middle-direction")]
    [InlineData("bad-cell")]
    public async Task InvalidWalkRequestsCannotConfirm(string kind)
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        MapMovementRequest request = kind switch
        {
            "wrong-map" => WalkRequest(TestPlay.NorthMapId, TargetCell),
            "wrong-cell" => WalkRequest(TestPlay.MapId, TargetCell + 1),
            "empty-path" => new MapMovementRequest { MapId = TestPlay.MapId },
            "bad-direction" => WalkRequest(TestPlay.MapId, TargetCell, 8),
            "bad-middle-direction" => new MapMovementRequest
            {
                MapId = TestPlay.MapId,
                KeyMovements = { StartCell, (8 << 12) | TargetCell, TargetCell },
            },
            "bad-cell" => WalkRequest(TestPlay.MapId, 0xFFF),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        connection.Client(request);

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, _adapter.Capture(bot), _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("other-actor")]
    [InlineData("wrong-endpoint")]
    [InlineData("empty-path")]
    [InlineData("bad-cell")]
    public async Task NonmatchingSelfMovementCannotConfirm(string kind)
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();

        var movement = new MapMovementEvent
        {
            ActorId = kind == "other-actor" ? TestPlay.Key.CharacterId + 1 : TestPlay.Key.CharacterId,
        };

        if (kind is not "empty-path")
        {
            movement.Cells.Add(StartCell);
            movement.Cells.Add(kind switch
            {
                "wrong-endpoint" => TargetCell + 1,
                "bad-cell" => 0xFFF,
                _ => TargetCell,
            });
        }

        connection.Server(movement);

        if (kind == "other-actor")
            connection.Response(new MapMovementConfirmResponse());

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task FinalMovementResponseWithoutTheSelfMovementIsUnconfirmed()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();
        connection.Response(new MapMovementConfirmResponse());

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task MovementRefusalIsUnconfirmed()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();
        connection.Server(new MapMovementRefusedEvent());

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Theory]
    [InlineData("movement")]
    [InlineData("refusal")]
    [InlineData("response")]
    public async Task UnexpectedServerMovementBeforeClickCancelsAcknowledgedIntent(string kind)
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world);
        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        switch (kind)
        {
            case "movement":
                connection.Server(new MapMovementEvent { ActorId = TestPlay.Key.CharacterId, Cells = { StartCell, TargetCell } });
                break;
            case "refusal":
                connection.Server(new MapMovementRefusedEvent());
                break;
            case "response":
                connection.Response(new MapMovementConfirmResponse());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await bot.ProcessedAsync();

        Assert.True(bot.GuiIntentState is GuiIntentCorrelationState.Cancelled or GuiIntentCorrelationState.Interrupted, bot.GuiIntentDetail);
        Assert.Null(await outcome);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, _adapter.Capture(bot), _fixture.Window, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Theory]
    [InlineData("response")]
    [InlineData("movement")]
    public async Task ServerMovementBeforeTheClientRequestIsUnconfirmed(string kind)
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        if (kind == "response")
            connection.Response(new MapMovementConfirmResponse());
        else
            connection.Server(new MapMovementEvent { ActorId = TestPlay.Key.CharacterId, Cells = { StartCell, TargetCell } });

        Assert.Null(await outcome);
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task ReplacedSessionCannotUseAnOldSessionOutcome()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        var replacement = _play.Connect(TestPlay.Account);
        bot.Attach(replacement.Session, SafeState());
        await bot.ProcessedAsync();

        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.Key.CharacterId, Cells = { StartCell, TargetCell } });
        connection.Response(new MapMovementConfirmResponse());

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task TranslationBlockedAfterClickCompletesWaiterWithoutConfirmation()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.ServerRaw([0x12, 0x7F, 0x01]);
        await bot.ProcessedAsync();

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task PartialPositionAfterClickCompletesWaiterWithoutConfirmation()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Server(new MapInformationEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task MapChangeAfterClickCompletesWaiterWithoutConfirmation()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await bot.ProcessedAsync();

        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task UnexpectedClientGameplayAfterClickInterruptsAndPauses()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        var behaviorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> behavior = bot.RunAsync(
            async (_, cancellationToken) =>
            {
                behaviorStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return true;
            },
            TestContext.Current.CancellationToken);
        await behaviorStarted.Task;
        connection.Client(new MapMovementCancelRequest { CellId = StartCell });
        await bot.ProcessedAsync();

        Assert.Null(await outcome);
        Assert.Equal(Interruption.Player, (await Assert.ThrowsAsync<BotInterruptedException>(() => behavior)).Reason);
        Assert.True(bot.IsPaused, $"{bot.GuiIntentState}: {bot.GuiIntentDetail}");
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, _adapter.Capture(bot), _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SecondClientWalkAfterTheExpectedRequestInterruptsAndPauses()
    {
        var (connection, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();

        var behaviorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> behavior = bot.RunAsync(
            async (_, cancellationToken) =>
            {
                behaviorStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return true;
            },
            TestContext.Current.CancellationToken);
        await behaviorStarted.Task;
        connection.Client(WalkRequest(TestPlay.MapId, TargetCell));
        await bot.ProcessedAsync();

        Assert.Equal(GuiIntentCorrelationState.Interrupted, bot.GuiIntentState);
        Assert.Null(await outcome);
        Assert.Equal(Interruption.Player, (await Assert.ThrowsAsync<BotInterruptedException>(() => behavior)).Reason);
        Assert.True(bot.IsPaused, bot.GuiIntentDetail);
        Assert.False(await bot.MarkGuiIntentClickedAsync(intent.IntentId, _adapter.Capture(bot), _fixture.Window, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaiterExpiresWithTheIntention()
    {
        var (_, bot, intent) = await ClickIntentAsync();
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        _play.Time.Advance(intent.ExpiresAt - _play.Time.GetUtcNow());
        await bot.ProcessedAsync();

        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.Null(await outcome);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task ClickedIntentExpiryRevokesInputWithoutAnOutcomeWaiter()
    {
        var (_, bot, intent) = await ClickIntentAsync();
        CancellationToken clickCancellationToken = bot.GetGuiClickCancellationToken(intent.IntentId);

        _play.Time.Advance(intent.ExpiresAt - _play.Time.GetUtcNow());
        await bot.ProcessedAsync();

        Assert.True(clickCancellationToken.IsCancellationRequested);
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, bot.GuiIntentState);
    }

    [Fact]
    public async Task WaiterCancellationCompletesOnTheActorLoop()
    {
        var (_, bot, intent) = await ClickIntentAsync();
        using var cancellation = new CancellationTokenSource();
        Task<GuiServerOutcome?> outcome = bot.WaitForGuiOutcomeAsync(intent.IntentId, cancellation.Token);
        cancellation.Cancel();
        await bot.ProcessedAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await outcome);
    }

    [Fact]
    public async Task OnlyOneGuiOutcomeWaiterCanBeRegistered()
    {
        var (_, bot, intent) = await ClickIntentAsync();
        Task<GuiServerOutcome?> first = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);
        Task<GuiServerOutcome?> second = bot.WaitForGuiOutcomeAsync(intent.IntentId, TestContext.Current.CancellationToken);

        Assert.Null(await second);
        Assert.False(first.IsCompleted);
    }

    [Fact]
    public async Task UnknownGuiIntentWaitReturnsNullAndCannotBeConsumed()
    {
        var (_, bot) = await EnterAsync();

        Assert.Null(await bot.WaitForGuiOutcomeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await bot.ConsumeGuiIntentAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private async Task<(TestConnection Connection, BotActor Bot)> EnterAsync()
    {
        return await _play.EnterAsync(300);
    }

    private async Task<(TestConnection Connection, BotActor Bot, GuiActionIntent Intent)> ClickIntentAsync()
    {
        var (connection, bot) = await EnterAsync();
        var world = _adapter.Capture(bot);
        var intent = CreateIntent(world) with { TargetCellId = new LogicalCell(TargetCell) };

        Assert.Equal(GuiIntentRegistrationStatus.Registered,
            (await bot.RegisterGuiIntentAsync(intent, world, _fixture.Window, TestContext.Current.CancellationToken)).Status);
        Assert.True(await bot.AcknowledgeGuiIntentAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));
        Assert.True(await bot.MarkGuiIntentClickedAsync(intent.IntentId, world, _fixture.Window, TestContext.Current.CancellationToken));

        return (connection, bot, intent);
    }

    private static MapMovementRequest WalkRequest(long mapId, int targetCell, int direction = 0)
    {
        return new MapMovementRequest
        {
            MapId = mapId,
            KeyMovements = { (direction << 12) | targetCell },
        };
    }

    private MapMovementRequest ReachableWalkRequest(out int[] cells)
    {
        var map = _play.Maps.Get(TestPlay.MapId);
        Assert.True(CellPathfinder.TryFind(map.Cells.Span, StartCell, TargetCell, out var path));
        cells = path.Cells.ToArray();

        return MapWalk.Request(TestPlay.MapId, path);
    }

    private const int StartCell = 300;
    private const int TargetCell = 301;

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
