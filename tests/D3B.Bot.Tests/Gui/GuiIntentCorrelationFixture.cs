using System.Collections.Immutable;
using D3B.Bot.Gui;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiIntentCorrelationFixture
{
    public DateTimeOffset Now { get; } = new(2035, 4, 5, 6, 7, 8, TimeSpan.Zero);
    public ManualGuiTimeProvider Time { get; }
    public GameWindowIdentity Identity { get; }
    public GameWindowSnapshot Window { get; }
    public GuiWorldSnapshot World { get; }
    public GuiActionIntent Intent { get; }

    public GuiIntentCorrelationFixture()
    {
        Time = new ManualGuiTimeProvider(Now);
        Identity = new GameWindowIdentity(451, 9001, "/synthetic/dofus", "synthetic", GameWindowIdentityStatus.Verified);
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
            new DesktopPixels(0, 0),
            800,
            600,
            0,
            800,
            600,
            null,
            Now);
        World = new GuiWorldSnapshot(
            3,
            5,
            Now,
            100,
            new LogicalCell(30),
            GuiCellProvenance.ServerConfirmed,
            true,
            false,
            false,
            false,
            false,
            false,
            ImmutableArray<GuiKnownElement>.Empty,
            ProtocolObservationStatus.Complete);
        Intent = CreateIntent();
    }

    public GuiActionIntent CreateIntent(Guid? intentId = null, DateTimeOffset? createdAt = null, DateTimeOffset? expiresAt = null)
    {
        return new GuiActionIntent(
            intentId ?? Guid.NewGuid(),
            GuiActionKind.WalkToCell,
            3,
            5,
            100,
            new LogicalCell(31),
            null,
            Identity,
            "calibration-id",
            GuiClientAction.Walk,
            GuiServerOutcome.MovementConfirmed,
            createdAt ?? Now.AddSeconds(-1),
            expiresAt ?? Now.AddMinutes(1));
    }

    public GuiClientRequestObservation MatchingRequest(bool potentiallyConcurrentHumanInput = false)
    {
        return new GuiClientRequestObservation(
            Intent.SessionEpoch,
            Intent.MapId,
            GuiClientAction.Walk,
            Intent.TargetCellId,
            null,
            potentiallyConcurrentHumanInput);
    }

    public GuiServerOutcomeObservation MatchingOutcome()
    {
        return new GuiServerOutcomeObservation(
            Intent.SessionEpoch,
            Intent.MapId,
            GuiServerOutcome.MovementConfirmed,
            Intent.TargetCellId,
            null);
    }
}
