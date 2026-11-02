using D3B.Data.Monsters;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class FightMovesTests
{
    private static readonly FightMoves _moves = new(States());
    private static readonly Combatant _walker = Caster() with { Dodge = 0 };

    [Fact]
    public void AFighterStepsToTheFourCellsAtADistanceOfOne()
    {
        Span<int> steps = stackalloc int[4];
        var count = FightMoves.Steps(Middle, steps);

        Assert.Equal(4, count);
        Assert.Equal(CellSet.Of(Off(-1, 0), Off(0, -1), Off(0, 1), Off(1, 0)), CellSet.Of(steps));
        Assert.Equal(1, FightMoves.Steps(0, steps));
        Assert.Equal(2, FightMoves.Steps(13, steps));
    }

    [Fact]
    public void AnEnemyBesideTacklesByLockAgainstDodge()
    {
        var tackler = Enemy(-1, Off(1, 0)) with { Lock = 0 };

        Assert.Equal(0.5, _moves.KeptLeaving([_walker, tackler], _walker, Middle));
        Assert.Equal(0.25, _moves.KeptLeaving([_walker, tackler, tackler with { Id = -2, Cell = Off(0, 1) }], _walker, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler], _walker with { Dodge = 2 }, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler with { Cell = Off(1, 1) }], _walker, Middle));
    }

    [Fact]
    public void WhatCannotTackleDoesNot()
    {
        var tackler = Enemy(-1, Off(1, 0));

        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler with { MonsterProperties = MonsterProperties.CanMove }], _walker, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler with { States = [11] }], _walker, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler with { IsAlive = false }], _walker, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler with { Team = 0 }], _walker, Middle));
        Assert.Equal(1, _moves.KeptLeaving([_walker, tackler], _walker with { States = [12] }, Middle));
    }

    [Fact]
    public void ATackleTakesThePointsNotKeptRoundedToTheNearest()
    {
        Assert.Equal(3, FightMoves.Lost(5, 0.5));
        Assert.Equal(2, FightMoves.Lost(3, 0.25));
        Assert.Equal(0, FightMoves.Lost(6, 1));
        Assert.Equal(0, FightMoves.Lost(0, 0));
    }

    [Fact]
    public void AFighterReachesTheCellsItsMpWalksToAroundFightersAndHoles()
    {
        var reachable = _moves.Reachable(Open(Off(1, 0)), [_walker with { MovementPoints = 2 }, Enemy(-1, Off(-1, 0)) with { Team = 0 }], _walker with { MovementPoints = 2 });

        Assert.Equal((2, 6), reachable[Middle]);
        Assert.Equal((1, 6), reachable[Off(0, 1)]);
        Assert.Equal((0, 6), reachable[Off(1, 1)]);
        Assert.False(reachable.ContainsKey(Off(1, 0)));
        Assert.False(reachable.ContainsKey(Off(-1, 0)));
        Assert.False(reachable.ContainsKey(Off(2, 0)));
        Assert.All(reachable, pair => Assert.True(MapGeometry.Distance(Middle, pair.Key) <= 2));
    }

    [Fact]
    public void LeavingATacklerCostsMpAndApBeforeTheStep()
    {
        var walker = _walker with { MovementPoints = 4 };
        var reachable = _moves.Reachable(Open(), [walker, Enemy(-1, Off(1, 0))], walker);

        Assert.Equal((1, 3), reachable[Off(0, 1)]);
        Assert.False(reachable.ContainsKey(Off(0, 3)));
    }
}
