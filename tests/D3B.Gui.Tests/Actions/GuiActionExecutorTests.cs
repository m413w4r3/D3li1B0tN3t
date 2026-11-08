using System.Diagnostics.CodeAnalysis;
using D3B.Gui.Actions;
using D3B.Gui.Input;
using D3B.Gui.Safety;

namespace D3B.Gui.Tests.Actions;

public sealed class GuiActionExecutorTests
{
    [Fact]
    public async Task ExpiredIntentionIsRejectedWithoutInput()
    {
        using GuiActionTestFixture fixture = new();
        fixture.Intent = fixture.Intent with { ExpiresAt = fixture.Now };
        RecordingGuiInput input = new();
        GuiActionExecutor executor = CreateExecutor(fixture, null);

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, outcome.Status);
        Assert.Equal(GuiSafetyReason.IntentExpired, outcome.BlockedReason);
        Assert.Equal(0, input.ClickCount);
    }

    [Fact]
    public async Task DuplicateIntentionIsConsumedOnlyOnce()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new() { Result = new GuiInputResult(GuiInputStatus.Disabled) };
        GuiActionExecutor executor = CreateExecutor(fixture, new NeverConfirms());

        GuiActionOutcome first = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);
        GuiActionOutcome second = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, first.Status);
        Assert.Equal(GuiActionOutcomeStatus.Blocked, second.Status);
        Assert.Equal(GuiSafetyReason.Busy, second.BlockedReason);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task DisabledInputProducesNoClick()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new() { IsEnabled = false };
        GuiActionExecutor executor = CreateExecutor(fixture, new NeverConfirms());

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, outcome.Status);
        Assert.Equal(GuiSafetyReason.Disabled, outcome.BlockedReason);
        Assert.Equal(0, input.ClickCount);
    }

    [Fact]
    public async Task PreviewCannotCallARealInputImplementation()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new() { EmitsRealInput = true };
        GuiActionExecutor executor = CreateExecutor(fixture, new NeverConfirms());

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Blocked, outcome.Status);
        Assert.Equal(GuiSafetyReason.Disabled, outcome.BlockedReason);
        Assert.Equal(0, input.ClickCount);
    }

    [Fact]
    public async Task FakeInputEmitsAtMostOneClickAndMatchingConfirmationPasses()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new();
        FakeGuiOutcomeConfirmation confirmation = new() { Outcome = GuiServerOutcome.MovementConfirmed };
        GuiActionExecutor executor = CreateExecutor(fixture, confirmation);

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Pass, outcome.Status);
        Assert.True(outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Equal(new D3B.Gui.Windowing.DesktopPixels(590, 420), input.LastPoint);
    }

    [Fact]
    public async Task ClickWithoutMatchingConfirmationRemainsUnconfirmed()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new();
        GuiActionExecutor executor = CreateExecutor(fixture, null);

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Unconfirmed, outcome.Status);
        Assert.True(outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    [SuppressMessage("xUnit", "xUnit1051", Justification = "This test controls cancellation to verify the interrupted outcome.")]
    public async Task CancellationAfterClickIsInterrupted()
    {
        using GuiActionTestFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        RecordingGuiInput input = new() { CancelAfterClick = cancellation };
        GuiActionExecutor executor = CreateExecutor(fixture, new NeverConfirms());

        GuiActionOutcome outcome = await Execute(fixture, executor, input, cancellation.Token);

        Assert.Equal(GuiActionOutcomeStatus.Interrupted, outcome.Status);
        Assert.True(outcome.ClickEmitted);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task InputFailureIsFailedAndNeverRetried()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new() { Result = new GuiInputResult(GuiInputStatus.Failed, "synthetic failure") };
        GuiActionExecutor executor = CreateExecutor(fixture, new NeverConfirms());

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);
        GuiActionOutcome repeated = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Failed, outcome.Status);
        Assert.Equal(GuiActionOutcomeStatus.Blocked, repeated.Status);
        Assert.Equal(1, input.ClickCount);
    }

    [Theory]
    [InlineData(GuiInputStatus.Canceled)]
    [InlineData(GuiInputStatus.TimedOut)]
    [InlineData(GuiInputStatus.Failed)]
    public async Task UncertainInputFailureIsUnconfirmedAndNeverRetried(GuiInputStatus status)
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new()
        {
            Result = new GuiInputResult(status, "synthetic uncertainty", ClickMayHaveBeenEmitted: true),
        };
        FakeGuiOutcomeConfirmation confirmation = new() { Outcome = GuiServerOutcome.MovementConfirmed };
        GuiActionExecutor executor = CreateExecutor(fixture, confirmation);

        GuiActionOutcome outcome = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);
        GuiActionOutcome repeated = await Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        Assert.Equal(GuiActionOutcomeStatus.Unconfirmed, outcome.Status);
        Assert.True(outcome.ClickEmitted);
        Assert.Equal(GuiActionOutcomeStatus.Blocked, repeated.Status);
        Assert.Equal(GuiSafetyReason.Busy, repeated.BlockedReason);
        Assert.Equal(0, confirmation.CallCount);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public async Task ConcurrentSecondIntentionIsBlockedWhileFirstWaitsForConfirmation()
    {
        using GuiActionTestFixture fixture = new();
        RecordingGuiInput input = new();
        TaskCompletionSource<GuiServerOutcome?> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeGuiOutcomeConfirmation confirmation = new() { Waiter = waiter };
        GuiActionExecutor executor = CreateExecutor(fixture, confirmation);

        Task<GuiActionOutcome> firstTask = Execute(fixture, executor, input, TestContext.Current.CancellationToken);

        while (confirmation.CallCount == 0)
            await Task.Yield();

        GuiActionIntent secondIntent = fixture.CreateIntent();
        GuiActionOutcome second = await executor.ExecuteAsync(
            secondIntent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target,
            input,
            TestContext.Current.CancellationToken);

        waiter.SetResult(GuiServerOutcome.MovementConfirmed);
        GuiActionOutcome first = await firstTask;

        Assert.Equal(GuiActionOutcomeStatus.Blocked, second.Status);
        Assert.Equal(GuiSafetyReason.Busy, second.BlockedReason);
        Assert.Equal(GuiActionOutcomeStatus.Pass, first.Status);
        Assert.Equal(1, input.ClickCount);
    }

    [Fact]
    public void PassCannotBeConstructedWithoutAnEmittedClick()
    {
        Assert.Throws<ArgumentException>(() => new GuiActionOutcome(GuiActionOutcomeStatus.Pass, null, false, null));
    }

    private static GuiActionExecutor CreateExecutor(GuiActionTestFixture fixture, IGuiOutcomeConfirmation? confirmation)
    {
        if (confirmation is null)
            return new GuiActionExecutor(fixture.CreateGate(), fixture.Options, fixture.Time);

        return new GuiActionExecutor(fixture.CreateGate(), fixture.Options, fixture.Time, confirmation);
    }

    private static Task<GuiActionOutcome> Execute(
        GuiActionTestFixture fixture,
        GuiActionExecutor executor,
        IGuiInput input,
        CancellationToken cancellationToken)
    {
        return executor.ExecuteAsync(
            fixture.Intent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target,
            input,
            cancellationToken);
    }
}
