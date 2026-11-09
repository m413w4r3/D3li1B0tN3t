using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Requires BotActor acknowledgement and revocation permission before one delegated GUI click.</summary>
public sealed class AcknowledgedGuiInput : IGuiInput
{
    private readonly Guid _intentId;
    private readonly GuiWorldSnapshot _world;
    private readonly GameWindowSnapshot _window;
    private readonly Func<Guid, GuiWorldSnapshot, GameWindowSnapshot, CancellationToken, Task<bool>> _clickGate;
    private readonly CancellationToken _actorRevocationToken;
    private readonly IGuiInput _inner;
    private int _clickAttempted;

    /// <inheritdoc />
    public bool IsEnabled => _inner.IsEnabled;

    /// <inheritdoc />
    public bool EmitsRealInput => _inner.EmitsRealInput;

    /// <summary>Creates an input decorator bound to one intent and the snapshots used to register it.</summary>
    public AcknowledgedGuiInput(
        Guid intentId,
        GuiWorldSnapshot world,
        GameWindowSnapshot window,
        Func<Guid, GuiWorldSnapshot, GameWindowSnapshot, CancellationToken, Task<bool>> clickGate,
        IGuiInput inner,
        CancellationToken actorRevocationToken)
    {
        if (intentId == Guid.Empty)
            throw new ArgumentException("A click gate requires a non-empty intention identifier.", nameof(intentId));

        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(clickGate);
        ArgumentNullException.ThrowIfNull(inner);

        _intentId = intentId;
        _world = world;
        _window = window;
        _clickGate = clickGate;
        _actorRevocationToken = actorRevocationToken;
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<GuiInputResult> ClickAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedWindow);

        if (Interlocked.Exchange(ref _clickAttempted, 1) != 0)
            return new GuiInputResult(GuiInputStatus.Failed, "This intention already attempted its one permitted click.");

        if (!SameWindowObservation(_window, expectedWindow))
            return new GuiInputResult(GuiInputStatus.WindowChanged, "The click request does not match the registered window snapshot.");

        if (cancellationToken.IsCancellationRequested || _actorRevocationToken.IsCancellationRequested)
            return new GuiInputResult(GuiInputStatus.Canceled, "The click was canceled before actor acknowledgement.");

        CancellationTokenSource linkedSource;

        try
        {
            linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _actorRevocationToken);
        }
        catch (ObjectDisposedException)
        {
            return new GuiInputResult(GuiInputStatus.Canceled, "The actor revoked the intention before input.");
        }

        using (linkedSource)
        {
            CancellationToken clickCancellationToken = linkedSource.Token;
            bool acknowledged;

            try
            {
                acknowledged = await _clickGate(_intentId, _world, _window, clickCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (clickCancellationToken.IsCancellationRequested)
            {
                return new GuiInputResult(GuiInputStatus.Canceled, "The click was canceled before actor acknowledgement.");
            }
            catch (Exception)
            {
                return new GuiInputResult(GuiInputStatus.Failed, "The actor click acknowledgement failed.");
            }

            if (!acknowledged)
                return new GuiInputResult(GuiInputStatus.WindowChanged, "The actor rejected the click acknowledgement.");

            if (_actorRevocationToken.IsCancellationRequested)
                return new GuiInputResult(GuiInputStatus.Canceled, "The actor revoked the intention before input delegation.");

            if (clickCancellationToken.IsCancellationRequested)
                return new GuiInputResult(GuiInputStatus.Canceled, "The click was canceled after actor acknowledgement.");

            return await _inner.ClickAsync(point, _window, clickCancellationToken).ConfigureAwait(false);
        }
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
