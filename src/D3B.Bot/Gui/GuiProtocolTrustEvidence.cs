using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Immutable proof inputs for deciding whether one first WalkToCell GUI action is safe.</summary>
public sealed record GuiProtocolTrustEvidence
{
    /// <summary>Gets the requested action and its expected protocol contract.</summary>
    public GuiActionIntent Intent { get; init; }

    /// <summary>Gets the protocol snapshot taken before the evidence window.</summary>
    public GuiWorldSnapshot Start { get; init; }

    /// <summary>Gets the protocol snapshot taken after the evidence window.</summary>
    public GuiWorldSnapshot End { get; init; }

    /// <summary>Gets the verified window observation at the start of the evidence window.</summary>
    public GameWindowSnapshot WindowStart { get; init; }

    /// <summary>Gets the verified window observation at the end of the evidence window.</summary>
    public GameWindowSnapshot WindowEnd { get; init; }

    /// <summary>Gets the captured scene bound to the target and calibration.</summary>
    public CapturedFrame Frame { get; init; }

    /// <summary>Gets the measured calibration result used for the target projection.</summary>
    public CalibrationResult Calibration { get; init; }

    /// <summary>Gets the visual target localization result for the requested cell.</summary>
    public TargetRegion Target { get; init; }

    /// <summary>Gets the planned start cell, when a movement plan was supplied.</summary>
    public int? PlanStartCell { get; init; }

    /// <summary>Gets the planned destination cell, when a movement plan was supplied.</summary>
    public int? PlanTargetCell { get; init; }

    /// <summary>Gets whether a supplied movement plan was reachable.</summary>
    public bool? PlanReachable { get; init; }

    /// <summary>Gets whether an undecoded message was observed during the evidence window.</summary>
    public bool UnknownMessageObservedDuringWindow { get; init; }

    /// <summary>Gets whether protocol translation became blocked during the evidence window.</summary>
    public bool TranslationLatchClosedDuringWindow { get; init; }

    /// <summary>Gets whether client gameplay traffic was observed during the evidence window.</summary>
    public bool ClientGameplayObservedDuringWindow { get; init; }

    /// <summary>Gets whether human input could not be distinguished from synthesized input.</summary>
    public bool HumanInputAmbiguityObserved { get; init; }

    /// <summary>Gets the evidence evaluation time supplied by its collector.</summary>
    public DateTimeOffset Now { get; init; }

    /// <summary>Gets the maximum permitted age of the end protocol snapshot.</summary>
    public TimeSpan MaxSnapshotAge { get; init; }

    /// <summary>Gets whether synthetic calibration may be accepted by this evaluation.</summary>
    public bool AllowSyntheticCalibration { get; }

    /// <summary>Creates production evidence with synthetic calibration disabled.</summary>
    public GuiProtocolTrustEvidence(
        GuiActionIntent intent,
        GuiWorldSnapshot start,
        GuiWorldSnapshot end,
        GameWindowSnapshot windowStart,
        GameWindowSnapshot windowEnd,
        CapturedFrame frame,
        CalibrationResult calibration,
        TargetRegion target,
        int? planStartCell,
        int? planTargetCell,
        bool? planReachable,
        bool unknownMessageObservedDuringWindow,
        bool translationLatchClosedDuringWindow,
        bool clientGameplayObservedDuringWindow,
        bool humanInputAmbiguityObserved,
        DateTimeOffset now,
        TimeSpan maxSnapshotAge)
        : this(
            intent,
            start,
            end,
            windowStart,
            windowEnd,
            frame,
            calibration,
            target,
            planStartCell,
            planTargetCell,
            planReachable,
            unknownMessageObservedDuringWindow,
            translationLatchClosedDuringWindow,
            clientGameplayObservedDuringWindow,
            humanInputAmbiguityObserved,
            now,
            maxSnapshotAge,
            false)
    {
    }

    /// <summary>Creates evidence with synthetic calibration enabled for the friend test assembly only.</summary>
    internal GuiProtocolTrustEvidence(
        GuiActionIntent intent,
        GuiWorldSnapshot start,
        GuiWorldSnapshot end,
        GameWindowSnapshot windowStart,
        GameWindowSnapshot windowEnd,
        CapturedFrame frame,
        CalibrationResult calibration,
        TargetRegion target,
        int? planStartCell,
        int? planTargetCell,
        bool? planReachable,
        bool unknownMessageObservedDuringWindow,
        bool translationLatchClosedDuringWindow,
        bool clientGameplayObservedDuringWindow,
        bool humanInputAmbiguityObserved,
        DateTimeOffset now,
        TimeSpan maxSnapshotAge,
        bool allowSyntheticCalibration)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(windowStart);
        ArgumentNullException.ThrowIfNull(windowEnd);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(target);

        Intent = intent;
        Start = start;
        End = end;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Frame = frame;
        Calibration = calibration;
        Target = target;
        PlanStartCell = planStartCell;
        PlanTargetCell = planTargetCell;
        PlanReachable = planReachable;
        UnknownMessageObservedDuringWindow = unknownMessageObservedDuringWindow;
        TranslationLatchClosedDuringWindow = translationLatchClosedDuringWindow;
        ClientGameplayObservedDuringWindow = clientGameplayObservedDuringWindow;
        HumanInputAmbiguityObserved = humanInputAmbiguityObserved;
        Now = now;
        MaxSnapshotAge = maxSnapshotAge;
        AllowSyntheticCalibration = allowSyntheticCalibration;
    }
}
