using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.Spells;

namespace D3B.Data.Tests;

internal static class TestTables
{
    /// <summary>A Rogue's bomb, as client 3.7.12.12 numbers it.</summary>
    public const int Bomb = 3112;

    private static readonly int[] _replies = [259, 5422, 63_535];
    private static readonly string[] _weaponZones = ["P", "X1,0,10,1"];
    private static readonly int[] _itemIcons = [34_009, 38_017];
    private static readonly int[] _jobIcons = [1];
    private static readonly int[] _spellIcons = [11_556];
    private static readonly int[] _monsterIcons = [1];
    private static readonly int[] _levelIds = [1001, 1002, 2001];
    private static readonly int[] _zoneCells = [10, 11];
    private static readonly string[] _strings = ["a,A", "I", "HS!4", "C", ""];
    private static readonly int[] _stateEffects = [3];
    private static readonly byte[] _spellGrades = [1, 2];
    private static readonly int[] _breedSpells = [100, 200];
    private static readonly string[] _keywords = ["unknown", "hitPoints", "actionPoints"];
    private static readonly int[] _formulaIds = [4];
    private static readonly string[] _formulas = ["function main()\n   return 1\nend"];

    /// <summary>Wheat and ash wood, of known types, a bread that heals 10 to 15 life, and a test item whose type the client lacks.</summary>
    public static ItemCatalog Items()
    {
        return new ItemCatalog(
            new Item[] { new(289, 1001, 34, 1, 1, 34_009), new(303, 1002, 38, 1, 1, 38_017), new(468, 1004, 33, 1, 1, 0, 10, 15), new(34_569, 1003, 264, 0, 0, 0) },
            new ItemType[] { new(34, 2001, 9, 2), new(38, 2002, 9, 2) });
    }

    /// <summary>A sword striking earth damage and giving strength, and a hammer, as client 3.7.12.12 holds them.</summary>
    public static WeaponCatalog Weapons()
    {
        return new WeaponCatalog(
            new Weapon[]
            {
                new() { ItemId = 6751, TypeId = 6, ApCost = 4, MinRange = 1, Range = 1, CriticalHitProbability = 30, CriticalHitBonus = 10, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 0, EffectCount = 2 },
                new() { ItemId = 7000, TypeId = 7, ApCost = 5, MinRange = 1, Range = 1, CriticalHitProbability = 25, CriticalHitBonus = 5, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 2, EffectCount = 1 },
            },
            new WeaponEffect[] { new() { EffectId = 97, DiceNum = 16, DiceSide = 25 }, new() { EffectId = 118, DiceNum = 21, DiceSide = 30 }, new() { EffectId = 100, DiceNum = 10, DiceSide = 20 } },
            new WeaponType[] { new() { Id = 6, Zone = 0 }, new() { Id = 7, Zone = 1 } },
            _weaponZones);
    }

    /// <summary>The lumberjack cuts ash; using a zaap belongs to no profession.</summary>
    public static JobCatalog Jobs()
    {
        return new JobCatalog(
            new Job[] { new(Job.Base, 3001, Job.NoIcon), new(2, 3002, 1) },
            new Skill[] { new(6, 4001, 2, 303, 1, 1, false), new(184, 4002, Job.Base, Skill.NoItem, 0, 1, false) });
    }

    public static InteractiveCatalog Interactives()
    {
        return new InteractiveCatalog(new Interactive[] { new(1, 5001), new(16, 5002) });
    }

    /// <summary>
    /// Wheat's and ash wood's icons, every pixel 1 and 2; the lumberjack's, every pixel 3; a monster's, every pixel 5; at 64
    /// pixels. A spell's, every pixel 4, at 48.
    /// </summary>
    public static IconCatalog Icons()
    {
        const int Large = 64 * 64 * 4;

        return new IconCatalog(
            new IconTable(64, _itemIcons, Enumerable.Repeat((byte)1, Large).Concat(Enumerable.Repeat((byte)2, Large)).ToArray()),
            new IconTable(64, _jobIcons, Enumerable.Repeat((byte)3, Large).ToArray()),
            new IconTable(48, _spellIcons, Enumerable.Repeat((byte)4, 48 * 48 * 4).ToArray()),
            new IconTable(64, _monsterIcons, Enumerable.Repeat((byte)5, Large).ToArray()));
    }

    /// <summary>One bank; banker 100 has two chest replies, banker 6394 one.</summary>
    public static BankCatalog Banks()
    {
        return new BankCatalog(
            new Bank[] { new(2756, 191_104_002) },
            new Banker[] { new(100, 6001, 0, 2), new(6394, 6002, 2, 1) },
            _replies);
    }

    /// <summary>
    /// Spell 100 in two grades: level 1001 deals water damage, 10 to 12, or 13 to 15 on a critical hit; level 1002, cast
    /// out of state 4, summons a bomb, previewed in a circle. Spell 200's level 2001 reaches two cells. Both are a variant
    /// of the spells every character has; the bomb makes a wall.
    /// </summary>
    public static SpellCatalog Spells()
    {
        var point = new SpellZone { Shape = SpellZoneShape.Point, Size = 0, MinSize = 0, DamageDecreaseStepPercent = 10, MaxDamageDecreaseApplyCount = 4, ForcedDirection = -1 };
        var cells = point with { Shape = SpellZoneShape.Custom, FirstCell = 0, CellCount = 2 };

        return new SpellCatalog(
            new Spell[]
            {
                new() { Id = 100, NameId = 7001, FirstLevel = 0, LevelCount = 2, PreviewZone = point },
                new() { Id = 200, NameId = 7002, FirstLevel = 2, LevelCount = 1 },
            },
            new SpellLevel[]
            {
                new() { Id = 1001, SpellId = 100, Grade = 1, ApCost = 3, Range = 5, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1, CriticalEffectCount = 1 },
                new() { Id = 1002, SpellId = 100, Grade = 2, ApCost = 3, StatesCriterion = 2, Properties = SpellLevelProperties.NeedFreeCell, FirstEffect = 2, EffectCount = 1, PreviewZoneCount = 1 },
                new() { Id = 2001, SpellId = 200, Grade = 1, ApCost = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 1, FirstPreviewZone = 1 },
            },
            _levelIds,
            new SpellEffect[]
            {
                new() { EffectId = 96, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
                new() { EffectId = 96, TargetMask = 0, Triggers = 1, DiceNum = 13, DiceSide = 15, Zone = point },
                new() { EffectId = 181, TargetMask = 3, Triggers = 1, DiceNum = Bomb, DiceSide = 1, Zone = point },
                new() { EffectId = 96, TargetMask = 0, Triggers = 1, DiceNum = 5, DiceSide = 5, Zone = cells },
            },
            new SpellPreviewZone[] { new() { Display = point with { Shape = SpellZoneShape.Circle, Size = 1 }, CasterMask = 4, Activation = point, ActivationMask = 4 } },
            _zoneCells,
            _strings,
            new SpellVariant[] { new(481, SpellVariant.CommonBreed, 100, 200) },
            new SpellBomb[] { new(Bomb, 13_457, 13_455, 2, 13_456, 0) },
            new SpellBombWall[] { new(2, 13_458, 0xFF0000, true, 1, 7) });
    }

    /// <summary>State 3 cannot be moved and has one effect; state 8 does nothing of note.</summary>
    public static SpellStateCatalog States()
    {
        return new SpellStateCatalog(
            new SpellState[] { new(3, 8001, SpellStateProperties.CantBeMoved, 0, 1, 0), new(8, 8002, SpellStateProperties.None, 1, 0, 1002) },
            _stateEffects);
    }

    /// <summary>Water damage, and summoning a monster.</summary>
    public static EffectCatalog Effects()
    {
        return new EffectCatalog(new Effect[]
        {
            new(96, 9001, 0, '\0', 2, 3, 0, 0, 0, 0, EffectProperties.UseDice | EffectProperties.UseInFight),
            new(181, 9002, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.None),
        });
    }

    /// <summary>
    /// Monster 31 in two grades, levels 1 and 2, casting spell 100 at its own grade, its life scaled by a ratio; the bomb,
    /// one grade and no spell. Race 1 is of super-race 2.
    /// </summary>
    public static MonsterCatalog Monsters()
    {
        return new MonsterCatalog(
            new Monster[]
            {
                new() { Id = 31, NameId = 10_001, Race = 1, Properties = MonsterProperties.CanPlay | MonsterProperties.CanTackle, FirstGrade = 0, GradeCount = 2, FirstSpell = 0, SpellCount = 1, FirstRatio = 0, RatioCount = 1 },
                new() { Id = Bomb, NameId = 10_002, Race = 1, Properties = MonsterProperties.UseBombSlot, FirstGrade = 2, GradeCount = 1, FirstSpell = 1, FirstRatio = 1 },
            },
            new MonsterGrade[]
            {
                new() { Grade = 1, Level = 1, Characteristics = new MonsterCharacteristics { LifePoints = 20, ActionPoints = 6, MovementPoints = 3 } },
                new() { Grade = 2, Level = 2, StartingSpellLevelId = 1001, Characteristics = new MonsterCharacteristics { LifePoints = 30, ActionPoints = 7, MovementPoints = 3 } },
                new() { Grade = 1, Level = 1, Characteristics = new MonsterCharacteristics { LifePoints = 10, ActionPoints = 6 } },
            },
            new MonsterSpell[] { new(100, 0, 2) },
            _spellGrades,
            new MonsterRatio[] { new(0, 1.5f) },
            new MonsterRace[] { new(1, 2, 11_001) },
            new MonsterRace[] { new(2, MonsterRace.NoSuperRace, 11_002) });
    }

    /// <summary>Breed 1, with spells 100 and 200, a role, and strength costing 1 a point up to 100, then 2.</summary>
    public static BreedCatalog Breeds()
    {
        return new BreedCatalog(
            new Breed[] { new() { Id = 1, NameId = 12_001, FirstSpell = 0, SpellCount = 2, FirstRole = 0, RoleCount = 1, FirstStatCost = 0, StatCostCount = 2 } },
            _breedSpells,
            new BreedRole[] { new(1, 12_002) },
            new BreedRoleValue[] { new(1, 3, 0) },
            new BreedStatCost[] { new(10, 0, 1), new(10, 100, 2) });
    }

    /// <summary>Three characteristics, the life scaled by formula 4.</summary>
    public static CharacteristicCatalog Characteristics()
    {
        return new CharacteristicCatalog(
            new Characteristic[] { new(-1, 0, 1, 69, Characteristic.NoFormula, false, false), new(0, 13_001, 1, 32, 4, true, false), new(1, 13_002, 2, 0, Characteristic.NoFormula, true, false) },
            _keywords,
            _formulaIds,
            _formulas);
    }
}
