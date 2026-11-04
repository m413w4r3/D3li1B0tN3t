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
    public void ServerPlacementAndConfirmationPassTheGuard()
    {
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(Trusted(), connected: true));
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(Trusted() with { Provenance = CellProvenance.ServerConfirmed }, connected: true));
    }

    [Fact]
    public void ARefusedPositionHasAnAmbiguousGuardReason()
    {
        Assert.Equal(MovementRefusal.AmbiguousPosition, MovementGuard.Refuse(Trusted() with { Provenance = CellProvenance.ServerRefused }, connected: true));
    }

    [Fact]
    public void AnAmbiguousMovementAnswerOverridesTrustedCellProvenance()
    {
        Assert.Equal(MovementRefusal.AmbiguousPosition, MovementGuard.Refuse(Trusted() with { MovementAnswerAmbiguous = true }, connected: true));
        Assert.Equal(MovementRefusal.AmbiguousPosition, MovementGuard.Refuse(Trusted() with { Provenance = CellProvenance.ServerConfirmed, MovementAnswerAmbiguous = true }, connected: true));
    }

    [Fact]
    public async Task AnExpectedMapMismatchIsLogged()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        var allowed = await bot.RunAsync(
            (context, _) => Task.FromResult(MovementGuard.IsAllowed(context, TestPlay.NorthMapId)),
            TestContext.Current.CancellationToken);

        Assert.False(allowed);
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains("reason MapChanged", StringComparison.Ordinal));
        Assert.Empty(connection.Sent);
    }
}
