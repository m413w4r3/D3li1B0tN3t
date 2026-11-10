using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Interactives;

public sealed class ElementCandidatesTests
{
    private static ElementCandidate Tree(ElementCandidateReport report)
    {
        return Assert.Single(report.Candidates.Where(candidate => candidate.ElementId == TestPlay.TreeElementId));
    }

    private static async Task AddServerElementAsync(
        TestConnection connection,
        BotActor bot,
        int elementId,
        int skillId,
        int instanceId,
        int? cell = null)
    {
        var information = TestPlay.Information(bot.State.Cell!.Value, bot.State.MapId!.Value);
        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = elementId,
            InteractiveId = 34,
            OnCurrentMap = true,
            EnabledSkills = { new InteractiveSkill { SkillId = skillId, InstanceId = instanceId } },
        });

        if (cell is { } serverCell)
        {
            information.StatedElements.Add(new StatedElement
            {
                ElementId = elementId,
                CellId = serverCell,
                State = 0,
                OnCurrentMap = true,
            });
        }

        connection.Server(information);
        await bot.ProcessedAsync();
    }

    private static void AssertNoRequests(TestConnection connection)
    {
        Assert.Empty(connection.Sent);
        Assert.DoesNotContain(connection.Sent, request => request is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent, request => request is MapChangeRequest);
        Assert.DoesNotContain(connection.Sent, request => request is InteractiveUseRequest);
    }

    private static string Projection(ElementCandidate candidate)
    {
        return string.Join(
            ":",
            candidate.ElementId,
            candidate.MapId,
            candidate.ServerKnown,
            candidate.PackKnown,
            candidate.InteractiveId,
            candidate.ServerCell,
            candidate.PackCell,
            candidate.GfxId,
            candidate.ElementState,
            candidate.SkillId,
            candidate.SkillInstanceId,
            string.Join(",", candidate.SkillIds),
            candidate.SkillEnabled,
            candidate.Catalog,
            candidate.GatheredItemId,
            candidate.GatheredItemName,
            string.Join(",", candidate.PossibleItemIds),
            candidate.SkillMinLevel,
            candidate.UsesFromCurrentCell,
            candidate.Identity,
            candidate.IdentitySource,
            candidate.CellConflict,
            candidate.PreparationState,
            candidate.PreparationReason);
    }

    [Fact]
    public async Task ACandidateListNamesEveryHarvestableElementOnTheMap()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        await AddServerElementAsync(connection, bot, 600_001, TestPlay.TreeSkillId, 9_999);

        var report = ElementCandidates.Capture(bot, play.Tables);
        var tree = Tree(report);
        var serverOnly = Assert.Single(report.Candidates.Where(candidate => candidate.ElementId == 600_001));

        Assert.Equal(2, report.Candidates.Length);
        Assert.Equal([TestPlay.TreeElementId, 600_001], report.Candidates.Select(candidate => candidate.ElementId));
        Assert.True(tree.ServerKnown);
        Assert.True(tree.PackKnown);
        Assert.Equal(ElementCandidateIdentity.Verified, tree.Identity);
        Assert.True(serverOnly.ServerKnown);
        Assert.False(serverOnly.PackKnown);
        Assert.Equal(ElementCandidateTargetVerdict.Ambiguous, report.Target);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, report.Action);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnAvailableSkillIsReportedAsEnabledAndReady()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        var ready = ElementCandidates.Capture(
            bot,
            play.Tables,
            itemName: itemId => itemId == TestPlay.Ash ? "Ash wood" : null,
            actionEnabled: true);
        var candidate = Tree(ready);

        Assert.True(candidate.SkillEnabled == true);
        Assert.Equal(TestPlay.TreeInstanceId, candidate.SkillInstanceId);
        Assert.Equal(ElementCandidateCatalog.Matched, candidate.Catalog);
        Assert.Equal(TestPlay.Ash, candidate.GatheredItemId);
        Assert.Equal("Ash wood", candidate.GatheredItemName);
        Assert.True(candidate.UsesFromCurrentCell == true);
        Assert.Equal(ElementUsePreparationState.Ready, candidate.PreparationState);
        Assert.Equal(ElementCandidateTargetVerdict.Identified, ready.Target);
        Assert.Equal(ElementCandidateActionVerdict.Ready, ready.Action);

        var disabledAction = ElementCandidates.Capture(bot, play.Tables, actionEnabled: false);

        Assert.Equal(ElementCandidateTargetVerdict.Identified, disabledAction.Target);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, disabledAction.Action);
        Assert.Equal("ACTION_DISABLED", disabledAction.ActionReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task ADisabledSkillIsReportedWithoutAReadyAction()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.True(candidate.SkillEnabled == false);
        Assert.Equal(ElementUsePreparationState.Unavailable, candidate.PreparationState);
        Assert.Equal("SKILL_DISABLED", candidate.PreparationReason);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, report.Action);
        Assert.Equal("SKILL_DISABLED", report.ActionReason);
        Assert.Equal(ElementCandidateTargetVerdict.Identified, report.Target);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnElementThePackLacksIsStillListedFromServerState()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        await AddServerElementAsync(connection, bot, 600_001, TestPlay.TreeSkillId, 9_999);

        var candidate = Assert.Single(ElementCandidates.Capture(bot, play.Tables).Candidates.Where(item => item.ElementId == 600_001));

        Assert.False(candidate.PackKnown);
        Assert.Null(candidate.PackCell);
        Assert.Null(candidate.GfxId);
        Assert.Equal(ElementUsePreparationState.Unavailable, candidate.PreparationState);
        Assert.Equal("ELEMENT_NOT_ON_MAP", candidate.PreparationReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task APackElementAbsentFromServerStateIsListedAsUnknown()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(435, TestPlay.NorthMapId);

        var unknown = ElementCandidates.Capture(bot, play.Tables, graphics: null);
        var candidate = Assert.Single(unknown.Candidates);

        Assert.False(candidate.ServerKnown);
        Assert.True(candidate.PackKnown);
        Assert.Equal(ElementCandidateIdentity.Unknown, candidate.Identity);
        Assert.Null(candidate.SkillId);
        Assert.Equal(ElementUsePreparationState.Unavailable, candidate.PreparationState);
        Assert.Equal("ELEMENT_NOT_KNOWN", candidate.PreparationReason);

        var graphics = new GraphicSkills();
        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);
        var inferred = ElementCandidates.Capture(bot, play.Tables, graphics: graphics);
        var inferredCandidate = Assert.Single(inferred.Candidates);

        Assert.Equal(ElementCandidateIdentity.Inferred, inferredCandidate.Identity);
        Assert.Equal(TestPlay.Ash, inferredCandidate.GatheredItemId);
        Assert.Equal("ELEMENT_NOT_KNOWN", inferredCandidate.PreparationReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task ASkillOutsideTheCatalogHasNoReliableResource()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.Server(new InteractiveElementUpdatedEvent
        {
            Element = new InteractiveElement
            {
                ElementId = TestPlay.TreeElementId,
                InteractiveId = 34,
                OnCurrentMap = true,
                EnabledSkills = { new InteractiveSkill { SkillId = 99_999, InstanceId = 9_999 } },
            },
        });
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.Equal(ElementCandidateCatalog.Unmatched, candidate.Catalog);
        Assert.Null(candidate.GatheredItemId);
        Assert.Equal(ElementCandidateIdentity.Unknown, candidate.Identity);
        Assert.Equal(ElementUsePreparationState.Unavailable, candidate.PreparationState);
        Assert.Equal("SKILL_NOT_IN_CATALOG", candidate.PreparationReason);
        Assert.Equal(ElementCandidateTargetVerdict.Ambiguous, report.Target);
        Assert.Equal("SKILL_NOT_IN_CATALOG", report.ActionReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnElementWithAnUnknownSkillAlongsideAKnownHarvestIsAmbiguous()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var tree = TestPlay.Tree(enabled: true);
        tree.EnabledSkills.Add(new InteractiveSkill { SkillId = 99_999, InstanceId = 9_999 });
        connection.Server(new InteractiveElementUpdatedEvent { Element = tree });
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.Equal(ElementCandidateCatalog.Partial, candidate.Catalog);
        Assert.Equal(ElementCandidateIdentity.Ambiguous, candidate.Identity);
        Assert.Null(candidate.GatheredItemId);
        Assert.Equal([TestPlay.Ash], candidate.PossibleItemIds);
        Assert.Equal(ElementUsePreparationState.Blocked, candidate.PreparationState);
        Assert.Equal("TARGET_AMBIGUOUS", candidate.PreparationReason);
        Assert.Equal(ElementCandidateTargetVerdict.Ambiguous, report.Target);
        Assert.Equal("TARGET_AMBIGUOUS", report.ActionReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnElementWithSeveralHarvestSkillsIsAmbiguous()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var tree = TestPlay.Tree(enabled: true);
        tree.EnabledSkills.Add(new InteractiveSkill { SkillId = TestPlay.ChestnutSkillId, InstanceId = 8_878 });
        connection.Server(new InteractiveElementUpdatedEvent { Element = tree });
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.Equal(ElementCandidateIdentity.Ambiguous, candidate.Identity);
        Assert.Null(candidate.SkillId);
        Assert.Equal([TestPlay.ChestnutSkillId, TestPlay.TreeSkillId], candidate.SkillIds);
        Assert.Equal([TestPlay.Ash, TestPlay.Chestnut], candidate.PossibleItemIds);
        Assert.Null(candidate.GatheredItemId);
        Assert.Equal(ElementUsePreparationState.Blocked, candidate.PreparationState);
        Assert.Equal("TARGET_AMBIGUOUS", candidate.PreparationReason);
        Assert.Equal(ElementCandidateTargetVerdict.Ambiguous, report.Target);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AConflictingServerCellIsReportedAndBlocksTheAction()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.Server(new StatedElementUpdatedEvent
        {
            Element = new StatedElement { ElementId = TestPlay.TreeElementId, CellId = 177, State = 0, OnCurrentMap = true },
        });
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.Equal(177, candidate.ServerCell);
        Assert.Equal(TestPlay.TreeCell, candidate.PackCell);
        Assert.True(candidate.CellConflict);
        Assert.Null(candidate.UsesFromCurrentCell);
        Assert.Equal(ElementCandidateIdentity.Ambiguous, candidate.Identity);
        Assert.Equal(TestPlay.Ash, candidate.GatheredItemId);
        Assert.Equal(ElementUsePreparationState.Blocked, candidate.PreparationState);
        Assert.Equal("ELEMENT_CELL_CONFLICT", candidate.PreparationReason);
        Assert.Equal("ELEMENT_CELL_CONFLICT", report.ActionReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnUnknownPositionIsReportedAndBlocksTheAction()
    {
        await using var play = new TestPlay();
        var connection = play.Connect(TestPlay.Account);
        connection.Server(TestPlay.Chosen());
        Assert.True(play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);

        Assert.NotNull(report.Snapshot);
        Assert.Empty(report.Candidates);
        Assert.Equal(ElementCandidateTargetVerdict.Unknown, report.Target);
        Assert.Equal("GUARD_MAP_UNKNOWN", report.ActionReason);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AnUntrustedPositionIsReportedAndBlocksTheAction()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 190, 191 } });
        connection.Client(new MapMovementCancelRequest { CellId = 190 });
        await bot.ProcessedAsync();
        var sentCount = connection.Sent.Count;

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.Equal(CellProvenance.ClientReported, report.Snapshot!.State.Provenance);
        Assert.False(report.Snapshot.State.IsPositionTrusted);
        Assert.Null(candidate.UsesFromCurrentCell);
        Assert.Equal("GUARD_CLIENT_REPORTED", report.ActionReason);
        Assert.Equal(ElementCandidateTargetVerdict.Identified, report.Target);
        Assert.Equal(sentCount, connection.Sent.Count);
        Assert.DoesNotContain(connection.Sent.Skip(sentCount), request => request is MapMovementRequest);
        Assert.DoesNotContain(connection.Sent.Skip(sentCount), request => request is MapChangeRequest);
        Assert.DoesNotContain(connection.Sent.Skip(sentCount), request => request is InteractiveUseRequest);
    }

    [Fact]
    public async Task AClosedTranslationLatchKeepsDiagnosticsButNeverClaimsReady()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();

        var report = ElementCandidates.Capture(bot, play.Tables);
        var candidate = Tree(report);

        Assert.True(report.Snapshot!.TranslationBlocked);
        Assert.Equal(ElementCandidateIdentity.Verified, candidate.Identity);
        Assert.Equal(TestPlay.Ash, candidate.GatheredItemId);
        Assert.Equal(ElementUsePreparationState.Blocked, candidate.PreparationState);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", candidate.PreparationReason);
        Assert.Equal(ElementCandidateActionVerdict.Blocked, report.Action);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", report.ActionReason);
        Assert.DoesNotContain(report.Candidates, item => item.PreparationState is ElementUsePreparationState.Ready);
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task AChangedMapOrReplacedSessionIsNeverMixedIntoTheReport()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var viewA = bot.View;
        var reportA = ElementCandidates.Capture(bot, play.Tables);

        Assert.Equal(TestPlay.MapId, reportA.Snapshot!.State.MapId);
        Assert.Equal(viewA.Revision, reportA.Snapshot.Revision);
        Assert.Equal(viewA.SessionEpoch, reportA.Snapshot.SessionEpoch);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(435, TestPlay.NorthMapId));
        await bot.ProcessedAsync();

        var viewB = bot.View;
        var reportB = ElementCandidates.Capture(bot, play.Tables);

        Assert.Equal(TestPlay.NorthMapId, reportB.Snapshot!.State.MapId);
        Assert.Equal(TestPlay.NorthMapId, reportB.Snapshot.Map!.Id);
        Assert.Equal(viewB.Revision, reportB.Snapshot.Revision);
        Assert.Equal(viewB.SessionEpoch, reportB.Snapshot.SessionEpoch);
        Assert.Equal(TestPlay.MapId, reportA.Snapshot.State.MapId);

        connection.Close();
        await bot.ProcessedAsync();
        var replacement = play.Connect(TestPlay.Account);
        replacement.Server(TestPlay.Chosen());
        await bot.ProcessedAsync();

        var reportC = ElementCandidates.Capture(bot, play.Tables);

        Assert.NotEqual(reportB.Snapshot.SessionEpoch, reportC.Snapshot!.SessionEpoch);
        Assert.Equal(TestPlay.NorthMapId, reportB.Snapshot.State.MapId);
        Assert.Equal(TestPlay.MapId, reportA.Snapshot.State.MapId);
        AssertNoRequests(connection);
        AssertNoRequests(replacement);
    }

    [Fact]
    public async Task TheCandidateListIsDeterministicAndSorted()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var information = TestPlay.Information(190);
        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = 600_002,
            InteractiveId = 34,
            OnCurrentMap = true,
            EnabledSkills =
            {
                new InteractiveSkill { SkillId = TestPlay.TreeSkillId, InstanceId = 9_002 },
                new InteractiveSkill { SkillId = TestPlay.ChestnutSkillId, InstanceId = 9_003 },
            },
        });
        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = 600_001,
            InteractiveId = 34,
            OnCurrentMap = true,
            EnabledSkills = { new InteractiveSkill { SkillId = TestPlay.TreeSkillId, InstanceId = 9_001 } },
        });
        connection.Server(information);
        await bot.ProcessedAsync();

        var snapshot = SupervisedElementUse.Capture(bot);
        var first = ElementCandidates.Build(snapshot, play.Tables, graphics: null, itemName: null);
        var second = ElementCandidates.Build(snapshot, play.Tables, graphics: null, itemName: null);

        Assert.Equal([TestPlay.TreeElementId, 600_001, 600_002], first.Candidates.Select(candidate => candidate.ElementId));
        Assert.All(first.Candidates, candidate =>
        {
            Assert.Equal(candidate.SkillIds.Order(), candidate.SkillIds);
            Assert.Equal(candidate.PossibleItemIds.Order(), candidate.PossibleItemIds);
        });
        Assert.Equal(first.Candidates.Select(Projection), second.Candidates.Select(Projection));
        AssertNoRequests(connection);
    }

    [Fact]
    public async Task TheCandidateBuilderSendsNothingAndConsumesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        var report = ElementCandidates.Capture(bot, play.Tables);
        var preparation = SupervisedElementUse.Prepare(
            bot,
            play.Tables,
            new ElementUseTarget(TestPlay.MapId, 190, TestPlay.TreeElementId, TestPlay.TreeSkillId));

        Assert.NotEmpty(report.Candidates);
        Assert.Null(bot.Activity);
        Assert.False(preparation.AttemptConsumed);
        AssertNoRequests(connection);
    }
}
