namespace D3B.Engine.Tactics;

/// <summary>A buff a cast of the search gives a fighter: what it changes, and for how many rounds (D209).</summary>
/// <param name="CasterId">The fighter who cast it.</param>
/// <param name="FighterId">The fighter it changes.</param>
/// <param name="Characteristic">The characteristic it changes, or 0 for a state.</param>
/// <param name="Change">The points it adds to the characteristic, below 0 for a deboost; the state's id for a state.</param>
/// <param name="Duration">The rounds it lasts, its caster's turn of the last one ending it (D193); -1 for the whole fight.</param>
/// <param name="SpellId">The spell that gave it, whose dispel takes it back (D227).</param>
public readonly record struct ImaginedBuff(long CasterId, long FighterId, int Characteristic, int Change, int Duration, int SpellId = 0)
{
    /// <summary>Gets whether it sets a state rather than changing a characteristic.</summary>
    public bool IsState => Characteristic is 0;
}
