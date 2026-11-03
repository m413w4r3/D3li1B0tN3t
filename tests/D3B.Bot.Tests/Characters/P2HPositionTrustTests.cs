using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Characters;

public sealed class P2HPositionTrustTests
{
    private static CharacterState Chosen()
    {
        return CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);
    }

    private static CharacterState OnMap(int cell)
    {
        var current = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.MapId });

        return CharacterEvents.Apply(current, RelayDirection.FromServer, TestPlay.Information(cell));
    }

    [Fact]
    public void AServerPlacementIsTrusted()
    {
        var placed = OnMap(300);

        Assert.Equal(CellProvenance.ServerPlaced, placed.Provenance);
        Assert.True(placed.IsPositionTrusted);
    }

    [Fact]
    public void AnAnnouncedMovementDoesNotConfirmItsDestination()
    {
        var placed = OnMap(300);
        var announced = CharacterEvents.Apply(placed, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });

        Assert.Equal(300, announced.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, announced.Provenance);
        Assert.True(announced.IsPositionTrusted);
    }

    [Fact]
    public void AServerConfirmationTrustsTheWalkDestination()
    {
        var announced = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });
        var confirmed = CharacterEvents.Apply(announced, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Equal(302, confirmed.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, confirmed.Provenance);
        Assert.True(confirmed.IsPositionTrusted);
    }

    [Fact]
    public void AValidRefusalTrustsTheServerNamedCell()
    {
        var point = MapGeometry.PointOf(311);

        var refused = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        Assert.Equal(311, refused.Cell);
        Assert.Equal(CellProvenance.ServerRefused, refused.Provenance);
        Assert.True(refused.IsPositionTrusted);
    }

    [Fact]
    public void AnInvalidRefusalClearsTheCellAndItsTrust()
    {
        var refused = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapMovementRefusedEvent { X = int.MaxValue, Y = int.MinValue });

        Assert.Null(refused.Cell);
        Assert.Equal(CellProvenance.Unknown, refused.Provenance);
        Assert.False(refused.IsPositionTrusted);
    }

    [Fact]
    public async Task AClientCancelIsUntrustedAndAutomatedMovementSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(368);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 368, 369 } });
        connection.Client(new MapMovementCancelRequest { CellId = 368 });
        await bot.ProcessedAsync();

        Assert.Equal(CellProvenance.ClientReported, bot.State.Provenance);
        Assert.False(bot.State.IsPositionTrusted);
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(434, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public void ASettledWalkIsEstimatedHeldAndUntrusted()
    {
        var announced = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });

        var settled = CharacterEvents.NotConfirmed(announced);

        Assert.Equal(302, settled.Cell);
        Assert.Equal(CellProvenance.Estimated, settled.Provenance);
        Assert.True(settled.IsWalkHeld);
        Assert.False(settled.IsPositionTrusted);
    }

    [Fact]
    public void AMapChangeClearsProvenanceUntilTheNewMapPlacesTheCharacter()
    {
        var changed = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var placed = CharacterEvents.Apply(changed, RelayDirection.FromServer, TestPlay.Information(350, TestPlay.NorthMapId));

        Assert.Null(changed.Cell);
        Assert.Equal(CellProvenance.Unknown, changed.Provenance);
        Assert.False(changed.IsPositionTrusted);
        Assert.Equal(350, placed.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, placed.Provenance);
        Assert.True(placed.IsPositionTrusted);
    }

    [Fact]
    public void AFightAndRoleplayReturnNeedFreshMapInformationToTrustTheCell()
    {
        var fighting = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        var roleplay = CharacterEvents.Apply(fighting, RelayDirection.FromServer, new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        var current = CharacterEvents.Apply(roleplay, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.MapId });
        var placed = CharacterEvents.Apply(current, RelayDirection.FromServer, TestPlay.Information(300));

        Assert.True(fighting.IsFighting);
        Assert.Equal(CellProvenance.Unknown, fighting.Provenance);
        Assert.False(fighting.IsPositionTrusted);
        Assert.False(roleplay.IsFighting);
        Assert.Equal(CellProvenance.Unknown, roleplay.Provenance);
        Assert.False(roleplay.IsPositionTrusted);
        Assert.True(placed.IsPositionTrusted);
    }

    [Fact]
    public async Task AReconnectStartsUnknownAndNavigationSendsNothingUntilPlacement()
    {
        await using var play = new TestPlay();
        var (first, bot) = await play.EnterAsync(300);

        Assert.True(bot.State.IsPositionTrusted);
        first.Close();
        await bot.ProcessedAsync();
        Assert.True(bot.State.IsPositionTrusted);

        var next = play.Connect(TestPlay.Account);
        next.Server(TestPlay.Chosen());
        await bot.ProcessedAsync();

        Assert.Equal(CellProvenance.Unknown, bot.State.Provenance);
        Assert.False(bot.State.IsPositionTrusted);
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(388, TestContext.Current.CancellationToken));
        Assert.Empty(next.Sent);
    }

    [Fact]
    public void ALateConfirmationOnlyTrustsAnEstimatedHeldCell()
    {
        var announced = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });
        var held = CharacterEvents.NotConfirmed(announced);
        var late = CharacterEvents.Apply(held, RelayDirection.FromServer, new MapMovementConfirmResponse());
        var standing = OnMap(350);
        var unchanged = CharacterEvents.Apply(standing, RelayDirection.FromServer, new MapMovementConfirmResponse());
        var unknown = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Equal(302, late.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, late.Provenance);
        Assert.False(late.IsWalkHeld);
        Assert.True(late.IsPositionTrusted);
        Assert.Equal(350, unchanged.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, unchanged.Provenance);
        Assert.Null(unknown.Cell);
        Assert.Equal(CellProvenance.Unknown, unknown.Provenance);
    }

    [Fact]
    public void ContradictoryEventsAfterAMapChangeCannotRestoreTrustOrACell()
    {
        var changed = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var oldInformation = CharacterEvents.Apply(changed, RelayDirection.FromServer, TestPlay.Information(300, TestPlay.MapId));
        var oldMovement = CharacterEvents.Apply(oldInformation, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 302 } });
        var confirmed = CharacterEvents.Apply(oldMovement, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Null(changed.Cell);
        Assert.Equal(CellProvenance.Unknown, changed.Provenance);
        Assert.Same(changed, oldInformation);
        Assert.Null(oldMovement.Cell);
        Assert.Empty(oldMovement.Path);
        Assert.Null(confirmed.Cell);
        Assert.Equal(CellProvenance.Unknown, confirmed.Provenance);
        Assert.False(confirmed.IsPositionTrusted);
    }

    [Fact]
    public async Task P2GCancelThenManualConfirmationAllowsNavigationFromTheConfirmedCell()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(368);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 368, 369 } });
        connection.Client(new MapMovementCancelRequest { CellId = 368 });
        await bot.ProcessedAsync();

        Assert.Equal(CellProvenance.ClientReported, bot.State.Provenance);
        Assert.False(bot.State.IsPositionTrusted);
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(434, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 368, 434, out var manualPath));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { manualPath.Cells.ToArray() } });
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        Assert.Equal(434, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, bot.State.Provenance);
        Assert.True(bot.State.IsPositionTrusted);

        var walk = bot.WalkAsync(433, TestContext.Current.CancellationToken);
        var request = Assert.IsType<MapMovementRequest>(await connection.SentAsync(1));
        Assert.Equal(TestPlay.MapId, request.MapId);
        Assert.Equal(434, request.KeyMovements[0] & 0xFFF);
        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 434, 433, out var automaticPath));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { automaticPath.Cells.ToArray() } });
        connection.Response(new MapMovementConfirmResponse());

        Assert.Equal(WalkOutcome.Arrived, await walk);
        Assert.Equal(433, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerConfirmed, bot.State.Provenance);
    }
}
