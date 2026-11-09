using D3B.Gui.Input;

namespace D3B.Gui.Tests.Input;

public sealed class GuiClickLeaseTests
{
    [Fact]
    public void RevocationBeforeTheLaunchPointRefusesTheClick()
    {
        GuiClickLease lease = new();

        lease.Cancel();

        Assert.True(lease.IsCancelled);
        Assert.False(lease.LaunchStarted);
        Assert.False(lease.TryBeginClick());
    }

    [Fact]
    public void LaunchPointWinsBeforeLaterRevocationAndCannotBeClaimedTwice()
    {
        GuiClickLease lease = new();

        Assert.True(lease.TryBeginClick());
        lease.Cancel();

        Assert.True(lease.IsCancelled);
        Assert.True(lease.LaunchStarted);
        Assert.False(lease.TryBeginClick());
    }
}
