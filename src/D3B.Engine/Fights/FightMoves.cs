using System.Collections.Immutable;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>How a fighter walks in a fight: step by step to the four cells beside it, one MP a step, tackled as it leaves its enemies.</summary>
/// <remarks>
/// <para>
/// Measured on the 298 walks of the 49 fights captured (client.md): each step leads to a cell at a distance of 1, a
/// diagonal of the screen, that a fighter may stand on and no fighter stands on, and the server took one MP a step, the
/// walk never longer than the MP the walker had.
/// </para>
/// <para>
/// A fighter leaving a cell beside enemies that tackle is tackled: it keeps a share of its MP and AP, the product over
/// those enemies of <c>(dodge + 2) / (2 (lock + 2))</c>, each at most 1, and loses the rest, rounded to the nearest, a half
/// up. An enemy tackles when it lives, is seen, no state of its prevents tackling and, a monster, its monster tackles; the
/// walker escapes when a state of its says it cannot be tackled. The formula is the references' idea (D181), measured on
/// the 3 walks tackled: 3 AP and 2 MP lost of 6 and 3 kept at a half, 2 and 1 of 7 and 4 at two thirds, 1 and 1 of 2 and 4
/// at three quarters, the half rounded up; and on 6 walks that lost nothing, leaving monsters that do not tackle or a lock
/// their dodge outweighs. The server announces the tackle (action 104), then each loss (101 AP, 127 MP).
/// </para>
/// <para>Built once per pack and shared.</para>
/// </remarks>
public sealed class FightMoves
{
    private readonly SpellStateCatalog _states;

    /// <summary>Initializes a new instance of the <see cref="FightMoves"/> class.</summary>
    /// <param name="states">The pack's states.</param>
    public FightMoves(SpellStateCatalog states)
    {
        ArgumentNullException.ThrowIfNull(states);

        _states = states;
    }

    /// <summary>Gets the four cells a fighter steps to from a cell: those at a distance of 1, on the map.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <param name="steps">Where to write them: four suffice.</param>
    /// <returns>How many it wrote.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell does not exist.</exception>
    public static int Steps(int cell, Span<int> steps)
    {
        var count = 0;

        for (var direction = 1; direction < MapCell.DirectionCount; direction += 2)
        {
            var next = MapGeometry.Neighbour(cell, direction);

            if (next >= 0)
                steps[count++] = next;
        }

        return count;
    }

    /// <summary>Gets how many points a fighter loses to a tackle.</summary>
    /// <param name="points">Its MP or AP before.</param>
    /// <param name="kept">The share it keeps, from <see cref="KeptLeaving"/>.</param>
    /// <returns>The points it loses.</returns>
    public static int Lost(int points, double kept)
    {
        return points <= 0 ? 0 : (int)Math.Round(points * (1 - kept), MidpointRounding.AwayFromZero);
    }

    /// <summary>Gets the share of its points a fighter keeps leaving a cell: 1 when nothing tackles it.</summary>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="walker">The fighter walking.</param>
    /// <param name="cell">The cell it leaves.</param>
    /// <returns>The share, from 0 to 1.</returns>
    public double KeptLeaving(IReadOnlyList<Combatant> fighters, Combatant walker, int cell)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(walker);

        if (walker.States.Any(state => Has(state, SpellStateProperties.CantBeTackled)))
            return 1;

        var kept = 1.0;

        foreach (var tackler in fighters)
        {
            if (tackler.Team == walker.Team || !Tackles(tackler) || MapGeometry.Distance(cell, tackler.Cell) is not 1)
                continue;

            kept *= Math.Clamp((walker.Dodge + 2) / (2.0 * (tackler.Lock + 2)), 0, 1);
        }

        return kept;
    }

    /// <summary>Gets the cells a fighter may walk to this turn, each with the MP and AP it would have left there.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the walker among them.</param>
    /// <param name="walker">The fighter walking, on a cell.</param>
    /// <returns>Each cell it reaches, its own first, with the points left by the path that keeps the most MP, then AP.</returns>
    public IReadOnlyDictionary<int, (int MovementPoints, int ActionPoints)> Reachable(FightMap map, IReadOnlyList<Combatant> fighters, Combatant walker)
    {
        var reached = new Dictionary<int, (int MovementPoints, int ActionPoints)>();

        Explore(map, fighters, walker, reached, previous: null);

        return reached;
    }

    /// <summary>Gets the path a fighter walks to a cell, the one <see cref="Reachable"/> keeps.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the walker among them.</param>
    /// <param name="walker">The fighter walking, on a cell.</param>
    /// <param name="cell">The cell it walks to.</param>
    /// <returns>Every cell of the path, the walker's first and the cell last; empty when it does not reach the cell.</returns>
    /// <remarks>A walk in a fight names every cell it steps on, one MP each (client.md), so that the bot sends this path's turning points.</remarks>
    public ImmutableArray<int> Path(FightMap map, IReadOnlyList<Combatant> fighters, Combatant walker, int cell)
    {
        var reached = new Dictionary<int, (int MovementPoints, int ActionPoints)>();
        var previous = new Dictionary<int, int>();

        Explore(map, fighters, walker, reached, previous);

        if (!reached.ContainsKey(cell))
            return [];

        var path = new List<int> { cell };

        for (var step = cell; previous.TryGetValue(step, out var before); step = before)
            path.Add(before);

        path.Reverse();

        return [.. path];
    }

    private void Explore(FightMap map, IReadOnlyList<Combatant> fighters, Combatant walker, Dictionary<int, (int MovementPoints, int ActionPoints)> reached, Dictionary<int, int>? previous)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(walker);

        if (walker.Cell is Combatant.NoCell)
            return;

        var blocked = default(CellSet);

        foreach (var fighter in fighters)
        {
            if (fighter.IsAlive && fighter.Id != walker.Id && fighter.Cell is not Combatant.NoCell)
                blocked.Add(fighter.Cell);
        }

        // More MP left on a cell is never worse, since a tackle takes a share: the best label is kept, as Dijkstra would.
        var queue = new PriorityQueue<int, (int, int)>();
        Span<int> steps = stackalloc int[4];

        reached[walker.Cell] = (walker.MovementPoints, walker.ActionPoints);
        queue.Enqueue(walker.Cell, (-walker.MovementPoints, -walker.ActionPoints));

        while (queue.TryDequeue(out var cell, out var priority))
        {
            var (mp, ap) = reached[cell];

            if ((-priority.Item1, -priority.Item2) != (mp, ap))
                continue;

            var kept = KeptLeaving(fighters, walker, cell);
            var leftMp = mp - Lost(mp, kept) - 1;
            var leftAp = ap - Lost(ap, kept);

            if (leftMp < 0)
                continue;

            var count = Steps(cell, steps);

            for (var index = 0; index < count; index++)
            {
                var next = steps[index];

                if (!map.IsWalkable(next) || blocked.Contains(next))
                    continue;

                if (reached.TryGetValue(next, out var best) && (best.MovementPoints, best.ActionPoints).CompareTo((leftMp, leftAp)) >= 0)
                    continue;

                reached[next] = (leftMp, leftAp);
                previous?.Remove(next);
                previous?.Add(next, cell);
                queue.Enqueue(next, (-leftMp, -leftAp));
            }
        }
    }

    private bool Has(int state, SpellStateProperties property)
    {
        return _states.TryGetState(state, out var found) && (found.Properties & property) is not 0;
    }

    private bool Tackles(Combatant fighter)
    {
        return fighter.IsAlive
            && fighter.IsVisible
            && fighter.Cell is not Combatant.NoCell
            && (fighter.MonsterId is null || (fighter.MonsterProperties & MonsterProperties.CanTackle) is not 0)
            && !fighter.States.Any(state => Has(state, SpellStateProperties.CantTackle));
    }
}
