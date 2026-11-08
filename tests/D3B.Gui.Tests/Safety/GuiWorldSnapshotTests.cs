using System.Collections.Immutable;
using D3B.Gui.Calibration;
using D3B.Gui.Safety;

namespace D3B.Gui.Tests.Safety;

public sealed class GuiWorldSnapshotTests
{
    [Fact]
    public void SnapshotRetainsItsSessionAndRevisionIdentityWhenCopied()
    {
        var element = new GuiKnownElement(10, new LogicalCell(20), 30, 1, [40, 41]);
        var snapshot = new GuiWorldSnapshot(
            7,
            19,
            DateTimeOffset.UnixEpoch,
            100,
            new LogicalCell(20),
            GuiCellProvenance.ServerConfirmed,
            true,
            false,
            false,
            false,
            false,
            false,
            [element],
            ProtocolObservationStatus.Complete);

        var changed = snapshot with { StateRevision = 20 };
        var enabledSkillIds = element.EnabledSkillIds.Add(42);

        Assert.Equal(7, snapshot.SessionEpoch);
        Assert.Equal(19, snapshot.StateRevision);
        Assert.Equal(7, changed.SessionEpoch);
        Assert.Equal(20, changed.StateRevision);
        Assert.Equal(ImmutableArray.Create(40, 41), element.EnabledSkillIds);
        Assert.Equal(ImmutableArray.Create(40, 41, 42), enabledSkillIds);
        Assert.False(snapshot.IsUserInterrupted);
        Assert.NotSame(snapshot, changed);
    }
}
