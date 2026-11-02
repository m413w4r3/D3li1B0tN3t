using BenchmarkDotNet.Attributes;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Benchmarks.Fights;

/// <summary>A fight's rules on a map of the installed build: the cells a level may be cast on, a line of sight, an effect's reach, and a walk's cells.</summary>
/// <remarks>
/// The map is one a captured fight was fought on, 191104008, read from the installed build's pack, which is never committed:
/// <c>D3B_PACK</c> names a pack, else the pack of the build installed where the launcher puts it. The caster stands on the
/// walkable cell nearest the map's middle with a range bonus of 2, four enemies around it; the level is the first of the
/// pack testing the line of sight at a range of 1 to 6, and the effect a circle of 2. The AI of 9e asks these many times a
/// turn.
/// </remarks>
[MemoryDiagnoser]
public class CastRulesBenchmarks
{
    private const long MapId = 191_104_008;

    private GamePack? _pack;
    private CastRules _rules = null!;
    private FightMoves _moves = null!;
    private FightMap _map = null!;
    private Combatant[] _fighters = [];
    private Combatant _caster = null!;
    private SpellLevel _level;
    private SpellEffect _effect;
    private int _far;

    private static string PackPath()
    {
        var path = Environment.GetEnvironmentVariable("D3B_PACK");

        if (string.IsNullOrEmpty(path))
        {
            var client = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");

            path = PackLocations.PathOf(PackLocations.DefaultRoot, ClientBuild.Read(client));
        }

        return File.Exists(path) ? path : throw new FileNotFoundException($"No pack at {path}: set D3B_PACK, or extract the installed build's with scripts/extract.cs.", path);
    }

    [GlobalSetup]
    public void Setup()
    {
        _pack = GamePack.Open(PackPath());

        var tables = new GameTables(_pack);

        _map = FightMap.Of(new GameMaps(_pack).Get(MapId));
        _rules = new CastRules(tables.Spells, tables.States);
        _moves = new FightMoves(tables.States);
        _level = tables.Spells.Levels.ToArray().First(level => level is { MinRange: 1, Range: 6 } && (level.Properties & SpellLevelProperties.CastTestLos) is not 0);
        _effect = tables.Spells.EffectsOf(_level)[0] with { Zone = new SpellZone { Shape = SpellZoneShape.Circle, Size = 2, ForcedDirection = -1 } };

        var middle = _map.WalkableCells.ToArray().MinBy(cell => MapGeometry.Distance(cell, MapData.CellCount / 2));
        var around = _map.WalkableCells.ToArray().Where(cell => MapGeometry.Distance(cell, middle) is 3).Take(4).ToArray();

        _caster = new Combatant { Id = 1, Team = 0, Cell = middle, ActionPoints = 12, MovementPoints = 6, RangeBonus = 2 };
        _fighters = [_caster, .. around.Select((cell, index) => new Combatant { Id = -1 - index, Team = 1, Cell = cell, Lock = 10, MonsterId = 31 })];
        _far = _map.WalkableCells.ToArray().MaxBy(cell => MapGeometry.Distance(cell, middle));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pack?.Dispose();
    }

    [Benchmark]
    public int CellsToCastOn()
    {
        return _rules.Targets(_map, _fighters, _caster, _level, CastHistory.Empty, 1).Count;
    }

    [Benchmark]
    public bool LineOfSightAcrossTheMap()
    {
        return LineOfSight.IsClear(_map, default, _caster.Cell, _far);
    }

    [Benchmark]
    public int FightersAnEffectReaches()
    {
        return _rules.Reached(_map, _fighters, _caster, _effect, _fighters[1].Cell).Count;
    }

    [Benchmark]
    public int CellsToWalkTo()
    {
        return _moves.Reachable(_map, _fighters, _caster).Count;
    }
}
