using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>The cells a spell level reaches from its caster's cell: its range, its shape, and the caster's range bonus.</summary>
/// <remarks>
/// <para>
/// The range counts on the map's coordinates (<see cref="MapPoint"/>), as <see cref="MapGeometry.Distance"/> does, between
/// the level's least and most. The caster's range bonus, characteristic 19, adds to the most when the level says it may
/// be boosted; the most never falls below the least, nor below 0.
/// </para>
/// <list type="bullet">
/// <item><description>No shape: every cell at a range between the least and the most, a diamond on the coordinates.</description></item>
/// <item><description><see cref="SpellLevelProperties.CastInLine"/>: the cells of the caster's row or column of coordinates.</description></item>
/// <item><description><see cref="SpellLevelProperties.CastInDiagonal"/>: the cells of its diagonals, their range counted in diagonal steps, or as the
/// distance with <see cref="SpellLevelProperties.ProportionalDiagonal"/>.</description></item>
/// <item><description>Both: the line's cells and the diagonal's.</description></item>
/// </list>
/// <para>
/// Measured on the 396 casts the fighters chose in the 49 fights captured (client.md): 392 lie within their level's range,
/// 10 of them only once the caster's bonus boosts it, and the 78 of a level cast in line lie in line; 4 casts at a range of
/// 1 reached the diagonal neighbour, at a distance of 2, so a diagonal counts its steps. The 4 others an Eliotrope cast
/// through its portals, which 9f measures. No capture tells <see cref="SpellLevelProperties.ProportionalDiagonal"/>, on 6
/// levels, from the steps: it counts the distance, as its name says.
/// </para>
/// </remarks>
public static class SpellRange
{
    /// <summary>Gets the most range a level is cast at by a caster.</summary>
    /// <param name="level">The level.</param>
    /// <param name="rangeBonus">The caster's range bonus, characteristic 19, which may be negative.</param>
    /// <returns>The most range, never below the least.</returns>
    public static int MaxRange(in SpellLevel level, int rangeBonus)
    {
        var most = level.Range + ((level.Properties & SpellLevelProperties.RangeCanBeBoosted) is not 0 ? rangeBonus : 0);

        return Math.Max(Math.Max(most, level.MinRange), 0);
    }

    /// <summary>Tells whether a level cast from a cell reaches another.</summary>
    /// <param name="level">The level.</param>
    /// <param name="rangeBonus">The caster's range bonus.</param>
    /// <param name="from">The caster's cell.</param>
    /// <param name="to">The cell cast on.</param>
    /// <returns>Whether the cell lies within the level's range and shape.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static bool Reaches(in SpellLevel level, int rangeBonus, int from, int to)
    {
        var start = MapGeometry.PointOf(from);
        var end = MapGeometry.PointOf(to);
        var dx = Math.Abs(end.X - start.X);
        var dy = Math.Abs(end.Y - start.Y);
        var least = (int)level.MinRange;
        var most = MaxRange(level, rangeBonus);
        var inLine = (level.Properties & SpellLevelProperties.CastInLine) is not 0;
        var inDiagonal = (level.Properties & SpellLevelProperties.CastInDiagonal) is not 0;

        if (!inLine && !inDiagonal)
            return dx + dy >= least && dx + dy <= most;

        if (inLine && (dx is 0 || dy is 0) && dx + dy >= least && dx + dy <= most)
            return true;

        if (!inDiagonal || dx != dy)
            return false;

        var steps = (level.Properties & SpellLevelProperties.ProportionalDiagonal) is not 0 ? dx + dy : dx;

        return steps >= least && steps <= most;
    }

    /// <summary>Gets the cells a level cast from a cell reaches, before the line of sight and the cells' own conditions.</summary>
    /// <param name="level">The level.</param>
    /// <param name="rangeBonus">The caster's range bonus.</param>
    /// <param name="from">The caster's cell.</param>
    /// <returns>The cells within the level's range and shape.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell does not exist.</exception>
    public static CellSet Cells(in SpellLevel level, int rangeBonus, int from)
    {
        var cells = default(CellSet);

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            if (Reaches(level, rangeBonus, from, cell))
                cells.Add(cell);
        }

        return cells;
    }
}
