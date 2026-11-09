using System.Collections.Immutable;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Bot.Gui;

/// <summary>Supplies one planned same-map walk and every dependency used to validate and execute it.</summary>
public sealed class GuiWalkToCellRequest
{
    /// <summary>Gets the actor whose state and intent inbox own this walk.</summary>
    public BotActor Bot { get; }

    /// <summary>Gets the adapter that captures the actor's published protocol state.</summary>
    public GuiWorldSnapshotAdapter SnapshotAdapter { get; }

    /// <summary>Gets the explicit X11 window locator.</summary>
    public IGameWindowLocator WindowLocator { get; }

    /// <summary>Gets the capture service for the explicit X11 window.</summary>
    public IWindowCapture WindowCapture { get; }

    /// <summary>Gets the caller-selected X11 window identifier.</summary>
    public long Xid { get; }

    /// <summary>Gets the expected process identity policy for the selected window.</summary>
    public GameWindowExpectation WindowExpectation { get; }

    /// <summary>Gets the prepared profile and its original validation result.</summary>
    public CalibrationResult Calibration { get; }

    /// <summary>Gets the configured GUI policy.</summary>
    public GuiOptions GuiOptions { get; }

    /// <summary>Gets the existing snapshot-bound action gate.</summary>
    public GuiActionGate Gate { get; }

    /// <summary>Gets the existing one-click action executor.</summary>
    public GuiActionExecutor Executor { get; }

    /// <summary>Gets the input implementation used by the executor.</summary>
    public IGuiInput Input { get; }

    /// <summary>Gets the map source used to validate the supplied path against one map.</summary>
    public GameMaps Maps { get; }

    /// <summary>Gets the candidate path to validate and execute.</summary>
    public WalkToCellPlan Plan { get; }

    /// <summary>Gets screen regions that must not receive a click.</summary>
    public ImmutableArray<WindowLocalRect> UiExclusions { get; }

    /// <summary>Gets the minimum distance from the client-area edge for a projected target.</summary>
    public int SafetyMargin { get; }

    /// <summary>Gets the clock used for snapshot, calibration, intent, and confirmation age checks.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Creates a request with an explicit selected XID, prepared calibration, and reachable path.</summary>
    public GuiWalkToCellRequest(
        BotActor bot,
        GuiWorldSnapshotAdapter snapshotAdapter,
        IGameWindowLocator windowLocator,
        IWindowCapture windowCapture,
        long xid,
        GameWindowExpectation windowExpectation,
        CalibrationResult calibration,
        GuiOptions guiOptions,
        GuiActionGate gate,
        GuiActionExecutor executor,
        IGuiInput input,
        GameMaps maps,
        WalkToCellPlan plan,
        ImmutableArray<WindowLocalRect> uiExclusions,
        int safetyMargin,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(snapshotAdapter);
        ArgumentNullException.ThrowIfNull(windowLocator);
        ArgumentNullException.ThrowIfNull(windowCapture);
        ArgumentNullException.ThrowIfNull(windowExpectation);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(guiOptions);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(xid);
        ArgumentOutOfRangeException.ThrowIfNegative(safetyMargin);

        Bot = bot;
        SnapshotAdapter = snapshotAdapter;
        WindowLocator = windowLocator;
        WindowCapture = windowCapture;
        Xid = xid;
        WindowExpectation = windowExpectation;
        Calibration = calibration;
        GuiOptions = guiOptions;
        Gate = gate;
        Executor = executor;
        Input = input;
        Maps = maps;
        Plan = plan;
        UiExclusions = uiExclusions.IsDefault ? ImmutableArray<WindowLocalRect>.Empty : uiExclusions;
        SafetyMargin = safetyMargin;
        TimeProvider = timeProvider;
    }

    /// <summary>Creates a request from a prepared calibration profile.</summary>
    public GuiWalkToCellRequest(
        BotActor bot,
        GuiWorldSnapshotAdapter snapshotAdapter,
        IGameWindowLocator windowLocator,
        IWindowCapture windowCapture,
        long xid,
        GameWindowExpectation windowExpectation,
        CalibrationProfile calibrationProfile,
        GuiOptions guiOptions,
        GuiActionGate gate,
        GuiActionExecutor executor,
        IGuiInput input,
        GameMaps maps,
        WalkToCellPlan plan,
        ImmutableArray<WindowLocalRect> uiExclusions,
        int safetyMargin,
        TimeProvider timeProvider)
        : this(
            bot,
            snapshotAdapter,
            windowLocator,
            windowCapture,
            xid,
            windowExpectation,
            MakeCalibrationResult(calibrationProfile),
            guiOptions,
            gate,
            executor,
            input,
            maps,
            plan,
            uiExclusions,
            safetyMargin,
            timeProvider)
    {
    }

    private static CalibrationResult MakeCalibrationResult(CalibrationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new CalibrationResult(
            CalibrationStatus.Calibrated,
            profile,
            null,
            ImmutableArray<CalibrationProblem>.Empty,
            profile.IsSynthetic);
    }
}
