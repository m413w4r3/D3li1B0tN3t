using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Characters;

public sealed class CharacterPositionReliabilityTests
{
    private static CharacterState Chosen()
    {
        return CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);
    }

    private static CharacterState OnMap(int cell)
    {
        var state = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.MapId });

        return CharacterEvents.Apply(state, RelayDirection.FromServer, TestPlay.Information(cell));
    }

    [Fact]
    public void ACurrentMapClearsMapStateButKeepsTheLastWalkEnd()
    {
        var state = Chosen() with
        {
            MapId = TestPlay.MapId,
            SubAreaId = TestPlay.SubAreaId,
            Cell = 300,
            Path = [300, 302],
            LastWalkEnd = 302,
            IsWalkHeld = true,
            Elements = ImmutableSortedDictionary<int, MapElement>.Empty.Add(1, new MapElement(1, 2, [], [])),
            Npcs = ImmutableSortedDictionary<int, MapNpc>.Empty.Add(-1, new MapNpc(-1, 10, 101)),
            MonsterGroups = ImmutableSortedDictionary<long, MapMonsterGroup>.Empty.Add(-2, new MapMonsterGroup(-2, 102, [])),
            Use = new CurrentUse(1, 2),
            Dialog = new NpcDialog(-1),
            Storage = new OpenStorage(10),
            ZaapDialog = new ZaapDialog(TeleporterType.Zaap, ImmutableSortedDictionary<long, int>.Empty),
            HavenBagOwnerId = TestPlay.CharacterId,
        };

        var moved = CharacterEvents.Apply(state, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });

        Assert.Equal(TestPlay.NorthMapId, moved.MapId);
        Assert.Null(moved.SubAreaId);
        Assert.Null(moved.Cell);
        Assert.Empty(moved.Path);
        Assert.False(moved.IsWalkHeld);
        Assert.Equal(302, moved.LastWalkEnd);
        Assert.Empty(moved.Elements);
        Assert.Empty(moved.Npcs);
        Assert.Empty(moved.MonsterGroups);
        Assert.Null(moved.Use);
        Assert.Null(moved.Dialog);
        Assert.Null(moved.Storage);
        Assert.Null(moved.ZaapDialog);
        Assert.Null(moved.HavenBagOwnerId);
    }

    [Fact]
    public void ARepeatedCurrentMapClearsItsPreviouslyPlacedCell()
    {
        var standing = OnMap(300);

        var duplicate = CharacterEvents.Apply(standing, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.MapId });

        Assert.Equal(TestPlay.MapId, duplicate.MapId);
        Assert.Null(duplicate.Cell);
        Assert.Empty(duplicate.Path);
    }

    [Fact]
    public void AnInvalidSelfPlacementClearsThePreviousCell()
    {
        var standing = OnMap(300);
        var invalid = TestPlay.Information(MapData.CellCount);

        var placed = CharacterEvents.Apply(standing, RelayDirection.FromServer, invalid);

        Assert.Null(placed.Cell);
    }

    [Fact]
    public void AnUnmappableRefusalClearsThePreviousCell()
    {
        var standing = OnMap(300) with { PendingMovementMapId = TestPlay.MapId };

        var refused = CharacterEvents.Apply(standing, RelayDirection.FromServer, new MapMovementRefusedEvent { X = int.MaxValue, Y = int.MinValue });

        Assert.Null(refused.Cell);
        Assert.Empty(refused.Path);
    }

    [Fact]
    public void ADelayedCancelAfterMapChangeCannotPlaceTheCharacter()
    {
        var walking = CharacterEvents.Apply(OnMap(300) with { PendingMovementMapId = TestPlay.MapId }, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        var current = CharacterEvents.Apply(walking, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });

        var canceled = CharacterEvents.Apply(current, RelayDirection.FromClient, new MapMovementCancelRequest { CellId = 301 });

        Assert.Null(canceled.Cell);
        Assert.Empty(canceled.Path);
    }

    [Fact]
    public void ADelayedRefusalAfterMapChangeCannotPlaceTheCharacter()
    {
        var walking = CharacterEvents.Apply(OnMap(300) with { PendingMovementMapId = TestPlay.MapId }, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        var current = CharacterEvents.Apply(walking, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var point = MapGeometry.PointOf(301);

        var refused = CharacterEvents.Apply(current, RelayDirection.FromServer, new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        Assert.Same(current, refused);
        Assert.Null(refused.Cell);
    }

    [Fact]
    public void MovementCannotBeAcceptedUntilTheCurrentMapPlacesTheCharacter()
    {
        var current = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var announced = CharacterEvents.Apply(current with { PendingMovementMapId = TestPlay.NorthMapId }, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });
        var confirmed = CharacterEvents.Apply(announced, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Null(announced.Cell);
        Assert.Empty(announced.Path);
        Assert.Null(confirmed.Cell);
    }

    [Fact]
    public void AConfirmationWithoutAWalkOnlyReleasesTheHeldState()
    {
        var current = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var held = current with { IsWalkHeld = true };

        var confirmed = CharacterEvents.Apply(held, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Null(confirmed.Cell);
        Assert.False(confirmed.IsWalkHeld);
    }

    [Fact]
    public void OldMapInformationMovementAndConfirmationCannotRestoreItsCell()
    {
        var standing = OnMap(300) with { PendingMovementMapId = TestPlay.MapId };
        var walking = CharacterEvents.Apply(standing, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 302 } });
        var changed = CharacterEvents.Apply(walking, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var oldInformation = CharacterEvents.Apply(changed, RelayDirection.FromServer, TestPlay.Information(300, TestPlay.MapId));
        var oldMovement = CharacterEvents.Apply(oldInformation with { PendingMovementMapId = TestPlay.NorthMapId }, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 302 } });
        var confirmed = CharacterEvents.Apply(oldMovement, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Same(changed, oldInformation);
        Assert.Null(oldMovement.Cell);
        Assert.Empty(oldMovement.Path);
        Assert.Null(confirmed.Cell);
        Assert.Equal(302, confirmed.LastWalkEnd);
    }

    [Fact]
    public void AStaleMovementStartingElsewhereCannotReplaceANewMapPlacement()
    {
        var changed = CharacterEvents.Apply(OnMap(300), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var placed = CharacterEvents.Apply(changed, RelayDirection.FromServer, TestPlay.Information(350, TestPlay.NorthMapId));
        var staleMovement = CharacterEvents.Apply(placed with { PendingMovementMapId = TestPlay.NorthMapId }, RelayDirection.FromServer, new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 302 } });
        var confirmed = CharacterEvents.Apply(staleMovement, RelayDirection.FromServer, new MapMovementConfirmResponse());

        Assert.Equal(350, staleMovement.Cell);
        Assert.Empty(staleMovement.Path);
        Assert.Equal(350, confirmed.Cell);
    }

    [Fact]
    public void AReconnectedCharacterNeedsFreshMapAndPlacementEvents()
    {
        var character = TestPlay.Chosen().Success.Character;
        var prior = OnMap(300);
        var reconnected = CharacterState.Chosen(TestPlay.Key, character);
        var earlyInformation = CharacterEvents.Apply(reconnected, RelayDirection.FromServer, TestPlay.Information(302, TestPlay.NorthMapId));
        var earlyConfirmation = CharacterEvents.Apply(earlyInformation, RelayDirection.FromServer, new MapMovementConfirmResponse());
        var current = CharacterEvents.Apply(earlyConfirmation, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        var placed = CharacterEvents.Apply(current, RelayDirection.FromServer, TestPlay.Information(302, TestPlay.NorthMapId));

        Assert.Equal(TestPlay.MapId, prior.MapId);
        Assert.Null(reconnected.MapId);
        Assert.Null(reconnected.Cell);
        Assert.Same(reconnected, earlyInformation);
        Assert.Null(earlyConfirmation.Cell);
        Assert.Null(current.Cell);
        Assert.Equal(TestPlay.NorthMapId, placed.MapId);
        Assert.Equal(302, placed.Cell);
    }

    [Fact]
    public void MapInformationWithUnknownFieldsStillPlacesTheCharacter()
    {
        var information = TestPlay.Information(300);
        var withUnknownField = MapInformationEvent.Parser.ParseFrom([.. information.ToByteArray(), 0x98, 0x06, 0x01]);
        var current = CharacterEvents.Apply(Chosen(), RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.MapId });

        var placed = CharacterEvents.Apply(current, RelayDirection.FromServer, withUnknownField);

        Assert.Equal(TestPlay.MapId, placed.MapId);
        Assert.Equal(300, placed.Cell);
    }

    [Fact]
    public async Task NavigationWithoutAPlacedPositionIsBusyAndSendsNothing()
    {
        await using var play = new TestPlay();
        var connection = play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen());
        Assert.True(play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();

        Assert.Null(bot.State.MapId);
        Assert.Null(bot.State.Cell);
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(388, TestContext.Current.CancellationToken));
        Assert.Equal(TravelOutcome.Busy, await bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }
}
