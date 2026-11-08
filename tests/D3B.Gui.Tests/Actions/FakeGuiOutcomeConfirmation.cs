using D3B.Gui.Actions;

namespace D3B.Gui.Tests.Actions;

internal sealed class FakeGuiOutcomeConfirmation : IGuiOutcomeConfirmation
{
    public GuiServerOutcome? Outcome { get; set; }
    public TaskCompletionSource<GuiServerOutcome?>? Waiter { get; set; }
    public CancellationTokenSource? CancelDuringWait { get; set; }
    public int CallCount { get; private set; }

    public Task<GuiServerOutcome?> WaitForConfirmationAsync(GuiActionIntent intent, CancellationToken cancellationToken)
    {
        CallCount++;

        if (CancelDuringWait is not null)
        {
            CancelDuringWait.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }

        if (Waiter is not null)
            return Waiter.Task;

        return Task.FromResult(Outcome);
    }
}
