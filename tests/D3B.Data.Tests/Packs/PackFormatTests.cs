using System.Security.Cryptography;
using System.Text;
using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Data.Texts;
using D3B.Data.World;
using MemoryPack;

namespace D3B.Data.Tests.Packs;

/// <summary>
/// Fails when what a pack is written as changes while <see cref="GamePack.FormatVersion"/> stays: a host would then
/// misread the packs of its own format (D25).
/// </summary>
/// <remarks>
/// The layout is the SHA-256 of what defines the format: the header and trailer, and, for every type a pack holds, its
/// full name, which the index records and a read checks, and the bytes MemoryPack writes for a sample of it. Every field
/// of a sample has its own value, so that moving, retyping or adding a field changes the bytes; renaming a field does
/// not, and is no format change. The bytes are taken before compression, which changes with zstd's version, not with
/// the format.
/// </remarks>
public sealed class PackFormatTests
{
    // The layout of each format since this record began, oldest first. Once a pack of a format is published, its line is
    // never edited: a changed layout is a new format, and a new line. Format 1, session 2a, held only texts and predates
    // the record. The samples below are part of the layouts recorded here, so they change only with a new format.
    private static readonly (int Format, string Layout)[] _formats =
    [
        (2, "44e092e069deeecb28f1afb3f0619ff7921a30003a89974ecd5d5a93923533c8"),
        (3, "94032ad3554bc9df362222810fcb95534e53d56997b1dc1a839ac598dde39ede"),
        (4, "4f0e5099257dbba4ee548612249d8a5c9c9da7387fdfc765b15132d9c81fa222"),
        (5, "9de3d67e4a573c63eb675ab6c1ec13cc5e0b653014fa144c5b3de3c408867ff7"),
        (6, "1c90f607d5c45ba002152f90ea53357e25606148b2c9536c4ac1a53b1c708907"),
        (7, "ce9c6ec360dcce84bd518b449148b2a40d44a05a7ce6d19e618ef0780c300a6b"),
        (8, "8a4430ff75cfa0a4f52eaa0fbb717cbfa7c45b4c0a23157e31da9d348359ebcc"),
    ];

    private static readonly string[] _criteria = ["c24"];
    private static readonly int[] _harvestables = [43];
    private static readonly int[] _inhabitants = [210, 211];
    private static readonly int[] _replies = [67];
    private static readonly int[] _itemIcons = [71];
    private static readonly int[] _jobIcons = [72];
    private static readonly int[] _spellIcons = [208];
    private static readonly int[] _monsterIcons = [212];
    private static readonly int[] _levelIds = [97, 98];
    private static readonly int[] _zoneCells = [99, 100, 101];
    private static readonly string[] _strings = ["s0", "s1", "s2", "s3", "s4"];
    private static readonly int[] _stateEffects = [150, 151];
    private static readonly byte[] _spellGrades = [3, 4];
    private static readonly int[] _breedSpells = [175, 176];
    private static readonly string[] _keywords = ["k185"];
    private static readonly int[] _formulaIds = [186];
    private static readonly string[] _formulas = ["f187"];
    private static readonly string[] _weaponZones = ["z203"];

    [Fact]
    public void TheLastRecordedFormatIsTheCurrentOne()
    {
        var format = _formats[^1].Format;

        Assert.True(
            format == GamePack.FormatVersion,
            $"GamePack.FormatVersion is {GamePack.FormatVersion}, where the last recorded format is {format}: record the layout of format {GamePack.FormatVersion}.");

        for (var i = 1; i < _formats.Length; i++)
            Assert.True(_formats[i].Format > _formats[i - 1].Format, "The recorded formats must be in ascending order.");
    }

    [Fact]
    public void TheSerializedLayoutChangesOnlyWithTheFormatVersion()
    {
        var (format, recorded) = _formats[^1];
        var current = Layout();

        Assert.True(
            current == recorded,
            $"The pack's layout is now {current}, where format {format} recorded {recorded}: raise GamePack.FormatVersion, then record the new layout under it.");
    }

    private static string Layout()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(PackLayout.Magic);
        hash.AppendData(BitConverter.GetBytes(PackLayout.HeaderLength));
        hash.AppendData(BitConverter.GetBytes(PackLayout.TrailerLength));

        Append(hash, new PackIndex
        {
            Info = new PackInfo
            {
                ClientVersion = "3.7.12.12",
                ClientBuildDate = new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.FromHours(2)),
                Fingerprint = "f1",
                ExtractorVersion = "e2",
            },
            Entries = [new PackEntry { Name = "n3", Type = "t4", Offset = 5, CompressedLength = 6, Length = 7 }],
        });
        Append(hash, new TextTable("fr", [8, 9], ["a", "b"]));
        Append(hash, new MapData(
            10,
            11,
            12,
            13,
            14,
            Enumerable.Repeat(new MapCell(MapCellProperties.Walkable | MapCellProperties.Blue, -130, 15, 0x83, 1, 0x21, 9), MapData.CellCount).ToArray(),
            new MapInteractive[] { new(16, 17, 70) }));
        Append(hash, new WorldGraph(
            new WorldVertex[] { new(18, 2), new(19, 3) },
            new WorldEdge[] { new(0, 1, 0, 1) },
            new WorldTransition[] { new(WorldTransitionType.Interactive, 5, 20, 21, 22, 23, 0) },
            _criteria));
        Append(hash, new WorldAtlas(
            new MapPosition[] { new(25, 26, 27, 28, 29, 30, 31) },
            new SubArea[] { new(32, 33, 34, 35, 36, true, 0, 1, 0, 2) },
            new Area[] { new(37, 38, 39) },
            new Waypoint[] { new(40, 41, 42, true) },
            _harvestables,
            _inhabitants));
        Append(hash, new ItemCatalog(new Item[] { new(44, 45, 46, 47, 48, 68, 214, 215) }, new ItemType[] { new(49, 50, 51, 52) }));
        Append(hash, new WeaponCatalog(
            new Weapon[] { new() { ItemId = 195, TypeId = 196, ApCost = 197, MinRange = 198, Range = 199, CriticalHitProbability = 200, CriticalHitBonus = 201, MaxCastPerTurn = 3, Properties = SpellLevelProperties.CastInDiagonal, FirstEffect = 0, EffectCount = 1 } },
            new WeaponEffect[] { new() { EffectId = 202, DiceNum = 204, DiceSide = 205, Value = 206 } },
            new WeaponType[] { new() { Id = 207, Zone = 0 } },
            _weaponZones));
        Append(hash, new JobCatalog(new Job[] { new(53, 54, 69) }, new Skill[] { new(55, 56, 57, 58, 59, 60, true) }));
        Append(hash, new InteractiveCatalog(new Interactive[] { new(61, 62) }));
        Append(hash, new BankCatalog(new Bank[] { new(63, 64) }, new Banker[] { new(65, 66, 0, 1) }, _replies));
        Append(hash, new IconCatalog(
            new IconTable(2, _itemIcons, Pixels(73, 2)),
            new IconTable(3, _jobIcons, Pixels(74, 3)),
            new IconTable(4, _spellIcons, Pixels(209, 4)),
            new IconTable(5, _monsterIcons, Pixels(213, 5))));
        Append(hash, new SpellCatalog(
            new Spell[] { new() { Id = 75, NameId = 76, TypeId = 77, Order = 78, IconId = 79, Properties = SpellProperties.BypassSummoningLimit, FirstLevel = 0, LevelCount = 2, PreviewZone = Zone(102) } },
            new SpellLevel[]
            {
                new()
                {
                    Id = 80,
                    SpellId = 81,
                    Grade = 82,
                    Breed = 83,
                    ApCost = 84,
                    MinRange = 85,
                    Range = 86,
                    CriticalHitProbability = 87,
                    MaxStack = 88,
                    MaxCastPerTurn = 89,
                    MaxCastPerTarget = 90,
                    MaxGlobalCastPerTurn = 91,
                    MaxGlobalCastPerTarget = 92,
                    MinCastInterval = 93,
                    InitialCooldown = 94,
                    GlobalCooldown = 95,
                    MinPlayerLevel = 96,
                    StatesCriterion = 1,
                    IgnoresPrisonGlyph = true,
                    Properties = SpellLevelProperties.CastTestLos,
                    FirstEffect = 0,
                    EffectCount = 2,
                    CriticalEffectCount = 1,
                    FirstPreviewZone = 0,
                    PreviewZoneCount = 1,
                },
            },
            _levelIds,
            new[] { Effect(110), Effect(130), Effect(160) },
            new SpellPreviewZone[] { new() { Id = 140, Display = Zone(141), IsHidden = true, CasterMask = 2, Activation = Zone(142), ActivationMask = 3 } },
            _zoneCells,
            _strings,
            new SpellVariant[] { new(143, 144, 145, 146) },
            new SpellBomb[] { new(147, 148, 149, 152, 153, 154) },
            new SpellBombWall[] { new(155, 156, 157, true, 158, 159) }));
        Append(hash, new SpellStateCatalog(new SpellState[] { new(161, 162, SpellStateProperties.Incurable, 0, 2, 163) }, _stateEffects));
        Append(hash, new EffectCatalog(new Effect[] { new(164, 165, 166, '/', 167, 5, -1, 168, 169.5f, 170, EffectProperties.UseDice) }));
        Append(hash, new MonsterCatalog(
            new Monster[]
            {
                new()
                {
                    Id = 171,
                    NameId = 172,
                    GfxId = 173,
                    Race = 174,
                    Properties = MonsterProperties.CanTackle,
                    FirstGrade = 0,
                    GradeCount = 1,
                    FirstSpell = 0,
                    SpellCount = 1,
                    FirstRatio = 0,
                    RatioCount = 1,
                    ScaleGradeRef = 5,
                    SummonCost = 6,
                    IsBounty = true,
                    AggressiveZoneSize = 7,
                    AggressiveLevelDiff = 8,
                    AggressiveAttackDelay = 9,
                    CorrespondingMiniBossId = 10,
                    SpeedAdjust = 11,
                },
            },
            new MonsterGrade[] { new() { Grade = 12, Level = 13, Experience = 14, StartingSpellLevelId = 15, Characteristics = Characteristics(200), Bonus = Characteristics(300) } },
            new MonsterSpell[] { new(16, 0, 2) },
            _spellGrades,
            new MonsterRatio[] { new(17, 18.5f) },
            new MonsterRace[] { new(19, 20, 21) },
            new MonsterRace[] { new(22, MonsterRace.NoSuperRace, 23) }));
        Append(hash, new BreedCatalog(
            new Breed[] { new() { Id = 177, NameId = 178, FirstSpell = 0, SpellCount = 2, FirstRole = 0, RoleCount = 1, FirstStatCost = 0, StatCostCount = 1 } },
            _breedSpells,
            new BreedRole[] { new(179, 180) },
            new BreedRoleValue[] { new(181, 182, 183) },
            new BreedStatCost[] { new(184, 188, 189) }));
        Append(hash, new CharacteristicCatalog(new Characteristic[] { new(190, 191, 192, 193, 194, true, false) }, _keywords, _formulaIds, _formulas));

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static SpellZone Zone(int seed)
    {
        return new SpellZone
        {
            Shape = SpellZoneShape.Cone,
            Size = (byte)seed,
            MinSize = (byte)(seed + 1),
            DamageDecreaseStepPercent = 24,
            MaxDamageDecreaseApplyCount = 25,
            StopsAtTarget = true,
            ForcedDirection = 26,
            IncludesCarried = false,
            OnlyInLineOfSight = true,
            FirstCell = 1,
            CellCount = 2,
        };
    }

    private static SpellEffect Effect(int seed)
    {
        return new SpellEffect
        {
            Uid = seed,
            EffectId = (ushort)(seed + 1),
            Order = seed + 2,
            TargetMask = 2,
            Duration = 27,
            Random = seed + 3.5f,
            Group = (short)(seed + 4),
            Dispellable = 28,
            Delay = 29,
            Triggers = 4,
            Element = 30,
            TriggerDuration = seed + 5,
            Value = seed + 6,
            DiceNum = seed + 7,
            DiceSide = seed + 8,
            Properties = SpellEffectProperties.ForClientOnly,
            Zone = Zone(seed + 9),
        };
    }

    private static MonsterCharacteristics Characteristics(int seed)
    {
        return new MonsterCharacteristics
        {
            LifePoints = seed,
            ActionPoints = (short)(seed + 1),
            MovementPoints = (short)(seed + 2),
            RangeBonus = (short)(seed + 3),
            Vitality = seed + 4,
            Strength = (short)(seed + 5),
            Intelligence = (short)(seed + 6),
            Chance = (short)(seed + 7),
            Agility = (short)(seed + 8),
            Wisdom = (short)(seed + 9),
            DamageBonus = (short)(seed + 10),
            PercentDamageBonus = (short)(seed + 11),
            HealBonus = (short)(seed + 12),
            TrapDamageBonus = (short)(seed + 13),
            TrapDamageBonusPercent = (short)(seed + 14),
            CriticalDamageBonus = (short)(seed + 15),
            PushDamageBonus = (short)(seed + 16),
            EarthDamageBonus = (short)(seed + 17),
            FireDamageBonus = (short)(seed + 18),
            WaterDamageBonus = (short)(seed + 19),
            AirDamageBonus = (short)(seed + 20),
            NeutralDamageBonus = (short)(seed + 21),
            CriticalHitBonus = (short)(seed + 22),
            ApAttack = (short)(seed + 23),
            MpAttack = (short)(seed + 24),
            ApLostDodge = (short)(seed + 25),
            MpLostDodge = (short)(seed + 26),
            ReductionEarth = (short)(seed + 27),
            ReductionWater = (short)(seed + 28),
            ReductionFire = (short)(seed + 29),
            ReductionAir = (short)(seed + 30),
            ReductionNeutral = (short)(seed + 31),
            ReductionEarthFlat = (short)(seed + 32),
            ReductionAirFlat = (short)(seed + 33),
            ReductionFireFlat = (short)(seed + 34),
            ReductionWaterFlat = (short)(seed + 35),
            ReductionNeutralFlat = (short)(seed + 36),
            CriticalDamageReduction = (short)(seed + 37),
            PushDamageReduction = (short)(seed + 38),
            DamageReflect = (short)(seed + 39),
            MaxSummon = (short)(seed + 40),
            HonourPoints = (short)(seed + 41),
            InitiativeBonus = seed + 42,
            TackleBonus = (short)(seed + 43),
            TackleEvade = (short)(seed + 44),
            ApRemoval = (short)(seed + 45),
        };
    }

    private static byte[] Pixels(int seed, int size)
    {
        return [.. Enumerable.Range(seed, size * size * 4).Select(value => (byte)value)];
    }

    private static void Append<T>(IncrementalHash hash, T sample)
    {
        var bytes = MemoryPackSerializer.Serialize(sample);

        hash.AppendData(Encoding.UTF8.GetBytes(typeof(T).FullName!));
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
