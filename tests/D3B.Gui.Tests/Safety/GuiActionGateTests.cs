using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Safety;

public sealed class GuiActionGateTests
{
    [Theory]
    [InlineData("Disabled", GuiSafetyReason.Disabled)]
    [InlineData("NotX11", GuiSafetyReason.NotX11)]
    [InlineData("WindowNotFound", GuiSafetyReason.WindowNotFound)]
    [InlineData("WindowIdentityUnverified", GuiSafetyReason.WindowIdentityUnverified)]
    [InlineData("WindowNotActive", GuiSafetyReason.WindowNotActive)]
    [InlineData("WindowChanged", GuiSafetyReason.WindowChanged)]
    [InlineData("WindowChangedAfterCapture", GuiSafetyReason.WindowChanged)]
    [InlineData("WindowDifferentFromIntent", GuiSafetyReason.WindowChanged)]
    [InlineData("CaptureFailed", GuiSafetyReason.CaptureFailed)]
    [InlineData("SceneUnverified", GuiSafetyReason.SceneUnverified)]
    [InlineData("NotCalibrated", GuiSafetyReason.NotCalibrated)]
    [InlineData("CalibrationStale", GuiSafetyReason.CalibrationStale)]
    [InlineData("CalibrationTooOld", GuiSafetyReason.CalibrationStale)]
    [InlineData("CalibrationIdentityChanged", GuiSafetyReason.CalibrationStale)]
    [InlineData("TargetUnverified", GuiSafetyReason.TargetUnverified)]
    [InlineData("TargetIdentityChanged", GuiSafetyReason.TargetUnverified)]
    [InlineData("AmbiguousTarget", GuiSafetyReason.AmbiguousTarget)]
    [InlineData("Disconnected", GuiSafetyReason.Disconnected)]
    [InlineData("MapUnknown", GuiSafetyReason.MapUnknown)]
    [InlineData("UnknownProtocolMap", GuiSafetyReason.MapUnknown)]
    [InlineData("MapDifferent", GuiSafetyReason.MapUnknown)]
    [InlineData("CellUnknown", GuiSafetyReason.CellUnknown)]
    [InlineData("PartialProtocolCell", GuiSafetyReason.CellUnknown)]
    [InlineData("TargetCellChanged", GuiSafetyReason.CellUnknown)]
    [InlineData("PositionUntrusted", GuiSafetyReason.PositionUntrusted)]
    [InlineData("PositionAmbiguous", GuiSafetyReason.PositionAmbiguous)]
    [InlineData("StateChanged", GuiSafetyReason.StateChanged)]
    [InlineData("SessionChanged", GuiSafetyReason.StateChanged)]
    [InlineData("IntentExpired", GuiSafetyReason.IntentExpired)]
    [InlineData("InFight", GuiSafetyReason.InFight)]
    [InlineData("Busy", GuiSafetyReason.Busy)]
    [InlineData("UserInterrupted", GuiSafetyReason.UserInterrupted)]
    [InlineData("ProtocolContradiction", GuiSafetyReason.ProtocolContradiction)]
    [InlineData("TranslationBlocked", GuiSafetyReason.ProtocolContradiction)]
    public void GateReturnsEachSafetyCause(string scenario, GuiSafetyReason expectedReason)
    {
        using GuiActionTestFixture fixture = new();
        ApplyScenario(fixture, scenario);

        GuiReadiness readiness = fixture.CreateGate().Evaluate(
            fixture.Intent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target);

        Assert.False(readiness.Allowed);
        Assert.Equal(expectedReason, readiness.ReasonCode);
        Assert.Equal(Guid.Empty, readiness.AuthorizationId);
    }

    [Fact]
    public void GateAllowsOnlyACompleteAndBoundSnapshot()
    {
        using GuiActionTestFixture fixture = new();

        GuiReadiness readiness = fixture.CreateGate().Evaluate(
            fixture.Intent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target);

        Assert.True(readiness.Allowed);
        Assert.Equal(GuiSafetyReason.Ready, readiness.ReasonCode);
        Assert.NotEqual(Guid.Empty, readiness.AuthorizationId);
        Assert.True(readiness.IsBoundTo(fixture.Intent, fixture.World, fixture.Window, fixture.Calibration, fixture.Target));
    }

    [Fact]
    public void ReadinessCannotTransferAcrossSessionRevisionWindowCalibrationOrTarget()
    {
        using GuiActionTestFixture fixture = new();
        GuiReadiness readiness = fixture.CreateGate().Evaluate(
            fixture.Intent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target);

        Assert.False(readiness.IsBoundTo(fixture.Intent with { SessionEpoch = 5 }, fixture.World, fixture.Window, fixture.Calibration, fixture.Target));
        Assert.False(readiness.IsBoundTo(fixture.Intent with { StateRevision = 10 }, fixture.World, fixture.Window, fixture.Calibration, fixture.Target));
        Assert.False(readiness.IsBoundTo(fixture.Intent, fixture.World with { StateRevision = 10 }, fixture.Window, fixture.Calibration, fixture.Target));

        GameWindowIdentity otherIdentity = new(452, 9002, "/synthetic/other", "other title", GameWindowIdentityStatus.Verified);
        Assert.False(readiness.IsBoundTo(fixture.Intent, fixture.World, fixture.CreateWindow(otherIdentity), fixture.Calibration, fixture.Target));

        CalibrationResult otherCalibration = fixture.CreateCalibration(createdAt: fixture.Now.AddSeconds(-1), mapId: GuiActionTestFixture.MapId);
        Assert.False(readiness.IsBoundTo(fixture.Intent, fixture.World, fixture.Window, otherCalibration, fixture.Target));
        Assert.False(readiness.IsBoundTo(fixture.Intent with { CalibrationIdentity = "other-calibration" }, fixture.World, fixture.Window, fixture.Calibration, fixture.Target));

        TargetRegion otherTarget = fixture.CreateTarget(targetCellId: GuiActionTestFixture.TargetCellId + 1);
        Assert.False(readiness.IsBoundTo(fixture.Intent, fixture.World, fixture.Window, fixture.Calibration, otherTarget));

        TargetRegion otherCapture = fixture.CreateTarget(captureId: Guid.NewGuid());
        Assert.False(readiness.IsBoundTo(fixture.Intent, fixture.World, fixture.Window, fixture.Calibration, otherCapture));
    }

    [Fact]
    public void DisabledOptionsNeverProduceReadiness()
    {
        using GuiActionTestFixture fixture = new();

        GuiReadiness readiness = fixture.CreateGate(new GuiOptions { Enabled = false }).Evaluate(
            fixture.Intent,
            fixture.World,
            fixture.Window,
            fixture.Frame,
            fixture.Calibration,
            fixture.Target);

        Assert.False(readiness.Allowed);
        Assert.Equal(GuiSafetyReason.Disabled, readiness.ReasonCode);
    }

    private static void ApplyScenario(GuiActionTestFixture fixture, string scenario)
    {
        switch (scenario)
        {
            case "Disabled":
                fixture.Options = fixture.Options with { Enabled = false };
                break;
            case "NotX11":
                fixture.Window = fixture.Window with { IsSessionX11 = false };
                break;
            case "WindowNotFound":
                fixture.Window = fixture.Window with
                {
                    Identity = fixture.WindowIdentity with { Status = GameWindowIdentityStatus.NotFound },
                    Exists = false,
                };
                break;
            case "WindowIdentityUnverified":
                GameWindowIdentity unverified = fixture.WindowIdentity with { Status = GameWindowIdentityStatus.UnverifiedExecutableUnreadable };
                fixture.Window = fixture.CreateWindow(unverified);
                break;
            case "WindowNotActive":
                fixture.Window = fixture.Window with { IsActive = false };
                break;
            case "WindowChanged":
                GameWindowSnapshot changedBefore = fixture.Window with { DesktopOrigin = new DesktopPixels(91, 20) };
                fixture.ReplaceFrame(fixture.CreateFrame(before: changedBefore));
                fixture.Target = fixture.CreateTarget();
                break;
            case "WindowChangedAfterCapture":
                GameWindowSnapshot changedAfter = fixture.Window with { ClientWidth = fixture.Window.ClientWidth - 1 };
                fixture.ReplaceFrame(fixture.CreateFrame(after: changedAfter));
                fixture.Target = fixture.CreateTarget();
                break;
            case "WindowDifferentFromIntent":
                fixture.Intent = fixture.Intent with
                {
                    WindowIdentity = fixture.WindowIdentity with { Xid = fixture.WindowIdentity.Xid + 1 },
                };
                break;
            case "CaptureFailed":
                fixture.ReplaceFrame(fixture.CreateFrame(CaptureStatus.Failed, contentUsable: false));
                fixture.Target = fixture.CreateTarget();
                break;
            case "SceneUnverified":
                fixture.ReplaceFrame(fixture.CreateFrame(contentUsable: false));
                fixture.Target = fixture.CreateTarget();
                break;
            case "NotCalibrated":
                fixture.Calibration = fixture.CreateCalibration(CalibrationStatus.NotCalibrated);
                break;
            case "CalibrationStale":
                fixture.Calibration = fixture.CreateCalibration(CalibrationStatus.Stale);
                break;
            case "CalibrationTooOld":
                fixture.Calibration = fixture.CreateCalibration(createdAt: fixture.Now.AddSeconds(-31));
                fixture.Target = fixture.CreateTarget(calibrationId: fixture.Calibration.Profile!.CalibrationId);
                fixture.Intent = fixture.Intent with { CalibrationIdentity = fixture.Calibration.Profile.CalibrationId };
                break;
            case "CalibrationIdentityChanged":
                fixture.Intent = fixture.Intent with { CalibrationIdentity = "another-profile" };
                break;
            case "TargetUnverified":
                fixture.Target = fixture.CreateTarget(TargetStatus.Unverified);
                break;
            case "TargetIdentityChanged":
                fixture.Target = fixture.CreateTarget(calibrationId: "another-profile");
                break;
            case "AmbiguousTarget":
                fixture.Target = fixture.CreateTarget(TargetStatus.Ambiguous);
                break;
            case "Disconnected":
                fixture.World = fixture.CreateWorld(isConnected: false);
                break;
            case "MapUnknown":
                fixture.World = fixture.CreateWorld(mapId: null);
                break;
            case "UnknownProtocolMap":
                fixture.World = fixture.World with { ProtocolObservationStatus = ProtocolObservationStatus.Unknown };
                break;
            case "MapDifferent":
                fixture.World = fixture.CreateWorld(mapId: GuiActionTestFixture.MapId + 1);
                break;
            case "CellUnknown":
                fixture.World = fixture.CreateWorld(cellId: null, provenance: null);
                break;
            case "PartialProtocolCell":
                fixture.World = fixture.World with { ProtocolObservationStatus = ProtocolObservationStatus.Partial };
                break;
            case "TargetCellChanged":
                fixture.Target = fixture.CreateTarget(targetCellId: GuiActionTestFixture.TargetCellId + 1);
                break;
            case "PositionUntrusted":
                fixture.World = fixture.CreateWorld(provenance: GuiCellProvenance.ClientReported);
                break;
            case "PositionAmbiguous":
                fixture.World = fixture.World with { HasAmbiguousMovement = true };
                break;
            case "StateChanged":
                fixture.Intent = fixture.Intent with { StateRevision = fixture.Intent.StateRevision + 1 };
                break;
            case "SessionChanged":
                fixture.Intent = fixture.Intent with { SessionEpoch = fixture.Intent.SessionEpoch + 1 };
                break;
            case "IntentExpired":
                fixture.Intent = fixture.Intent with { ExpiresAt = fixture.Now };
                break;
            case "InFight":
                fixture.World = fixture.World with { IsFighting = true };
                break;
            case "Busy":
                fixture.World = fixture.World with { IsWalking = true };
                break;
            case "UserInterrupted":
                fixture.World = fixture.World with { IsUserInterrupted = true };
                break;
            case "ProtocolContradiction":
                fixture.World = fixture.World with { ProtocolObservationStatus = ProtocolObservationStatus.Contradictory };
                break;
            case "TranslationBlocked":
                fixture.World = fixture.World with { ProtocolObservationStatus = ProtocolObservationStatus.TranslationBlocked };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown gate scenario.");
        }
    }
}
