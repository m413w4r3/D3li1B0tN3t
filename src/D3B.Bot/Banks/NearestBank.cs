using D3B.Data.Banks;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Bot.Banks;

/// <summary>The bank nearest a zone, for a character of whom nothing is known, as the profile editor's previews say it (D127, D245).</summary>
/// <remarks>
/// The path is found from each bank's front to the zone, criteria not met and no zaap, and a door more into the bank: what a
/// harvest's and a hunt's preview show, the bot itself banking at the nearest bank its own criteria and zaaps reach.
/// </remarks>
public static class NearestBank
{
    /// <summary>Finds the bank nearest a zone's maps.</summary>
    /// <param name="graph">The world graph.</param>
    /// <param name="banks">The banks.</param>
    /// <param name="pathfinder">The pathfinder over the graph.</param>
    /// <param name="maps">The zone's maps.</param>
    /// <returns>The bank's map and the changes of maps from it, or no bank when none reaches the zone on foot.</returns>
    public static (long? Bank, int Changes) Of(WorldGraph graph, BankCatalog banks, WorldPathfinder pathfinder, IReadOnlyCollection<long> maps)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(banks);
        ArgumentNullException.ThrowIfNull(pathfinder);
        ArgumentNullException.ThrowIfNull(maps);

        if (maps.Count is 0)
            return (null, 0);

        var sought = maps.ToHashSet();
        var rules = new WorldPathRules { Facts = new CriterionFacts() };
        (long? Bank, int Changes) nearest = (null, int.MaxValue);

        foreach (var bank in banks.Banks.Span)
        {
            if (!TryGetVertex(graph, bank.MapId, out var vertex) || !pathfinder.TryFind(vertex, sought, rules, out var path))
                continue;

            // The bank's door is one change more, from its front into its inside.
            var changes = path.Steps.Count + 1;

            if (changes < nearest.Changes)
                nearest = (bank.MapId, changes);
        }

        return nearest.Bank is null ? (null, 0) : nearest;
    }

    private static bool TryGetVertex(WorldGraph graph, long mapId, out int vertex)
    {
        for (var zone = 1; zone <= WorldVertex.MaxZone; zone++)
        {
            if (graph.TryGetVertex(mapId, zone, out vertex))
                return true;
        }

        vertex = -1;

        return false;
    }
}
