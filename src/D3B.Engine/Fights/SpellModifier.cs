namespace D3B.Engine.Fights;

/// <summary>A change to one of a fighter's spells, which a buff gives it: more range, a lower cost, no line of sight to test.</summary>
/// <param name="Kind">The change, by the effect's id that gives it.</param>
/// <param name="SpellId">The spell it changes.</param>
/// <param name="Value">By how much, for a change that counts.</param>
/// <remarks>
/// Measured (client.md): the server announces each as a buff on the fighter, of effects 280 to 299, naming the spell
/// changed and the value, and removes it with the buffs of the spell that gave it. The rules apply those that change a
/// cast (<see cref="CastRules.Modified"/>); the others, on damage, heals and critical hits, are 9d's.
/// </remarks>
public readonly record struct SpellModifier(SpellModifierKind Kind, int SpellId, int Value);
