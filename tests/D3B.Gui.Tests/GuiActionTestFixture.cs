using System.Collections.Immutable;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests;

internal sealed class GuiActionTestFixture : IDisposable
{
    public const long MapId = 191105026;
    public const int CurrentCellId = 30;
    public const int TargetCellId = 31;
    public const int ElementId = 12;

    public DateTimeOffset Now { get; } = new(2035, 3, 4, 12, 0, 0, TimeSpan.Zero);
    public ManualTimeProvider Time { get; }
    public GuiOptions Options { get; set; } = new() { Enabled = true };
    public GuiWorldSnapshot World { get; set; }
    public GameWindowSnapshot Window { get; set; }
    public CapturedFrame Frame { get; private set; }
    public CalibrationResult Calibration { get; set; }
    public TargetRegion Target { get; set; }
    public GuiActionIntent Intent { get; set; }

    public GameWindowIdentity WindowIdentity { get; } = new(
        451,
        9001,
        "/synthetic/dofus",
        "synthetic fixture title",
        GameWindowIdentityStatus.Verified);

    public GuiActionTestFixture()
    {
        Time = new ManualTimeProvider(Now);
        Window = CreateWindow(WindowIdentity);
        World = CreateWorld();
        Calibration = CreateCalibration();
        Frame = CreateFrame();
        Target = CreateTarget();
        Intent = CreateIntent();
    }

    public GuiActionGate CreateGate(GuiOptions? options = null)
    {
        return new GuiActionGate(options ?? Options, Time);
    }

    public GuiActionIntent CreateIntent(
        Guid? intentId = null,
        GuiActionKind actionKind = GuiActionKind.WalkToCell,
        DateTimeOffset? expiresAt = null,
        int? targetCellId = null,
        int? targetElementId = null,
        GameWindowIdentity? windowIdentity = null,
        string? calibrationIdentity = null,
        long? sessionEpoch = null,
        long? stateRevision = null)
    {
        return new GuiActionIntent(
            intentId ?? Guid.NewGuid(),
            actionKind,
            sessionEpoch ?? 4,
            stateRevision ?? 9,
            MapId,
            new LogicalCell(targetCellId ?? TargetCellId),
            actionKind == GuiActionKind.UseElement ? targetElementId ?? ElementId : targetElementId,
            windowIdentity ?? WindowIdentity,
            calibrationIdentity ?? Calibration.Profile!.CalibrationId,
            actionKind == GuiActionKind.UseElement ? GuiClientAction.UseElement : GuiClientAction.Walk,
            actionKind == GuiActionKind.UseElement ? GuiServerOutcome.ElementUseEnded : GuiServerOutcome.MovementConfirmed,
            Now.AddSeconds(-1),
            expiresAt ?? Now.AddMinutes(1));
    }

    public GuiWorldSnapshot CreateWorld(
        long? mapId = MapId,
        int? cellId = CurrentCellId,
        GuiCellProvenance? provenance = GuiCellProvenance.ServerPlaced,
        bool isConnected = true)
    {
        return new GuiWorldSnapshot(
            4,
            9,
            Now,
            mapId,
            cellId is { } value ? new LogicalCell(value) : null,
            provenance,
            isConnected,
            false,
            false,
            false,
            false,
            false,
            [new GuiKnownElement(ElementId, new LogicalCell(TargetCellId), 3, 1, [40])],
            ProtocolObservationStatus.Complete);
    }

    public GameWindowSnapshot CreateWindow(GameWindowIdentity? identity = null)
    {
        return new GameWindowSnapshot(
            identity ?? WindowIdentity,
            true,
            true,
            true,
            false,
            true,
            true,
            true,
            true,
            true,
            true,
            new DesktopPixels(90, 20),
            784,
            564,
            2,
            780,
            560,
            null,
            Now);
    }

    public CalibrationResult CreateCalibration(
        CalibrationStatus status = CalibrationStatus.Calibrated,
        DateTimeOffset? createdAt = null,
        long mapId = MapId,
        GameWindowIdentity? windowIdentity = null)
    {
        if (!AffineScreenMapper.TryCreate([4, 0, 260, 0, 4, 200], out AffineScreenMapper? mapper) || mapper is null)
            throw new InvalidOperationException("The synthetic calibration mapper must be non-singular.");

        CalibrationProfile profile = new(
            "synthetic-build",
            mapId,
            windowIdentity ?? WindowIdentity,
            800,
            600,
            new ClientAreaGeometry(new DesktopPixels(92, 22), new ClientAreaPixels(2, 2), 780, 560),
            1,
            1,
            mapper,
            ImmutableArray<CalibrationAnchor>.Empty,
            ImmutableArray<double>.Empty,
            createdAt ?? Now,
            true);

        return new CalibrationResult(status, status == CalibrationStatus.NotCalibrated ? null : profile, null, [], true);
    }

    public CapturedFrame CreateFrame(
        CaptureStatus status = CaptureStatus.Captured,
        bool contentUsable = true,
        GameWindowSnapshot? before = null,
        GameWindowSnapshot? after = null,
        GameWindowIdentity? identity = null)
    {
        GameWindowIdentity actualIdentity = identity ?? Window.Identity;
        GameWindowSnapshot beforeSnapshot = before ?? Window;
        GameWindowSnapshot afterSnapshot = after ?? Window;
        Mat image = new(600, 800, MatType.CV_8UC3, Scalar.All(25));

        return new CapturedFrame(
            Guid.NewGuid(),
            actualIdentity,
            Now,
            Now,
            beforeSnapshot,
            afterSnapshot,
            image.Cols,
            image.Rows,
            image,
            status,
            status == CaptureStatus.Captured ? null : "Synthetic capture status.",
            contentUsable);
    }

    public TargetRegion CreateTarget(
        TargetStatus status = TargetStatus.Verified,
        int? targetCellId = null,
        long? mapId = null,
        int elementId = ElementId,
        Guid? captureId = null,
        string? calibrationId = null,
        WindowLocalPixels? proposedClickPoint = null)
    {
        return new TargetRegion(
            mapId ?? MapId,
            elementId,
            new LogicalCell(targetCellId ?? TargetCellId),
            9123,
            40,
            new WindowLocalPixels(500, 400),
            new WindowLocalRect(488, 390, 24, 20),
            proposedClickPoint ?? new WindowLocalPixels(500, 400),
            new TargetConfidence(0.98, 0.2, 0.78, 0.8, 0.1, ImmutableArray<TargetEvidence>.Empty),
            status,
            null,
            captureId ?? Frame.CaptureId,
            calibrationId ?? Calibration.Profile!.CalibrationId);
    }

    public void ReplaceFrame(CapturedFrame frame)
    {
        Frame.Dispose();
        Frame = frame;
    }

    public void Dispose()
    {
        Frame.Dispose();
    }
}
