using D3B.Data.Effects;
using D3B.Data.Items;
using D3B.Data.Spells;

namespace D3B.Engine.Fights;

/// <summary>The weapons' blows as levels of spell 0, which the rules, the effects and the AI cast as they cast a spell's (D220).</summary>
/// <remarks>
/// <para>
/// Measured on the blows captured (D218): the client asks for spell 0 on a cell, and the server announces the blow in a
/// sequence of its own, with no spell nor level, the AP the weapon costs spent. Spell 0's own level is the blow with no
/// weapon, the punch; a weapon's blow is that level with the weapon's fields: its AP, ranges, line, diagonal and line of
/// sight, critical chance and casts a turn, and as effects those of the weapon's the server uses in fights, rolled as the
/// weapon rolls them, reaching what the punch reaches, the cell struck and every fighter there. A critical hit adds the
/// weapon's bonus to the blow's damage (<see cref="Effects.EffectFormulas.DealtByWeapon"/>), and its effects are the same.
/// </para>
/// <para>
/// Only swords' blows are made, the owner's choice: the blows captured are a sword's, and every sword strikes the cell
/// struck, its type's zone <c>P</c>. The other types wait for blows of theirs, their zones above all.
/// </para>
/// <para>
/// The levels follow the pack's, which no cast names: <see cref="Spells"/> is the pack's spells with them, the catalog the
/// engine's parts share. Built once per pack and shared, from any thread.
/// </para>
/// </remarks>
public sealed class WeaponBlows
{
    /// <summary>The spell a weapon's blow is cast as, the character's list naming it.</summary>
    public const int SpellId = 0;

    /// <summary>The identifier of the first blow's level: a weapon's is this plus its item's, above every level of client 3.7.12.12.</summary>
    public const int FirstLevelId = 1_000_000;

    // The swords' type, the only one whose blows were captured (D217).
    private const int Swords = 6;

    // A sword's zone, the cell struck: the punch's zone.
    private const string CellStruck = "P";

    private readonly Dictionary<int, SpellLevel> _blows = [];
    private readonly Dictionary<int, int> _criticalBonuses = [];

    /// <summary>Gets the pack's spells and the blows' levels after them.</summary>
    public SpellCatalog Spells { get; }

    /// <summary>Initializes a new instance of the <see cref="WeaponBlows"/> class with no weapon: the pack's spells alone.</summary>
    /// <param name="spells">The pack's spells.</param>
    public WeaponBlows(SpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        Spells = spells;
    }

    /// <summary>Initializes a new instance of the <see cref="WeaponBlows"/> class.</summary>
    /// <param name="spells">The pack's spells, spell 0 among them.</param>
    /// <param name="weapons">The pack's weapons.</param>
    /// <param name="effects">The pack's effects, which say which of a weapon's are used in fights.</param>
    /// <exception cref="ArgumentException">The pack's levels reach <see cref="FirstLevelId"/>.</exception>
    public WeaponBlows(SpellCatalog spells, WeaponCatalog weapons, EffectCatalog effects)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(effects);

        var levels = spells.Levels.Span;

        if (!levels.IsEmpty && levels[^1].Id >= FirstLevelId)
            throw new ArgumentException($"The pack's levels reach {levels[^1].Id}, where the blows' start at {FirstLevelId}.", nameof(spells));

        // A pack without the punch, as some tests' are, has no blow to make.
        if (!spells.TryGetLevel(SpellId, 1, out var punch) || punch.EffectCount is 0)
        {
            Spells = spells;

            return;
        }

        var reach = spells.EffectsOf(punch)[0];
        var blowLevels = new List<SpellLevel>();
        var blowEffects = new List<SpellEffect>();

        foreach (var weapon in weapons.Weapons.Span)
        {
            if (weapon.TypeId is not Swords || !weapons.TryGetType(weapon.TypeId, out var type) || weapons.ZoneOf(type) is not CellStruck)
                continue;

            var first = blowEffects.Count;

            foreach (var effect in weapons.EffectsOf(weapon))
            {
                if (effects.TryGetEffect(effect.EffectId, out var found) && (found.Properties & EffectProperties.UseInFight) is not 0)
                    blowEffects.Add(reach with { Uid = 0, EffectId = effect.EffectId, DiceNum = effect.DiceNum, DiceSide = effect.DiceSide, Value = effect.Value });
            }

            // 4 swords of client 3.7.12.12 do nothing in a fight.
            if (blowEffects.Count == first)
                continue;

            blowLevels.Add(new SpellLevel
            {
                Id = FirstLevelId + weapon.ItemId,
                SpellId = SpellId,
                Grade = 1,
                ApCost = weapon.ApCost,
                MinRange = weapon.MinRange,
                Range = weapon.Range,
                CriticalHitProbability = weapon.CriticalHitProbability,
                MaxCastPerTurn = weapon.MaxCastPerTurn,
                StatesCriterion = SpellLevel.NoCriterion,
                Properties = weapon.Properties,
                FirstEffect = first,
                EffectCount = checked((ushort)(blowEffects.Count - first)),
            });
            _criticalBonuses[FirstLevelId + weapon.ItemId] = weapon.CriticalHitBonus;
        }

        Spells = spells.WithLevels(blowLevels.ToArray(), blowEffects.ToArray());

        foreach (var level in blowLevels)
        {
            Spells.TryGetLevel(level.Id, out var held);
            _blows[level.Id - FirstLevelId] = held;
        }
    }

    /// <summary>Gets whether a level is a weapon's blow, a level of spell 0: its damage is a weapon's (D220).</summary>
    /// <param name="level">The level.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsBlow(in SpellLevel level)
    {
        return level.SpellId is SpellId;
    }

    /// <summary>Finds the blow of a weapon.</summary>
    /// <param name="itemId">The weapon's item.</param>
    /// <param name="level">The blow's level, of <see cref="Spells"/>, when found.</param>
    /// <returns>Whether D3B strikes the weapon's blow: a sword's that does something in a fight.</returns>
    public bool TryGetBlow(int itemId, out SpellLevel level)
    {
        return _blows.TryGetValue(itemId, out level);
    }

    /// <summary>Gets what a blow's critical hit adds to its damage.</summary>
    /// <param name="level">The level cast.</param>
    /// <returns>The weapon's critical bonus for a weapon's blow; 0 for any other level, the punch's among them.</returns>
    public int CriticalBonusOf(in SpellLevel level)
    {
        return _criticalBonuses.GetValueOrDefault(level.Id);
    }
}
