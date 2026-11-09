using System.Collections.Immutable;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Applies protocol, scene, window, calibration, and input evidence to one GUI action.</summary>
/// <remarks>
/// An allowed decision is one required proof, not standalone authorization. GuiWalkToCellCoordinator separately checks
/// the plan path and binds its endpoint to the verified visual target before it registers or delegates input.
/// </remarks>
public sealed class GuiProtocolTrustPolicy
{
    private readonly GuiActionGate _gate;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the policy with the existing GUI gate and authoritative clock.</summary>
    public GuiProtocolTrustPolicy(GuiActionGate gate, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _gate = gate;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns exactly the first failed proof in the fixed policy order.</summary>
    public GuiProtocolTrustDecision Evaluate(GuiProtocolTrustEvidence? evidence)
    {
        if (evidence is null)
            return Blocked(GuiSafetyReason.ProtocolContradiction, "Protocol trust evidence is required.");

        if (evidence.Intent is null
            || evidence.Start is null
            || evidence.End is null
            || evidence.WindowStart is null
            || evidence.WindowEnd is null
            || evidence.Frame is null
            || evidence.Calibration is null
            || evidence.Target is null)
        {
            return Blocked(GuiSafetyReason.ProtocolContradiction, "The evidence bundle is missing a required input.");
        }

        GuiActionIntent intent = evidence.Intent;

        if (intent.ActionKind != GuiActionKind.WalkToCell)
            return Blocked(GuiSafetyReason.Disabled, "The protocol-trust policy accepts WalkToCell only.");

        if (intent.ExpectedClientAction != GuiClientAction.Walk
            || intent.ExpectedServerOutcome != GuiServerOutcome.MovementConfirmed)
        {
            return Blocked(GuiSafetyReason.ProtocolContradiction, "WalkToCell requires a Walk request and MovementConfirmed outcome.");
        }

        GuiReadiness readiness = _gate.Evaluate(
            intent,
            evidence.End,
            evidence.WindowEnd,
            evidence.Frame,
            evidence.Calibration,
            evidence.Target);

        if (!readiness.Allowed || readiness.ReasonCode != GuiSafetyReason.Ready)
        {
            return Blocked(readiness.ReasonCode, readiness.Detail ?? "The existing GUI action gate did not return Ready.");
        }

        if (!TryDescribeSnapshotDifference(evidence.Start, evidence.End, out string snapshotDifference))
        {
            GuiSafetyReason reason = evidence.Start.ProtocolObservationStatus != evidence.End.ProtocolObservationStatus
                ? GuiSafetyReason.ProtocolContradiction
                : GuiSafetyReason.StateChanged;
            return Blocked(reason, $"Start and end protocol snapshots differ in {snapshotDifference}.");
        }

        GuiProtocolTrustDecision? stateDecision = ValidateCompleteProtocolState(evidence.End);
        if (stateDecision is not null)
            return stateDecision;

        if (evidence.TranslationLatchClosedDuringWindow)
            return Blocked(GuiSafetyReason.ProtocolContradiction, "The translation latch closed during the evidence window.");

        if (evidence.UnknownMessageObservedDuringWindow)
            return Blocked(GuiSafetyReason.ProtocolContradiction, "An undecoded message was observed during the evidence window.");

        if (evidence.ClientGameplayObservedDuringWindow)
            return Blocked(GuiSafetyReason.UserInterrupted, "Client gameplay was observed during the evidence window.");

        if (evidence.HumanInputAmbiguityObserved)
            return Blocked(GuiSafetyReason.UserInterrupted, "Concurrent human input cannot be attributed safely.");

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (evidence.Now > now)
            return Blocked(GuiSafetyReason.StateChanged, "The evidence evaluation time is in the future.");

        if (evidence.MaxSnapshotAge < TimeSpan.Zero)
            return Blocked(GuiSafetyReason.StateChanged, "The maximum snapshot age cannot be negative.");

        if (evidence.End.CapturedAt > evidence.Now)
            return Blocked(GuiSafetyReason.StateChanged, "The end snapshot was captured after its evidence time.");

        if (evidence.Start.CapturedAt > evidence.End.CapturedAt)
            return Blocked(GuiSafetyReason.StateChanged, "The start snapshot is newer than the end snapshot.");

        TimeSpan snapshotAge = now - evidence.End.CapturedAt;
        if (snapshotAge < TimeSpan.Zero || snapshotAge > evidence.MaxSnapshotAge)
            return Blocked(GuiSafetyReason.StateChanged, "The end protocol snapshot is stale or from the future.");

        GuiProtocolTrustDecision? windowDecision = ValidateWindowEvidence(evidence.WindowStart, evidence.WindowEnd);
        if (windowDecision is not null)
            return windowDecision;

        GuiProtocolTrustDecision? calibrationDecision = ValidateCalibrationEvidence(evidence);
        if (calibrationDecision is not null)
            return calibrationDecision;

        GuiProtocolTrustDecision? targetDecision = ValidateTargetEvidence(evidence);
        if (targetDecision is not null)
            return targetDecision;

        if (evidence.PlanStartCell is null || evidence.PlanStartCell != evidence.End.CellId?.Value)
            return Blocked(GuiSafetyReason.PositionUntrusted, "The planned start cell does not match the server-proven current cell.");

        if (evidence.PlanTargetCell is null || evidence.PlanTargetCell != intent.TargetCellId.Value)
            return Blocked(GuiSafetyReason.CellUnknown, "The planned destination does not match the action target cell.");

        if (evidence.PlanReachable == false)
            return Blocked(GuiSafetyReason.PositionUntrusted, "The supplied movement plan is not reachable.");

        return new GuiProtocolTrustDecision(true, GuiSafetyReason.Ready, null);
    }

    private static GuiProtocolTrustDecision? ValidateCompleteProtocolState(GuiWorldSnapshot world)
    {
        if (world.ProtocolObservationStatus != ProtocolObservationStatus.Complete)
        {
            GuiSafetyReason reason = world.ProtocolObservationStatus switch
            {
                ProtocolObservationStatus.Unknown => GuiSafetyReason.MapUnknown,
                ProtocolObservationStatus.Partial => GuiSafetyReason.CellUnknown,
                _ => GuiSafetyReason.ProtocolContradiction,
            };
            return Blocked(reason, "Complete protocol observation is required for a GUI movement action.");
        }

        if (world.CellProvenance is not (GuiCellProvenance.ServerPlaced or GuiCellProvenance.ServerConfirmed))
            return Blocked(GuiSafetyReason.PositionUntrusted, "The current cell lacks server placement or confirmation provenance.");

        if (world.MapId is null)
            return Blocked(GuiSafetyReason.MapUnknown, "The current server-proven map is unknown.");

        if (world.CellId is null)
            return Blocked(GuiSafetyReason.CellUnknown, "The current server-proven cell is unknown.");

        if (!world.IsConnected)
            return Blocked(GuiSafetyReason.Disconnected, "The game session is disconnected.");

        if (world.IsUserInterrupted)
            return Blocked(GuiSafetyReason.UserInterrupted, "The player has paused or taken control of the character.");

        if (world.IsFighting)
            return Blocked(GuiSafetyReason.InFight, "GUI movement is unavailable during a fight.");

        if (world.HasAmbiguousMovement)
            return Blocked(GuiSafetyReason.PositionAmbiguous, "The movement state is ambiguous.");

        if (world.IsWalking || world.IsUsing || world.IsInDialog)
            return Blocked(GuiSafetyReason.Busy, "The character is walking, using an element, or in a dialog.");

        return null;
    }

    private static GuiProtocolTrustDecision? ValidateWindowEvidence(GameWindowSnapshot start, GameWindowSnapshot end)
    {
        if (start.Identity.Status != GameWindowIdentityStatus.Verified
            || end.Identity.Status != GameWindowIdentityStatus.Verified)
        {
            return Blocked(GuiSafetyReason.WindowIdentityUnverified, "Both window observations require verified process identity.");
        }

        if (!IsWindowActive(start) || !IsWindowActive(end))
            return Blocked(GuiSafetyReason.WindowNotActive, "Both window observations must remain mapped, viewable, focused, and active.");

        if (!SameWindowIdentity(start.Identity, end.Identity) || !SameWindowStateAndGeometry(start, end))
            return Blocked(GuiSafetyReason.WindowChanged, "The window identity, state, or geometry changed during the evidence window.");

        return null;
    }

    private static GuiProtocolTrustDecision? ValidateCalibrationEvidence(GuiProtocolTrustEvidence evidence)
    {
        CalibrationResult calibration = evidence.Calibration;
        CalibrationProfile? profile = calibration.Profile;

        if (calibration.Status == CalibrationStatus.Stale)
            return Blocked(GuiSafetyReason.CalibrationStale, "The calibration is marked stale.");

        if (!calibration.IsCalibrated || profile is null)
            return Blocked(GuiSafetyReason.NotCalibrated, "A calibrated profile is required.");

        if (profile.IsSynthetic || calibration.IsSynthetic)
        {
            if (!evidence.AllowSyntheticCalibration)
                return Blocked(GuiSafetyReason.CalibrationStale, "Synthetic calibration is allowed only by an explicit test policy.");
        }

        DateTimeOffset now = evidence.Now;
        if (profile.CreatedAt > now
            || profile.MapId != evidence.Intent.MapId
            || !SameWindowIdentity(profile.WindowIdentity, evidence.WindowEnd.Identity)
            || !string.Equals(profile.CalibrationId, evidence.Intent.CalibrationIdentity, StringComparison.Ordinal)
            || profile.CaptureWidth != evidence.Frame.PixelWidth
            || profile.CaptureHeight != evidence.Frame.PixelHeight)
        {
            return Blocked(GuiSafetyReason.CalibrationStale, "The calibration is stale or bound to another map, window, calibration id, or frame.");
        }

        return null;
    }

    private static GuiProtocolTrustDecision? ValidateTargetEvidence(GuiProtocolTrustEvidence evidence)
    {
        TargetRegion target = evidence.Target;
        CalibrationProfile profile = evidence.Calibration.Profile!;

        if (target.Status == TargetStatus.Unverified)
            return Blocked(GuiSafetyReason.TargetUnverified, "The visual target did not pass verification.");

        if (target.Status == TargetStatus.Ambiguous)
            return Blocked(GuiSafetyReason.AmbiguousTarget, "More than one visual target remains plausible.");

        if (target.Status != TargetStatus.Verified
            || target.CaptureId != evidence.Frame.CaptureId
            || !string.Equals(target.CalibrationId, profile.CalibrationId, StringComparison.Ordinal)
            || target.MapId != evidence.Intent.MapId
            || target.Cell != evidence.Intent.TargetCellId
            || target.FrameWidth != evidence.Frame.PixelWidth
            || target.FrameHeight != evidence.Frame.PixelHeight
            || !HasSafeClickPoint(target, profile, evidence.Frame.PixelWidth, evidence.Frame.PixelHeight))
        {
            return Blocked(GuiSafetyReason.TargetUnverified, "The target is not bound to the current frame, capture, calibration, map, cell, and safe client area.");
        }

        return null;
    }

    private static bool TryDescribeSnapshotDifference(GuiWorldSnapshot start, GuiWorldSnapshot end, out string difference)
    {
        if (start.SessionEpoch != end.SessionEpoch)
            difference = "session epoch";
        else if (start.StateRevision != end.StateRevision)
            difference = "state revision";
        else if (start.MapId != end.MapId)
            difference = "map";
        else if (start.CellId != end.CellId)
            difference = "cell";
        else if (start.CellProvenance != end.CellProvenance)
            difference = "cell provenance";
        else if (start.IsConnected != end.IsConnected)
            difference = "connection state";
        else if (start.IsFighting != end.IsFighting)
            difference = "fight state";
        else if (start.IsWalking != end.IsWalking)
            difference = "walking state";
        else if (start.IsUsing != end.IsUsing)
            difference = "use state";
        else if (start.IsInDialog != end.IsInDialog)
            difference = "dialog state";
        else if (start.HasAmbiguousMovement != end.HasAmbiguousMovement)
            difference = "movement ambiguity";
        else if (start.ProtocolObservationStatus != end.ProtocolObservationStatus)
            difference = "protocol observation status";
        else if (start.IsUserInterrupted != end.IsUserInterrupted)
            difference = "user interruption state";
        else if (!KnownElementsMatch(start.KnownElements, end.KnownElements))
            difference = "known elements";
        else
        {
            difference = string.Empty;
            return true;
        }

        return false;
    }

    private static bool KnownElementsMatch(ImmutableArray<GuiKnownElement> first, ImmutableArray<GuiKnownElement> second)
    {
        if (first.IsDefault != second.IsDefault || first.Length != second.Length)
            return false;

        for (int index = 0; index < first.Length; index++)
        {
            GuiKnownElement? left = first[index];
            GuiKnownElement? right = second[index];
            if (left is null || right is null)
            {
                if (left != right)
                    return false;
                continue;
            }

            if (left.ElementId != right.ElementId
                || left.Cell != right.Cell
                || left.InteractiveId != right.InteractiveId
                || left.State != right.State
                || !left.EnabledSkillIds.SequenceEqual(right.EnabledSkillIds))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWindowActive(GameWindowSnapshot window)
    {
        return window.Exists
            && window.IsMapped
            && window.IsViewable
            && !window.IsMinimized
            && window.IsFocused
            && window.IsActive
            && window.IsSessionX11
            && window.HasValidDisplay
            && window.IsXTestAvailable
            && window.GeometryReliable;
    }

    private static bool SameWindowIdentity(GameWindowIdentity first, GameWindowIdentity second)
    {
        return first.Status == GameWindowIdentityStatus.Verified
            && second.Status == GameWindowIdentityStatus.Verified
            && first.Xid == second.Xid
            && first.ProcessId == second.ProcessId
            && string.Equals(first.ExecutablePath, second.ExecutablePath, StringComparison.Ordinal);
    }

    private static bool SameWindowStateAndGeometry(GameWindowSnapshot first, GameWindowSnapshot second)
    {
        return first.Exists == second.Exists
            && first.IsMapped == second.IsMapped
            && first.IsViewable == second.IsViewable
            && first.IsMinimized == second.IsMinimized
            && first.IsFocused == second.IsFocused
            && first.IsActive == second.IsActive
            && first.IsXTestAvailable == second.IsXTestAvailable
            && first.IsSessionX11 == second.IsSessionX11
            && first.HasValidDisplay == second.HasValidDisplay
            && first.GeometryReliable == second.GeometryReliable
            && first.DesktopOrigin == second.DesktopOrigin
            && first.WindowWidth == second.WindowWidth
            && first.WindowHeight == second.WindowHeight
            && first.BorderWidth == second.BorderWidth
            && first.ClientWidth == second.ClientWidth
            && first.ClientHeight == second.ClientHeight;
    }

    private static bool HasSafeClickPoint(TargetRegion target, CalibrationProfile profile, int frameWidth, int frameHeight)
    {
        if (target.ProposedClickPoint is not { } point)
            return false;

        ClientAreaGeometry geometry = profile.ClientAreaGeometry;
        long left = (long)geometry.OriginInWindow.X + 2;
        long top = (long)geometry.OriginInWindow.Y + 2;
        long right = (long)geometry.OriginInWindow.X + geometry.Width - 2;
        long bottom = (long)geometry.OriginInWindow.Y + geometry.Height - 2;

        return point.X >= left
            && point.X < right
            && point.Y >= top
            && point.Y < bottom
            && point.X < frameWidth
            && point.Y < frameHeight;
    }

    private static GuiProtocolTrustDecision Blocked(GuiSafetyReason reason, string detail)
    {
        return new GuiProtocolTrustDecision(false, reason, detail);
    }
}
