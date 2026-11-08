using System.Collections.Immutable;
using D3B.Bot.Gui;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiProtocolTrustTestFixture : IDisposable
{
    public const long MapId = 191105026;
    public const int CurrentCell = 30;
    public const int TargetCell = 31;

    public DateTimeOffset Now { get; } = new(2035, 3, 4, 12, 0, 0, TimeSpan.Zero);
    public ManualGuiTimeProvider Time { get; }
    public GameWindowIdentity Identity { get; }
    public GameWindowSnapshot Window { get; }
    public GuiWorldSnapshot World { get; }
    public GuiActionIntent Intent { get; }
    public CapturedFrame Frame { get; }
    public CalibrationResult Calibration { get; }
    public TargetRegion Target { get; }

    public GuiProtocolTrustTestFixture()
    {
        Time = new ManualGuiTimeProvider(Now);
        Identity = new GameWindowIdentity(451, 9001, "/synthetic/dofus", "synthetic fixture", GameWindowIdentityStatus.Verified);
        Window = new GameWindowSnapshot(
            Identity,
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
        World = CreateWorld();
        Intent = CreateIntent();
        Frame = CreateFrame();
        Calibration = CreateCalibration();
        Target = CreateTarget();
    }

    public GuiProtocolTrustPolicy CreatePolicy(GuiOptions? options = null)
    {
        GuiActionGate gate = new(options ?? new GuiOptions { Enabled = true }, Time);
        return new GuiProtocolTrustPolicy(gate, Time);
    }

    public GuiProtocolTrustEvidence CreateEvidence(bool allowSyntheticCalibration = true)
    {
        if (allowSyntheticCalibration)
        {
            return new GuiProtocolTrustEvidence(
                Intent,
                World,
                World,
                Window,
                Window,
                Frame,
                Calibration,
                Target,
                CurrentCell,
                TargetCell,
                true,
                false,
                false,
                false,
                false,
                Now,
                TimeSpan.FromSeconds(5),
                true);
        }

        return new GuiProtocolTrustEvidence(
            Intent,
            World,
            World,
            Window,
            Window,
            Frame,
            Calibration,
            Target,
            CurrentCell,
            TargetCell,
            true,
            false,
            false,
            false,
            false,
            Now,
            TimeSpan.FromSeconds(5));
    }

    public GuiActionIntent CreateIntent(GuiActionKind actionKind = GuiActionKind.WalkToCell)
    {
        return new GuiActionIntent(
            Guid.NewGuid(),
            actionKind,
            4,
            9,
            MapId,
            new LogicalCell(TargetCell),
            actionKind == GuiActionKind.UseElement ? 12 : null,
            Identity,
            CalibrationProfileForIntent().CalibrationId,
            actionKind == GuiActionKind.UseElement ? GuiClientAction.UseElement : GuiClientAction.Walk,
            actionKind == GuiActionKind.UseElement ? GuiServerOutcome.ElementUseEnded : GuiServerOutcome.MovementConfirmed,
            Now.AddSeconds(-1),
            Now.AddMinutes(1));
    }

    public GuiWorldSnapshot CreateWorld(
        long? sessionEpoch = 4,
        long? stateRevision = 9,
        long? mapId = MapId,
        int? cellId = CurrentCell,
        GuiCellProvenance? provenance = GuiCellProvenance.ServerPlaced,
        bool isConnected = true,
        bool isFighting = false,
        bool isWalking = false,
        bool isUsing = false,
        bool isInDialog = false,
        bool hasAmbiguousMovement = false,
        ProtocolObservationStatus status = ProtocolObservationStatus.Complete,
        bool isUserInterrupted = false,
        ImmutableArray<GuiKnownElement>? knownElements = null,
        DateTimeOffset? capturedAt = null)
    {
        return new GuiWorldSnapshot(
            sessionEpoch ?? 4,
            stateRevision ?? 9,
            capturedAt ?? Now,
            mapId,
            cellId is { } value ? new LogicalCell(value) : null,
            provenance,
            isConnected,
            isFighting,
            isWalking,
            isUsing,
            isInDialog,
            hasAmbiguousMovement,
            knownElements ?? [new GuiKnownElement(12, new LogicalCell(TargetCell), 3, 1, [40])],
            status)
        {
            IsUserInterrupted = isUserInterrupted,
        };
    }

    public GameWindowSnapshot CreateWindow(GameWindowIdentity? identity = null)
    {
        return Window with { Identity = identity ?? Identity };
    }

    public CalibrationResult CreateCalibration(
        CalibrationStatus status = CalibrationStatus.Calibrated,
        DateTimeOffset? createdAt = null,
        long mapId = MapId,
        GameWindowIdentity? windowIdentity = null,
        int captureWidth = 800,
        int captureHeight = 600,
        bool synthetic = true)
    {
        if (!AffineScreenMapper.TryCreate([4, 0, 260, 0, 4, 200], out AffineScreenMapper? mapper) || mapper is null)
            throw new InvalidOperationException("The synthetic calibration mapper must be non-singular.");

        CalibrationProfile profile = new(
            "synthetic-build",
            mapId,
            windowIdentity ?? Identity,
            captureWidth,
            captureHeight,
            new ClientAreaGeometry(new DesktopPixels(92, 22), new ClientAreaPixels(2, 2), 780, 560),
            1,
            1,
            mapper,
            ImmutableArray<CalibrationAnchor>.Empty,
            ImmutableArray<double>.Empty,
            createdAt ?? Now,
            synthetic);

        return new CalibrationResult(status, profile, null, ImmutableArray<CalibrationProblem>.Empty, synthetic);
    }

    public CapturedFrame CreateFrame()
    {
        Mat image = new(600, 800, MatType.CV_8UC3, Scalar.All(25));
        return new CapturedFrame(
            Guid.NewGuid(),
            Identity,
            Now,
            Now,
            Window,
            Window,
            image.Cols,
            image.Rows,
            image,
            CaptureStatus.Captured,
            null,
            true);
    }

    public TargetRegion CreateTarget(
        TargetStatus status = TargetStatus.Verified,
        int? targetCell = TargetCell,
        long mapId = MapId,
        Guid? captureId = null,
        string? calibrationId = null,
        WindowLocalPixels? point = null,
        bool hasPoint = true,
        int? frameWidth = null,
        int? frameHeight = null)
    {
        return new TargetRegion(
            mapId,
            12,
            new LogicalCell(targetCell ?? TargetCell),
            9123,
            40,
            new WindowLocalPixels(500, 400),
            new WindowLocalRect(488, 390, 24, 20),
            hasPoint ? point ?? new WindowLocalPixels(500, 400) : null,
            new TargetConfidence(0.98, 0.2, 0.78, 0.8, 0.1, ImmutableArray<TargetEvidence>.Empty),
            status,
            null,
            captureId ?? Frame.CaptureId,
            calibrationId ?? Calibration.Profile!.CalibrationId,
            new WindowLocalPixels(500, 400),
            frameWidth ?? Frame.PixelWidth,
            frameHeight ?? Frame.PixelHeight);
    }

    private CalibrationProfile CalibrationProfileForIntent()
    {
        if (!AffineScreenMapper.TryCreate([4, 0, 260, 0, 4, 200], out AffineScreenMapper? mapper) || mapper is null)
            throw new InvalidOperationException("The synthetic calibration mapper must be non-singular.");

        return new CalibrationProfile(
            "synthetic-build",
            MapId,
            Identity,
            800,
            600,
            new ClientAreaGeometry(new DesktopPixels(92, 22), new ClientAreaPixels(2, 2), 780, 560),
            1,
            1,
            mapper,
            ImmutableArray<CalibrationAnchor>.Empty,
            ImmutableArray<double>.Empty,
            Now,
            true);
    }

    public void Dispose()
    {
        Frame.Dispose();
    }
}
