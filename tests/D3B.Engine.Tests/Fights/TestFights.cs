using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tests.Maps;

namespace D3B.Engine.Tests.Fights;

/// <summary>Maps, fighters and spells a fight's tests build, around cell 286, the point (16, -4), in the map's middle.</summary>
internal static class TestFights
{
    /// <summary>The cell the tests' casters stand on.</summary>
    public const int Middle = 286;

    /// <summary>A walkable cell that blocks a line of sight.</summary>
    public static readonly MapCell Pillar = new(MapCellProperties.Walkable, 0, 0, 0, 0, 1 << 4, 0);

    /// <summary>Gets the cell at coordinates.</summary>
    public static int At(int x, int y)
    {
        Assert.True(MapGeometry.TryGetCell(new MapPoint(x, y), out var cell));

        return cell;
    }

    /// <summary>Gets the cell at an offset from <see cref="Middle"/>, on the coordinates.</summary>
    public static int Off(int dx, int dy)
    {
        return At(16 + dx, -4 + dy);
    }

    /// <summary>A map whose every cell may be walked on and seen through but those listed, which may be neither.</summary>
    public static FightMap Open(params int[] blocked)
    {
        return new FightMap(1, TestCells.OpenBut(blocked));
    }

    /// <summary>An open map whose listed cells may be walked on but block a line of sight.</summary>
    public static FightMap WithPillars(params int[] pillars)
    {
        var cells = TestCells.OpenBut();

        foreach (var pillar in pillars)
            cells[pillar] = Pillar;

        return new FightMap(1, cells);
    }

    /// <summary>The caster, of team 0, on <see cref="Middle"/>, with 6 AP and 3 MP.</summary>
    public static Combatant Caster(int cell = Middle)
    {
        return new Combatant { Id = 1, Team = 0, Cell = cell, ActionPoints = 6, MovementPoints = 3, BreedId = 15, Life = 100, MaxLife = 100 };
    }

    /// <summary>An enemy monster, of team 1.</summary>
    public static Combatant Enemy(long id, int cell)
    {
        return new Combatant { Id = id, Team = 1, Cell = cell, MonsterId = 31, Life = 50, MaxLife = 100, MonsterProperties = Data.Monsters.MonsterProperties.CanTackle | Data.Monsters.MonsterProperties.CanMove | Data.Monsters.MonsterProperties.CanBePushed };
    }

    /// <summary>A zone of a shape and sizes.</summary>
    public static SpellZone Zone(SpellZoneShape shape, int size = 1, int minSize = 0, int forced = -1, bool stops = false, bool seen = false)
    {
        return new SpellZone
        {
            Shape = shape,
            Size = (byte)size,
            MinSize = (byte)minSize,
            DamageDecreaseStepPercent = 10,
            MaxDamageDecreaseApplyCount = 4,
            ForcedDirection = (sbyte)forced,
            StopsAtTarget = stops,
            OnlyInLineOfSight = seen,
        };
    }

    /// <summary>
    /// Spells 10 to 60 of grade 1, levels 1010 to 1060, each with one effect in a point: 10 tests the line of sight at a
    /// range of 1 to 3, twice a turn and once a target; 20 needs a free cell, its range 5 boosted; 30 needs its caster in
    /// state 5, waits a round and every other round; 40 summons on a free cell; 50 needs a taken cell, seen; 60 is cast in
    /// line or diagonal; 70 needs its caster in state 9, which prevents casting.
    /// </summary>
    public static SpellCatalog Spells()
    {
        var point = Zone(SpellZoneShape.Point);
        string[] strings = ["a,A", "I", "HS=5", "HS=9"];

        SpellLevel Level(int spell, int index, SpellLevelProperties properties)
        {
            return new SpellLevel
            {
                Id = 1000 + spell,
                SpellId = spell,
                Grade = 1,
                ApCost = 3,
                MinRange = 1,
                Range = 3,
                StatesCriterion = SpellLevel.NoCriterion,
                Properties = properties,
                FirstEffect = index,
                EffectCount = 1,
            };
        }

        var levels = new[]
        {
            Level(10, 0, SpellLevelProperties.CastTestLos) with { MaxCastPerTurn = 2, MaxCastPerTarget = 1 },
            Level(20, 1, SpellLevelProperties.NeedFreeCell | SpellLevelProperties.RangeCanBeBoosted) with { Range = 5 },
            Level(30, 2, SpellLevelProperties.None) with { StatesCriterion = 2, InitialCooldown = 1, MinCastInterval = 2 },
            Level(40, 3, SpellLevelProperties.NeedFreeCell),
            Level(50, 4, SpellLevelProperties.NeedTakenCell | SpellLevelProperties.NeedVisibleEntity),
            Level(60, 5, SpellLevelProperties.CastInLine | SpellLevelProperties.CastInDiagonal),
            Level(70, 6, SpellLevelProperties.None) with { StatesCriterion = 3 },
        };

        var spells = levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 }).ToArray();
        var effects = levels.Select(level => new SpellEffect { EffectId = level.SpellId is 40 ? (ushort)181 : (ushort)96, TargetMask = 0, Triggers = 1, Zone = point }).ToArray();

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>State 9 prevents casting, 11 tackling, 12 being tackled.</summary>
    public static SpellStateCatalog States()
    {
        return new SpellStateCatalog(
            new SpellState[]
            {
                new(9, 1, SpellStateProperties.PreventsSpellCast, 0, 0, 0),
                new(11, 2, SpellStateProperties.CantTackle, 0, 0, 0),
                new(12, 3, SpellStateProperties.CantBeTackled, 0, 0, 0),
            },
            ReadOnlyMemory<int>.Empty);
    }

    /// <summary>Gets a level of <see cref="Spells"/> by its spell.</summary>
    public static SpellLevel Level(SpellCatalog spells, int spell)
    {
        Assert.True(spells.TryGetLevel(1000 + spell, out var level));

        return level;
    }
}
