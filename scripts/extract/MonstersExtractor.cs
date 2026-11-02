using System.Globalization;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the monsters, their grades, spells and characteristic ratios, and their races; and the sub-areas each lists as its own.</summary>
/// <remarks>
/// <para>
/// The server names a monster fighter by its monster and grade, so a fight relies on every monster's grades being its own,
/// numbered from 1 in order, and on each spell having a grade for each of them: the extraction fails otherwise. The spells
/// and starting spells the pack lacks are counted, not relied on yet (client.md).
/// </para>
/// <para>
/// Drops, looks, animations, achievements, idols and the aggression's immunity criteria are left out. A monster's
/// <c>subareas</c> are not kept: the atlas keeps the sub-areas' own lists, which the extraction holds against them
/// (<see cref="WorldExtractor"/>).
/// </para>
/// </remarks>
internal static class MonstersExtractor
{
    private static readonly string[] _tables = ["monsters", "monsterraces", "monstersuperraces"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static (MonsterCatalog Monsters, IReadOnlySet<(int SubAreaId, int MonsterId)> SubAreas) Read(ClientInstall client, SpellCatalog spells)
    {
        try
        {
            var monsters = new List<Monster>();
            var grades = new List<MonsterGrade>();
            var monsterSpells = new List<MonsterSpell>();
            var spellGrades = new List<byte>();
            var ratios = new List<MonsterRatio>();
            var subAreas = new HashSet<(int SubAreaId, int MonsterId)>();

            foreach (var record in UnityDataTable.ReadById(client, "monsters").OrderBy(record => record.Integer("id")))
            {
                var id = checked((int)record.Integer("id"));
                var monsterGrades = record.Items("grades");
                var ids = record.Int32Array("spells");
                var texts = record.StringArray("spellGrades");
                var monsterRatios = record.Items("characRatios");

                if (ids.Length != texts.Length)
                    throw new InvalidDataException($"Monster {id} has {ids.Length} spells and {texts.Length} lists of their grades.");

                monsters.Add(new Monster
                {
                    Id = id,
                    NameId = record.Int32("nameId"),
                    GfxId = record.UInt16("gfxId"),
                    Race = record.Int16("race"),
                    Properties = SpellsExtractor.Known<MonsterProperties>(record.Int32("m_flags"), $"Monster {id}"),
                    FirstGrade = grades.Count,
                    GradeCount = checked((byte)monsterGrades.Count),
                    FirstSpell = monsterSpells.Count,
                    SpellCount = checked((byte)ids.Length),
                    FirstRatio = ratios.Count,
                    RatioCount = checked((byte)monsterRatios.Count),
                    ScaleGradeRef = record.Byte("scaleGradeRef"),
                    SummonCost = record.Int16("summonCost"),
                    IsBounty = record.Boolean("isBounty"),
                    AggressiveZoneSize = record.Byte("aggressiveZoneSize"),
                    AggressiveLevelDiff = record.Int16("aggressiveLevelDiff"),
                    AggressiveAttackDelay = record.Int16("aggressiveAttackDelay"),
                    CorrespondingMiniBossId = record.UInt16("correspondingMiniBossId"),
                    SpeedAdjust = record.SByte("speedAdjust"),
                });

                for (var i = 0; i < monsterGrades.Count; i++)
                    grades.Add(Grade(monsterGrades[i], id, i + 1));

                foreach (var subArea in record.Int32Array("subareas"))
                    subAreas.Add((subArea, id));

                for (var i = 0; i < ids.Length; i++)
                {
                    monsterSpells.Add(new MonsterSpell(ids[i], spellGrades.Count, checked((byte)monsterGrades.Count)));
                    spellGrades.AddRange(SpellGrades(texts[i], monsterGrades.Count, id));
                }

                foreach (var ratio in monsterRatios)
                {
                    var values = ratio.SingleArray("values");

                    // Each ratio is a characteristic's identifier and its ratio, both as floats, in client 3.7.12.12.
                    if (values.Length is not 2 || values[0] != MathF.Round(values[0]))
                        throw new InvalidDataException($"Monster {id} has a characteristic ratio of {values.Length} values, where the extractor measured a characteristic and a ratio.");

                    ratios.Add(new MonsterRatio(checked((int)values[0]), values[1]));
                }
            }

            var races = UnityDataTable.ReadById(client, "monsterraces").Select(record => new MonsterRace(record.Int32("id"), record.Int32("superRaceId"), record.Int32("nameId"))).OrderBy(race => race.Id).ToArray();
            var superRaces = UnityDataTable.ReadById(client, "monstersuperraces").Select(record => new MonsterRace(record.Int32("id"), MonsterRace.NoSuperRace, record.Int32("nameId"))).OrderBy(race => race.Id).ToArray();
            var catalog = new MonsterCatalog(monsters.ToArray(), grades.ToArray(), monsterSpells.ToArray(), spellGrades.ToArray(), ratios.ToArray(), races, superRaces);
            var missingSpells = monsterSpells.Count(spell => !spells.TryGetSpell(spell.SpellId, out _));
            var missingStarts = grades.Count(grade => grade.StartingSpellLevelId is not MonsterGrade.NoStartingSpell && !spells.TryGetLevel(grade.StartingSpellLevelId, out _));
            var missingRaces = monsters.Count(monster => !catalog.TryGetRace(monster.Race, out _));
            var missingSuperRaces = races.Count(race => !catalog.TryGetSuperRace(race.SuperRaceId, out _));

            Console.WriteLine($"Monsters: {monsters.Count:N0}, {grades.Count:N0} grades, {monsterSpells.Count:N0} spells, {ratios.Count:N0} characteristic ratios; {races.Length:N0} races, {superRaces.Length:N0} super-races");
            Console.WriteLine($"  naming a spell the pack lacks: {missingSpells:N0} spells; a starting spell level it lacks: {missingStarts:N0} grades; a race it lacks: {missingRaces:N0} monsters; a super-race it lacks: {missingSuperRaces:N0} races");

            return (catalog, subAreas);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The monsters' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static MonsterGrade Grade(UnityFields record, int monster, int grade)
    {
        if (record.Int32("grade") != grade || record.UInt16("monsterId") != monster)
            throw new InvalidDataException($"Monster {monster} holds grade {record.Int32("grade")} of monster {record.UInt16("monsterId")} as its grade {grade}.");

        return new MonsterGrade
        {
            Grade = checked((byte)grade),
            Level = record.UInt16("level"),
            Experience = record.Int32("xp"),
            StartingSpellLevelId = record.Int32("startingSpellId"),
            Characteristics = Characteristics(record, grade: true),
            Bonus = Characteristics(record.Nested("bonusCharacteristics"), grade: false),
        };
    }

    /// <summary>Reads a grade's characteristics or its bonus, which hold the same fields at other widths.</summary>
    private static MonsterCharacteristics Characteristics(UnityFields record, bool grade)
    {
        short Short(string name)
        {
            return checked((short)record.Integer(name));
        }

        return new MonsterCharacteristics
        {
            LifePoints = record.Int32("lifePoints"),
            ActionPoints = Short("actionPoints"),
            MovementPoints = Short("movementPoints"),
            RangeBonus = Short("rangeBonus"),
            Vitality = record.Int32("vitality"),
            Strength = Short("strength"),
            Intelligence = Short("intelligence"),
            Chance = Short("chance"),
            Agility = Short("agility"),
            Wisdom = Short("wisdom"),
            DamageBonus = Short("damageBonus"),
            PercentDamageBonus = Short("percentDamageBonus"),
            HealBonus = Short("healBonus"),
            TrapDamageBonus = Short("trapDamageBonus"),
            TrapDamageBonusPercent = Short("trapDamageBonusPercent"),
            CriticalDamageBonus = Short("criticalDamageBonus"),
            PushDamageBonus = Short("pushDamageBonus"),
            EarthDamageBonus = Short("earthDamageBonus"),
            FireDamageBonus = Short("fireDamageBonus"),
            WaterDamageBonus = Short("waterDamageBonus"),
            AirDamageBonus = Short("airDamageBonus"),
            NeutralDamageBonus = Short("neutralDamageBonus"),
            CriticalHitBonus = Short("criticalHitBonus"),
            ApAttack = Short("apAttack"),
            MpAttack = Short("mpAttack"),
            ApLostDodge = Short("paLostDodge"),
            MpLostDodge = Short("mpLostDodge"),
            ReductionEarth = Short("reductionEarth"),
            ReductionWater = Short("reductionWater"),
            ReductionFire = Short("reductionFire"),
            ReductionAir = Short("reductionAir"),
            ReductionNeutral = Short("reductionNeutral"),
            ReductionEarthFlat = Short("reductionEarthFlat"),
            ReductionAirFlat = Short("reductionAirFlat"),
            ReductionFireFlat = Short("reductionFireFlat"),
            ReductionWaterFlat = Short("reductionWaterFlat"),
            ReductionNeutralFlat = Short("reductionNeutralFlat"),
            CriticalDamageReduction = Short("criticalDamageReduction"),
            PushDamageReduction = Short("pushDamageReduction"),
            DamageReflect = Short("damageReflect"),
            MaxSummon = Short("maxSummon"),
            HonourPoints = Short("honoursPoints"),
            InitiativeBonus = checked((int)record.Integer("initiativeBonus")),
            TackleBonus = Short("tackleBonus"),
            TackleEvade = Short("tackleEvade"),
            ApRemoval = grade ? (short)0 : Short("aPRemoval"),
        };
    }

    /// <summary>Reads a spell's grade at each of its monster's grades, from the client's text.</summary>
    /// <remarks>
    /// The client writes one pair a grade, the spell's grade and the monster grade's level, separated by <c>;</c>. 1,062 of the
    /// 14,469 lists of client 3.7.12.12 hold more pairs than their monster has grades, the extra ones of no level: they are
    /// left out. A list shorter than the grades leaves the others without a spell grade, 0.
    /// </remarks>
    private static byte[] SpellGrades(string text, int gradeCount, int monster)
    {
        var grades = new byte[gradeCount];
        var pairs = text.Split(';');

        for (var i = 0; i < Math.Min(gradeCount, pairs.Length); i++)
        {
            var parts = pairs[i].Split(',');

            if (parts.Length is not 2 || !byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out grades[i]))
                throw new InvalidDataException($"Monster {monster} lists a spell's grades as '{text}', where the extractor measured pairs of a grade and a level.");
        }

        return grades;
    }
}
