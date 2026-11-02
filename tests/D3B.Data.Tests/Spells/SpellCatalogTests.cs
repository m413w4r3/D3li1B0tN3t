using D3B.Data.Spells;

namespace D3B.Data.Tests.Spells;

public sealed class SpellCatalogTests
{
    [Fact]
    public void ACastFindsItsLevelByIdentifierOrByGrade()
    {
        var catalog = TestTables.Spells();

        Assert.True(catalog.TryGetLevel(1002, out var level));
        Assert.Equal((100, 2), (level.SpellId, (int)level.Grade));
        Assert.True(catalog.TryGetLevel(100, 2, out var byGrade));
        Assert.Equal(1002, byGrade.Id);
        Assert.False(catalog.TryGetLevel(100, 3, out _));
        Assert.False(catalog.TryGetLevel(100, 0, out _));
        Assert.False(catalog.TryGetLevel(300, 1, out _));
        Assert.False(catalog.TryGetLevel(1003, out _));
        Assert.True(catalog.TryGetSpell(100, out var spell));
        Assert.Equal([1001, 1002], catalog.LevelIdsOf(spell).ToArray());
    }

    [Fact]
    public void ALevelHasItsOwnEffectsThenItsCriticalOnes()
    {
        var catalog = TestTables.Spells();

        Assert.True(catalog.TryGetLevel(1001, out var damage));
        Assert.Equal([(96, 10, 12)], catalog.EffectsOf(damage).ToArray().Select(effect => ((int)effect.EffectId, effect.DiceNum, effect.DiceSide)));
        Assert.Equal([(96, 13, 15)], catalog.CriticalEffectsOf(damage).ToArray().Select(effect => ((int)effect.EffectId, effect.DiceNum, effect.DiceSide)));
        Assert.Equal("a,A", catalog.TargetMaskOf(catalog.EffectsOf(damage)[0]));
        Assert.Equal("I", catalog.TriggersOf(catalog.EffectsOf(damage)[0]));
        Assert.Null(catalog.StatesCriterionOf(damage));
        Assert.True(catalog.TryGetLevel(1002, out var summon));
        Assert.Equal((181, TestTables.Bomb, 1), ((int)catalog.EffectsOf(summon)[0].EffectId, catalog.EffectsOf(summon)[0].DiceNum, catalog.EffectsOf(summon)[0].DiceSide));
        Assert.True(catalog.CriticalEffectsOf(summon).IsEmpty);
        Assert.Equal("HS!4", catalog.StatesCriterionOf(summon));
        Assert.Equal(SpellZoneShape.Circle, catalog.PreviewZonesOf(summon)[0].Display.Shape);
    }

    [Fact]
    public void AZoneListsItsOwnCells()
    {
        var catalog = TestTables.Spells();

        Assert.True(catalog.TryGetLevel(2001, out var level));

        var zone = catalog.EffectsOf(level)[0].Zone;

        Assert.Equal(SpellZoneShape.Custom, zone.Shape);
        Assert.Equal([10, 11], catalog.CellsOf(zone).ToArray());
        Assert.True(catalog.CellsOf(catalog.EffectsOf(TestLevel(catalog, 1001))[0].Zone).IsEmpty);
    }

    [Fact]
    public void ASpellFindsItsVariantAndABombItsWall()
    {
        var catalog = TestTables.Spells();

        Assert.True(catalog.TryGetVariantOf(200, out var variant));
        Assert.Equal((481, SpellVariant.CommonBreed, 100, 200), (variant.Id, variant.BreedId, variant.SpellId, variant.VariantSpellId));
        Assert.True(catalog.TryGetVariantOf(100, out _));
        Assert.False(catalog.TryGetVariantOf(300, out _));
        Assert.True(catalog.TryGetBomb(TestTables.Bomb, out var bomb));
        Assert.True(catalog.TryGetBombWall(bomb.WallId, out var wall));
        Assert.Equal((13_458, true, 1, 7), (wall.SpellId, wall.Linear, wall.MinHop, wall.MaxHop));
        Assert.False(catalog.TryGetBomb(31, out _));
    }

    [Fact]
    public void LevelsOutOfOrderAreRejected()
    {
        var catalog = TestTables.Spells();
        var levels = catalog.Levels.ToArray();

        (levels[0], levels[1]) = (levels[1], levels[0]);

        Assert.Throws<ArgumentException>(() => With(catalog, levels: levels));
    }

    [Fact]
    public void EffectsThatDoNotFollowTheirLevelsAreRejected()
    {
        var catalog = TestTables.Spells();

        Assert.Throws<ArgumentException>(() => With(catalog, effects: catalog.Effects[..3]));
    }

    [Fact]
    public void AMaskOrAZoneTheCatalogLacksIsRejected()
    {
        var catalog = TestTables.Spells();
        var effects = catalog.Effects.ToArray();
        var masks = (SpellEffect[])effects.Clone();

        masks[0] = masks[0] with { TargetMask = catalog.Strings.Length };
        effects[3] = effects[3] with { Zone = effects[3].Zone with { CellCount = 3 } };

        Assert.Throws<ArgumentException>(() => With(catalog, effects: masks));
        Assert.Throws<ArgumentException>(() => With(catalog, effects: effects));
    }

    [Fact]
    public void LevelsNoSpellListsFollowTheCatalogsWithTheirEffects()
    {
        var catalog = TestTables.Spells();
        var blow = catalog.Effects.Span[0] with { EffectId = 97, DiceNum = 16, DiceSide = 25 };
        var extended = catalog.WithLevels(
            [new SpellLevel { Id = 1_006_751, SpellId = 0, Grade = 1, ApCost = 4, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 }],
            [blow]);

        Assert.True(extended.TryGetLevel(1_006_751, out var level));
        Assert.Equal([blow], extended.EffectsOf(level).ToArray());
        Assert.Equal(catalog.Levels.Length + 1, extended.Levels.Length);
        Assert.Equal(catalog.EffectsOf(TestLevel(catalog, 1001)).ToArray(), extended.EffectsOf(TestLevel(extended, 1001)).ToArray());
        Assert.Equal(catalog.LevelIds.ToArray(), extended.LevelIds.ToArray());
        Assert.False(catalog.TryGetLevel(1_006_751, out _));
        Assert.Throws<ArgumentException>(() => catalog.WithLevels([new SpellLevel { Id = 1001, StatesCriterion = SpellLevel.NoCriterion }], []));
    }

    private static SpellLevel TestLevel(SpellCatalog catalog, int id)
    {
        Assert.True(catalog.TryGetLevel(id, out var level));

        return level;
    }

    private static SpellCatalog With(SpellCatalog catalog, ReadOnlyMemory<SpellLevel>? levels = null, ReadOnlyMemory<SpellEffect>? effects = null)
    {
        return new SpellCatalog(
            catalog.Spells,
            levels ?? catalog.Levels,
            catalog.LevelIds,
            effects ?? catalog.Effects,
            catalog.PreviewZones,
            catalog.ZoneCells,
            catalog.Strings,
            catalog.Variants,
            catalog.Bombs,
            catalog.BombWalls);
    }
}
