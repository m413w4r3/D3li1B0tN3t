using MemoryPack;

namespace D3B.Data.Spells;

/// <summary>The spells, their levels and effects, their variants, and the Rogue's bombs and walls.</summary>
/// <remarks>
/// <para>
/// Spells and levels are sorted by identifier and found by binary search, a spell cast naming its level by identifier. A
/// row's list, such as a level's effects, is a slice of one array the catalog holds (<see cref="Tables"/>); the texts of
/// masks, triggers and criteria, a few thousand distinct among a hundred thousand effects, are held once in
/// <see cref="Strings"/> and named by index.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class SpellCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "spells/catalog";

    /// <summary>Gets the spells, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Spell> Spells { get; }

    /// <summary>Gets the spells' levels, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SpellLevel> Levels { get; }

    /// <summary>Gets the levels of every spell by grade, each spell's together, in the order of the spells.</summary>
    public ReadOnlyMemory<int> LevelIds { get; }

    /// <summary>Gets the levels' effects, each level's effects then its critical effects, in the order of the levels.</summary>
    public ReadOnlyMemory<SpellEffect> Effects { get; }

    /// <summary>Gets the levels' preview zones, each level's together, in the order of the levels.</summary>
    public ReadOnlyMemory<SpellPreviewZone> PreviewZones { get; }

    /// <summary>Gets the cells the zones list, each zone's together.</summary>
    public ReadOnlyMemory<int> ZoneCells { get; }

    /// <summary>Gets the texts of the effects' target masks and triggers, the levels' criteria and the preview zones' masks, each once.</summary>
    public ReadOnlyMemory<string> Strings { get; }

    /// <summary>Gets the spells' variants, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SpellVariant> Variants { get; }

    /// <summary>Gets the Rogue's bombs, in ascending order of identifier, their monsters'.</summary>
    public ReadOnlyMemory<SpellBomb> Bombs { get; }

    /// <summary>Gets the walls between bombs, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SpellBombWall> BombWalls { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellCatalog"/> class.</summary>
    /// <param name="spells">The spells, in strictly ascending order of identifier, each naming its own levels.</param>
    /// <param name="levels">The levels, in strictly ascending order of identifier, each naming its own effects and preview zones.</param>
    /// <param name="levelIds">The spells' levels by grade, each spell's together and in the order of the spells.</param>
    /// <param name="effects">The levels' effects, each level's together and in the order of the levels.</param>
    /// <param name="previewZones">The levels' preview zones, each level's together and in the order of the levels.</param>
    /// <param name="zoneCells">The cells the zones list.</param>
    /// <param name="strings">The texts the effects, levels and preview zones name.</param>
    /// <param name="variants">The variants, in strictly ascending order of identifier.</param>
    /// <param name="bombs">The bombs, in strictly ascending order of identifier.</param>
    /// <param name="bombWalls">The walls, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, a slice does not follow the one before, or a row names a cell or a text the catalog lacks.</exception>
    [MemoryPackConstructor]
    public SpellCatalog(
        ReadOnlyMemory<Spell> spells,
        ReadOnlyMemory<SpellLevel> levels,
        ReadOnlyMemory<int> levelIds,
        ReadOnlyMemory<SpellEffect> effects,
        ReadOnlyMemory<SpellPreviewZone> previewZones,
        ReadOnlyMemory<int> zoneCells,
        ReadOnlyMemory<string> strings,
        ReadOnlyMemory<SpellVariant> variants,
        ReadOnlyMemory<SpellBomb> bombs,
        ReadOnlyMemory<SpellBombWall> bombWalls)
    {
        Tables.CheckAscending(spells.Span, static spell => spell.Id, nameof(spells));
        Tables.CheckAscending(levels.Span, static level => level.Id, nameof(levels));
        Tables.CheckAscending(variants.Span, static variant => variant.Id, nameof(variants));
        Tables.CheckAscending(bombs.Span, static bomb => bomb.Id, nameof(bombs));
        Tables.CheckAscending(bombWalls.Span, static wall => wall.Id, nameof(bombWalls));
        Tables.CheckSlices(spells.Span, static spell => (spell.FirstLevel, spell.LevelCount), levelIds.Length, nameof(levelIds));
        Tables.CheckSlices(levels.Span, static level => (level.FirstEffect, level.EffectCount + level.CriticalEffectCount), effects.Length, nameof(effects));
        Tables.CheckSlices(levels.Span, static level => (level.FirstPreviewZone, level.PreviewZoneCount), previewZones.Length, nameof(previewZones));

        foreach (var spell in spells.Span)
            CheckZone(spell.PreviewZone, zoneCells.Length);

        foreach (var level in levels.Span)
            CheckString(level.StatesCriterion, strings.Length, nameof(levels), allowNone: true);

        foreach (var effect in effects.Span)
        {
            CheckZone(effect.Zone, zoneCells.Length);
            CheckString(effect.TargetMask, strings.Length, nameof(effects), allowNone: false);
            CheckString(effect.Triggers, strings.Length, nameof(effects), allowNone: false);
        }

        foreach (var zone in previewZones.Span)
        {
            CheckZone(zone.Display, zoneCells.Length);
            CheckZone(zone.Activation, zoneCells.Length);
            CheckString(zone.CasterMask, strings.Length, nameof(previewZones), allowNone: false);
            CheckString(zone.ActivationMask, strings.Length, nameof(previewZones), allowNone: false);
        }

        Spells = spells;
        Levels = levels;
        LevelIds = levelIds;
        Effects = effects;
        PreviewZones = previewZones;
        ZoneCells = zoneCells;
        Strings = strings;
        Variants = variants;
        Bombs = bombs;
        BombWalls = bombWalls;
    }

    private static void CheckZone(SpellZone zone, int cells)
    {
        if (zone.FirstCell < 0 || zone.FirstCell + zone.CellCount > cells)
            throw new ArgumentException($"A zone lists cells {zone.FirstCell} to {zone.FirstCell + zone.CellCount}, where there are {cells}.", nameof(cells));
    }

    private static void CheckString(int index, int strings, string name, bool allowNone)
    {
        if ((index < 0 || index >= strings) && !(allowNone && index is SpellLevel.NoCriterion))
            throw new ArgumentException($"The {name} name text {index}, where there are {strings}.", name);
    }

    /// <summary>Gets a catalog holding these spells and more levels, which no spell lists: such as the weapons' blows <c>D3B.Engine</c> makes of spell 0.</summary>
    /// <param name="levels">The levels, in strictly ascending order of identifier and above every level of the catalog, each naming its effects in <paramref name="effects"/> from its first, and no preview zone.</param>
    /// <param name="effects">Their effects, each level's together and in the order of the levels, naming the catalog's texts and listing no cell.</param>
    /// <returns>A new catalog, every array copied once: the levels follow the catalog's, and their effects the catalog's.</returns>
    /// <exception cref="ArgumentException">A level is not above the catalog's, or its effects do not follow, or name a text the catalog lacks.</exception>
    public SpellCatalog WithLevels(ReadOnlySpan<SpellLevel> levels, ReadOnlySpan<SpellEffect> effects)
    {
        var allLevels = new SpellLevel[Levels.Length + levels.Length];
        var allEffects = new SpellEffect[Effects.Length + effects.Length];

        Levels.Span.CopyTo(allLevels);
        Effects.Span.CopyTo(allEffects);
        effects.CopyTo(allEffects.AsSpan(Effects.Length));

        for (var index = 0; index < levels.Length; index++)
        {
            allLevels[Levels.Length + index] = levels[index] with
            {
                FirstEffect = levels[index].FirstEffect + Effects.Length,
                FirstPreviewZone = PreviewZones.Length,
                PreviewZoneCount = 0,
            };
        }

        return new SpellCatalog(Spells, allLevels, LevelIds, allEffects, PreviewZones, ZoneCells, Strings, Variants, Bombs, BombWalls);
    }

    /// <summary>Finds a spell.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="spell">The spell, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetSpell(int id, out Spell spell)
    {
        return Tables.TryFind(Spells.Span, id, static spell => spell.Id, out spell);
    }

    /// <summary>Finds a spell level, such as the one a cast names.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="level">The level, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetLevel(int id, out SpellLevel level)
    {
        return Tables.TryFind(Levels.Span, id, static level => level.Id, out level);
    }

    /// <summary>Finds a spell's level by grade, such as one an effect casts.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="grade">The grade, from 1.</param>
    /// <param name="level">The level, when found.</param>
    /// <returns>Whether the catalog holds the spell and the spell that grade.</returns>
    public bool TryGetLevel(int spellId, int grade, out SpellLevel level)
    {
        if (TryGetSpell(spellId, out var spell) && grade >= 1 && grade <= spell.LevelCount)
            return TryGetLevel(LevelIdsOf(spell)[grade - 1], out level);

        level = default;

        return false;
    }

    /// <summary>Gets a spell's levels.</summary>
    /// <param name="spell">A spell of the catalog.</param>
    /// <returns>The identifiers of its levels, grade 1 first.</returns>
    public ReadOnlySpan<int> LevelIdsOf(in Spell spell)
    {
        return LevelIds.Span.Slice(spell.FirstLevel, spell.LevelCount);
    }

    /// <summary>Gets what a level does when cast.</summary>
    /// <param name="level">A level of the catalog.</param>
    /// <returns>Its effects, in the order the client lists them.</returns>
    public ReadOnlySpan<SpellEffect> EffectsOf(in SpellLevel level)
    {
        return Effects.Span.Slice(level.FirstEffect, level.EffectCount);
    }

    /// <summary>Gets what a level does on a critical hit.</summary>
    /// <param name="level">A level of the catalog.</param>
    /// <returns>Its critical effects, in the order the client lists them.</returns>
    public ReadOnlySpan<SpellEffect> CriticalEffectsOf(in SpellLevel level)
    {
        return Effects.Span.Slice(level.FirstEffect + level.EffectCount, level.CriticalEffectCount);
    }

    /// <summary>Gets the zones the client previews for a level.</summary>
    /// <param name="level">A level of the catalog.</param>
    /// <returns>Its preview zones.</returns>
    public ReadOnlySpan<SpellPreviewZone> PreviewZonesOf(in SpellLevel level)
    {
        return PreviewZones.Span.Slice(level.FirstPreviewZone, level.PreviewZoneCount);
    }

    /// <summary>Gets the cells a zone lists.</summary>
    /// <param name="zone">A zone of the catalog.</param>
    /// <returns>Its cells.</returns>
    public ReadOnlySpan<int> CellsOf(in SpellZone zone)
    {
        return ZoneCells.Span.Slice(zone.FirstCell, zone.CellCount);
    }

    /// <summary>Gets which fighters an effect reaches.</summary>
    /// <param name="effect">An effect of the catalog.</param>
    /// <returns>Its target mask as the client writes it, such as <c>a,A</c>, or empty.</returns>
    public string TargetMaskOf(in SpellEffect effect)
    {
        return Strings.Span[effect.TargetMask];
    }

    /// <summary>Gets when an effect applies.</summary>
    /// <param name="effect">An effect of the catalog.</param>
    /// <returns>Its triggers as the client writes them, such as <c>I</c>, or empty.</returns>
    public string TriggersOf(in SpellEffect effect)
    {
        return Strings.Span[effect.Triggers];
    }

    /// <summary>Gets the states a level needs its caster in, or out of.</summary>
    /// <param name="level">A level of the catalog.</param>
    /// <returns>Its criterion as the client writes it, such as <c>HS!4</c>, or null for none.</returns>
    public string? StatesCriterionOf(in SpellLevel level)
    {
        return level.StatesCriterion is SpellLevel.NoCriterion ? null : Strings.Span[level.StatesCriterion];
    }

    /// <summary>Finds the variant a spell is part of.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="variant">The variant, when found.</param>
    /// <returns>Whether the spell has a variant.</returns>
    public bool TryGetVariantOf(int spellId, out SpellVariant variant)
    {
        foreach (var candidate in Variants.Span)
        {
            if (candidate.SpellId == spellId || candidate.VariantSpellId == spellId)
            {
                variant = candidate;

                return true;
            }
        }

        variant = default;

        return false;
    }

    /// <summary>Finds a bomb.</summary>
    /// <param name="monsterId">The bomb's monster.</param>
    /// <param name="bomb">The bomb, when found.</param>
    /// <returns>Whether the monster is a bomb.</returns>
    public bool TryGetBomb(int monsterId, out SpellBomb bomb)
    {
        return Tables.TryFind(Bombs.Span, monsterId, static bomb => bomb.Id, out bomb);
    }

    /// <summary>Finds a wall between bombs.</summary>
    /// <param name="id">Its identifier, as a bomb names it.</param>
    /// <param name="wall">The wall, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetBombWall(int id, out SpellBombWall wall)
    {
        return Tables.TryFind(BombWalls.Span, id, static wall => wall.Id, out wall);
    }
}
