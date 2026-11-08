using D3B.Gui.Actions;
using D3B.Gui.Calibration;

namespace D3B.Gui.Tests.Actions;

public sealed class GuiActionIntentTests
{
    [Fact]
    public void IntentionRequiresAnIncreasingValidityWindow()
    {
        using GuiActionTestFixture fixture = new();
        Assert.Throws<ArgumentException>(() => fixture.CreateIntent(expiresAt: fixture.Now.AddSeconds(-1)));
    }

    [Fact]
    public void ElementUseRequiresAnElementIdentifier()
    {
        using GuiActionTestFixture fixture = new();
        Assert.Throws<ArgumentException>(() => new GuiActionIntent(
            Guid.NewGuid(),
            GuiActionKind.UseElement,
            4,
            9,
            GuiActionTestFixture.MapId,
            new LogicalCell(GuiActionTestFixture.TargetCellId),
            null,
            fixture.WindowIdentity,
            fixture.Calibration.Profile!.CalibrationId,
            GuiClientAction.UseElement,
            GuiServerOutcome.ElementUseEnded,
            fixture.Now.AddSeconds(-1),
            fixture.Now.AddMinutes(1)));
    }

    [Fact]
    public void CellMovementRejectsAnElementIdentifier()
    {
        using GuiActionTestFixture fixture = new();
        Assert.Throws<ArgumentException>(() => fixture.CreateIntent(actionKind: GuiActionKind.WalkToCell, targetElementId: GuiActionTestFixture.ElementId));
    }

    [Fact]
    public void IntentionsUseTheNeutralExpectedActionAndOutcomeContracts()
    {
        using GuiActionTestFixture fixture = new();
        GuiActionIntent walk = fixture.Intent;
        GuiActionIntent use = fixture.CreateIntent(actionKind: GuiActionKind.UseElement);

        Assert.Equal(GuiClientAction.Walk, walk.ExpectedClientAction);
        Assert.Equal(GuiServerOutcome.MovementConfirmed, walk.ExpectedServerOutcome);
        Assert.Equal(GuiClientAction.UseElement, use.ExpectedClientAction);
        Assert.Equal(GuiServerOutcome.ElementUseEnded, use.ExpectedServerOutcome);
    }
}
