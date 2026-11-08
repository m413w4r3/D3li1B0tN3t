namespace D3B.Gui.Actions;

/// <summary>Provides the P2N default that never treats an input click as confirmation.</summary>
public sealed class NeverConfirms : IGuiOutcomeConfirmation
{
    /// <inheritdoc />
    public Task<GuiServerOutcome?> WaitForConfirmationAsync(GuiActionIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<GuiServerOutcome?>(null);
    }
}
