using System.Collections.Immutable;
using D3B.Bot.Gui;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

public sealed class GuiProtocolTrustPolicyTests
{
    [Fact]
    public void CompleteEvidenceWithSyntheticCalibrationExplicitlyAllowedIsAllowed()
    {
        using GuiProtocolTrustTestFixture fixture = new();

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence());

        Assert.True(decision.Allowed);
        Assert.Equal(GuiSafetyReason.Ready, decision.Reason);
        Assert.Null(decision.Detail);
    }

    [Fact]
    public void MissingEvidenceFailsClosed()
    {
        using GuiProtocolTrustTestFixture fixture = new();

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(null);

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.ProtocolContradiction, decision.Reason);
    }

    [Theory]
    [InlineData(GuiActionKind.UseElement)]
    public void UnsupportedActionIsRejected(GuiActionKind actionKind)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with { Intent = fixture.CreateIntent(actionKind) };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.Disabled, decision.Reason);
    }

    [Fact]
    public void WalkContractMustExpectWalkAndMovementConfirmation()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with
        {
            Intent = fixture.Intent with { ExpectedClientAction = GuiClientAction.UseElement },
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.ProtocolContradiction, decision.Reason);
    }

    [Fact]
    public void ExistingGateMustBeReady()
    {
        using GuiProtocolTrustTestFixture fixture = new();

        GuiProtocolTrustDecision decision = fixture.CreatePolicy(new GuiOptions { Enabled = false }).Evaluate(fixture.CreateEvidence());

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.Disabled, decision.Reason);
    }

    [Theory]
    [InlineData("SessionEpoch", GuiSafetyReason.StateChanged)]
    [InlineData("StateRevision", GuiSafetyReason.StateChanged)]
    [InlineData("Map", GuiSafetyReason.StateChanged)]
    [InlineData("Cell", GuiSafetyReason.StateChanged)]
    [InlineData("Provenance", GuiSafetyReason.StateChanged)]
    [InlineData("Connected", GuiSafetyReason.StateChanged)]
    [InlineData("Fight", GuiSafetyReason.StateChanged)]
    [InlineData("Walking", GuiSafetyReason.StateChanged)]
    [InlineData("Using", GuiSafetyReason.StateChanged)]
    [InlineData("Dialog", GuiSafetyReason.StateChanged)]
    [InlineData("AmbiguousMovement", GuiSafetyReason.StateChanged)]
    [InlineData("ProtocolStatus", GuiSafetyReason.ProtocolContradiction)]
    [InlineData("UserInterruption", GuiSafetyReason.StateChanged)]
    [InlineData("KnownElements", GuiSafetyReason.StateChanged)]
    public void StartAndEndSnapshotChangesAreRejected(string change, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with { Start = Change(fixture.World, change) };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Theory]
    [InlineData(ProtocolObservationStatus.Unknown, GuiSafetyReason.MapUnknown)]
    [InlineData(ProtocolObservationStatus.Partial, GuiSafetyReason.CellUnknown)]
    [InlineData(ProtocolObservationStatus.Contradictory, GuiSafetyReason.ProtocolContradiction)]
    [InlineData(ProtocolObservationStatus.TranslationBlocked, GuiSafetyReason.ProtocolContradiction)]
    public void IncompleteOrBlockedProtocolSnapshotsAreRejected(ProtocolObservationStatus status, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiWorldSnapshot world = fixture.World with { ProtocolObservationStatus = status };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { Start = world, End = world });

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void ClosedTranslationLatchBlocksEvenInGuiClientInputMode()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with { TranslationLatchClosedDuringWindow = true };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.ProtocolContradiction, decision.Reason);
        Assert.Null(typeof(GuiProtocolTrustEvidence).GetProperty(nameof(GameInputMode)));
    }

    [Fact]
    public void UnknownMessageBlocks()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with { UnknownMessageObservedDuringWindow = true };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.ProtocolContradiction, decision.Reason);
    }

    [Fact]
    public void KnownNonPositionTrafficCanPassOnlyWhileTheTranslationLatchStaysOpen()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with
        {
            UnknownMessageObservedDuringWindow = false,
            TranslationLatchClosedDuringWindow = false,
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData("ClientGameplay", GuiSafetyReason.UserInterrupted)]
    [InlineData("HumanInput", GuiSafetyReason.UserInterrupted)]
    public void ConcurrentOrUnattributedInputInterrupts(string source, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = source switch
        {
            "ClientGameplay" => fixture.CreateEvidence() with { ClientGameplayObservedDuringWindow = true },
            "HumanInput" => fixture.CreateEvidence() with { HumanInputAmbiguityObserved = true },
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Theory]
    [InlineData("Disconnected", GuiSafetyReason.Disconnected)]
    [InlineData("Paused", GuiSafetyReason.UserInterrupted)]
    [InlineData("Fight", GuiSafetyReason.InFight)]
    [InlineData("Walking", GuiSafetyReason.Busy)]
    [InlineData("Using", GuiSafetyReason.Busy)]
    [InlineData("Dialog", GuiSafetyReason.Busy)]
    [InlineData("AmbiguousMovement", GuiSafetyReason.PositionAmbiguous)]
    public void UnsafeCharacterStateBlocks(string state, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiWorldSnapshot world = Change(fixture.World, state);

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { Start = world, End = world });

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void SnapshotMustBeFreshAndNotFromTheFuture()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiWorldSnapshot stale = fixture.CreateWorld(capturedAt: fixture.Now.AddSeconds(-6));
        GuiProtocolTrustDecision staleDecision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { Start = stale, End = stale });
        GuiWorldSnapshot future = fixture.CreateWorld(capturedAt: fixture.Now.AddSeconds(1));
        GuiProtocolTrustDecision futureDecision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { Start = future, End = future });

        Assert.False(staleDecision.Allowed);
        Assert.Equal(GuiSafetyReason.StateChanged, staleDecision.Reason);
        Assert.False(futureDecision.Allowed);
        Assert.Equal(GuiSafetyReason.StateChanged, futureDecision.Reason);
    }

    [Fact]
    public void StartSnapshotCannotBeNewerThanEndSnapshot()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiWorldSnapshot start = fixture.CreateWorld(capturedAt: fixture.Now.AddSeconds(1));

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { Start = start });

        Assert.False(decision.Allowed);
        Assert.Equal(GuiSafetyReason.StateChanged, decision.Reason);
    }

    [Theory]
    [InlineData("Unverified", GuiSafetyReason.WindowIdentityUnverified)]
    [InlineData("Unfocused", GuiSafetyReason.WindowNotActive)]
    [InlineData("GeometryChanged", GuiSafetyReason.WindowChanged)]
    public void WindowMustRemainVerifiedActiveAndStable(string change, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GameWindowSnapshot start = change switch
        {
            "Unverified" => fixture.CreateWindow(fixture.Identity with { Status = GameWindowIdentityStatus.UnverifiedExecutableUnreadable }),
            "Unfocused" => fixture.Window with { IsFocused = false },
            "GeometryChanged" => fixture.Window with { ClientWidth = fixture.Window.ClientWidth - 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(fixture.CreateEvidence() with { WindowStart = start });

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Theory]
    [InlineData("Stale", GuiSafetyReason.CalibrationStale)]
    [InlineData("UnboundMap", GuiSafetyReason.CalibrationStale)]
    [InlineData("UnboundWindow", GuiSafetyReason.CalibrationStale)]
    [InlineData("UnboundFrame", GuiSafetyReason.CalibrationStale)]
    [InlineData("UnboundIdentity", GuiSafetyReason.CalibrationStale)]
    [InlineData("SyntheticProduction", GuiSafetyReason.CalibrationStale)]
    public void CalibrationMustBeFreshBoundAndRealOutsideTests(string problem, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        CalibrationResult calibration = problem switch
        {
            "Stale" => fixture.CreateCalibration(CalibrationStatus.Stale),
            "UnboundMap" => fixture.CreateCalibration(mapId: GuiProtocolTrustTestFixture.MapId + 1),
            "UnboundWindow" => fixture.CreateCalibration(windowIdentity: fixture.Identity with { Xid = fixture.Identity.Xid + 1 }),
            "UnboundFrame" => fixture.CreateCalibration(captureWidth: fixture.Frame.PixelWidth - 1),
            "UnboundIdentity" => fixture.Calibration,
            "SyntheticProduction" => fixture.Calibration,
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
        GuiProtocolTrustEvidence evidence = (problem == "SyntheticProduction"
            ? fixture.CreateEvidence(allowSyntheticCalibration: false)
            : fixture.CreateEvidence()) with
        {
            Calibration = calibration,
            Intent = problem == "UnboundIdentity"
                ? fixture.Intent with { CalibrationIdentity = "other-calibration" }
                : fixture.Intent,
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Theory]
    [InlineData("Unverified", GuiSafetyReason.TargetUnverified)]
    [InlineData("Ambiguous", GuiSafetyReason.AmbiguousTarget)]
    [InlineData("Map", GuiSafetyReason.TargetUnverified)]
    [InlineData("Cell", GuiSafetyReason.CellUnknown)]
    [InlineData("Capture", GuiSafetyReason.TargetUnverified)]
    [InlineData("Calibration", GuiSafetyReason.TargetUnverified)]
    [InlineData("Frame", GuiSafetyReason.TargetUnverified)]
    [InlineData("Point", GuiSafetyReason.TargetUnverified)]
    [InlineData("OutsidePoint", GuiSafetyReason.TargetUnverified)]
    public void TargetMustBeVerifiedAndBoundToTheCurrentScene(string problem, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        TargetRegion target = problem switch
        {
            "Unverified" => fixture.CreateTarget(TargetStatus.Unverified),
            "Ambiguous" => fixture.CreateTarget(TargetStatus.Ambiguous),
            "Map" => fixture.CreateTarget(mapId: GuiProtocolTrustTestFixture.MapId + 1),
            "Cell" => fixture.CreateTarget(targetCell: GuiProtocolTrustTestFixture.TargetCell + 1),
            "Capture" => fixture.CreateTarget(captureId: Guid.NewGuid()),
            "Calibration" => fixture.CreateTarget(calibrationId: "other-calibration"),
            "Frame" => fixture.CreateTarget(frameWidth: fixture.Frame.PixelWidth - 1),
            "Point" => fixture.CreateTarget(hasPoint: false),
            "OutsidePoint" => fixture.CreateTarget(point: new WindowLocalPixels(0, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
        GuiProtocolTrustEvidence evidence = fixture.CreateEvidence() with { Target = target };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Theory]
    [InlineData("Unreachable", GuiSafetyReason.PositionUntrusted)]
    [InlineData("WrongStart", GuiSafetyReason.PositionUntrusted)]
    [InlineData("WrongTarget", GuiSafetyReason.CellUnknown)]
    [InlineData("MissingStart", GuiSafetyReason.PositionUntrusted)]
    [InlineData("MissingTarget", GuiSafetyReason.CellUnknown)]
    public void PlannerEvidenceMustBeReachableAndMatchTheIntent(string problem, GuiSafetyReason expectedReason)
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence evidence = problem switch
        {
            "Unreachable" => fixture.CreateEvidence() with { PlanReachable = false },
            "WrongStart" => fixture.CreateEvidence() with { PlanStartCell = GuiProtocolTrustTestFixture.CurrentCell + 1 },
            "WrongTarget" => fixture.CreateEvidence() with { PlanTargetCell = GuiProtocolTrustTestFixture.TargetCell + 1 },
            "MissingStart" => fixture.CreateEvidence() with { PlanStartCell = null },
            "MissingTarget" => fixture.CreateEvidence() with { PlanTargetCell = null },
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };

        GuiProtocolTrustDecision decision = fixture.CreatePolicy().Evaluate(evidence);

        Assert.False(decision.Allowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void ExpiredIntentAndOldSessionAreRejected()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        GuiProtocolTrustEvidence expired = fixture.CreateEvidence() with
        {
            Intent = fixture.Intent with { ExpiresAt = fixture.Now },
        };
        GuiProtocolTrustEvidence oldSession = fixture.CreateEvidence() with
        {
            Intent = fixture.Intent with { SessionEpoch = fixture.Intent.SessionEpoch - 1 },
        };

        GuiProtocolTrustDecision expiredDecision = fixture.CreatePolicy().Evaluate(expired);
        GuiProtocolTrustDecision oldSessionDecision = fixture.CreatePolicy().Evaluate(oldSession);

        Assert.False(expiredDecision.Allowed);
        Assert.Equal(GuiSafetyReason.IntentExpired, expiredDecision.Reason);
        Assert.False(oldSessionDecision.Allowed);
        Assert.Equal(GuiSafetyReason.StateChanged, oldSessionDecision.Reason);
    }

    private static GuiWorldSnapshot Change(GuiWorldSnapshot world, string change)
    {
        return change switch
        {
            "SessionEpoch" => world with { SessionEpoch = world.SessionEpoch + 1 },
            "StateRevision" => world with { StateRevision = world.StateRevision + 1 },
            "Map" => world with { MapId = world.MapId + 1 },
            "Cell" => world with { CellId = new LogicalCell(world.CellId!.Value.Value + 1) },
            "Provenance" => world with { CellProvenance = GuiCellProvenance.ServerConfirmed },
            "Connected" => world with { IsConnected = false },
            "Fight" => world with { IsFighting = true },
            "Walking" => world with { IsWalking = true },
            "Using" => world with { IsUsing = true },
            "Dialog" => world with { IsInDialog = true },
            "AmbiguousMovement" => world with { HasAmbiguousMovement = true },
            "ProtocolStatus" => world with { ProtocolObservationStatus = ProtocolObservationStatus.Partial },
            "UserInterruption" => world with { IsUserInterrupted = true },
            "KnownElements" => world with
            {
                KnownElements = ImmutableArray.Create(world.KnownElements[0] with { ElementId = world.KnownElements[0].ElementId + 1 }),
            },
            "Disconnected" => world with { IsConnected = false },
            "Paused" => world with { IsUserInterrupted = true },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
    }
}
