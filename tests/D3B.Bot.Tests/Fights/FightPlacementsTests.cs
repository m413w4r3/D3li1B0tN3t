using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Fights;

public sealed class FightPlacementsTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The learner never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private static void FightBegins(TestConnection connection, long fightMapId, int[] red, int[] blue)
    {
        // Measured (D275): the roleplay's context destroyed, the fight's created, its map named, then the fight and its cells.
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = fightMapId });
        connection.Server(new FightStartingEvent { Type = FightType.Monsters });
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { red }, DefenderCells = { blue } } });
    }

    [Fact]
    public void EachSetOfCellsIsCountedApartByTheMapLeftAndTheMapFoughtOn()
    {
        var placements = new FightPlacements();
        var told = new List<FightPlacement>();

        placements.Learned += told.Add;
        placements.Seen(1, 2, [314, 300], [385, 371], null, _start);
        placements.Seen(1, 2, [300, 314, 300], [371, 385], null, _start.AddMinutes(5));
        placements.Seen(1, 3, [300, 314], [371, 385], null, _start.AddMinutes(7));
        placements.Seen(1, 2, [200], [201], null, _start.AddMinutes(9));
        placements.Seen(4, 2, [300], [301], null, _start);

        var known = placements.From(1);

        Assert.Equal(4, placements.Count);
        Assert.Equal(3, known.Count);
        Assert.Equal((2L, 2), (known[0].MapId, known[0].Seen));
        Assert.Equal([300, 314], known[0].RedCells);
        Assert.Equal([371, 385], known[0].BlueCells);
        Assert.Equal((_start, _start.AddMinutes(5)), (known[0].FirstSeen, known[0].LastSeen));
        Assert.Null(known[0].FromSubAreaId);
        Assert.Equal(5, told.Count);
    }

    [Fact]
    public async Task EveryFightBegunTeachesItsCellsTheMapFoughtOnAndWhereTheMapLeftLiesOnce()
    {
        var placements = new FightPlacements();
        var time = new FakeTimeProvider(_start);

        await using var pack = new TestPlay();
        await using var play = new TestPlay([new FightPlacementLearner(placements, pack.World, time)]);
        var (connection, _) = await play.EnterAsync(433);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        FightBegins(connection, TestPlay.NorthMapId, [314, 300], [385, 371, 999]);

        // The server gives the cells again as the fighters place: the same fight, counted once.
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 314, 300 }, DefenderCells = { 385, 371 } } });
        await UntilAsync(() => placements.Count is 1);

        // Over, the roleplay's context back on the map the fight began on, then another fight from it.
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        FightBegins(connection, TestPlay.NorthMapId, [314, 300], [385, 371]);
        await UntilAsync(() => placements.From(TestPlay.MapId) is [{ Seen: 2 }]);

        var placement = Assert.Single(placements.From(TestPlay.MapId));

        Assert.Equal((TestPlay.NorthMapId, TestPlay.SubAreaId, 1, 4, -18), (placement.MapId, placement.FromSubAreaId, placement.FromAreaId, placement.PosX, placement.PosY));
        Assert.Equal([300, 314], placement.RedCells);
        Assert.Equal([371, 385], placement.BlueCells);
    }
}
