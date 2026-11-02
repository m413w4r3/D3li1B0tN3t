using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Items;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

/// <summary>The weapons' blows as levels of spell 0, made from the punch and a sword's fields, and foreseen as a weapon's damage.</summary>
public sealed class WeaponBlowsTests
{
    private const int Sword = 6751;
    private const int Ornament = 6800;
    private const int Hammer = 7000;
    private const int Strength = 118;

    private static readonly string[] _zones = ["P", "X1,0,10,1"];
    private static readonly SpellCatalog _spells = Catalog();
    private static readonly EffectCatalog _effects = new(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(100, 0, 0, '\0', 2, 0, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(Strength, 0, FightCharacteristic.Strength, '+', 0, 1, 0, 0, 0, 0, EffectProperties.Boost),
    });

    private static readonly WeaponBlows _blows = new(_spells, Weapons(), _effects);

    /// <summary>Spell 0, the punch: a neutral blow of 6 to 7 on what stands on the cell struck, 3 AP, range 1, its own critical effects.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["a,A", "I"];
        var point = Zone(SpellZoneShape.Point);
        var punch = new SpellLevel { Id = 10_461, SpellId = WeaponBlows.SpellId, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, CriticalHitProbability = 5, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1, CriticalEffectCount = 1 };
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 9, EffectId = 100, TargetMask = 0, Triggers = 1, DiceNum = 6, DiceSide = 7, Zone = point },
            new SpellEffect { Uid = 10, EffectId = 100, TargetMask = 0, Triggers = 1, DiceNum = 9, DiceSide = 10, Zone = point },
        ];

        return new SpellCatalog(new[] { new Spell { Id = WeaponBlows.SpellId, NameId = 1, FirstLevel = 0, LevelCount = 1 } }, new[] { punch }, new[] { punch.Id }, effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>A sword striking earth damage of 16 to 25 and giving strength, 4 AP, a sword giving strength alone, and a hammer.</summary>
    private static WeaponCatalog Weapons()
    {
        return new WeaponCatalog(
            new Weapon[]
            {
                new() { ItemId = Sword, TypeId = 6, ApCost = 4, MinRange = 1, Range = 1, CriticalHitProbability = 30, CriticalHitBonus = 10, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 0, EffectCount = 2 },
                new() { ItemId = Ornament, TypeId = 6, ApCost = 3, MinRange = 1, Range = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 2, EffectCount = 1 },
                new() { ItemId = Hammer, TypeId = 7, ApCost = 5, MinRange = 1, Range = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 3, EffectCount = 1 },
            },
            new WeaponEffect[]
            {
                new() { EffectId = 97, DiceNum = 16, DiceSide = 25 },
                new() { EffectId = Strength, DiceNum = 21, DiceSide = 30 },
                new() { EffectId = Strength, DiceNum = 5 },
                new() { EffectId = 100, DiceNum = 10, DiceSide = 20 },
            },
            new WeaponType[] { new() { Id = 6, Zone = 0 }, new() { Id = 7, Zone = 1 } },
            _zones);
    }

    [Fact]
    public void ASwordsBlowIsALevelOfSpellZeroWithTheSwordsFieldsAndWhatItDoesInAFight()
    {
        Assert.True(_blows.TryGetBlow(Sword, out var blow));
        Assert.Equal((WeaponBlows.FirstLevelId + Sword, WeaponBlows.SpellId, 4, 1, 1, 30, 1), (blow.Id, blow.SpellId, (int)blow.ApCost, (int)blow.MinRange, (int)blow.Range, (int)blow.CriticalHitProbability, (int)blow.MaxCastPerTurn));
        Assert.Equal(SpellLevelProperties.CastTestLos, blow.Properties);
        Assert.Equal(0, blow.CriticalEffectCount);

        var effect = Assert.Single(_blows.Spells.EffectsOf(blow).ToArray());

        Assert.Equal((97, 16, 25, "a,A", "I", SpellZoneShape.Point), (effect.EffectId, effect.DiceNum, effect.DiceSide, _blows.Spells.TargetMaskOf(effect), _blows.Spells.TriggersOf(effect), effect.Zone.Shape));
        Assert.Equal(10, _blows.CriticalBonusOf(blow));
        Assert.True(WeaponBlows.IsBlow(blow));
        Assert.True(_blows.Spells.TryGetLevel(blow.Id, out _));
        Assert.False(_spells.TryGetLevel(blow.Id, out _));
    }

    [Fact]
    public void OnlySwordsThatDoSomethingInAFightStrikeAndThePunchHasNoWeaponsBonus()
    {
        Assert.False(_blows.TryGetBlow(Hammer, out _));
        Assert.False(_blows.TryGetBlow(Ornament, out _));
        Assert.True(_spells.TryGetLevel(10_461, out var punch));
        Assert.True(WeaponBlows.IsBlow(punch));
        Assert.Equal(0, _blows.CriticalBonusOf(punch));
        Assert.False(new WeaponBlows(_spells).TryGetBlow(Sword, out _));
    }

    [Fact]
    public void ABlowIsForeseenAsAWeaponsDamageItsCriticalHitAddingTheWeaponsBonus()
    {
        var effects = new SpellEffects(_blows, new CastRules(_blows.Spells, States()), new EffectActions(_effects));
        var caster = Caster() with
        {
            Characteristics = ImmutableSortedDictionary<int, int>.Empty
                .Add(FightCharacteristic.Strength, 100)
                .Add(FightCharacteristic.DealtDamageMultiplierWeapons, 150)
                .Add(FightCharacteristic.DealtDamageMultiplierSpells, 200),
        };
        var target = Enemy(-1, Off(1, 0)) with { Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(FightCharacteristic.ReceivedDamageMultiplierWeapons, 50) };
        Assert.True(_blows.TryGetBlow(Sword, out var blow));

        var normal = Assert.Single(effects.Foresee(Open(), [caster, target], caster, blow, target.Cell, critical: false));
        var critical = Assert.Single(effects.Foresee(Open(), [caster, target], caster, blow, target.Cell, critical: true));

        // 16 to 25 doubled, 32 to 50, half as much again as weapons deal, then halved as the target receives them.
        Assert.Equal((-1L, EffectElement.Earth, new LifeRange(24, 37)), (normal.TargetId, normal.Element, normal.Value));
        Assert.Equal(new LifeRange(31, 45), critical.Value);
    }
}
