using D3B.Gui.Actions;
using D3B.Gui.Safety;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Models one active GUI intention and the client/server observations that may confirm it.</summary>
/// <remarks>
/// This is an isolated model, not wired into BotActor or PlayerActions. A compatible packet is not proof of who
/// clicked: if simultaneous human input is observed, correlation stops and player control takes priority.
/// </remarks>
public sealed class GuiIntentCorrelation
{
    private readonly object _sync = new();
    private readonly HashSet<Guid> _seenIntentIds = [];
    private readonly TimeProvider _timeProvider;
    private GuiActionIntent? _activeIntent;
    private GuiActionIntent? _lastIntent;
    private GuiWorldSnapshot? _registeredWorld;
    private GameWindowSnapshot? _registeredWindow;
    private GuiIntentCorrelationState _state = GuiIntentCorrelationState.Idle;
    private string? _lastDetail;
    private int _clickCount;
    private bool _playerHasPriority;

    /// <summary>Creates the correlation model with an injectable clock for expiry checks.</summary>
    public GuiIntentCorrelation(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the current or most recent state.</summary>
    public GuiIntentCorrelationState State
    {
        get
        {
            lock (_sync)
                return _state;
        }
    }

    /// <summary>Gets the currently active intention, or the most recent one after a terminal state.</summary>
    public GuiActionIntent? Intent
    {
        get
        {
            lock (_sync)
                return _activeIntent ?? _lastIntent;
        }
    }

    /// <summary>Gets whether player control was given priority after an interruption.</summary>
    public bool PlayerHasPriority
    {
        get
        {
            lock (_sync)
                return _playerHasPriority;
        }
    }

    /// <summary>Gets the number of clicks accepted by this model for the active or most recent intention.</summary>
    public int ClickCount
    {
        get
        {
            lock (_sync)
                return _clickCount;
        }
    }

    /// <summary>Gets the latest terminal explanation, when present.</summary>
    public string? LastDetail
    {
        get
        {
            lock (_sync)
                return _lastDetail;
        }
    }

    /// <summary>Registers an intention before input and refuses duplicate, concurrent, or expired submissions.</summary>
    public GuiIntentRegistration Register(GuiActionIntent intent, GuiWorldSnapshot world, GameWindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);

        lock (_sync)
        {
            if (!_seenIntentIds.Add(intent.IntentId))
                return new GuiIntentRegistration(GuiIntentRegistrationStatus.Duplicate, intent.IntentId, "The intention identifier was already submitted.");

            if (_activeIntent is not null)
                return new GuiIntentRegistration(GuiIntentRegistrationStatus.Busy, intent.IntentId, "Another intention is still active.");

            _lastIntent = intent;
            _lastDetail = null;
            _playerHasPriority = false;
            _clickCount = 0;

            DateTimeOffset now = _timeProvider.GetUtcNow();

            if (now < intent.CreatedAt || now >= intent.ExpiresAt)
            {
                _state = GuiIntentCorrelationState.Cancelled;
                _lastDetail = "The intention was expired or not yet valid when registered.";
                return new GuiIntentRegistration(GuiIntentRegistrationStatus.Expired, intent.IntentId, _lastDetail);
            }

            _activeIntent = intent;
            _registeredWorld = world;
            _registeredWindow = window;
            _state = GuiIntentCorrelationState.Registered;

            return new GuiIntentRegistration(GuiIntentRegistrationStatus.Registered, intent.IntentId, null);
        }
    }

    /// <summary>Acknowledges registration only when the session, window, and decision snapshot still match.</summary>
    public bool Acknowledge(GuiWorldSnapshot world, GameWindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);

        lock (_sync)
        {
            if (_state != GuiIntentCorrelationState.Registered || _activeIntent is null)
                return false;

            if (ExpireIfNeeded())
                return false;

            if (!MatchesRegisteredSnapshot(world, window))
            {
                Interrupt("The session, window, or snapshot changed before acknowledgement.");
                return false;
            }

            _state = GuiIntentCorrelationState.Acknowledged;
            return true;
        }
    }

    /// <summary>Records at most one click after a second session/window/snapshot and expiry check.</summary>
    public bool MarkClicked(GuiWorldSnapshot world, GameWindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);

        lock (_sync)
        {
            if (_state != GuiIntentCorrelationState.Acknowledged || _activeIntent is null || _clickCount != 0)
                return false;

            if (ExpireIfNeeded())
                return false;

            if (!MatchesRegisteredSnapshot(world, window))
            {
                Interrupt("The session, window, or snapshot changed before input.");
                return false;
            }

            _clickCount = 1;
            _state = GuiIntentCorrelationState.Clicked;
            return true;
        }
    }

    /// <summary>Matches one client request or interrupts and gives the player priority.</summary>
    public bool ObserveClientRequest(GuiClientRequestObservation request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_sync)
        {
            if (_activeIntent is null)
                return false;

            if (_state != GuiIntentCorrelationState.Clicked)
            {
                if (!IsTerminal(_state))
                    Interrupt("A client request arrived outside the one-click correlation window.");

                return false;
            }

            if (ExpireIfNeeded())
                return false;

            if (request.PotentiallyConcurrentHumanInput)
            {
                Interrupt("A simultaneous human action is indistinguishable from this client request.");
                return false;
            }

            if (!MatchesClientRequest(_activeIntent, request))
            {
                Interrupt("An unexpected client request occurred; player control takes priority.");
                return false;
            }

            _state = GuiIntentCorrelationState.ClientRequestSeen;
            return true;
        }
    }

    /// <summary>Records a matching server outcome after the compatible client request.</summary>
    public bool ObserveServerOutcome(GuiServerOutcomeObservation outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        lock (_sync)
        {
            if (_activeIntent is null)
                return false;

            if (_state != GuiIntentCorrelationState.ClientRequestSeen)
            {
                if (_state is GuiIntentCorrelationState.Clicked or GuiIntentCorrelationState.Acknowledged)
                    SetTerminal(GuiIntentCorrelationState.Unconfirmed, "A server outcome arrived before the compatible client request.");

                return false;
            }

            if (ExpireIfNeeded())
                return false;

            if (!MatchesServerOutcome(_activeIntent, outcome))
            {
                SetTerminal(GuiIntentCorrelationState.Unconfirmed, "The server outcome was absent from or contradicted the action contract.");
                return false;
            }

            _state = GuiIntentCorrelationState.ServerConfirmed;
            return true;
        }
    }

    /// <summary>Consumes the intention permanently after its matching server outcome was observed.</summary>
    public bool Consume()
    {
        lock (_sync)
        {
            if (_state != GuiIntentCorrelationState.ServerConfirmed || _activeIntent is null)
                return false;

            SetTerminal(GuiIntentCorrelationState.Consumed, null);
            return true;
        }
    }

    /// <summary>Ends an action without retry when a confirmation wait completes without evidence.</summary>
    public bool MarkUnconfirmed(string? detail = null)
    {
        lock (_sync)
        {
            if (_activeIntent is null || _state is not (GuiIntentCorrelationState.Clicked or GuiIntentCorrelationState.ClientRequestSeen))
                return false;

            SetTerminal(GuiIntentCorrelationState.Unconfirmed, detail ?? "No matching server confirmation arrived.");
            return true;
        }
    }

    /// <summary>Cancels before input or leaves a post-click action terminal and unconfirmed.</summary>
    public bool Cancel(string? detail = null)
    {
        lock (_sync)
        {
            if (_activeIntent is null)
                return false;

            if (_state is GuiIntentCorrelationState.Registered or GuiIntentCorrelationState.Acknowledged)
            {
                SetTerminal(GuiIntentCorrelationState.Cancelled, detail ?? "The intention was canceled before input.");
                return true;
            }

            if (_state is GuiIntentCorrelationState.Clicked or GuiIntentCorrelationState.ClientRequestSeen)
            {
                SetTerminal(GuiIntentCorrelationState.Unconfirmed, detail ?? "The intention was canceled after input and cannot be retried.");
                return true;
            }

            return false;
        }
    }

    private bool MatchesRegisteredSnapshot(GuiWorldSnapshot world, GameWindowSnapshot window)
    {
        GuiActionIntent intent = _activeIntent!;
        GuiWorldSnapshot registeredWorld = _registeredWorld!;
        GameWindowSnapshot registeredWindow = _registeredWindow!;

        return intent.SessionEpoch == world.SessionEpoch
            && intent.StateRevision == world.StateRevision
            && intent.MapId == world.MapId
            && SnapshotMatches(registeredWorld, world)
            && IdentityMatches(intent.WindowIdentity, window.Identity)
            && SameWindowState(registeredWindow, window);
    }

    private bool ExpireIfNeeded()
    {
        GuiActionIntent intent = _activeIntent!;
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (now >= intent.ExpiresAt || now < intent.CreatedAt)
        {
            if (_state is GuiIntentCorrelationState.Registered or GuiIntentCorrelationState.Acknowledged)
                SetTerminal(GuiIntentCorrelationState.Cancelled, "The intention expired before input.");
            else
                SetTerminal(GuiIntentCorrelationState.Unconfirmed, "The intention expired after input and cannot be retried.");

            return true;
        }

        return false;
    }

    private void Interrupt(string detail)
    {
        _playerHasPriority = true;
        SetTerminal(GuiIntentCorrelationState.Interrupted, detail);
    }

    private void SetTerminal(GuiIntentCorrelationState state, string? detail)
    {
        _state = state;
        _lastDetail = detail;
        _activeIntent = null;
        _registeredWorld = null;
        _registeredWindow = null;
    }

    private static bool MatchesClientRequest(GuiActionIntent intent, GuiClientRequestObservation request)
    {
        if (request.SessionEpoch != intent.SessionEpoch || request.MapId != intent.MapId || request.Action != intent.ExpectedClientAction)
            return false;

        if (intent.ActionKind == GuiActionKind.WalkToCell)
        {
            return request.TargetCellId == intent.TargetCellId && request.TargetElementId is null;
        }

        return intent.ActionKind == GuiActionKind.UseElement
            && request.TargetElementId == intent.TargetElementId
            && (request.TargetCellId is null || request.TargetCellId == intent.TargetCellId);
    }

    private static bool MatchesServerOutcome(GuiActionIntent intent, GuiServerOutcomeObservation outcome)
    {
        if (outcome.SessionEpoch != intent.SessionEpoch
            || outcome.MapId != intent.MapId
            || outcome.Outcome != intent.ExpectedServerOutcome)
        {
            return false;
        }

        if (intent.ActionKind == GuiActionKind.WalkToCell)
            return outcome.TargetCellId == intent.TargetCellId && outcome.TargetElementId is null;

        return intent.ActionKind == GuiActionKind.UseElement
            && outcome.TargetElementId == intent.TargetElementId
            && (outcome.TargetCellId is null || outcome.TargetCellId == intent.TargetCellId);
    }

    private static bool SnapshotMatches(GuiWorldSnapshot expected, GuiWorldSnapshot actual)
    {
        return expected.SessionEpoch == actual.SessionEpoch
            && expected.StateRevision == actual.StateRevision
            && expected.MapId == actual.MapId
            && expected.CellId == actual.CellId
            && expected.CellProvenance == actual.CellProvenance
            && expected.IsConnected == actual.IsConnected
            && expected.IsFighting == actual.IsFighting
            && expected.IsWalking == actual.IsWalking
            && expected.IsUsing == actual.IsUsing
            && expected.IsInDialog == actual.IsInDialog
            && expected.HasAmbiguousMovement == actual.HasAmbiguousMovement
            && expected.ProtocolObservationStatus == actual.ProtocolObservationStatus
            && expected.IsUserInterrupted == actual.IsUserInterrupted
            && KnownElementsMatch(expected, actual);
    }

    private static bool SameWindowState(GameWindowSnapshot expected, GameWindowSnapshot actual)
    {
        return IdentityMatches(expected.Identity, actual.Identity)
            && expected.Exists == actual.Exists
            && expected.IsMapped == actual.IsMapped
            && expected.IsViewable == actual.IsViewable
            && expected.IsMinimized == actual.IsMinimized
            && expected.IsFocused == actual.IsFocused
            && expected.IsActive == actual.IsActive
            && expected.IsXTestAvailable == actual.IsXTestAvailable
            && expected.IsSessionX11 == actual.IsSessionX11
            && expected.HasValidDisplay == actual.HasValidDisplay
            && expected.GeometryReliable == actual.GeometryReliable
            && expected.DesktopOrigin == actual.DesktopOrigin
            && expected.WindowWidth == actual.WindowWidth
            && expected.WindowHeight == actual.WindowHeight
            && expected.BorderWidth == actual.BorderWidth
            && expected.ClientWidth == actual.ClientWidth
            && expected.ClientHeight == actual.ClientHeight;
    }

    private static bool KnownElementsMatch(GuiWorldSnapshot expected, GuiWorldSnapshot actual)
    {
        if (expected.KnownElements.IsDefaultOrEmpty || actual.KnownElements.IsDefaultOrEmpty)
            return expected.KnownElements.IsDefaultOrEmpty && actual.KnownElements.IsDefaultOrEmpty;

        return expected.KnownElements.SequenceEqual(actual.KnownElements);
    }

    private static bool IdentityMatches(GameWindowIdentity expected, GameWindowIdentity actual)
    {
        return expected.Xid == actual.Xid
            && expected.ProcessId == actual.ProcessId
            && string.Equals(expected.ExecutablePath, actual.ExecutablePath, StringComparison.Ordinal)
            && expected.Status == actual.Status;
    }

    private static bool IsTerminal(GuiIntentCorrelationState state)
    {
        return state is GuiIntentCorrelationState.Consumed
            or GuiIntentCorrelationState.Cancelled
            or GuiIntentCorrelationState.Interrupted
            or GuiIntentCorrelationState.Unconfirmed;
    }
}
