using D3B.Gui.Calibration;
using D3B.Gui.Actions;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Safety;

/// <summary>Classifies action readiness without performing input or other I/O.</summary>
public sealed class GuiActionGate
{
    private readonly GuiOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the gate with immutable GUI policy and the clock used for expiry checks.</summary>
    public GuiActionGate(GuiOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns the first failing guard in the documented, deterministic order.</summary>
    /// <remarks>
    /// Order: configuration, X11 availability, window existence and identity, active window state, capture result and
    /// scene usability, capture-window consistency, calibration status/freshness/identity, target status/identity,
    /// connection, map, cell, position provenance/ambiguity, snapshot revision, expiry, fight, busy state, player
    /// interruption, and protocol contradiction. A ready result is only a snapshot-bound readiness finding; the
    /// executor separately enforces Preview and input policy.
    /// </remarks>
    public GuiReadiness Evaluate(
        GuiActionIntent intent,
        GuiWorldSnapshot world,
        GameWindowSnapshot window,
        CapturedFrame frame,
        CalibrationResult calibration,
        TargetRegion target)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(target);

        CalibrationProfile? profile = calibration.Profile;

        if (!_options.Enabled)
            return Blocked(GuiSafetyReason.Disabled, "GUI features are disabled.");

        if (_options.Backend != GuiBackend.X11
            || !window.IsSessionX11
            || !window.HasValidDisplay
            || !window.IsXTestAvailable)
        {
            return Blocked(GuiSafetyReason.NotX11, "A usable X11 display and XTest extension are required.");
        }

        if (!window.Exists || window.Identity.Status == GameWindowIdentityStatus.NotFound || window.Identity.Xid <= 0)
            return Blocked(GuiSafetyReason.WindowNotFound, "The selected window is absent.");

        if (window.Identity.Status != GameWindowIdentityStatus.Verified)
            return Blocked(GuiSafetyReason.WindowIdentityUnverified, "The selected window process identity is not verified.");

        if (!window.IsMapped || !window.IsViewable || window.IsMinimized || !window.IsFocused || !window.IsActive)
            return Blocked(GuiSafetyReason.WindowNotActive, "The selected window is not mapped and active.");

        if (frame.Status != CaptureStatus.Captured)
            return Blocked(GuiSafetyReason.CaptureFailed, "The window capture did not complete successfully.");

        if (!frame.ContentUsable
            || frame.PixelWidth <= 0
            || frame.PixelHeight <= 0
            || frame.PixelWidth > _options.MaxWidth
            || frame.PixelHeight > _options.MaxHeight
            || frame.Image.Empty())
        {
            return Blocked(GuiSafetyReason.SceneUnverified, "The captured image is empty or outside configured dimensions.");
        }

        if (!window.GeometryReliable
            || intent.WindowIdentity is null
            || !GuiReadiness.IdentityMatches(window.Identity, intent.WindowIdentity)
            || !GuiReadiness.IdentityMatches(window.Identity, frame.WindowIdentity)
            || !IsSameWindowObservation(window, frame.WindowSnapshotBefore)
            || !IsSameWindowObservation(window, frame.WindowSnapshotAfter))
        {
            return Blocked(GuiSafetyReason.WindowChanged, "The window changed around capture or differs from the intention.");
        }

        if (calibration.Status == CalibrationStatus.Stale)
            return Blocked(GuiSafetyReason.CalibrationStale, "The calibration is marked stale.");

        if (!calibration.IsCalibrated || profile is null)
        {
            return Blocked(GuiSafetyReason.NotCalibrated, "A calibrated profile is required.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        TimeSpan calibrationAge = now - profile.CreatedAt;

        if (calibration.Status == CalibrationStatus.Stale
            || calibrationAge < TimeSpan.Zero
            || calibrationAge > TimeSpan.FromSeconds(_options.CalibrationMaxAgeSeconds)
            || !GuiReadiness.IdentityMatches(profile.WindowIdentity, window.Identity)
            || profile.MapId != intent.MapId
            || !string.Equals(intent.CalibrationIdentity, profile.CalibrationId, StringComparison.Ordinal)
            || profile.CaptureWidth != frame.PixelWidth
            || profile.CaptureHeight != frame.PixelHeight
            || !ClientGeometryMatches(profile.ClientAreaGeometry, window))
        {
            return Blocked(GuiSafetyReason.CalibrationStale, "The calibration is expired or bound to another view.");
        }

        if (target.Status == TargetStatus.Unverified)
            return Blocked(GuiSafetyReason.TargetUnverified, "The visual target did not pass verification.");

        if (target.Status == TargetStatus.Ambiguous)
            return Blocked(GuiSafetyReason.AmbiguousTarget, "The visual target has competing candidates.");

        if (target.Status != TargetStatus.Verified
            || target.CaptureId != frame.CaptureId
            || !string.Equals(target.CalibrationId, profile.CalibrationId, StringComparison.Ordinal)
            || target.MapId != intent.MapId
            || target.ProposedClickPoint is null
            || !IsClickPointInsideSafeClientArea(target, profile.ClientAreaGeometry, frame)
            || !IsTargetElementCompatible(intent, world, target))
        {
            return Blocked(GuiSafetyReason.TargetUnverified, "The target does not match the current capture, map, cell, or action.");
        }

        if (!world.IsConnected)
            return Blocked(GuiSafetyReason.Disconnected, "The game session is disconnected.");

        if (world.MapId is null
            || world.MapId != intent.MapId
            || world.ProtocolObservationStatus == ProtocolObservationStatus.Unknown)
            return Blocked(GuiSafetyReason.MapUnknown, "The observed map is unknown or differs from the intention.");

        if (world.CellId is null
            || target.Cell != intent.TargetCellId
            || world.ProtocolObservationStatus == ProtocolObservationStatus.Partial)
            return Blocked(GuiSafetyReason.CellUnknown, "The observed cell is unknown or the target cell differs from the intention.");

        if (world.CellProvenance is not (GuiCellProvenance.ServerPlaced or GuiCellProvenance.ServerConfirmed))
            return Blocked(GuiSafetyReason.PositionUntrusted, "The current position lacks accepted server provenance.");

        if (world.HasAmbiguousMovement)
            return Blocked(GuiSafetyReason.PositionAmbiguous, "The current movement evidence is ambiguous.");

        if (world.SessionEpoch != intent.SessionEpoch || world.StateRevision != intent.StateRevision)
            return Blocked(GuiSafetyReason.StateChanged, "The current session or state revision differs from the intention.");

        if (now < intent.CreatedAt || now >= intent.ExpiresAt)
            return Blocked(GuiSafetyReason.IntentExpired, "The action intention is outside its validity interval.");

        if (world.IsFighting)
            return Blocked(GuiSafetyReason.InFight, "GUI actions are not available during a fight.");

        if (world.IsWalking || world.IsUsing || world.IsInDialog)
            return Blocked(GuiSafetyReason.Busy, "The character is already performing another action.");

        if (world.IsUserInterrupted)
            return Blocked(GuiSafetyReason.UserInterrupted, "The player has taken control.");

        if (world.ProtocolObservationStatus is ProtocolObservationStatus.TranslationBlocked or ProtocolObservationStatus.Contradictory)
            return Blocked(GuiSafetyReason.ProtocolContradiction, "Protocol observations are blocked or contradictory.");

        return CreateReadiness(true, GuiSafetyReason.Ready, null);

        GuiReadiness Blocked(GuiSafetyReason reason, string detail)
        {
            return CreateReadiness(false, reason, detail);
        }

        GuiReadiness CreateReadiness(bool allowed, GuiSafetyReason reason, string? detail)
        {
            string calibrationIdentity = profile?.CalibrationId ?? string.Empty;
            string targetIdentity = GuiReadiness.CreateTargetIdentity(target);
            GameWindowIdentity identity = window.Identity;
            Guid authorizationId = allowed ? Guid.NewGuid() : Guid.Empty;

            return new GuiReadiness(
                allowed,
                reason,
                world.SessionEpoch,
                world.StateRevision,
                identity,
                calibrationIdentity,
                targetIdentity,
                authorizationId,
                detail,
                intent.IntentId);
        }
    }

    private static bool IsTargetElementCompatible(GuiActionIntent intent, GuiWorldSnapshot world, TargetRegion target)
    {
        if (intent.ActionKind == GuiActionKind.WalkToCell)
        {
            return intent.TargetElementId is null
                && intent.ExpectedClientAction == GuiClientAction.Walk
                && intent.ExpectedServerOutcome == GuiServerOutcome.MovementConfirmed;
        }

        if (intent.ActionKind != GuiActionKind.UseElement
            || intent.TargetElementId != target.ElementId
            || intent.ExpectedClientAction != GuiClientAction.UseElement
            || intent.ExpectedServerOutcome != GuiServerOutcome.ElementUseEnded)
            return false;

        return !world.KnownElements.IsDefaultOrEmpty
            && world.KnownElements.Any(element => element.ElementId == target.ElementId && element.Cell == target.Cell);
    }

    private static bool IsSameWindowObservation(GameWindowSnapshot expected, GameWindowSnapshot actual)
    {
        return GuiReadiness.IdentityMatches(expected.Identity, actual.Identity)
            && actual.Exists
            && actual.IsMapped == expected.IsMapped
            && actual.IsViewable == expected.IsViewable
            && actual.IsMinimized == expected.IsMinimized
            && actual.IsFocused == expected.IsFocused
            && actual.IsActive == expected.IsActive
            && actual.IsXTestAvailable == expected.IsXTestAvailable
            && actual.IsSessionX11 == expected.IsSessionX11
            && actual.HasValidDisplay == expected.HasValidDisplay
            && actual.GeometryReliable == expected.GeometryReliable
            && actual.DesktopOrigin == expected.DesktopOrigin
            && actual.WindowWidth == expected.WindowWidth
            && actual.WindowHeight == expected.WindowHeight
            && actual.BorderWidth == expected.BorderWidth
            && actual.ClientWidth == expected.ClientWidth
            && actual.ClientHeight == expected.ClientHeight;
    }

    private static bool ClientGeometryMatches(ClientAreaGeometry geometry, GameWindowSnapshot window)
    {
        long expectedDesktopX = (long)window.DesktopOrigin.X + window.BorderWidth;
        long expectedDesktopY = (long)window.DesktopOrigin.Y + window.BorderWidth;

        return geometry.Width == window.ClientWidth
            && geometry.Height == window.ClientHeight
            && geometry.OriginInWindow.X == window.BorderWidth
            && geometry.OriginInWindow.Y == window.BorderWidth
            && geometry.OriginDesktop.X == expectedDesktopX
            && geometry.OriginDesktop.Y == expectedDesktopY;
    }

    private static bool IsClickPointInsideSafeClientArea(TargetRegion target, ClientAreaGeometry geometry, CapturedFrame frame)
    {
        if (target.ProposedClickPoint is not { } point)
            return false;

        long left = (long)geometry.OriginInWindow.X + 2;
        long top = (long)geometry.OriginInWindow.Y + 2;
        long right = (long)geometry.OriginInWindow.X + geometry.Width - 2;
        long bottom = (long)geometry.OriginInWindow.Y + geometry.Height - 2;

        return point.X >= left
            && point.X < right
            && point.Y >= top
            && point.Y < bottom
            && point.X < frame.PixelWidth
            && point.Y < frame.PixelHeight;
    }
}
