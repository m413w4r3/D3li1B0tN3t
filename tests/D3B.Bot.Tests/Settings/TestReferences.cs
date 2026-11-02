using D3B.Bot.Settings;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.World;

namespace D3B.Bot.Tests.Settings;

/// <summary>The game's values of a small world: two areas, three sub-areas, a lumberjack and a miner, and two monsters.</summary>
/// <remarks>
/// The forest lists ash and chestnut on its two maps, the hills chestnut on one, the city iron on none; tofus and larvae live
/// in the forest, larvae in the hills, none in the city.
/// </remarks>
internal static class TestReferences
{
    public const int Amakna = 1;
    public const int Astrub = 2;
    public const int Forest = 10;
    public const int Hills = 11;
    public const int City = 20;
    public const int Lumberjack = 2;
    public const int Miner = 24;
    public const int Ash = 303;
    public const int Chestnut = 304;
    public const int Iron = 312;
    public const int Larva = 489;
    public const int Tofu = 98;

    public static WorldAtlas Atlas()
    {
        SubArea[] subAreas = [new(Forest, 110, Amakna, 5, SubArea.NoZaap, true, 0, 2, 0, 2), new(Hills, 111, Amakna, 20, SubArea.NoZaap, true, 2, 1, 2, 1), new(City, 120, Astrub, 1, SubArea.NoZaap, true, 3, 1, 3, 0)];
        Area[] areas = [new(Amakna, 101, 0), new(Astrub, 102, 0)];
        MapPosition[] positions = [new(1_001, 0, 0, Forest, 1, 0, 0), new(1_002, 1, 0, Forest, 1, 0, 0), new(1_003, 2, 0, Hills, 1, 0, 0)];

        return new WorldAtlas(positions, subAreas, areas, Array.Empty<Waypoint>(), new[] { Ash, Chestnut, Chestnut, Iron }, new[] { Tofu, Larva, Larva });
    }

    public static JobCatalog Jobs()
    {
        Job[] jobs = [new(Job.Base, 200, Job.NoIcon), new(Lumberjack, 202, 1), new(Miner, 224, 13)];
        Skill[] skills =
        [
            new(6, 306, Lumberjack, Ash, 1, 1, false),
            new(7, 307, Lumberjack, Chestnut, 10, 1, false),
            new(8, 308, Miner, Iron, 1, 1, false),
            new(184, 384, Job.Base, Skill.NoItem, 0, 1, false),
        ];

        return new JobCatalog(jobs, skills);
    }

    public static ItemCatalog Items()
    {
        Item[] items = [new(Ash, 403, 38, 1, 5, 38_017), new(Chestnut, 404, 38, 10, 5, 38_086), new(Iron, 412, 39, 1, 5, 39_005)];

        return new ItemCatalog(items, Array.Empty<ItemType>());
    }

    public static GameReferences Game()
    {
        return new GameReferences(Atlas(), Jobs(), Items());
    }
}
