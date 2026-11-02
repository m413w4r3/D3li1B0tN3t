using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;

namespace D3B.Engine.Benchmarks.Tactics;

/// <summary>A turn of the fight's AI planned on a map of the installed build: the beam search, then the enemies' reply on its finalists.</summary>
/// <remarks>
/// The map is the one 9d's bench of <c>Foresee</c> uses, 191104008, read from the installed build's pack, which is never
/// committed: <c>D3B_PACK</c> names a pack, else the pack of the build installed where the launcher puts it. The character
/// stands on the walkable cell nearest the map's middle with 12 AP and 3 MP and spells 13858 and 13286 at their first
/// level; four enemies stand 3 to 6 cells away, each casting spell 13858 back (D201).
/// </remarks>
[MemoryDiagnoser]
public class FightTacticsBenchmarks
{
    private const long MapId = 191_104_008;

    private GamePack? _pack;
    private FightTactics _tactics = null!;
    private ImaginedFight _fight = null!;
    private FightKnowledge _knowledge = null!;

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
        var map = FightMap.Of(new GameMaps(_pack).Get(MapId));
        SpellLevel Level(int spell)
        {
            return tables.Spells.TryGetLevel(spell, 1, out var level) ? level : throw new InvalidOperationException($"The pack holds no spell {spell}.");
        }

        _tactics = new FightTactics(tables);

        var middle = map.WalkableCells.ToArray().MinBy(cell => MapGeometry.Distance(cell, MapData.CellCount / 2));
        var around = map.WalkableCells.ToArray().Where(cell => MapGeometry.Distance(cell, middle) is >= 3 and <= 6).Take(4).ToArray();
        var characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(15, 140).Add(16, 10);
        var caster = new Combatant { Id = 1, Team = 0, Cell = middle, ActionPoints = 12, MovementPoints = 3, Life = 400, MaxLife = 400, Level = 50, BreedId = 1, Characteristics = characteristics };
        Combatant[] enemies = [.. around.Select((cell, index) => new Combatant { Id = -1 - index, Team = 1, Cell = cell, MonsterId = 31, Life = 80, MaxLife = 80, ActionPoints = 6, MovementPoints = 3, MonsterProperties = Data.Monsters.MonsterProperties.CanMove | Data.Monsters.MonsterProperties.CanTackle })];
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(caster.Id, [Level(13_858), Level(13_286)]);

        foreach (var enemy in enemies)
            spells = spells.Add(enemy.Id, [Level(13_858)]);

        _fight = new ImaginedFight { Map = map, Fighters = [caster, .. enemies], ActorId = caster.Id, Round = 1 };
        _knowledge = new FightKnowledge { Spells = spells };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pack?.Dispose();
    }

    [Benchmark]
    public int ATurnPlanned()
    {
        return _tactics.Plan(_fight, _knowledge, TacticWeights.Default, TurnSearchLimits.Default).Weighed;
    }
}
