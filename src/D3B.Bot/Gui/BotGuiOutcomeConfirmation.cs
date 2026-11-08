using D3B.Gui.Actions;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Bounds the actor outcome wait and confirms it only while the selected window remains safe and stable.</summary>
/// <remarks>
/// The intent carries window identity but no geometry snapshot. When a probe is configured, this adapter captures a
/// safe expected-window observation before waiting and requires an exact state and geometry match after confirmation.
/// Without a probe, a non-null actor outcome is withheld.
/// </remarks>
public sealed class BotGuiOutcomeConfirmation : IGuiOutcomeConfirmation
{
    private readonly Func<Guid, CancellationToken, Task<GuiServerOutcome?>> _waiter;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _confirmationTimeout;
    private readonly Func<GuiActionIntent, CancellationToken, Task<GameWindowSnapshot>>? _currentWindowProbe;

    /// <summary>Creates a bounded confirmation adapter with an optional current-window probe.</summary>
    public BotGuiOutcomeConfirmation(
        Func<Guid, CancellationToken, Task<GuiServerOutcome?>> waiter,
        TimeProvider timeProvider,
        TimeSpan confirmationTimeout,
        Func<GuiActionIntent, CancellationToken, Task<GameWindowSnapshot>>? currentWindowProbe = null)
    {
        ArgumentNullException.ThrowIfNull(waiter);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (confirmationTimeout <= TimeSpan.Zero || confirmationTimeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(confirmationTimeout), "The confirmation timeout must be positive and finite.");

        _waiter = waiter;
        _timeProvider = timeProvider;
        _confirmationTimeout = confirmationTimeout;
        _currentWindowProbe = currentWindowProbe;
    }

    /// <inheritdoc />
    public async Task<GuiServerOutcome?> WaitForConfirmationAsync(
        GuiActionIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        using CancellationTokenSource timeoutSource = new(_confirmationTimeout, _timeProvider);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        GameWindowSnapshot? expectedWindow = null;

        if (_currentWindowProbe is not null)
        {
            try
            {
                expectedWindow = await _currentWindowProbe(intent, linkedSource.Token)
                    .WaitAsync(linkedSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }

            if (linkedSource.IsCancellationRequested)
                return cancellationToken.IsCancellationRequested ? throw new OperationCanceledException(cancellationToken) : null;

            if (expectedWindow is null || !IsSafeForIntent(expectedWindow, intent))
                return null;
        }

        GuiServerOutcome? outcome;

        try
        {
            outcome = await _waiter(intent.IntentId, linkedSource.Token)
                .WaitAsync(linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        if (linkedSource.IsCancellationRequested)
            return cancellationToken.IsCancellationRequested ? throw new OperationCanceledException(cancellationToken) : null;

        if (outcome is null)
            return null;

        if (_currentWindowProbe is null || expectedWindow is null)
            return null;

        GameWindowSnapshot currentWindow;

        try
        {
            currentWindow = await _currentWindowProbe(intent, linkedSource.Token)
                .WaitAsync(linkedSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        if (linkedSource.IsCancellationRequested)
            return cancellationToken.IsCancellationRequested ? throw new OperationCanceledException(cancellationToken) : null;

        return IsSafeForIntent(currentWindow, intent)
            && SameWindowObservation(expectedWindow, currentWindow)
            ? outcome
            : null;
    }

    private static bool IsSafeForIntent(GameWindowSnapshot window, GuiActionIntent intent)
    {
        return SameIdentity(intent.WindowIdentity, window.Identity)
            && window.Identity.Status == GameWindowIdentityStatus.Verified
            && window.Exists
            && window.IsMapped
            && window.IsViewable
            && !window.IsMinimized
            && window.IsFocused
            && window.IsActive
            && window.GeometryReliable
            && window.WindowWidth > 0
            && window.WindowHeight > 0
            && window.ClientWidth > 0
            && window.ClientHeight > 0;
    }

    private static bool SameWindowObservation(GameWindowSnapshot expected, GameWindowSnapshot actual)
    {
        return SameIdentity(expected.Identity, actual.Identity)
            && expected.Exists == actual.Exists
            && expected.IsMapped == actual.IsMapped
            && expected.IsViewable == actual.IsViewable
            && expected.IsMinimized == actual.IsMinimized
            && expected.IsFocused == actual.IsFocused
            && expected.IsActive == actual.IsActive
            && expected.GeometryReliable == actual.GeometryReliable
            && expected.DesktopOrigin == actual.DesktopOrigin
            && expected.WindowWidth == actual.WindowWidth
            && expected.WindowHeight == actual.WindowHeight
            && expected.BorderWidth == actual.BorderWidth
            && expected.ClientWidth == actual.ClientWidth
            && expected.ClientHeight == actual.ClientHeight;
    }

    private static bool SameIdentity(GameWindowIdentity expected, GameWindowIdentity actual)
    {
        return expected.Xid == actual.Xid
            && expected.ProcessId == actual.ProcessId
            && string.Equals(expected.ExecutablePath, actual.ExecutablePath, StringComparison.Ordinal)
            && expected.Status == actual.Status;
    }
}
