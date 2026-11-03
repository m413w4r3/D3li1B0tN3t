using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Movement;

public sealed class MovementGuardTests
{
    private static CharacterState Trusted()
    {
        return new CharacterState
        {
            Key = TestPlay.Key,
            Name = "Character",
            MapId = TestPlay.MapId,
            Cell = 300,
            Provenance = CellProvenance.ServerPlaced,
        };
    }

    [Fact]
    public void TheGuardReturnsTheSpecificReasonForEveryUnsafeState()
    {
        var trusted = Trusted();

        Assert.Equal(MovementRefusal.Disconnected, MovementGuard.Refuse(trusted, connected: false));
        Assert.Equal(MovementRefusal.MapUnknown, MovementGuard.Refuse(trusted with { MapId = null }, connected: true));
        Assert.Equal(MovementRefusal.CellUnknown, MovementGuard.Refuse(trusted with { Cell = null }, connected: true));
        Assert.Equal(MovementRefusal.Walking, MovementGuard.Refuse(trusted with { Path = [300, 301] }, connected: true));
        Assert.Equal(MovementRefusal.WalkHeld, MovementGuard.Refuse(trusted with { IsWalkHeld = true, Provenance = CellProvenance.Estimated }, connected: true));
        Assert.Equal(MovementRefusal.Fighting, MovementGuard.Refuse(trusted with { Fight = new FightState() }, connected: true));
        Assert.Equal(MovementRefusal.Busy, MovementGuard.Refuse(trusted with { Use = new CurrentUse(1, 2) }, connected: true));
        Assert.Equal(MovementRefusal.Busy, MovementGuard.Refuse(trusted with { Dialog = new NpcDialog(-1) }, connected: true));
        Assert.Equal(MovementRefusal.ClientReported, MovementGuard.Refuse(trusted with { Provenance = CellProvenance.ClientReported }, connected: true));
        Assert.Equal(MovementRefusal.Estimated, MovementGuard.Refuse(trusted with { Provenance = CellProvenance.Estimated }, connected: true));
        Assert.Equal(MovementRefusal.ProvenanceUnknown, MovementGuard.Refuse(trusted with { Provenance = CellProvenance.Unknown }, connected: true));
    }

    [Fact]
    public void EveryServerDerivedPositionPassesTheGuard()
    {
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(Trusted(), connected: true));
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(Trusted() with { Provenance = CellProvenance.ServerConfirmed }, connected: true));
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(Trusted() with { Provenance = CellProvenance.ServerRefused }, connected: true));
    }
}
