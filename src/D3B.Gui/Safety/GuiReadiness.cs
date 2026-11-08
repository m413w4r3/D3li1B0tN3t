using System.Globalization;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Safety;

/// <summary>A point-in-time gate result bound to one action, session, window, calibration, and target.</summary>
public sealed record GuiReadiness
{
    private readonly Guid _intentId;

    /// <summary>Gets whether all gate checks passed for this exact intention.</summary>
    public bool Allowed { get; }

    /// <summary>Gets the gate result or the first deterministic safety cause.</summary>
    public GuiSafetyReason ReasonCode { get; }

    /// <summary>Gets the session epoch observed by the gate.</summary>
    public long SessionEpoch { get; }

    /// <summary>Gets the state revision observed by the gate.</summary>
    public long StateRevision { get; }

    /// <summary>Gets the window identity observed by the gate.</summary>
    public GameWindowIdentity WindowIdentity { get; }

    /// <summary>Gets the identity of the calibration profile used by the gate.</summary>
    public string CalibrationIdentity { get; }

    /// <summary>Gets the identity of the visual target used by the gate.</summary>
    public string TargetIdentity { get; }

    /// <summary>Gets the unique, process-local identifier for this readiness result.</summary>
    public Guid AuthorizationId { get; }

    /// <summary>Gets a bounded explanation when the gate rejects the action.</summary>
    public string? Detail { get; }

    internal GuiReadiness(
        bool allowed,
        GuiSafetyReason reasonCode,
        long sessionEpoch,
        long stateRevision,
        GameWindowIdentity windowIdentity,
        string calibrationIdentity,
        string targetIdentity,
        Guid authorizationId,
        string? detail,
        Guid intentId)
    {
        ArgumentNullException.ThrowIfNull(windowIdentity);
        ArgumentNullException.ThrowIfNull(calibrationIdentity);
        ArgumentNullException.ThrowIfNull(targetIdentity);

        Allowed = allowed;
        ReasonCode = reasonCode;
        SessionEpoch = sessionEpoch;
        StateRevision = stateRevision;
        WindowIdentity = windowIdentity;
        CalibrationIdentity = calibrationIdentity;
        TargetIdentity = targetIdentity;
        AuthorizationId = authorizationId;
        Detail = detail;
        _intentId = intentId;
    }

    /// <summary>Checks that this readiness still names the exact arguments from its gate evaluation.</summary>
    /// <remarks>This is a binding check, not a cryptographic proof or a substitute for a fresh gate evaluation.</remarks>
    public bool IsBoundTo(
        GuiActionIntent intent,
        GuiWorldSnapshot world,
        GameWindowSnapshot window,
        CalibrationResult calibration,
        TargetRegion target)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(target);

        CalibrationProfile? profile = calibration.Profile;

        if (!Allowed || AuthorizationId == Guid.Empty || _intentId != intent.IntentId || profile is null)
            return false;

        return SessionEpoch == world.SessionEpoch
            && StateRevision == world.StateRevision
            && intent.SessionEpoch == world.SessionEpoch
            && intent.StateRevision == world.StateRevision
            && intent.MapId == world.MapId
            && intent.MapId == target.MapId
            && intent.TargetCellId == target.Cell
            && intent.WindowIdentity is not null
            && IdentityMatches(WindowIdentity, intent.WindowIdentity)
            && IdentityMatches(WindowIdentity, window.Identity)
            && profile.MapId == intent.MapId
            && IdentityMatches(profile.WindowIdentity, window.Identity)
            && string.Equals(CalibrationIdentity, profile.CalibrationId, StringComparison.Ordinal)
            && string.Equals(intent.CalibrationIdentity, profile.CalibrationId, StringComparison.Ordinal)
            && string.Equals(target.CalibrationId, profile.CalibrationId, StringComparison.Ordinal)
            && IsActionTargetCompatible(intent, target)
            && string.Equals(TargetIdentity, CreateTargetIdentity(target), StringComparison.Ordinal);
    }

    internal static string CreateTargetIdentity(TargetRegion target)
    {
        ArgumentNullException.ThrowIfNull(target);

        string bounds = target.VisualBoundingBox is { } box
            ? string.Join(",", box.X, box.Y, box.Width, box.Height)
            : "none";
        string click = target.ProposedClickPoint is { } point
            ? string.Join(",", point.X, point.Y)
            : "none";
        string evidence = string.Join(",", target.Confidence.Evidence);

        return string.Join(
            ":",
            target.CaptureId.ToString("N", CultureInfo.InvariantCulture),
            target.CalibrationId,
            target.MapId.ToString(CultureInfo.InvariantCulture),
            target.ElementId.ToString(CultureInfo.InvariantCulture),
            target.Cell.Value.ToString(CultureInfo.InvariantCulture),
            target.GfxId.ToString(CultureInfo.InvariantCulture),
            target.ExpectedSkillId?.ToString(CultureInfo.InvariantCulture) ?? "none",
            target.ProjectedCellCenter.X.ToString(CultureInfo.InvariantCulture),
            target.ProjectedCellCenter.Y.ToString(CultureInfo.InvariantCulture),
            bounds,
            click,
            target.Status.ToString(),
            target.Confidence.Score.ToString("R", CultureInfo.InvariantCulture),
            target.Confidence.RunnerUpScore.ToString("R", CultureInfo.InvariantCulture),
            target.Confidence.Margin.ToString("R", CultureInfo.InvariantCulture),
            target.Confidence.RequiredScore.ToString("R", CultureInfo.InvariantCulture),
            target.Confidence.RequiredMargin.ToString("R", CultureInfo.InvariantCulture),
            evidence);
    }

    internal static bool IdentityMatches(GameWindowIdentity expected, GameWindowIdentity actual)
    {
        return expected.Xid == actual.Xid
            && (expected.ProcessId is null || actual.ProcessId is null || expected.ProcessId == actual.ProcessId)
            && (expected.ExecutablePath is null
                || actual.ExecutablePath is null
                || string.Equals(expected.ExecutablePath, actual.ExecutablePath, StringComparison.Ordinal));
    }

    private static bool IsActionTargetCompatible(GuiActionIntent intent, TargetRegion target)
    {
        return intent.ActionKind switch
        {
            GuiActionKind.WalkToCell => intent.TargetElementId is null,
            GuiActionKind.UseElement => intent.TargetElementId == target.ElementId,
            _ => false,
        };
    }
}
