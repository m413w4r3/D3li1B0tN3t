using System.Collections.Immutable;

namespace D3B.Engine.Fights;

/// <summary>A mark on cells, a glyph or a trap, and the spell it casts on a fighter there.</summary>
/// <param name="Id">The mark's id: the server's, or one the search gives a mark it imagines.</param>
/// <param name="AuthorId">The fighter who set it, who casts its spell.</param>
/// <param name="Team">The author's team.</param>
/// <param name="Kind">When it casts its spell.</param>
/// <param name="SpellId">The spell it casts.</param>
/// <param name="Grade">The grade it casts that spell at.</param>
/// <param name="Centre">The cell it was cast on, its zone's middle, which its spell is cast on.</param>
/// <param name="Cells">The cells it covers, those of its zone fighters may stand on.</param>
/// <param name="EndRound">The round it goes in, as its author's turn of that round begins.</param>
public sealed record CellMark(int Id, long AuthorId, int Team, CellMarkKind Kind, int SpellId, int Grade, int Centre, ImmutableArray<int> Cells, int EndRound)
{
    /// <summary>Tells whether the mark covers a cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>Whether it does.</returns>
    public bool Covers(int cell)
    {
        return Cells.Contains(cell);
    }
}
