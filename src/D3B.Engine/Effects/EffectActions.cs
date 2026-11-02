using System.Collections.Frozen;
using D3B.Data.Effects;

namespace D3B.Engine.Effects;

/// <summary>What each effect id does: the pack's effects read once, every effect as data, never per spell.</summary>
/// <remarks>
/// <para>
/// The pack says the element of damage, steals and heals, the characteristic a boost changes and how, and the category of
/// a spell's modifiers (D182). D3B adds what the pack leaves to the client's code: which id damages, steals, heals,
/// shields, pushes, teleports, summons, sets a state or casts a spell, and who casts that spell where, named as the client's
/// actions are (D183) and measured on the fights captured where cast (client.md); the best and worst elements, which the
/// pack gives no element.
/// </para>
/// <para>Built once per pack and shared: nothing in it changes.</para>
/// </remarks>
public sealed class EffectActions
{
    private const byte SpellModifierCategory = 3;

    private static readonly EffectAction _other = new(EffectKind.Other, EffectElement.None, 0);

    private static readonly FrozenDictionary<int, EffectAction> _named = new Dictionary<int, EffectAction>
    {
        [96] = new(EffectKind.Damage, EffectElement.Water, 0),
        [97] = new(EffectKind.Damage, EffectElement.Earth, 0),
        [98] = new(EffectKind.Damage, EffectElement.Air, 0),
        [99] = new(EffectKind.Damage, EffectElement.Fire, 0),
        [100] = new(EffectKind.Damage, EffectElement.Neutral, 0),
        [2822] = new(EffectKind.Damage, EffectElement.Best, 0),
        [91] = new(EffectKind.Steal, EffectElement.Water, 0),
        [92] = new(EffectKind.Steal, EffectElement.Earth, 0),
        [93] = new(EffectKind.Steal, EffectElement.Air, 0),
        [94] = new(EffectKind.Steal, EffectElement.Fire, 0),
        [95] = new(EffectKind.Steal, EffectElement.Neutral, 0),
        [2828] = new(EffectKind.Steal, EffectElement.Best, 0),
        [2890] = new(EffectKind.Steal, EffectElement.Worst, 0),
        [82] = new(EffectKind.FixedDamage, EffectElement.Neutral, 0),
        [144] = new(EffectKind.FixedDamage, EffectElement.Neutral, 0),
        [108] = new(EffectKind.Heal, EffectElement.Fire, 0),
        [2998] = new(EffectKind.Heal, EffectElement.Water, 0),
        [2999] = new(EffectKind.Heal, EffectElement.Air, 0),
        [3000] = new(EffectKind.Heal, EffectElement.Earth, 0),
        [3001] = new(EffectKind.Heal, EffectElement.Neutral, 0),
        [81] = new(EffectKind.Heal, EffectElement.Best, 0),
        [3002] = new(EffectKind.Heal, EffectElement.Best, 0),
        [143] = new(EffectKind.FixedHeal, EffectElement.None, 0),
        [407] = new(EffectKind.FixedHeal, EffectElement.None, 0),
        [1109] = new(EffectKind.PercentHeal, EffectElement.None, 0),
        [1040] = new(EffectKind.Shield, EffectElement.None, 0),
        [1039] = new(EffectKind.LifeShield, EffectElement.None, 0),
        [1020] = new(EffectKind.LevelShield, EffectElement.None, 0),
        [5] = new(EffectKind.Push, EffectElement.None, 0),
        [1021] = new(EffectKind.Push, EffectElement.None, 0),
        [1103] = new(EffectKind.Push, EffectElement.None, 0),
        [6] = new(EffectKind.Pull, EffectElement.None, 0),
        [4] = new(EffectKind.Teleport, EffectElement.None, 0),
        [8] = new(EffectKind.Swap, EffectElement.None, 0),
        [1023] = new(EffectKind.Swap, EffectElement.None, 0),
        [181] = new(EffectKind.Summon, EffectElement.None, 0),
        [1008] = new(EffectKind.Summon, EffectElement.None, 0),
        [1011] = new(EffectKind.Summon, EffectElement.None, 0),
        [950] = new(EffectKind.StateSet, EffectElement.None, 0),
        [951] = new(EffectKind.StateRemoved, EffectElement.None, 0),
        [952] = new(EffectKind.StateDisabled, EffectElement.None, 0),
        [792] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [793] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [1017] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [1018] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [1019] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [1160] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2017] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2160] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2792] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2793] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2794] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2795] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [2960] = new(EffectKind.CastSpell, EffectElement.None, 0),
        [406] = new(EffectKind.DispelSpell, EffectElement.None, 0),
        [132] = new(EffectKind.Dispel, EffectElement.None, 0),
        [141] = new(EffectKind.Kill, EffectElement.None, 0),
        [1009] = new(EffectKind.Detonate, EffectElement.None, 0),
        [783] = new(EffectKind.PushToCell, EffectElement.None, 0),

        // The pack names no characteristic for these, its texts do (9f3): a bomb's combo, characteristic 94, and AP given back.
        [1027] = new(EffectKind.Boost, EffectElement.None, FightCharacteristic.BombComboBonus),
        [120] = new(EffectKind.Boost, EffectElement.None, 1),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, SpellCaster> _casters = new Dictionary<int, SpellCaster>
    {
        [792] = SpellCaster.TargetOnItself,
        [793] = SpellCaster.TargetOnItself,
        [2792] = SpellCaster.TargetOnItself,
        [2793] = SpellCaster.TargetOnItself,
        [1160] = SpellCaster.CasterOnTarget,
        [2160] = SpellCaster.CasterOnTarget,
        [1018] = SpellCaster.CasterOnTarget,
        [2794] = SpellCaster.TargetOnCell,
        [2795] = SpellCaster.TargetOnCell,
        [2960] = SpellCaster.CasterOnCell,
        [1017] = SpellCaster.TargetOnCaster,
        [2017] = SpellCaster.TargetOnCaster,
        [1019] = SpellCaster.CasterOnItself,
    }.ToFrozenDictionary();

    private readonly FrozenDictionary<int, EffectAction> _actions;

    /// <summary>Initializes a new instance of the <see cref="EffectActions"/> class.</summary>
    /// <param name="effects">The pack's effects.</param>
    public EffectActions(EffectCatalog effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var actions = new Dictionary<int, EffectAction>();

        foreach (var effect in effects.Effects.Span)
        {
            if (_named.TryGetValue(effect.Id, out var named))
            {
                // The pack's element where it gives one: the best and worst elements are D3B's, the pack giving none.
                actions[effect.Id] = named.Element is EffectElement.Best or EffectElement.Worst || effect.ElementId < 0 ? named : named with { Element = (EffectElement)effect.ElementId };
            }
            else if (effect.Category == SpellModifierCategory)
            {
                actions[effect.Id] = new EffectAction(EffectKind.SpellModifier, EffectElement.None, 0);
            }
            else if (effect.Characteristic > 0 && effect.Operator is '+' or '-')
            {
                actions[effect.Id] = new EffectAction(effect.Operator is '+' ? EffectKind.Boost : EffectKind.Deboost, EffectElement.None, effect.Characteristic);
            }
        }

        _actions = actions.ToFrozenDictionary();
    }

    /// <summary>Gets what an effect id does.</summary>
    /// <param name="effectId">The effect, by the id spells name and the server announces.</param>
    /// <returns>What it does, or <see cref="EffectKind.Other"/> for an effect the engine does not foresee.</returns>
    public EffectAction Of(int effectId)
    {
        return _actions.TryGetValue(effectId, out var action) ? action : _other;
    }

    /// <summary>Gets who casts the spell an effect of <see cref="EffectKind.CastSpell"/> casts, and where.</summary>
    /// <param name="effectId">The effect, by the id spells name.</param>
    /// <returns>Who casts it and on which cell; the fighter reached on its own cell for an id that casts no spell.</returns>
    /// <remarks>
    /// The spell and its grade are the effect's dice (client.md). Measured (D224): a Zobal's mask casts, by 2794 on itself,
    /// the spell that takes its other masks' states off, which casts on itself, by 792, the grades that do it.
    /// </remarks>
    public static SpellCaster CasterOf(int effectId)
    {
        return _casters.GetValueOrDefault(effectId, SpellCaster.TargetOnItself);
    }
}
