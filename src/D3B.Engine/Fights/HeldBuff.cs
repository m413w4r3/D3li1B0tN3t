namespace D3B.Engine.Fights;

/// <summary>A buff a fighter holds, as the server gave it or a cast the AI imagines gave it: what a dispel of its spell takes back (D227).</summary>
/// <param name="SpellId">The spell that gave it.</param>
/// <param name="EffectId">Its effect, by the id the pack and the server give it: 950 a state, a boost's, a deboost's or a spell modifier's otherwise.</param>
/// <param name="Value">The points a boost adds or a deboost takes, the state's id, or the spell a spell modifier changes.</param>
/// <param name="Parameter">A spell modifier's value; 0 otherwise.</param>
/// <param name="CasterId">The fighter who gave it, whose death takes it back; 0 when unknown.</param>
public readonly record struct HeldBuff(int SpellId, int EffectId, int Value, int Parameter = 0, long CasterId = 0);
