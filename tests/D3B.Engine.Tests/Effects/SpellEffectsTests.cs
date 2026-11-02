using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Effects;

/// <summary>A cast foreseen effect by effect: a fire blow in a circle, and a boost of the caster its mask names.</summary>
public sealed class SpellEffectsTests
{
    private static readonly SpellCatalog _spells = Catalog();
    private static readonly SpellEffects _effects = new(_spells, new CastRules(_spells, States()), new EffectActions(new EffectCatalog(new Effect[]
    {
        new(1, 0, 1, '+', 0, -1, 1, 0, 0, 0, EffectProperties.Boost),
        new(99, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
    })));

    private static readonly Combatant _caster = Caster() with { Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(FightCharacteristic.Intelligence, 100) };

    /// <summary>
    /// Spell 10, level 1010: fire damage of 10 to 12 in a circle of 1 on the enemies, then 2 AP for a round on the caster
    /// its mask names; on a critical hit, fire damage of 15 to 17.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "C"];
        var circle = Zone(SpellZoneShape.Circle);
        var level = new SpellLevel { Id = 1010, SpellId = 10, Grade = 1, ApCost = 3, MinRange = 1, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 2, CriticalEffectCount = 1 };
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 99, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = circle },
            new SpellEffect { Uid = 2, EffectId = 1, TargetMask = 2, Triggers = 1, DiceNum = 2, Duration = 1, Zone = Zone(SpellZoneShape.Point) },
            new SpellEffect { Uid = 3, EffectId = 99, TargetMask = 0, Triggers = 1, DiceNum = 15, DiceSide = 17, Zone = circle },
        ];

        return new SpellCatalog(new[] { new Spell { Id = 10, NameId = 10, FirstLevel = 0, LevelCount = 1 } }, new[] { level }, new[] { level.Id }, effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static IReadOnlyList<ForeseenEffect> Foresee(bool critical, params Combatant[] others)
    {
        return _effects.Foresee(Open(), [_caster, .. others], _caster, Level(_spells, 10), Off(3, 0), critical);
    }

    [Fact]
    public void ABlowIsForeseenOnEachEnemyItsZoneReachesLessAStepAway()
    {
        var foreseen = Foresee(false, Enemy(-1, Off(3, 0)), Enemy(-2, Off(3, 1)), Enemy(-3, Off(1, 0)));
        var blows = foreseen.Where(effect => effect.Action.Kind is EffectKind.Damage).ToList();

        Assert.Equal([-1L, -2L], blows.Select(blow => blow.TargetId));
        Assert.All(blows, blow => Assert.Equal(EffectElement.Fire, blow.Element));
        Assert.Equal(new LifeRange(20, 24), blows[0].Value);
        Assert.Equal(new LifeRange(18, 21), blows[1].Value);
        Assert.All(blows, blow => Assert.True(blow.IsCertain && blow.IsImmediate && !blow.IsRandom));
    }

    [Fact]
    public void AMaskNamingTheCasterReachesItOutsideTheZone()
    {
        var boost = Assert.Single(Foresee(false, Enemy(-1, Off(3, 0))), effect => effect.Action.Kind is EffectKind.Boost);

        Assert.Equal((_caster.Id, 1, new LifeRange(2, 2)), (boost.TargetId, boost.Action.Characteristic, boost.Value));
    }

    [Fact]
    public void ACriticalHitForeseesTheLevelsCriticalEffects()
    {
        var blow = Assert.Single(Foresee(true, Enemy(-1, Off(3, 0))));

        Assert.Equal(new LifeRange(30, 34), blow.Value);
    }

    [Fact]
    public void ResistancesAndADamageShareLowerTheBlow()
    {
        var resisting = Enemy(-1, Off(3, 0)) with
        {
            Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(34, 50),
            ReceivedDamageShares = [85],
        };

        var blow = Assert.Single(Foresee(false, resisting), effect => effect.Action.Kind is EffectKind.Damage);

        Assert.Equal(new LifeRange(8, 10), blow.Value);
    }
}
