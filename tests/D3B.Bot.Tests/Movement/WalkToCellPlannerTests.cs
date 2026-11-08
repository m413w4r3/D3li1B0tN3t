using D3B.Bot.Movement;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Engine.Tests.Maps;

namespace D3B.Bot.Tests.Movement;

public sealed class WalkToCellPlannerTests
{
    private const int FromCell = 145;

    private static readonly IReadOnlySet<int> _noExcludedCells = new HashSet<int>();

    [Fact]
    public void ChoosesTheQuickestReachableTerrainCandidateAndReturnsAValidPath()
    {
        var candidates = new[] { FromCell + 2, FromCell - 28 };
        var plan = WalkToCellPlanner.Plan(TestCells.OpenBut(), FromCell, candidates, _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.Reachable, plan.Status);
        Assert.Equal(FromCell - 28, plan.TargetCell);
        AssertPath(plan, FromCell);
    }

    [Fact]
    public void ReportsAnUnreachableDisconnectedCandidate()
    {
        const int destination = 200;
        var cells = TestCells.Where(cell => cell is FromCell or destination);

        var plan = WalkToCellPlanner.Plan(cells, FromCell, [destination], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.Unreachable, plan.Status);
        Assert.Null(plan.TargetCell);
        Assert.Empty(plan.PathCells.ToArray());
    }

    [Fact]
    public void ReportsNoCandidatesWhenTheCandidateListIsEmpty()
    {
        var plan = WalkToCellPlanner.Plan(TestCells.OpenBut(), FromCell, [], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.NoCandidates, plan.Status);
    }

    [Fact]
    public void ReportsNoCandidatesWhenEveryCandidateIsNonWalkable()
    {
        const int first = FromCell + 1;
        const int second = FromCell + 2;
        var cells = TestCells.OpenBut(first, second);

        var plan = WalkToCellPlanner.Plan(cells, FromCell, [first, second], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.NoCandidates, plan.Status);
    }

    [Fact]
    public void ReportsNoCandidatesWhenEveryCandidateIsExcluded()
    {
        var excludedCells = new HashSet<int> { FromCell + 1, FromCell + 2 };

        var plan = WalkToCellPlanner.Plan(TestCells.OpenBut(), FromCell, [FromCell + 1, FromCell + 2], excludedCells);

        Assert.Equal(WalkToCellPlanStatus.NoCandidates, plan.Status);
    }

    [Fact]
    public void NeverChoosesTheNearestExcludedInteractiveCell()
    {
        var excludedCells = new HashSet<int> { FromCell + 1 };
        var plan = WalkToCellPlanner.Plan(TestCells.OpenBut(), FromCell, [FromCell + 1, FromCell - 28], excludedCells);

        Assert.Equal(WalkToCellPlanStatus.Reachable, plan.Status);
        Assert.Equal(FromCell - 28, plan.TargetCell);
        AssertPath(plan, FromCell);
    }

    [Fact]
    public void RejectsTheCurrentCellAsATarget()
    {
        var cells = TestCells.OpenBut();
        var onlyCurrent = WalkToCellPlanner.Plan(cells, FromCell, [FromCell], _noExcludedCells);
        var withAnotherCell = WalkToCellPlanner.Plan(cells, FromCell, [FromCell, FromCell + 1], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.NoCandidates, onlyCurrent.Status);
        Assert.Equal(WalkToCellPlanStatus.Reachable, withAnotherCell.Status);
        Assert.Equal(FromCell + 1, withAnotherCell.TargetCell);
        Assert.True(withAnotherCell.Steps > 0);
        AssertPath(withAnotherCell, FromCell);
    }

    [Fact]
    public void RejectsInvalidCurrentCellsAndMalformedMaps()
    {
        var cells = TestCells.OpenBut();

        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, -1, [FromCell + 1], _noExcludedCells).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, MapData.CellCount, [FromCell + 1], _noExcludedCells).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(new MapCell[MapData.CellCount - 1], FromCell, [FromCell + 1], _noExcludedCells).Status);
    }

    [Fact]
    public void RejectsCandidateCellsOutsideTheSuppliedMap()
    {
        var cells = TestCells.OpenBut();

        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, [-1], _noExcludedCells).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, [MapData.CellCount], _noExcludedCells).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, [MapData.CellCount + 1], _noExcludedCells).Status);
    }

    [Fact]
    public void RejectsNullCollectionsAndInvalidExcludedCells()
    {
        var cells = TestCells.OpenBut();

        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, null!, _noExcludedCells).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, [FromCell + 1], null!).Status);
        Assert.Equal(WalkToCellPlanStatus.InvalidInput, WalkToCellPlanner.Plan(cells, FromCell, [FromCell + 1], new HashSet<int> { MapData.CellCount }).Status);
    }

    [Fact]
    public void APathStaysWithinTheSuppliedMapCells()
    {
        var plan = WalkToCellPlanner.Plan(TestCells.OpenBut(), FromCell, [FromCell + 2, FromCell - 28], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.Reachable, plan.Status);
        AssertPath(plan, FromCell);
        Assert.All(plan.PathCells.ToArray(), cell => Assert.InRange(cell, 0, MapData.CellCount - 1));
    }

    [Fact]
    public void EquidistantCandidatesUseCellPathfindersSearchOrderRegardlessOfCandidateOrder()
    {
        var cells = TestCells.OpenBut();
        var firstOrder = WalkToCellPlanner.Plan(cells, FromCell, [FromCell + 1, FromCell - 1], _noExcludedCells);
        var reverseOrder = WalkToCellPlanner.Plan(cells, FromCell, [FromCell - 1, FromCell + 1], _noExcludedCells);

        Assert.Equal(WalkToCellPlanStatus.Reachable, firstOrder.Status);
        Assert.Equal(FromCell + 1, firstOrder.TargetCell);
        Assert.Equal(firstOrder.TargetCell, reverseOrder.TargetCell);
        AssertPath(firstOrder, FromCell);
        AssertPath(reverseOrder, FromCell);
    }

    private static void AssertPath(WalkToCellPlan plan, int fromCell)
    {
        var pathCells = plan.PathCells.ToArray();

        Assert.NotEmpty(pathCells);
        Assert.Equal(fromCell, pathCells[0]);
        Assert.Equal(plan.TargetCell, pathCells[^1]);
        Assert.Equal(pathCells.Length - 1, plan.Steps);
        Assert.All(pathCells.Zip(pathCells.Skip(1)), pair => Assert.True(MapGeometry.Direction(pair.First, pair.Second) >= 0));
    }
}
