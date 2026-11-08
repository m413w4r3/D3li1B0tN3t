using D3B.Gui.Calibration;
using D3B.Gui.Windowing;

namespace D3B.Gui.Actions;

/// <summary>A single, expiring GUI action tied to one protocol snapshot and window.</summary>
public sealed record GuiActionIntent
{
    /// <summary>Gets the unique identifier for this intention.</summary>
    public Guid IntentId { get; init; }

    /// <summary>Gets the action kind.</summary>
    public GuiActionKind ActionKind { get; init; }

    /// <summary>Gets the session epoch at planning time.</summary>
    public long SessionEpoch { get; init; }

    /// <summary>Gets the state revision at planning time.</summary>
    public long StateRevision { get; init; }

    /// <summary>Gets the expected map identifier.</summary>
    public long MapId { get; init; }

    /// <summary>Gets the logical destination cell.</summary>
    public LogicalCell TargetCellId { get; init; }

    /// <summary>Gets the interactive element identifier for element use.</summary>
    public int? TargetElementId { get; init; }

    /// <summary>Gets the exact X11 window identity selected for this action.</summary>
    public GameWindowIdentity WindowIdentity { get; init; }

    /// <summary>Gets the exact calibration profile identity used to locate the target.</summary>
    public string CalibrationIdentity { get; init; }

    /// <summary>Gets the neutral client request expected after the click.</summary>
    public GuiClientAction ExpectedClientAction { get; init; }

    /// <summary>Gets the neutral server result required before success can be reported.</summary>
    public GuiServerOutcome ExpectedServerOutcome { get; init; }

    /// <summary>Gets when the intention became valid.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the intention expires.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Creates and validates one immutable action contract.</summary>
    public GuiActionIntent(
        Guid intentId,
        GuiActionKind actionKind,
        long sessionEpoch,
        long stateRevision,
        long mapId,
        LogicalCell targetCellId,
        int? targetElementId,
        GameWindowIdentity windowIdentity,
        string calibrationIdentity,
        GuiClientAction expectedClientAction,
        GuiServerOutcome expectedServerOutcome,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (intentId == Guid.Empty)
            throw new ArgumentException("An intention requires a non-empty identifier.", nameof(intentId));

        ArgumentNullException.ThrowIfNull(windowIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(calibrationIdentity);

        if (expiresAt <= createdAt)
            throw new ArgumentException("An intention must expire after it is created.", nameof(expiresAt));

        if (actionKind == GuiActionKind.UseElement && targetElementId is null)
            throw new ArgumentException("Element use requires an element identifier.", nameof(targetElementId));

        if (actionKind == GuiActionKind.WalkToCell && targetElementId is not null)
            throw new ArgumentException("Cell movement cannot carry an element identifier.", nameof(targetElementId));

        if (actionKind == GuiActionKind.WalkToCell
            && (expectedClientAction != GuiClientAction.Walk || expectedServerOutcome != GuiServerOutcome.MovementConfirmed))
        {
            throw new ArgumentException("Cell movement requires the movement request and confirmation contract.", nameof(expectedClientAction));
        }

        if (actionKind == GuiActionKind.UseElement
            && (expectedClientAction != GuiClientAction.UseElement || expectedServerOutcome != GuiServerOutcome.ElementUseEnded))
        {
            throw new ArgumentException("Element use requires the element request and completion contract.", nameof(expectedClientAction));
        }

        if (!Enum.IsDefined(actionKind) || !Enum.IsDefined(expectedClientAction) || !Enum.IsDefined(expectedServerOutcome))
            throw new ArgumentOutOfRangeException(nameof(actionKind));

        IntentId = intentId;
        ActionKind = actionKind;
        SessionEpoch = sessionEpoch;
        StateRevision = stateRevision;
        MapId = mapId;
        TargetCellId = targetCellId;
        TargetElementId = targetElementId;
        WindowIdentity = windowIdentity;
        CalibrationIdentity = calibrationIdentity;
        ExpectedClientAction = expectedClientAction;
        ExpectedServerOutcome = expectedServerOutcome;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}
