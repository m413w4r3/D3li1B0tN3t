using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Benchmarks.Effects;

/// <summary>What a cast is foreseen to do, on a map of the installed build: each effect, on each fighter it reaches, through the formulas.</summary>
/// <remarks>
/// The map is one a captured fight was fought on, 191104008, read from the installed build's pack, which is never committed:
/// <c>D3B_PACK</c> names a pack, else the pack of the build installed where the launcher puts it. The caster stands on the
/// walkable cell nearest the map's middle, four enemies around it with resistances; the level is spell 13858's first, a
/// fire blow in a circle of 2 with its buffs and the spells it casts, cast on the first enemy. The AI of 9e foresees each
/// cast it weighs, into a list it reuses, its zones kept from one cast to the next (<see cref="ZoneCells"/>, D197); cold,
/// every zone is reckoned again.
/// </remarks>
[MemoryDiagnoser]
public class SpellEffectsBenchmarks
{
    private const long MapId = 191_104_008;
    private const int Spell = 13_858;

    private GamePack? _pack;
    private SpellEffects _effects = null!;
    private FightMap _map = null!;
    private Combatant[] _fighters = [];
    private Combatant _caster = null!;
    private SpellLevel _level;
    private readonly List<ForeseenEffect> _foreseen = [];

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
        var rules = new CastRules(tables.Spells, tables.States);

        _map = FightMap.Of(new GameMaps(_pack).Get(MapId));
        _effects = new SpellEffects(tables.Spells, rules, new EffectActions(tables.Effects));

        if (!tables.Spells.TryGetLevel(Spell, 1, out _level))
            throw new InvalidOperationException($"The pack holds no spell {Spell}.");

        var middle = _map.WalkableCells.ToArray().MinBy(cell => MapGeometry.Distance(cell, MapData.CellCount / 2));
        var around = _map.WalkableCells.ToArray().Where(cell => MapGeometry.Distance(cell, middle) is 3).Take(4).ToArray();
        var resists = ImmutableSortedDictionary<int, int>.Empty.Add(34, 20).Add(55, 2);

        _caster = new Combatant { Id = 1, Team = 0, Cell = middle, ActionPoints = 12, MovementPoints = 6, Level = 19, Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(15, 140).Add(16, 10) };
        _fighters = [_caster, .. around.Select((cell, index) => new Combatant { Id = -1 - index, Team = 1, Cell = cell, MonsterId = 31, Life = 50, MaxLife = 50, Characteristics = resists })];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pack?.Dispose();
    }

    [Benchmark]
    public int ACastForeseen()
    {
        return _effects.Foresee(_map, _fighters, _caster, _level, _fighters[1].Cell, critical: false).Count;
    }

    [Benchmark]
    public int ACastForeseenIntoAList()
    {
        _foreseen.Clear();
        _effects.Foresee(_map, _fighters, _caster, _level, _fighters[1].Cell, false, _foreseen);

        return _foreseen.Count;
    }

    [Benchmark]
    public int ACastForeseenCold()
    {
        ZoneCells.Clear();

        return _effects.Foresee(_map, _fighters, _caster, _level, _fighters[1].Cell, critical: false).Count;
    }
}
