using System.Collections.Immutable;
using D3B.Bot.Gui;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Protocol.Game;
using D3B.Gui.Windowing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Gui;

public sealed class GuiWalkToCellCoordinatorTests
{
    [Fact]
    public async Task WalkToCellUsesOneClickAndRequiresTheFullServerConfirmationCycle()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        WalkToCellPlan plan = context.CreatePlan(301);
        GuiWalkToCellTestInput input = new();
        input.OnClick = async (_, _, cancellationToken) =>
        {
            MapData map = context.Play.Maps.Get(TestPlay.EastMapId);
            Assert.True(CellPathfinder.TryFind(map.Cells.Span, 300, 301, out CellPath? path));
            Assert.NotNull(path);

            context.Connection.Client(MapWalk.Request(TestPlay.EastMapId, path));
            await context.Bot.ProcessedAsync();

            MapMovementEvent movement = new() { ActorId = TestPlay.CharacterId };
            movement.Cells.AddRange(path.Cells.ToArray());
            context.Connection.Server(movement);
            await context.Bot.ProcessedAsync();

            context.Connection.Client(new MapMovementConfirmRequest());
            context.Connection.Response(new MapMovementConfirmResponse());
            await context.Bot.ProcessedAsync();
            return new D3B.Gui.Input.GuiInputResult(D3B.Gui.Input.GuiInputStatus.Sent);
        };

        GuiWalkToCellRequest request = context.CreateRequest(plan, input);
        GuiWalkToCellCoordinator coordinator = new(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true));

        GuiWalkToCellResult result = await coordinator.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Pass, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(TestPlay.EastMapId, result.MapId);
        Assert.Equal(301, result.TargetCellId);
        Assert.NotNull(result.IntentId);
        Assert.Equal(1, input.ClickCount);
        Assert.Equal(GuiIntentCorrelationState.Consumed, context.Bot.GuiIntentState);
        Assert.Equal(301, context.Bot.State.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, context.Bot.State.Provenance);
        Assert.False(await context.Bot.ConsumeGuiIntentAsync(result.IntentId!.Value, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("no-client-request")]
    [InlineData("wrong-client-cell")]
    [InlineData("wrong-client-map")]
    [InlineData("no-server-movement")]
    [InlineData("wrong-actor")]
    [InlineData("wrong-endpoint")]
    [InlineData("wrong-server-cell")]
    [InlineData("refusal")]
    [InlineData("missing-final-confirm")]
    [InlineData("out-of-order")]
    [InlineData("unknown-message")]
    [InlineData("partial-state")]
    public async Task IncompleteOrContradictoryProtocolCycleIsUnconfirmed(string condition)
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        using CancellationTokenSource timeoutClockCancellation = new();
        Task timeoutClock = Task.CompletedTask;
        GuiWalkToCellTestInput input = new();
        input.OnClick = async (_, _, cancellationToken) =>
        {
            timeoutClock = AdvanceConfirmationClockAsync(context.Play.Time, timeoutClockCancellation.Token);
            await DriveUnconfirmedCycleAsync(context, condition, cancellationToken);
            return new GuiInputResult(GuiInputStatus.Sent);
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        timeoutClockCancellation.Cancel();
        await timeoutClock;

        Assert.Equal(GuiActionOutcomeStatus.Unconfirmed, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
        Assert.NotEqual(GuiIntentCorrelationState.Consumed, context.Bot.GuiIntentState);
        Assert.False(context.Bot.State.Cell == 301 && context.Bot.State.Provenance == CellProvenance.ServerConfirmed);
    }

    [Fact]
    public async Task UnsafeProtocolSnapshotIsBlockedBeforeWindowOrCaptureWork()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        context.Bot.Pause();
        await context.Bot.ProcessedAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator().ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.Contains("player", result.Outcome.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, desktop.LocateCount);
        Assert.Equal(0, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Fact]
    public async Task UnreachablePlanIsBlockedBeforeWindowOrCaptureWork()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        MapData map = context.Play.Maps.Get(TestPlay.EastMapId);
        WalkToCellPlan plan = WalkToCellPlanner.Plan(map.Cells.Span, 300, [], new HashSet<int>());
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(plan, input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator().ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(1, desktop.LocateCount);
        Assert.Equal(1, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("paused")]
    [InlineData("fight")]
    [InlineData("walking")]
    [InlineData("using")]
    [InlineData("dialog")]
    [InlineData("ambiguous")]
    [InlineData("partial")]
    [InlineData("map-unknown")]
    [InlineData("estimated")]
    [InlineData("client-reported")]
    [InlineData("refused")]
    [InlineData("translation-blocked")]
    public async Task UnsafeProtocolStateBlocksBeforeWindowLookup(string condition)
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        WalkToCellPlan plan = context.CreatePlan(301);
        await ApplyProtocolConditionAsync(context, condition);
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(plan, input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator().ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(
            result.Outcome.Status is GuiActionOutcomeStatus.Blocked or GuiActionOutcomeStatus.Interrupted,
            result.Outcome.Detail);
        Assert.Equal(0, desktop.LocateCount);
        Assert.Equal(0, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Theory]
    [InlineData("S1")]
    [InlineData("S2")]
    public async Task StaleProtocolSnapshotsBlockBeforeInput(string snapshot)
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        DateTimeOffset now = context.Play.Time.GetUtcNow();
        ManualGuiTimeProvider adapterClock = new(now);
        GuiWorldSnapshotAdapter adapter = new(adapterClock);
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();

        if (snapshot == "S1")
        {
            adapterClock.UtcNow = now.AddSeconds(-3);
        }
        else
        {
            desktop.AfterCapture = () =>
            {
                adapterClock.UtcNow = now.AddSeconds(-3);
                return Task.CompletedTask;
            };
        }

        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(
            context.CreatePlan(301),
            input,
            desktop,
            adapter: adapter);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.StateChanged, result.Outcome.BlockedReason);
        Assert.Equal(snapshot == "S1" ? 0 : 1, desktop.LocateCount);
        Assert.Equal(snapshot == "S1" ? 0 : 1, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Idle, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task TrustPolicyRejectsAnEvidenceWindowThatOutlivesItsSnapshots()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.WindowForLocate = count =>
        {
            if (count == 2)
                context.Play.Time.Advance(TimeSpan.FromSeconds(3));

            return context.Window;
        };
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.StateChanged, result.Outcome.BlockedReason);
        Assert.Contains("stale", result.Outcome.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, desktop.LocateCount);
        Assert.Equal(1, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Idle, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task IntentThatExpiresAtFinalGuardIsCanceledWithoutClick()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.WindowForLocate = count =>
        {
            if (count == 3)
                context.Play.Time.Advance(TimeSpan.FromSeconds(3));

            return context.Window;
        };
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.IntentExpired, result.Outcome.BlockedReason);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, context.Bot.GuiIntentState);
    }

    [Theory]
    [InlineData("unverified")]
    [InlineData("inactive")]
    [InlineData("unmapped")]
    [InlineData("not-x11")]
    [InlineData("no-xtest")]
    [InlineData("unreliable-geometry")]
    [InlineData("wrong-xid")]
    public async Task UnsafeWindowIsBlockedBeforeCapture(string condition)
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.Window = condition switch
        {
            "unverified" => context.Window with { Identity = context.Window.Identity with { Status = GameWindowIdentityStatus.UnverifiedExecutableUnreadable } },
            "inactive" => context.Window with { IsFocused = false },
            "unmapped" => context.Window with { IsMapped = false },
            "not-x11" => context.Window with { IsSessionX11 = false },
            "no-xtest" => context.Window with { IsXTestAvailable = false },
            "unreliable-geometry" => context.Window with { GeometryReliable = false },
            "wrong-xid" => context.Window with { Identity = context.Window.Identity with { Xid = context.Window.Identity.Xid + 1 } },
            _ => context.Window,
        };
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator().ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(1, desktop.LocateCount);
        Assert.Equal(0, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Fact]
    public async Task WindowChangeAfterCaptureIsBlockedBeforeRegistration()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.WindowForLocate = count => count == 2
            ? context.Window with { DesktopOrigin = new DesktopPixels(100, 20) }
            : context.Window;
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.WindowChanged, result.Outcome.BlockedReason);
        Assert.Equal(2, desktop.LocateCount);
        Assert.Equal(1, desktop.CaptureCount);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Idle, context.Bot.GuiIntentState);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("invalid")]
    [InlineData("synthetic-production")]
    public async Task StaleInvalidOrProductionSyntheticCalibrationBlocksBeforeProjection(string condition)
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        CalibrationResult calibration = condition == "invalid"
            ? new CalibrationResult(CalibrationStatus.InvalidInput, null, null, [], false)
            : context.Calibration;
        if (condition == "stale")
            context.Play.Time.Advance(TimeSpan.FromSeconds(31));

        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(
            context.CreatePlan(301),
            input,
            desktop,
            calibration);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator().ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.True(
            result.Outcome.BlockedReason == GuiSafetyReason.CalibrationStale
            || result.Outcome.BlockedReason == GuiSafetyReason.NotCalibrated);
        Assert.Equal(1, desktop.CaptureCount);
        Assert.Null(result.Target);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Theory]
    [InlineData("capture-failed")]
    [InlineData("capture-unusable")]
    [InlineData("shifted-capture")]
    [InlineData("ui-overlap")]
    [InlineData("element-cell")]
    public async Task UnverifiedCaptureOrTargetBlocksWithoutInput(string condition)
    {
        await using GuiWalkToCellTestContext context = condition == "element-cell"
            ? await GuiWalkToCellTestContext.CreateAsync(301)
            : await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        WalkToCellPlan plan = condition == "element-cell"
            ? context.CreatePlanIgnoringInteractiveCells(300)
            : context.CreatePlan(301);
        if (condition == "element-cell")
        {
            MapInformationEvent information = TestPlay.Information(301, TestPlay.EastMapId);
            information.InteractiveElements.Add(new InteractiveElement
            {
                ElementId = TestPlay.BushElementId,
                InteractiveId = 34,
                OnCurrentMap = true,
            });
            information.StatedElements.Add(new StatedElement
            {
                ElementId = TestPlay.BushElementId,
                CellId = 300,
                State = 0,
                OnCurrentMap = true,
            });
            context.Connection.Server(information);
            await context.Bot.ProcessedAsync();
        }

        ImmutableArray<WindowLocalRect> exclusions = ImmutableArray<WindowLocalRect>.Empty;
        if (condition == "capture-failed")
            desktop.CaptureStatus = CaptureStatus.Failed;
        else if (condition == "capture-unusable")
            desktop.ContentUsable = false;
        else if (condition == "shifted-capture")
            desktop.AfterCapture = () =>
            {
                desktop.Window = desktop.Window with { DesktopOrigin = new DesktopPixels(100, 20) };
                return Task.CompletedTask;
            };
        else if (condition == "ui-overlap")
            exclusions = [new WindowLocalRect(2, 2, 780, 560)];

        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(plan, input, desktop, uiExclusions: exclusions);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.NotEqual(GuiIntentCorrelationState.ServerConfirmed, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task S1AndS2RevisionMismatchBlocksBeforeRegistration()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.AfterCapture = async () =>
        {
            context.Bot.MarkMovementRequested(TestPlay.EastMapId);
            await context.Bot.ProcessedAsync();
        };
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.StateChanged, result.Outcome.BlockedReason);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Idle, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task ASecondActiveIntentIsRefusedWithoutClick()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        await context.RegisterActiveIntentAsync(301);
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, result.Outcome.Status);
        Assert.Equal(GuiSafetyReason.Busy, result.Outcome.BlockedReason);
        Assert.False(result.Outcome.ClickEmitted);
    }

    [Fact]
    public async Task BackendFailureBeforeClickIsFailedAndDoesNotRetry()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, _) => Task.FromResult(new GuiInputResult(GuiInputStatus.Failed, "Synthetic backend failure."));
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Failed, result.Outcome.Status);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task BackendExceptionAfterClickGateIsUnconfirmedAndNeverRetried()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, _) => throw new InvalidOperationException("Synthetic backend exception.");
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Unconfirmed, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task PlayerPauseAfterClickIsInterrupted()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestInput input = new();
        input.OnClick = async (_, _, _) =>
        {
            context.Bot.Pause();
            await context.Bot.ProcessedAsync();
            return new GuiInputResult(GuiInputStatus.Sent);
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task WindowLossAfterAcknowledgementIsInterruptedBeforeInput()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        desktop.WindowForLocate = count => count == 3
            ? context.Window with
            {
                Identity = context.Window.Identity with { Status = GameWindowIdentityStatus.NotFound },
                Exists = false,
                IsMapped = false,
                IsViewable = false,
            }
            : context.Window;
        DisabledGuiInput input = new();
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.False(result.Outcome.ClickEmitted);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task WindowLossAfterClickIsInterruptedAndNeverRetried()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, _) =>
        {
            desktop.Window = context.Window with
            {
                Identity = context.Window.Identity with { Status = GameWindowIdentityStatus.NotFound },
                Exists = false,
                IsMapped = false,
                IsViewable = false,
            };
            return Task.FromResult(new GuiInputResult(GuiInputStatus.Sent));
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task CallerStopAfterClickIsInterruptedAndNeverRetried()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        using CancellationTokenSource stop = new();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, _) =>
        {
            stop.Cancel();
            return Task.FromResult(new GuiInputResult(GuiInputStatus.Sent));
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, stop.Token);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task InputCancellationDuringTheClickAttemptIsInterruptedAsUncertain()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        using CancellationTokenSource stop = new();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, cancellationToken) =>
        {
            stop.Cancel();
            return Task.FromCanceled<GuiInputResult>(cancellationToken);
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, stop.Token);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, context.Bot.GuiIntentState);
    }

    [Fact]
    public async Task ReplacedWindowAfterClickIsUnconfirmed()
    {
        await using GuiWalkToCellTestContext context = await GuiWalkToCellTestContext.CreateAsync();
        GuiWalkToCellTestDesktop desktop = context.CreateDesktop();
        GuiWalkToCellTestInput input = new();
        input.OnClick = (_, _, _) =>
        {
            desktop.Window = context.Window with
            {
                Identity = context.Window.Identity with { Xid = context.Window.Identity.Xid + 1 },
            };
            return Task.FromResult(new GuiInputResult(GuiInputStatus.Sent));
        };
        GuiWalkToCellRequest request = context.CreateRequest(context.CreatePlan(301), input, desktop);

        GuiWalkToCellResult result = await new GuiWalkToCellCoordinator(new GuiWalkToCellCoordinatorOptions(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            true)).ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Unconfirmed, result.Outcome.Status);
        Assert.True(result.Outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    private static async Task ApplyProtocolConditionAsync(GuiWalkToCellTestContext context, string condition)
    {
        if (condition == "disconnected")
        {
            context.Bot.Detach(context.Connection.Session);
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "paused")
        {
            context.Bot.Pause();
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "translation-blocked")
        {
            context.Connection.ServerRaw([0x12, 0x7F, 0x01]);
            await context.Bot.ProcessedAsync();
            return;
        }

        CharacterState state = context.Bot.State;
        state = condition switch
        {
            "fight" => state with { Fight = new FightState() },
            "walking" => state with { Path = [300, 301] },
            "using" => state with { Use = new CurrentUse(1, 2) },
            "dialog" => state with { Dialog = new NpcDialog(1) },
            "ambiguous" => state with { MovementAnswerAmbiguous = true },
            "partial" => state with { Cell = null },
            "map-unknown" => state with { MapId = null },
            "estimated" => state with { Provenance = CellProvenance.Estimated },
            "client-reported" => state with { Provenance = CellProvenance.ClientReported },
            "refused" => state with { Provenance = CellProvenance.ServerRefused },
            _ => state,
        };
        context.Bot.Attach(context.Connection.Session, state);
        await context.Bot.ProcessedAsync();
    }

    private static async Task DriveUnconfirmedCycleAsync(
        GuiWalkToCellTestContext context,
        string condition,
        CancellationToken cancellationToken)
    {
        MapData map = context.Play.Maps.Get(TestPlay.EastMapId);
        Assert.True(CellPathfinder.TryFind(map.Cells.Span, 300, 301, out CellPath? intendedPath));
        Assert.NotNull(intendedPath);

        if (condition == "no-client-request")
            return;

        if (condition == "wrong-client-cell")
        {
            Assert.True(CellPathfinder.TryFind(map.Cells.Span, 300, 302, out CellPath? wrongPath));
            Assert.NotNull(wrongPath);
            context.Connection.Client(MapWalk.Request(TestPlay.EastMapId, wrongPath));
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "wrong-client-map")
        {
            context.Connection.Client(MapWalk.Request(TestPlay.EastMapId + 1, intendedPath));
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "out-of-order")
        {
            MapMovementEvent outOfOrder = new() { ActorId = TestPlay.CharacterId };
            outOfOrder.Cells.AddRange(intendedPath.Cells.ToArray());
            context.Connection.Server(outOfOrder);
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "unknown-message")
        {
            context.Connection.ServerRaw([0x12, 0x7F, 0x01]);
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "partial-state")
        {
            context.Connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId + 100 });
            await context.Bot.ProcessedAsync();
            return;
        }

        context.Connection.Client(MapWalk.Request(TestPlay.EastMapId, intendedPath));
        await context.Bot.ProcessedAsync();

        if (condition == "no-server-movement")
            return;

        if (condition == "refusal")
        {
            context.Connection.Server(new MapMovementRefusedEvent());
            await context.Bot.ProcessedAsync();
            return;
        }

        MapMovementEvent movement = new()
        {
            ActorId = condition == "wrong-actor" ? TestPlay.CharacterId + 1 : TestPlay.CharacterId,
        };

        if (condition == "wrong-server-cell")
        {
            Assert.True(CellPathfinder.TryFind(map.Cells.Span, 300, 302, out CellPath? wrongPath));
            Assert.NotNull(wrongPath);
            movement.Cells.AddRange(wrongPath.Cells.ToArray());
        }
        else
        {
            movement.Cells.AddRange(intendedPath.Cells.ToArray());
        }

        context.Connection.Server(movement);
        await context.Bot.ProcessedAsync();

        if (condition == "wrong-endpoint")
        {
            context.Connection.Client(new MapMovementConfirmResponse());
            await context.Bot.ProcessedAsync();
            return;
        }

        if (condition == "missing-final-confirm")
            return;

        if (condition is not ("wrong-actor" or "wrong-server-cell"))
            throw new InvalidOperationException($"Unknown unconfirmed-cycle case '{condition}'.");

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task AdvanceConfirmationClockAsync(FakeTimeProvider timeProvider, CancellationToken cancellationToken)
    {
        try
        {
            for (int tick = 0; tick < 10; tick++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                timeProvider.Advance(TimeSpan.FromSeconds(1));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
