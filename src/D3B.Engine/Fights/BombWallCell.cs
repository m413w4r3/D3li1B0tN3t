namespace D3B.Engine.Fights;

/// <summary>A cell of a wall between two bombs of a kind, and the spell it casts on a fighter that comes into it.</summary>
/// <param name="Cell">The cell.</param>
/// <param name="SummonerId">The bombs' summoner, who casts the wall's spell.</param>
/// <param name="SpellId">The wall's spell, as the bombs' wall names it.</param>
/// <param name="Grade">The grade it is cast at: the bombs'.</param>
/// <param name="Combo">The two bombs' combos, halved, which raise its damage as an explosion's.</param>
public readonly record struct BombWallCell(int Cell, long SummonerId, int SpellId, int Grade, int Combo);
