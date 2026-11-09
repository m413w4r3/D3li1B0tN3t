using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Gui.Safety;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Interactives;

public sealed class SupervisedElementUseTests
{
    private static readonly string[] _temporaryGainReasons = ["ITEM_GAIN_NOT_PRESENT_AT_VERDICT", "ITEM_NOT_GAINED"];

    private static ElementUseTarget TreeTarget(int cell = 190)
    {
        return new ElementUseTarget(TestPlay.MapId, cell, TestPlay.TreeElementId, TestPlay.TreeSkillId);
    }

    private static SupervisedElementUseSession Session(TestPlay play)
    {
        return new SupervisedElementUseSession(play.Bots, play.Tables);
    }

    private static async Task FrontierOpenAsync(TestPlay play)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!play.Log.GetSnapshot().Any(record => record.Message.Contains("acceptance frontier open", StringComparison.Ordinal)))
        {
            Assert.True(DateTime.UtcNow < deadline, "The element-use acceptance frontier did not open.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AReadyPreparationNamesTheTargetAndItsProvenItem()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());

        Assert.Equal(ElementUsePreparationState.Ready, preparation.State);
        Assert.Equal("READY", preparation.Reason);
        Assert.True(preparation.CanAttempt);
        Assert.False(preparation.AttemptConsumed);
        Assert.Equal(bot.View.SessionEpoch, preparation.SessionEpoch);
        Assert.Equal(bot.View.Revision, preparation.StateRevision);
        Assert.Equal(TestPlay.MapId, preparation.MapId);
        Assert.Equal(190, preparation.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, preparation.Provenance);
        Assert.True(preparation.PositionTrusted);
        Assert.Equal(TestPlay.TreeElementId, preparation.ElementId);
        Assert.Equal(34, preparation.InteractiveId);
        Assert.Equal(TestPlay.TreeCell, preparation.ElementCell);
        Assert.Equal(TestPlay.TreeGfxId, preparation.GfxId);
        Assert.Equal(TestPlay.TreeSkillId, preparation.SkillId);
        Assert.Equal(TestPlay.TreeInstanceId, preparation.SkillInstanceId);
        Assert.Equal(TestPlay.Ash, preparation.ExpectedItemId);
        Assert.Equal(1, preparation.SkillMinLevel);
        Assert.True(preparation.UsesFromCurrentCell);
        Assert.False(preparation.TranslationBlocked);
        Assert.Equal(GameInputMode.ProtocolInjection, preparation.InputMode);
        Assert.True(preparation.Connected);
        Assert.True(preparation.SessionAttached);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AClientCancelledPositionBlocksWithoutSending()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 190, 191 } });
        connection.Client(new MapMovementCancelRequest { CellId = 190 });
        await bot.ProcessedAsync();

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var result = await Session(play).UseAsync(TreeTarget(), cancellationToken);

        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("GUARD_CLIENT_REPORTED", preparation.Reason);
        Assert.False(preparation.CanAttempt);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("GUARD_CLIENT_REPORTED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ASettledHeldWalkBlocksWithoutRecoveryMovement()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 190, 191 } });
        await bot.SettleWalkAsync();
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsWalkHeld);
        Assert.Equal(CellProvenance.Estimated, bot.State.Provenance);
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var result = await Session(play).UseAsync(TreeTarget(), cancellationToken);

        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("GUARD_WALK_HELD", preparation.Reason);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("GUARD_WALK_HELD", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AClosedTranslationLatchBlocksWithoutSending()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var result = await Session(play).UseAsync(TreeTarget(), cancellationToken);

        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", preparation.Reason);
        Assert.True(preparation.TranslationBlocked);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADisabledSkillIsUnavailable()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        await bot.ProcessedAsync();

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var result = await Session(play).UseAsync(TreeTarget(), cancellationToken);

        Assert.Equal(ElementUsePreparationState.Unavailable, preparation.State);
        Assert.Equal("SKILL_DISABLED", preparation.Reason);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("SKILL_DISABLED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnUnavailableSkillIsUnavailable()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        var unoffered = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget() with { SkillId = TestPlay.ChestnutSkillId });
        var unknown = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget() with { SkillId = 99_999 });

        Assert.Equal(ElementUsePreparationState.Unavailable, unoffered.State);
        Assert.Equal("SKILL_NOT_OFFERED", unoffered.Reason);
        Assert.Equal(ElementUsePreparationState.Unavailable, unknown.State);
        Assert.Equal("SKILL_NOT_IN_CATALOG", unknown.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AChangedSkillInstanceBlocksBeforeSending()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        Assert.Equal(ElementUsePreparationState.Ready, preparation.State);

        connection.Server(new InteractiveElementUpdatedEvent
        {
            Element = new InteractiveElement
            {
                ElementId = TestPlay.TreeElementId,
                InteractiveId = 34,
                OnCurrentMap = true,
                EnabledSkills = { new InteractiveSkill { SkillId = TestPlay.TreeSkillId, InstanceId = TestPlay.TreeInstanceId + 1 } },
            },
        });
        await bot.ProcessedAsync();

        var result = await SupervisedElementUse.ExecuteAsync(bot, play.Tables, preparation, cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("SKILL_INSTANCE_CHANGED", result.Reason);
        Assert.False(result.Evidence.RequestSent);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapChangeInvalidatesAPreparation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());

        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Client(new MapInformationRequest { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(350, TestPlay.NorthMapId));
        await bot.ProcessedAsync();

        var result = await SupervisedElementUse.ExecuteAsync(bot, play.Tables, preparation, cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("MAP_CHANGED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACellChangeInvalidatesAPreparation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());

        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 190, 191 } });
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        Assert.Equal(191, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, bot.State.Provenance);
        var result = await SupervisedElementUse.ExecuteAsync(bot, play.Tables, preparation, cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("CELL_CHANGED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ASessionChangeInvalidatesAPreparation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var next = play.Connect(TestPlay.Account);
        next.Server(TestPlay.Chosen());
        await bot.ProcessedAsync();

        var result = await SupervisedElementUse.ExecuteAsync(bot, play.Tables, preparation, cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("SESSION_CHANGED", result.Reason);
        Assert.Empty(connection.Sent);
        Assert.Empty(next.Sent);
    }

    [Fact]
    public async Task AnOutOfRangeElementProducesNoMovement()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var cancellationToken = TestContext.Current.CancellationToken;

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget(433));
        var result = await Session(play).UseAsync(TreeTarget(433), cancellationToken);

        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("ELEMENT_OUT_OF_RANGE", preparation.Reason);
        Assert.False(preparation.UsesFromCurrentCell);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("ELEMENT_OUT_OF_RANGE", result.Reason);
        Assert.Empty(connection.Sent);
        Assert.DoesNotContain(connection.Sent, request => request is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent, request => request is MapChangeRequest);
        Assert.DoesNotContain(connection.Sent, request => request is InteractiveUseRequest);
    }

    [Fact]
    public async Task AnUnknownElementIsUnavailable()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        var missing = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget() with { ElementId = 1 });

        Assert.Equal(ElementUsePreparationState.Unavailable, missing.State);
        Assert.Equal("ELEMENT_NOT_ON_MAP", missing.Reason);

        connection.Server(new MapInformationEvent
        {
            MapId = TestPlay.MapId,
            SubAreaId = TestPlay.SubAreaId,
            Actors =
            {
                new Actor { Id = TestPlay.CharacterId, Disposition = new ActorDisposition { CellId = 190 } },
            },
        });
        await bot.ProcessedAsync();

        var unknown = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());

        Assert.Equal(ElementUsePreparationState.Unavailable, unknown.State);
        Assert.Equal("ELEMENT_NOT_KNOWN", unknown.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AConflictingElementCellIsBlocked()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.Server(new StatedElementUpdatedEvent
        {
            Element = new StatedElement { ElementId = TestPlay.TreeElementId, CellId = 177, State = 0, OnCurrentMap = true },
        });
        await bot.ProcessedAsync();

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());

        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("ELEMENT_CELL_CONFLICT", preparation.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ASuccessfulHarvestSendsOneRequestAndNoMovement()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        var use = session.UseAsync(TreeTarget(), cancellationToken);
        var request = Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        await FrontierOpenAsync(play);

        Assert.Equal(TestPlay.TreeElementId, request.ElementId);
        Assert.Equal(TestPlay.TreeInstanceId, request.SkillInstanceId);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.PassHarvest, result.Outcome);
        Assert.Equal("PASS", result.Reason);
        Assert.Equal("PASS_HARVEST", result.Code);
        Assert.True(result.Evidence.RequestSent);
        Assert.True(result.Evidence.InteractiveUsedObserved);
        Assert.True(result.Evidence.InteractiveUseEndedObserved);
        Assert.Equal(0, result.Evidence.InventoryBefore);
        Assert.Equal(3, result.Evidence.InventoryAfter);
        Assert.Equal(TestPlay.Ash, result.Evidence.ExpectedItemId);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapChangeRequest);

        var second = await session.UseAsync(TreeTarget(), cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AServerRefusalIsRefusedAndNotRetried()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        var use = session.UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });
        await bot.ProcessedAsync();

        var result = await use;

        Assert.Equal(SupervisedElementUseOutcome.Refused, result.Outcome);
        Assert.Equal("SERVER_REFUSED", result.Reason);
        Assert.Equal("REFUSED", result.Code);
        Assert.Equal(TestPlay.TreeInstanceId, result.Evidence.ErrorSkillInstanceId);
        var second = await session.UseAsync(TreeTarget(), cancellationToken);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnUnchangedInventoryIsNotAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("ITEM_NOT_GAINED", result.Reason);
        Assert.Equal("UNCONFIRMED", result.Code);
        Assert.Equal(0, result.Evidence.InventoryBefore);
        Assert.Equal(0, result.Evidence.InventoryAfter);
    }

    [Fact]
    public async Task AnUncorrelatedEndIsNotAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.ChestnutSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_END", result.Reason);
        Assert.Equal("UNCONFIRMED", result.Code);
    }

    [Fact]
    public async Task ANoAnswerIsUnconfirmedAndConsumesTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        var use = session.UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.Equal("UNCONFIRMED", result.Code);
        var second = await session.UseAsync(TreeTarget(), cancellationToken);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ADisconnectInterruptsAndConsumesTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        var use = session.UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        connection.Close();
        await bot.ProcessedAsync();

        var result = await use;

        Assert.Equal(SupervisedElementUseOutcome.Interrupted, result.Outcome);
        Assert.Equal("INTERRUPTED_DISCONNECTED", result.Reason);
        Assert.Equal("INTERRUPTED", result.Code);
        var second = await session.UseAsync(TreeTarget(), cancellationToken);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnUncertainSendIsUnconfirmedAndConsumesTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        connection.Relay.SendWait = _ => Task.FromException(new IOException("test"));
        var session = Session(play);

        var result = await session.UseAsync(TreeTarget(), cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("SEND_UNCERTAIN", result.Reason);
        Assert.False(result.Evidence.RequestSent);
        Assert.True(result.Evidence.RequestMayHaveBeenSent);
        var second = await session.UseAsync(TreeTarget(), cancellationToken);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TrustRestorationIsRequiredAfterAClientCancel()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 190, 191 } });
        connection.Client(new MapMovementCancelRequest { CellId = 190 });
        await bot.ProcessedAsync();

        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        var blocked = await session.UseAsync(TreeTarget(), cancellationToken);
        Assert.Equal(ElementUsePreparationState.Blocked, preparation.State);
        Assert.Equal("GUARD_CLIENT_REPORTED", preparation.Reason);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, blocked.Outcome);
        Assert.Equal("GUARD_CLIENT_REPORTED", blocked.Reason);
        Assert.Empty(connection.Sent);

        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();
        var stillBlocked = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        Assert.Equal(ElementUsePreparationState.Blocked, stillBlocked.State);
        Assert.Equal("GUARD_CLIENT_REPORTED", stillBlocked.Reason);
        Assert.Empty(connection.Sent);

        connection.Server(TestPlay.Information(190));
        await bot.ProcessedAsync();
        play.Time.Advance(BotDelays.Measured.Display);
        var restored = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        Assert.Equal(ElementUsePreparationState.Ready, restored.State);
        Assert.Equal("READY", restored.Reason);

        var use = session.UseAsync(TreeTarget(), cancellationToken);
        var request = Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        Assert.Equal(TestPlay.TreeElementId, request.ElementId);
        Assert.Equal(TestPlay.TreeInstanceId, request.SkillInstanceId);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.PassHarvest, result.Outcome);
        Assert.Equal("PASS", result.Reason);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapMovementRequest);
    }

    [Fact]
    public async Task AStaleMatchingEndDoesNotSatisfyTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var session = Session(play);
        var use = session.UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_END", result.Reason);
        Assert.True(result.Evidence.InteractiveUsedObserved);
        Assert.False(result.Evidence.InteractiveUseEndedObserved);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnInventoryGainBeforeTheEndStillProvesTheHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.PassHarvest, result.Outcome);
        Assert.Equal("PASS", result.Reason);
        Assert.Equal(0, result.Evidence.InventoryBefore);
        Assert.Equal(3, result.Evidence.InventoryAfter);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AUseWithoutDurationIsNotAHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 0 });
        await bot.ProcessedAsync();

        var result = await use;

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("USE_HAS_NO_DURATION", result.Reason);
        Assert.True(result.Evidence.InteractiveUsedObserved);
        Assert.False(result.Evidence.InteractiveUseEndedObserved);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AConsumedPreparationCannotExecute()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var preparation = SupervisedElementUse.Prepare(bot, play.Tables, TreeTarget());
        Assert.Equal(ElementUsePreparationState.Ready, preparation.State);

        var consumed = preparation with { AttemptConsumed = true };
        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, SupervisedElementUse.ExecuteAsync(bot, play.Tables, consumed, cancellationToken));

        Assert.Empty(connection.Sent);
        Assert.Equal(SupervisedElementUseOutcome.Blocked, result.Outcome);
        Assert.Equal("ATTEMPT_ALREADY_CONSUMED", result.Reason);
    }

    [Fact]
    public async Task AStaleStartAndEndDuringThePreSendBarrierDoNotSatisfyTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.ObservationWait = async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };

        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        connection.Server(new InteractiveUsedEvent
        {
            ActorId = TestPlay.CharacterId,
            ElementId = TestPlay.TreeElementId,
            SkillId = TestPlay.TreeSkillId,
            Duration = 30,
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();
        release.TrySetResult();
        await connection.SentAsync(1);
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.False(result.Evidence.InteractiveUsedObserved);
        Assert.False(result.Evidence.InteractiveUseEndedObserved);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapChangeRequest);
    }

    [Fact]
    public async Task APostAttemptStartAndEndStillProduceAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.ObservationWait = async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };

        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        connection.Server(new InteractiveUsedEvent
        {
            ActorId = TestPlay.CharacterId,
            ElementId = TestPlay.TreeElementId,
            SkillId = TestPlay.TreeSkillId,
            Duration = 30,
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();
        release.TrySetResult();
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent
        {
            ActorId = TestPlay.CharacterId,
            ElementId = TestPlay.TreeElementId,
            SkillId = TestPlay.TreeSkillId,
            Duration = 30,
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.PassHarvest, result.Outcome);
        Assert.Equal("PASS", result.Reason);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapMovementRequest);
    }

    [Fact]
    public async Task AStaleMatchingErrorDuringThePreSendBarrierIsNotARefusal()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.ObservationWait = async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };

        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });
        await bot.ProcessedAsync();
        release.TrySetResult();
        await connection.SentAsync(1);

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.Null(result.Evidence.ErrorSkillInstanceId);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnErrorForAnotherSkillInstanceIsNotARefusal()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var cancellationToken = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), cancellationToken);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUseErrorEvent
        {
            ElementId = TestPlay.TreeElementId,
            SkillInstanceId = TestPlay.TreeInstanceId + 1,
        });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ATemporaryGainRemovedAfterTheEndIsNotAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), ct);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Contains(result.Reason, _temporaryGainReasons);
        Assert.Equal(0, result.Evidence.InventoryBefore);
        Assert.Equal(0, result.Evidence.InventoryAfter);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AGainReducedToTheInitialQuantityIsNotAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), ct);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 0 } });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Contains(result.Reason, _temporaryGainReasons);
        Assert.Equal(0, result.Evidence.InventoryBefore);
        Assert.Equal(0, result.Evidence.InventoryAfter);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AGainRemovedBeforeTheEndIsNotAPass()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), ct);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal(0, result.Evidence.InventoryAfter);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AStartAndEndObservedDuringTheWriteDoNotSatisfyTheAttempt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.SendWait = async token =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        };

        var use = Session(play).UseAsync(TreeTarget(), ct);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        connection.Server(new InteractiveUsedEvent
        {
            ActorId = TestPlay.CharacterId,
            ElementId = TestPlay.TreeElementId,
            SkillId = TestPlay.TreeSkillId,
            Duration = 30,
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();
        release.TrySetResult();
        await connection.SentAsync(1);
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.False(result.Evidence.InteractiveUsedObserved);
        Assert.False(result.Evidence.InteractiveUseEndedObserved);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapChangeRequest);
    }

    [Fact]
    public async Task ATooFarRefusalNamingOnlyTheSkillIdIsUnconfirmed()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), ct);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeSkillId });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);

        Assert.Equal(SupervisedElementUseOutcome.Unconfirmed, result.Outcome);
        Assert.Equal("NO_ANSWER", result.Reason);
        Assert.Null(result.Evidence.ErrorSkillInstanceId);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ATooFarRefusalNamingTheSkillIdThenTheInstanceIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var use = Session(play).UseAsync(TreeTarget(), ct);
        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeSkillId });
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });

        var result = await use;

        Assert.Equal(SupervisedElementUseOutcome.Refused, result.Outcome);
        Assert.Equal("SERVER_REFUSED", result.Reason);
        Assert.Equal(TestPlay.TreeInstanceId, result.Evidence.ErrorSkillInstanceId);
        Assert.Single(connection.Sent);
    }
}
