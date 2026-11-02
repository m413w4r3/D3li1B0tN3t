using D3B.Data.Effects;
using D3B.Data.Spells;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the spells, their levels and effects, their variants, and the Rogue's bombs and walls.</summary>
/// <remarks>
/// <para>
/// A level keeps every field of the client's, its flags with the bits the client's code gives them, and an effect every
/// field but those that hold the same on every effect of client 3.7.12.12: the extraction fails when one no longer does,
/// or when a flag or a zone's shape is one the extractor does not know, rather than drop what a later client means by it.
/// </para>
/// <para>
/// A cast names its level, so a fight relies on every spell's levels being the spell's, by grade from 1, and on every
/// effect naming an effect of the client's: the extraction fails otherwise. Levels no spell lists, whose spell the client
/// lacks, are left out; what a fight does not rely on yet, such as bombs naming no monster, is counted (client.md).
/// </para>
/// </remarks>
internal static class SpellsExtractor
{
    private static readonly string[] _tables = ["spells", "spelllevels", "spellvariants", "spellbombs", "spellbombwalls"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static SpellCatalog Read(ClientInstall client, EffectCatalog effects)
    {
        try
        {
            var spellRecords = UnityDataTable.ReadById(client, "spells").OrderBy(record => record.Int32("id")).ToArray();
            var levelRecords = UnityDataTable.ReadById(client, "spelllevels").ToDictionary(record => record.Int32("id"));
            var builder = new Builder(effects);
            var spells = new List<Spell>(spellRecords.Length);
            var listed = new HashSet<int>();

            foreach (var record in spellRecords)
            {
                var id = record.Int32("id");
                var levels = record.Int32Array("spellLevels");

                for (var grade = 1; grade <= levels.Length; grade++)
                {
                    if (!levelRecords.TryGetValue(levels[grade - 1], out var level))
                        throw new InvalidDataException($"Spell {id} lists level {levels[grade - 1]}, which the client does not hold.");

                    if (level.Int32("spellId") != id || level.Int32("grade") != grade)
                        throw new InvalidDataException($"Spell {id} lists level {levels[grade - 1]} as its grade {grade}, which is grade {level.Int32("grade")} of spell {level.Int32("spellId")}.");

                    if (!listed.Add(levels[grade - 1]))
                        throw new InvalidDataException($"Level {levels[grade - 1]} is listed twice.");
                }

                spells.Add(new Spell
                {
                    Id = id,
                    NameId = record.Int32("nameId"),
                    TypeId = record.UInt16("typeId"),
                    Order = record.Byte("order"),
                    IconId = record.Int16("iconId"),
                    Properties = Known<SpellProperties>(record.Int32("m_flags"), $"Spell {id}"),
                    FirstLevel = builder.LevelIds.Count,
                    LevelCount = checked((byte)levels.Length),
                    PreviewZone = builder.Zone(record.Nested("basePreviewZoneDescr")),
                });
                builder.LevelIds.AddRange(levels);
            }

            var orphans = 0;

            foreach (var record in levelRecords.Values.OrderBy(record => record.Int32("id")))
            {
                // 69 levels of client 3.7.12.12 name a spell the client lacks, and no spell lists them: no cast can name them.
                if (!listed.Contains(record.Int32("id")))
                {
                    orphans++;

                    continue;
                }

                builder.AddLevel(record);
            }

            var spellIds = spells.Select(spell => spell.Id).ToHashSet();
            var variants = UnityDataTable.ReadById(client, "spellvariants").Select(record => Variant(record, spellIds)).OrderBy(variant => variant.Id).ToArray();
            var bombs = UnityDataTable.ReadById(client, "spellbombs").Select(record => new SpellBomb(
                record.Int32("id"),
                record.Int32("chainReactionSpellId"),
                record.Int32("explodSpellId"),
                record.Int32("wallId"),
                record.Int32("instantSpellId"),
                record.Int32("comboCoeff"))).OrderBy(bomb => bomb.Id).ToArray();
            var walls = UnityDataTable.ReadById(client, "spellbombwalls").Select(record => new SpellBombWall(
                record.Int32("id"),
                record.Int32("spellId"),
                record.Int32("color"),
                record.Boolean("linear"),
                record.Int32("minHop"),
                record.Int32("maxHop"))).OrderBy(wall => wall.Id).ToArray();
            var catalog = builder.Build(spells, variants, bombs, walls);
            var effectCount = catalog.Effects.Length;
            var critical = 0;

            foreach (var level in catalog.Levels.Span)
                critical += level.CriticalEffectCount;

            var bombsWithout = bombs.Count(bomb => !catalog.TryGetSpell(bomb.ExplosionSpellId, out _) || !catalog.TryGetBombWall(bomb.WallId, out _));
            var wallsWithout = walls.Count(wall => !catalog.TryGetSpell(wall.SpellId, out _));

            Console.WriteLine($"Spells: {catalog.Spells.Length:N0}, {catalog.Levels.Length:N0} levels, {effectCount:N0} effects, {critical:N0} of them critical, {catalog.PreviewZones.Length:N0} preview zones, "
                + $"{catalog.ZoneCells.Length:N0} cells listed by zones, {catalog.Strings.Length:N0} masks, triggers and criteria; levels of no spell, left out: {orphans:N0}");
            Console.WriteLine($"Spell variants: {variants.Length:N0}; bombs: {bombs.Length:N0}, {bombsWithout:N0} naming an explosion or a wall the client lacks; walls: {walls.Length:N0}, {wallsWithout:N0} naming a spell the client lacks");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The spells' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static SpellVariant Variant(UnityFields record, HashSet<int> spells)
    {
        var id = record.Int32("id");
        var pair = record.Int32Array("spellIds");

        // Every variant of client 3.7.12.12 pairs two spells the client holds; a character uses one or the other.
        if (pair.Length is not 2)
            throw new InvalidDataException($"Spell variant {id} holds {pair.Length} spells, where the extractor measured 2.");

        foreach (var spell in pair)
        {
            if (!spells.Contains(spell))
                throw new InvalidDataException($"Spell variant {id} names spell {spell}, which the client does not hold.");
        }

        return new SpellVariant(id, record.Int32("breedId"), pair[0], pair[1]);
    }

    /// <summary>Rejects flags with a bit the client's code gives no meaning measured.</summary>
    public static T Known<T>(long value, string owner)
        where T : struct, Enum
    {
        long known = 0;

        foreach (var flag in Enum.GetValues<T>())
            known |= Convert.ToInt64(flag, System.Globalization.CultureInfo.InvariantCulture);

        if ((value & ~known) is not 0)
            throw new InvalidDataException($"{owner} sets bits 0x{value & ~known:X} of its flags, which the extractor does not know: read their meaning in the client's code.");

        return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>Gathers the levels' rows, and the effects, zones' cells and texts they slice.</summary>
    private sealed class Builder
    {
        private readonly EffectCatalog _effects;
        private readonly Dictionary<string, int> _strings = new(StringComparer.Ordinal);
        private readonly List<SpellLevel> _levels = [];
        private readonly List<SpellEffect> _spellEffects = [];
        private readonly List<SpellPreviewZone> _previewZones = [];
        private readonly List<int> _cells = [];

        public List<int> LevelIds { get; } = [];

        public Builder(EffectCatalog effects)
        {
            _effects = effects;
        }

        public void AddLevel(UnityFields record)
        {
            var id = record.Int32("id");
            var effects = record.Items("effects");
            var critical = record.Items("criticalEffect");
            var previews = record.Items("previewZones");
            var criterion = record.String("statesCriterion");
            var level = new SpellLevel
            {
                Id = id,
                SpellId = record.Int32("spellId"),
                Grade = checked((byte)record.Int32("grade")),
                Breed = record.UInt16("spellBreed"),
                ApCost = record.Int16("apCost"),
                MinRange = record.Byte("minRange"),
                Range = record.Byte("range"),
                CriticalHitProbability = record.Int16("criticalHitProbability"),
                MaxStack = record.SByte("maxStack"),
                MaxCastPerTurn = record.SByte("maxCastPerTurn"),
                MaxCastPerTarget = record.Byte("maxCastPerTarget"),
                MaxGlobalCastPerTurn = record.Byte("maxGlobalCastPerTurn"),
                MaxGlobalCastPerTarget = record.Byte("maxGlobalCastPerTarget"),
                MinCastInterval = record.Byte("minCastInterval"),
                InitialCooldown = record.SByte("initialCooldown"),
                GlobalCooldown = record.SByte("globalCooldown"),
                MinPlayerLevel = record.Int16("minPlayerLevel"),
                StatesCriterion = criterion.Length is 0 ? SpellLevel.NoCriterion : String(criterion),
                IgnoresPrisonGlyph = record.Boolean("ignorePrisonGlyph"),
                Properties = Known<SpellLevelProperties>(record.UInt16("m_flags"), $"Spell level {id}"),
                FirstEffect = _spellEffects.Count,
                EffectCount = checked((ushort)effects.Count),
                CriticalEffectCount = checked((ushort)critical.Count),
                FirstPreviewZone = _previewZones.Count,
                PreviewZoneCount = checked((byte)previews.Count),
            };

            foreach (var effect in effects.Concat(critical))
                _spellEffects.Add(Effect(effect, level));

            foreach (var preview in previews)
            {
                _previewZones.Add(new SpellPreviewZone
                {
                    Id = preview.UInt32("id"),
                    Display = Zone(preview.Nested("displayZoneDescr")),
                    IsHidden = preview.Boolean("isPreviewZoneHidden"),
                    CasterMask = String(preview.String("casterMask")),
                    Activation = Zone(preview.Nested("activationZoneDescr")),
                    ActivationMask = String(preview.String("activationMask")),
                });
            }

            _levels.Add(level);
        }

        public SpellZone Zone(UnityFields record)
        {
            var shape = record.Byte("shape");
            var cells = record.Int32Array("cellIds");

            if (!Enum.IsDefined((SpellZoneShape)shape))
                throw new InvalidDataException($"A zone has shape {shape}, which the client's code does not name.");

            var zone = new SpellZone
            {
                Shape = (SpellZoneShape)shape,
                Size = record.Byte("param1"),
                MinSize = record.Byte("param2"),
                DamageDecreaseStepPercent = record.SByte("damageDecreaseStepPercent"),
                MaxDamageDecreaseApplyCount = record.SByte("maxDamageDecreaseApplyCount"),
                StopsAtTarget = record.Boolean("isStopAtTarget"),
                ForcedDirection = record.SByte("forcedDirection"),
                IncludesCarried = record.Boolean("includeCarried"),
                OnlyInLineOfSight = record.Boolean("onlyAffectIfInSightLine"),
                FirstCell = _cells.Count,
                CellCount = checked((byte)cells.Length),
            };

            _cells.AddRange(cells);

            return zone;
        }

        public SpellCatalog Build(List<Spell> spells, SpellVariant[] variants, SpellBomb[] bombs, SpellBombWall[] walls)
        {
            var strings = new string[_strings.Count];

            foreach (var (text, index) in _strings)
                strings[index] = text;

            return new SpellCatalog(spells.ToArray(), _levels.ToArray(), LevelIds.ToArray(), _spellEffects.ToArray(), _previewZones.ToArray(), _cells.ToArray(), strings, variants, bombs, walls);
        }

        private SpellEffect Effect(UnityFields record, SpellLevel level)
        {
            var effectId = record.UInt16("actionId");

            if (!_effects.TryGetEffect(effectId, out _))
                throw new InvalidDataException($"An effect of spell level {level.Id} is effect {effectId}, which the client does not hold.");

            // Constant on every effect of client 3.7.12.12, so left out of the pack: a client setting one means something new.
            if (record.Int16("baseEffectId") is not 0 || record.Int16("targetId") is not 0 || record.Int16("modificator") is not 0 || record.Boolean("displayZero"))
                throw new InvalidDataException($"An effect of spell level {level.Id} sets its base effect, target, modifier or display of 0, which the extractor measured unset everywhere.");

            if (record.Int32("spellId") != level.SpellId)
                throw new InvalidDataException($"An effect of spell level {level.Id} names spell {record.Int32("spellId")}, where its level is spell {level.SpellId}'s.");

            return new SpellEffect
            {
                Uid = record.Int32("effectUid"),
                EffectId = effectId,
                Order = record.Int32("order"),
                TargetMask = String(record.String("targetMask")),
                Duration = record.SByte("duration"),
                Random = record.Single("random"),
                Group = record.Int16("group"),
                Dispellable = record.Byte("dispellable"),
                Delay = record.Byte("delay"),
                Triggers = String(record.String("triggers")),
                Element = record.SByte("effectElement"),
                TriggerDuration = record.Int32("effectTriggerDuration"),
                Value = record.Int32("value"),
                DiceNum = record.Int32("diceNum"),
                DiceSide = record.Int32("diceSide"),
                Properties = Known<SpellEffectProperties>(record.Byte("m_flags"), $"An effect of spell level {level.Id}"),
                Zone = Zone(record.Nested("zoneDescr")),
            };
        }

        private int String(string text)
        {
            if (!_strings.TryGetValue(text, out var index))
            {
                index = _strings.Count;
                _strings.Add(text, index);
            }

            return index;
        }
    }
}
