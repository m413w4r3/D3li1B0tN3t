using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Items;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The AI weighs a sword's blow as it weighs a spell: struck when it does more for its AP, and not otherwise (D217).</summary>
public sealed class WeaponTacticsTests
{
    private const int Blow = 100;
    private const int Strong = 6751;
    private const int Weak = 10_797;

    private static readonly string[] _zones = ["P"];
    private static readonly int[] _levelIds = [10_461, 1100];

    private static readonly WeaponBlows _blows = new(Catalog(), Weapons(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(100, 0, 0, '\0', 2, 0, 0, 0, 0, 0, EffectProperties.UseInFight),
    }));

    private static readonly FightTactics _tactics = new(_blows, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(100, 0, 0, '\0', 2, 0, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>Spell 0, the punch, neutral 6 to 7; spell 100, an earth blow of 10 to 12 on an enemy, 2 AP, range 1 to 4, twice a turn.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["a,A", "I", "A"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1100, SpellId = Blow, Grade = 1, ApCost = 2, MinRange = 1, Range = 4, MaxCastPerTurn = 2, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 10_461, SpellId = WeaponBlows.SpellId, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 2, EffectId = 97, TargetMask = 2, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
            new SpellEffect { Uid = 1, EffectId = 100, TargetMask = 0, Triggers = 1, DiceNum = 6, DiceSide = 7, Zone = point },
        ];
        Spell[] spells =
        [
            new Spell { Id = WeaponBlows.SpellId, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Blow, NameId = 2, FirstLevel = 1, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, _levelIds, effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>A strong sword, earth 40 to 50 for 2 AP, and a weak one, neutral 5 to 6 for 2 AP, both once a turn.</summary>
    private static WeaponCatalog Weapons()
    {
        return new WeaponCatalog(
            new Weapon[]
            {
                new() { ItemId = Strong, TypeId = 6, ApCost = 2, MinRange = 1, Range = 1, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 0, EffectCount = 1 },
                new() { ItemId = Weak, TypeId = 6, ApCost = 2, MinRange = 1, Range = 1, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 1, EffectCount = 1 },
            },
            new WeaponEffect[] { new() { EffectId = 97, DiceNum = 40, DiceSide = 50 }, new() { EffectId = 100, DiceNum = 5, DiceSide = 6 } },
            new WeaponType[] { new() { Id = 6, Zone = 0 } },
            _zones);
    }

    private static TurnPlan Plan(int weapon)
    {
        // The character, its 4 AP for two casts, beside an enemy too strong to kill this turn.
        var actor = Caster() with { ActionPoints = 4, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 500, MaxLife = 500 };
        Assert.True(_blows.TryGetBlow(weapon, out var blow));
        Assert.True(_blows.Spells.TryGetLevel(1100, out var spell));
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, enemy], ActorId = actor.Id, Round = 1 };
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [spell, blow]) };

        return _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
    }

    [Fact]
    public void AStrongSwordIsStruckBesideTheSpell()
    {
        var plan = Plan(Strong);

        Assert.Contains(TacticAction.Cast(WeaponBlows.SpellId, WeaponBlows.FirstLevelId + Strong, Off(1, 0)), plan.Actions);
        Assert.Contains(TacticAction.Cast(Blow, 1100, Off(1, 0)), plan.Actions);
    }

    [Fact]
    public void AWeakSwordLeavesTheAPToTheSpell()
    {
        var plan = Plan(Weak);

        Assert.DoesNotContain(plan.Actions, action => action.SpellId is WeaponBlows.SpellId && action.Kind is TacticActionKind.Cast);
        Assert.Equal(2, plan.Actions.Count(action => action.SpellId is Blow && action.Kind is TacticActionKind.Cast));
    }
}
