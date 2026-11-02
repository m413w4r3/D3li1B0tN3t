namespace D3B.Engine.Effects;

/// <summary>What an effect of a cast is foreseen to do to one fighter.</summary>
/// <param name="TargetId">The fighter it reaches.</param>
/// <param name="EffectId">The effect, by the id the pack and the server give it.</param>
/// <param name="Action">What the effect does.</param>
/// <param name="Element">The element of damage, a steal or a heal, resolved for the caster.</param>
/// <param name="Value">Its value: the life a fighter loses or heals, a shield's points, the cells of a push, the points of a boost, a state's id, the spell a dispel or a cast names.</param>
/// <param name="IsCertain">Whether its mask surely selects the fighter, not only maybe (D187).</param>
/// <param name="IsRandom">Whether it belongs to a group the server draws at random, so that it may not apply.</param>
/// <param name="IsImmediate">Whether it applies at once, its trigger <c>I</c> and no delay; otherwise it is a buff waiting for its trigger.</param>
/// <param name="Duration">The rounds a buff it gives lasts, its caster's turn of the last one ending it (D193); 0 for none, -1 for the whole fight.</param>
/// <param name="Grade">The grade of the spell an effect of <see cref="EffectKind.CastSpell"/> casts, its dice's second number; 0 for another effect.</param>
public readonly record struct ForeseenEffect(long TargetId, int EffectId, EffectAction Action, EffectElement Element, LifeRange Value, bool IsCertain, bool IsRandom, bool IsImmediate, int Duration, int Grade = 0);
