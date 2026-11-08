using D3B.Bot.Gui;
using D3B.Gui.Actions;
using D3B.Gui.Windowing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Gui;

public sealed class BotGuiOutcomeConfirmationTests
{
    [Fact]
    public async Task ReturnsAConfirmedOutcomeWhenTheWindowRemainsUnchanged()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        int probeCalls = 0;
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) => Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed),
            (_, _) =>
            {
                probeCalls++;
                return Task.FromResult(fixture.Window);
            });

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Equal(GuiServerOutcome.MovementConfirmed, outcome);
        Assert.Equal(2, probeCalls);
    }

    [Fact]
    public async Task ReturnsNullWhenTheActorWaiterReturnsNull()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        int probeCalls = 0;
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) => Task.FromResult<GuiServerOutcome?>(null),
            (_, _) =>
            {
                probeCalls++;
                return Task.FromResult(fixture.Window);
            });

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
        Assert.Equal(1, probeCalls);
    }

    [Fact]
    public async Task ReturnsNullWhenTheWaitTimesOut()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        FakeTimeProvider time = new(fixture.Now);
        TaskCompletionSource<GuiServerOutcome?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int waiterCalls = 0;
        BotGuiOutcomeConfirmation confirmation = new(
            (_, _) =>
            {
                waiterCalls++;
                return pending.Task;
            },
            time,
            TimeSpan.FromSeconds(5),
            (_, _) => Task.FromResult(fixture.Window));

        Task<GuiServerOutcome?> waiting = confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);
        await Task.Yield();
        time.Advance(TimeSpan.FromSeconds(5));

        GuiServerOutcome? outcome = await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Null(outcome);
        Assert.Equal(1, waiterCalls);
    }

    [Fact]
    public async Task PropagatesCallerCancellation()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        TaskCompletionSource<GuiServerOutcome?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BotGuiOutcomeConfirmation confirmation = new(
            (_, cancellationToken) =>
            {
                cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                return pending.Task;
            },
            TimeProvider.System,
            TimeSpan.FromMinutes(1),
            (_, _) => Task.FromResult(fixture.Window));
        using CancellationTokenSource cancellation = new();

        Task<GuiServerOutcome?> waiting = confirmation.WaitForConfirmationAsync(fixture.Intent, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task ReturnsNullWhenTheWindowIdentityChanges()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        Queue<GameWindowSnapshot> snapshots = new();
        snapshots.Enqueue(fixture.Window);
        snapshots.Enqueue(fixture.Window with
        {
            Identity = fixture.Window.Identity with { Xid = fixture.Window.Identity.Xid + 1 },
        });
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) => Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed),
            (_, _) => Task.FromResult(snapshots.Dequeue()));

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task ReturnsNullWhenFocusIsLost()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        Queue<GameWindowSnapshot> snapshots = new();
        snapshots.Enqueue(fixture.Window);
        snapshots.Enqueue(fixture.Window with { IsFocused = false });
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) => Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed),
            (_, _) => Task.FromResult(snapshots.Dequeue()));

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task ReturnsNullWhenWindowGeometryChanges()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        Queue<GameWindowSnapshot> snapshots = new();
        snapshots.Enqueue(fixture.Window);
        snapshots.Enqueue(fixture.Window with { ClientWidth = fixture.Window.ClientWidth - 1 });
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) => Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed),
            (_, _) => Task.FromResult(snapshots.Dequeue()));

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task WithholdsOutcomesWhenNoWindowProbeIsConfigured()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        int waiterCalls = 0;
        BotGuiOutcomeConfirmation confirmation = new(
            (_, _) =>
            {
                waiterCalls++;
                return Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed);
            },
            TimeProvider.System,
            TimeSpan.FromSeconds(5));

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
        Assert.Equal(1, waiterCalls);
    }

    [Fact]
    public async Task DoesNotRetryTheWaitAfterWindowStateChanges()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        int waiterCalls = 0;
        Queue<GameWindowSnapshot> snapshots = new();
        snapshots.Enqueue(fixture.Window);
        snapshots.Enqueue(fixture.Window with { IsActive = false });
        BotGuiOutcomeConfirmation confirmation = CreateConfirmation(
            fixture,
            (_, _) =>
            {
                waiterCalls++;
                return Task.FromResult<GuiServerOutcome?>(GuiServerOutcome.MovementConfirmed);
            },
            (_, _) => Task.FromResult(snapshots.Dequeue()));

        GuiServerOutcome? outcome = await confirmation.WaitForConfirmationAsync(fixture.Intent, CancellationToken.None);

        Assert.Null(outcome);
        Assert.Equal(1, waiterCalls);
    }

    private static BotGuiOutcomeConfirmation CreateConfirmation(
        GuiProtocolTrustTestFixture fixture,
        Func<Guid, CancellationToken, Task<GuiServerOutcome?>> waiter,
        Func<GuiActionIntent, CancellationToken, Task<GameWindowSnapshot>> probe)
    {
        return new BotGuiOutcomeConfirmation(waiter, TimeProvider.System, TimeSpan.FromSeconds(5), probe);
    }
}
