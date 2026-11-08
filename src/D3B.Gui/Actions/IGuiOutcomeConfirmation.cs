namespace D3B.Gui.Actions;

/// <summary>Waits for the compatible client request and matching server outcome for one intention.</summary>
/// <remarks>
/// Implementations must verify both parts of the intention contract before returning an outcome. A visual animation
/// alone is insufficient. A missing or ambiguous confirmation returns null; the executor never retries the click.
/// </remarks>
public interface IGuiOutcomeConfirmation
{
    /// <summary>Waits for the server outcome after matching the expected client request.</summary>
    Task<GuiServerOutcome?> WaitForConfirmationAsync(GuiActionIntent intent, CancellationToken cancellationToken);
}
