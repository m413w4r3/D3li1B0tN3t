using D3B.Bot.Characters;
using D3B.Bot.Harvesting;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestApproachTests
{
    [Fact]
    public async Task AFarPositionPlansAPathToAnEligibleUseCell()
    {
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(0);
        var map = play.Maps.Get(TestPlay.MapId);

        var approach = HarvestApproachPlanner.Plan(bot.State, map, TestPlay.TreeElementId);
        var useCells = ElementCells.UsedFrom(map.Cells.Span, TestPlay.TreeCell, map.Cells.Span[0].Zone);

        Assert.Equal("CONFIRMED", approach.Status);
        Assert.Equal("CONFIRMED", approach.Reason);
        Assert.Equal(TestPlay.TreeCell, approach.ElementCell);
        Assert.Contains(approach.UseCell, useCells);
        Assert.NotEqual(TestPlay.TreeCell, approach.UseCell);
        Assert.Equal(0, approach.Path[0]);
        Assert.Equal(approach.UseCell, approach.Path[^1]);
        Assert.Equal(approach.Path.Length - 1, approach.Distance);
        Assert.All(approach.Path, cell => Assert.True(map.Cells.Span[cell].IsWalkable));
    }

    [Fact]
    public async Task AnAlreadyEligiblePositionHasAZeroStepPath()
    {
        await using var play = new TestPlay();
        var map = play.Maps.Get(TestPlay.MapId);
        var useCell = ElementCells.UsedFrom(map.Cells.Span, TestPlay.TreeCell, 1)[0];
        var (_, bot) = await play.EnterAsync(useCell);

        var approach = HarvestApproachPlanner.Plan(bot.State, map, TestPlay.TreeElementId);

        Assert.Equal("CONFIRMED", approach.Status);
        Assert.Equal(useCell, approach.UseCell);
        Assert.Equal([useCell], approach.Path);
        Assert.Equal(0, approach.Distance);
    }

    [Fact]
    public async Task AWallRingWithNoReachableUseCellIsBlocked()
    {
        var blocked = Enumerable.Range(0, MapCell.DirectionCount)
            .Select(direction => MapGeometry.Neighbour(TestPlay.TreeCell, direction))
            .Where(cell => cell >= 0)
            .ToArray();
        await using var play = new TestPlay(blocked);
        var (_, bot) = await play.EnterAsync(TestPlay.TreeCell);
        var map = play.Maps.Get(TestPlay.MapId);

        var approach = HarvestApproachPlanner.Plan(bot.State, map, TestPlay.TreeElementId);

        Assert.Equal("BLOCKED", approach.Status);
        Assert.Equal("NO_PATH", approach.Reason);
        Assert.Empty(approach.Path);
    }

    [Fact]
    public async Task UntrustedPositionMapMismatchCellConflictAndUnknownElementAreBlocked()
    {
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(190);
        var map = play.Maps.Get(TestPlay.MapId);

        var untrusted = HarvestApproachPlanner.Plan(bot.State with { Provenance = CellProvenance.Estimated }, map, TestPlay.TreeElementId);
        var changedMap = HarvestApproachPlanner.Plan(bot.State, play.Maps.Get(TestPlay.NorthMapId), TestPlay.TreeElementId);
        var conflict = HarvestApproachPlanner.Plan(bot.State, map, TestPlay.TreeElementId, TestPlay.TreeCell + 1);
        var unknown = HarvestApproachPlanner.Plan(bot.State, map, int.MaxValue);

        Assert.Equal("POSITION_UNTRUSTED", untrusted.Reason);
        Assert.Equal("MAP_CHANGED", changedMap.Reason);
        Assert.Equal("ELEMENT_CELL_CONFLICT", conflict.Reason);
        Assert.Equal("ELEMENT_CELL_UNKNOWN", unknown.Reason);
        Assert.All([untrusted, changedMap, conflict, unknown], result => Assert.Equal("BLOCKED", result.Status));
    }

    [Fact]
    public async Task AnInvalidStartingCellInThePathFailsValidation()
    {
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(190);
        var source = play.Maps.Get(TestPlay.MapId);
        var cells = source.Cells.ToArray();
        cells[190] = new MapCell(MapCellProperties.None, 0, 0, 0, 0, 1 << 4, 0);
        var map = new MapData(
            source.Id,
            source.TopNeighbourId,
            source.BottomNeighbourId,
            source.LeftNeighbourId,
            source.RightNeighbourId,
            cells,
            source.Interactives);

        var approach = HarvestApproachPlanner.Plan(bot.State, map, TestPlay.TreeElementId);

        Assert.Equal("INVALID_PATH", approach.Reason);
        Assert.Equal("BLOCKED", approach.Status);
        Assert.Empty(approach.Path);
    }

    [Fact]
    public async Task PlanningIsPassiveAndDeterministic()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(0);
        var map = play.Maps.Get(TestPlay.MapId);
        var state = bot.State;

        var first = HarvestApproachPlanner.Plan(state, map, TestPlay.TreeElementId);
        var second = HarvestApproachPlanner.Plan(state, map, TestPlay.TreeElementId);

        Assert.Equal(first.UseCell, second.UseCell);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(first.Distance, second.Distance);
        Assert.Equal(state, bot.State);
        Assert.Empty(connection.Sent);
    }
}
