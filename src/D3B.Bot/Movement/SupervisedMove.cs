using System.Collections.Frozen;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Engine.World;
using D3B.Network.Relay;

namespace D3B.Bot.Movement;

/// <summary>Plans and executes one supervised map change to a directly neighbouring map, with no follow-up.</summary>
/// <remarks>
/// <para>
/// The move is deliberately narrower than a journey: both maps are named by the supervisor, the destination must be one
/// of the source map's four neighbours, and the pack must hold one plain <see cref="WorldTransitionType.Scroll"/> edge
/// from the character's current vertex to the destination. Doors, map actions, scrolls with an action, teleports, zaaps,
/// NPC ways, haven bags and multi-map paths are refused. Restricting the transition type also keeps
/// <see cref="MapChange"/> on its scroll path, which sends at most one walk to the edge and one map-change request.
/// </para>
/// <para>
/// The planning pass is passive. Execution runs <see cref="MapChange.ChangeMapAsync(BotContext, WorldStep, WorldPathRules, CancellationToken)"/>
/// exactly once with the prevalidated step and rules; no loop, retry or journey follows. A successful verdict requires the
/// server to have named the target map and placed the character there with a trusted cell.
/// </para>
/// </remarks>
internal static class SupervisedMove
{
    /// <summary>Reads what the bot's session and position allow, without sending a request.</summary>
    /// <param name="bot">The bot.</param>
    /// <returns>The passive readiness.</returns>
    /// <exception cref="ArgumentNullException">The bot is <see langword="null"/>.</exception>
    public static MovementReadiness Readiness(BotActor bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        return MovementReadiness.Read(bot);
    }

    /// <summary>Checks one explicit source and target pair without consuming the single move authorization.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="sourceMapId">The map the character must stand on.</param>
    /// <param name="targetMapId">The directly neighbouring map to reach.</param>
    /// <returns>The plan, ready or naming its first blocker.</returns>
    /// <exception cref="ArgumentNullException">The bot is <see langword="null"/>.</exception>
    public static SupervisedMovePlan Plan(BotActor bot, long sourceMapId, long targetMapId)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var readiness = MovementReadiness.Read(bot);

        if (sourceMapId <= 0)
            return SupervisedMovePlan.Blocked("INVALID_SOURCE_MAP", readiness);

        if (targetMapId <= 0)
            return SupervisedMovePlan.Blocked("INVALID_TARGET_MAP", readiness);

        if (!readiness.CanMove)
            return SupervisedMovePlan.Blocked(readiness.Reason, readiness);

        if (sourceMapId == targetMapId)
            return SupervisedMovePlan.Blocked("SAME_MAP", readiness);

        var state = bot.State;

        // The session and state can change between the readiness read and this snapshot; refuse from the same snapshot
        // the plan is built on rather than planning from a position that is no longer actionable.
        var currentGuard = MovementGuard.Refuse(state, bot.IsConnected);

        if (currentGuard is not MovementRefusal.None)
            return SupervisedMovePlan.Blocked(MovementReadiness.CodeOf(currentGuard), readiness);

        if (state.IsInHavenBag)
            return SupervisedMovePlan.Blocked("HAVEN_BAG", readiness);

        if (state.MapId != sourceMapId)
            return SupervisedMovePlan.Blocked("SOURCE_MAP_MISMATCH", readiness);

        if (state.Cell is not { } currentCell)
            return SupervisedMovePlan.Blocked("GUARD_CELL_UNKNOWN", readiness);

        if (!bot.Maps.TryGet(sourceMapId, out var sourceMap))
            return SupervisedMovePlan.Blocked("SOURCE_MAP_UNKNOWN", readiness);

        if (!bot.Maps.Contains(targetMapId))
            return SupervisedMovePlan.Blocked("TARGET_MAP_UNKNOWN", readiness);

        if (!bot.World.Graph.Contains(targetMapId))
            return SupervisedMovePlan.Blocked("TARGET_NOT_IN_GRAPH", readiness);

        if (!bot.World.TryGetVertex(sourceMap, currentCell, out var vertex))
            return SupervisedMovePlan.Blocked("SOURCE_VERTEX_UNKNOWN", readiness);

        var rules = RulesFor(bot, state);

        if (!bot.World.TryFind(vertex, new HashSet<long> { targetMapId }, rules, out var path))
            return SupervisedMovePlan.Blocked(ClassifyFailure(bot, sourceMap, vertex, targetMapId), readiness);

        if (path.Steps.Count != 1)
            return SupervisedMovePlan.Blocked(IsNeighbour(sourceMap, targetMapId) ? "MULTI_MAP_PATH" : "TARGET_NOT_NEIGHBOUR", readiness);

        var step = path.Steps[0];

        if (step.IsTeleport || step.IsNpcWay || step.Transition.Type is not WorldTransitionType.Scroll)
            return SupervisedMovePlan.Blocked("FORBIDDEN_TRANSITION", readiness);

        // A plain scroll asks the server for the neighbour it names; it must be the target the supervisor named, not
        // another map the server might redirect to.
        if (step.Transition.MapId is < int.MinValue or > int.MaxValue)
            return SupervisedMovePlan.Blocked("TARGET_MAP_OUT_OF_RANGE", readiness);

        if (step.Transition.MapId != targetMapId)
            return SupervisedMovePlan.Blocked("FORBIDDEN_TRANSITION", readiness);

        if (!IsNeighbour(sourceMap, targetMapId))
            return SupervisedMovePlan.Blocked("TARGET_NOT_NEIGHBOUR", readiness);

        if (bot.World.Graph.Vertices.Span[step.To].MapId != targetMapId)
            return SupervisedMovePlan.Blocked("TARGET_NOT_NEIGHBOUR", readiness);

        var unsafeWalk = UnsafeWalk(bot.World, sourceMap, state, currentCell, step, rules, out var walk);

        if (unsafeWalk is not null)
            return SupervisedMovePlan.Blocked(unsafeWalk, readiness);

        return new SupervisedMovePlan(true, "PLAN_READY", readiness, step, rules, step.Transition.Type, walk!.End);
    }

    /// <summary>Executes one supervised map change, at most once per call, and verifies the server placement on the target.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="sourceMapId">The map the character must stand on.</param>
    /// <param name="targetMapId">The directly neighbouring map to reach.</param>
    /// <param name="cancellationToken">Stops the move before or while it runs.</param>
    /// <returns>The verdict and its evidence; the method never launches a second transition.</returns>
    /// <exception cref="ArgumentNullException">The bot is <see langword="null"/>.</exception>
    public static async Task<SupervisedMoveResult> ExecuteAsync(BotActor bot, long sourceMapId, long targetMapId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var empty = EvidenceOf(bot, sourceMapId, targetMapId, null);

        if (cancellationToken.IsCancellationRequested)
            return Result(SupervisedMoveOutcome.Interrupted, "CANCELLED", empty);

        var plan = Plan(bot, sourceMapId, targetMapId);
        var evidence = EvidenceOf(bot, sourceMapId, targetMapId, plan);

        if (!plan.Ready || plan.Step is not { } step || plan.Rules is not { } rules)
            return Result(SupervisedMoveOutcome.Blocked, plan.Reason, evidence);

        try
        {
            var plannedCell = plan.Readiness.Cell;
            var outcome = await bot.RunAsync(async (context, token) =>
            {
                var state = context.State;
                var refusal = MovementGuard.Refuse(state, context.IsConnected);

                // Recheck the exact planned start in the behavior. A walked or held state would make MapChange take its
                // held-walk recovery path, which sends another movement request before the transition.
                if (refusal is not MovementRefusal.None
                    || state.MapId != sourceMapId
                    || plannedCell is not { } cell
                    || state.Cell != cell
                    || state.IsWalking
                    || state.IsWalkHeld)
                    return MapChangeOutcome.Busy;

                var source = context.Maps.Get(sourceMapId);

                if (UnsafeWalk(context.World, source, state, cell, step, rules, out _) is not null)
                    return MapChangeOutcome.Busy;

                return await context.ChangeMapAsync(step, rules, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            var state = bot.State;

            evidence = evidence with
            {
                EndMapId = state.MapId,
                EndCell = state.Cell,
                EndProvenance = state.Provenance,
                EndPositionTrusted = state.IsPositionTrusted,
            };

            return Verdict(outcome, state, targetMapId, evidence);
        }
        catch (BotInterruptedException exception)
        {
            return Result(SupervisedMoveOutcome.Interrupted, Interrupted(exception.Reason), Ended(bot, evidence));
        }
        catch (OperationCanceledException)
        {
            return Result(SupervisedMoveOutcome.Interrupted, "CANCELLED", Ended(bot, evidence));
        }
        catch (RelayInjectionException)
        {
            var reason = bot.IsMovementTranslationBlocked ? "TRANSLATION_LATCH_CLOSED" : "SEND_BLOCKED";

            return Result(SupervisedMoveOutcome.Blocked, reason, Ended(bot, evidence));
        }
        catch (ObjectDisposedException)
        {
            return Result(SupervisedMoveOutcome.Interrupted, "BOT_STOPPED", Ended(bot, evidence));
        }
        catch (InvalidOperationException)
        {
            return Result(SupervisedMoveOutcome.Blocked, "BEHAVIOR_REFUSED", Ended(bot, evidence));
        }
        catch (Exception exception)
        {
            return Result(SupervisedMoveOutcome.Failed, "ERROR", Ended(bot, evidence), exception.GetType().Name);
        }
    }

    private static WorldPathRules RulesFor(BotActor bot, CharacterState state)
    {
        var banned = new HashSet<int>();
        var transitions = bot.World.Graph.Transitions.Span;

        // Only a plain edge scroll: a door, a map action or a scroll with an action could leave by another route or need
        // a second walk, which this first supervised move deliberately does not allow.
        for (var i = 0; i < transitions.Length; i++)
        {
            if (transitions[i].Type is not WorldTransitionType.Scroll)
                banned.Add(i);
        }

        return new WorldPathRules
        {
            Facts = WorldTravel.FactsOf(state, bot.Time.GetUtcNow()),
            Banned = banned,
            KnownZaaps = FrozenSet<long>.Empty,
            NoTeleportFrom = bot.NoTeleportFrom,
            NoNpcWays = Enumerable.Range(0, bot.World.NpcWays.Count).ToHashSet(),
            Kamas = state.Kamas,
        };
    }

    private static string ClassifyFailure(BotActor bot, MapData sourceMap, int vertex, long targetMapId)
    {
        if (!bot.World.Graph.Contains(targetMapId))
            return "TARGET_NOT_IN_GRAPH";

        var (hasEdge, hasAllowed, hasForbidden) = DirectEdge(bot, vertex, targetMapId);

        if (!IsNeighbour(sourceMap, targetMapId))
            return hasEdge && hasForbidden && !hasAllowed ? "FORBIDDEN_TRANSITION" : "TARGET_NOT_NEIGHBOUR";

        if (hasEdge && hasForbidden && !hasAllowed)
            return "FORBIDDEN_TRANSITION";

        return hasEdge && hasAllowed ? "TRANSITION_UNAVAILABLE" : "NO_SIMPLE_TRANSITION";
    }

    private static (bool HasEdge, bool HasAllowed, bool HasForbidden) DirectEdge(BotActor bot, int vertex, long targetMapId)
    {
        var graph = bot.World.Graph;
        var vertices = graph.Vertices.Span;
        var hasEdge = false;
        var hasAllowed = false;
        var hasForbidden = false;

        foreach (var edge in graph.EdgesFrom(vertex))
        {
            if (vertices[edge.To].MapId != targetMapId)
                continue;

            hasEdge = true;

            foreach (var transition in graph.TransitionsOf(edge))
            {
                if (transition.Type is WorldTransitionType.Scroll)
                    hasAllowed = true;
                else
                    hasForbidden = true;
            }
        }

        return (hasEdge, hasAllowed, hasForbidden);
    }

    private static bool IsNeighbour(MapData map, long targetMapId)
    {
        return targetMapId != 0
            && (map.TopNeighbourId == targetMapId
                || map.BottomNeighbourId == targetMapId
                || map.LeftNeighbourId == targetMapId
                || map.RightNeighbourId == targetMapId);
    }

    /// <summary>Gets whether every exit is the cell where the previous walk ended, with the character elsewhere.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="exits">The cells the transition can be left from.</param>
    /// <returns>Whether <see cref="MapWalk"/> would split the walk to avoid that endpoint.</returns>
    internal static bool HasOnlyLastWalkEndExit(CharacterState state, IReadOnlyList<int> exits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(exits);

        return state.LastWalkEnd is { } last
            && state.Cell != last
            && exits.Count > 0
            && exits.All(exit => exit == last);
    }

    // The walk to the exit must stay within the single movement the supervisor authorized: it must not cross a map
    // action cell, which would move the character before the planned scroll, and it must not need the measured
    // last-walk-end split. Returns the first reason it is unsafe, or null with the path MapChange will take.
    private static string? UnsafeWalk(WorldPathfinder world, MapData map, CharacterState state, int from, WorldStep step, WorldPathRules rules, out CellPath? walk)
    {
        walk = null;

        var exits = world.ExitCells(map, step, rules);

        if (exits.Count is 0)
            return "NO_EXIT_CELL";

        if (HasOnlyLastWalkEndExit(state, exits))
            return "LAST_WALK_END_ONLY_EXIT";

        var candidates = exits.Any(exit => exit != state.LastWalkEnd)
            ? exits.Where(exit => exit != state.LastWalkEnd).ToArray()
            : exits.ToArray();

        if (!CellPathfinder.TryFindNearest(map.Cells.Span, from, candidates, out var path))
            return "NO_PATH_TO_EXIT";

        var actions = MapActionCells(world.Graph, map.Id);
        var cells = path.Cells.Span;

        for (var i = 1; i < cells.Length; i++)
        {
            if (actions.Contains(cells[i]))
                return "MAP_ACTION_ON_PATH";
        }

        walk = path;

        return null;
    }

    private static HashSet<int> MapActionCells(WorldGraph graph, long mapId)
    {
        var cells = new HashSet<int>();
        var vertices = graph.Vertices.Span;
        var edges = graph.Edges.Span;

        foreach (var edge in edges)
        {
            if (vertices[edge.From].MapId != mapId)
                continue;

            foreach (var transition in graph.TransitionsOf(edge))
            {
                if (transition.Type is WorldTransitionType.MapAction)
                    cells.Add(transition.CellId);
            }
        }

        return cells;
    }

    private static SupervisedMoveEvidence EvidenceOf(BotActor bot, long sourceMapId, long targetMapId, SupervisedMovePlan? plan)
    {
        var state = bot.State;

        return new SupervisedMoveEvidence
        {
            SourceMapId = sourceMapId,
            TargetMapId = targetMapId,
            StartMapId = state.MapId,
            StartCell = state.Cell,
            StartProvenance = state.Provenance,
            Transition = plan?.Transition,
            TransitionIndex = plan?.Step?.TransitionIndex,
            ExitCell = plan?.ExitCell,
        };
    }

    private static SupervisedMoveEvidence Ended(BotActor bot, SupervisedMoveEvidence evidence)
    {
        var state = bot.State;

        return evidence with
        {
            EndMapId = state.MapId,
            EndCell = state.Cell,
            EndProvenance = state.Provenance,
            EndPositionTrusted = state.IsPositionTrusted,
        };
    }

    private static SupervisedMoveResult Verdict(MapChangeOutcome outcome, CharacterState state, long targetMapId, SupervisedMoveEvidence evidence)
    {
        return outcome switch
        {
            MapChangeOutcome.Changed when state.MapId == targetMapId && state.Cell is null => Result(SupervisedMoveOutcome.Failed, "NOT_PLACED", evidence),
            MapChangeOutcome.Changed when state.MapId == targetMapId && !state.IsPositionTrusted => Result(SupervisedMoveOutcome.Failed, "POSITION_UNTRUSTED", evidence),
            MapChangeOutcome.Changed when state.MapId == targetMapId => Result(SupervisedMoveOutcome.Pass, "PASS", evidence),
            MapChangeOutcome.Changed => Result(SupervisedMoveOutcome.Failed, "UNEXPECTED_MAP", evidence),
            MapChangeOutcome.Elsewhere => Result(SupervisedMoveOutcome.Failed, "UNEXPECTED_MAP", evidence),
            MapChangeOutcome.Busy => Result(SupervisedMoveOutcome.Blocked, "BUSY", evidence),
            MapChangeOutcome.NoPath => Result(SupervisedMoveOutcome.Blocked, "PLAN_INVALID", evidence),
            MapChangeOutcome.NotWalked => Result(SupervisedMoveOutcome.Failed, "WALK_NOT_COMPLETED", evidence),
            MapChangeOutcome.Refused => Result(SupervisedMoveOutcome.Failed, "SERVER_REFUSED", evidence),
            MapChangeOutcome.NoAnswer => Result(SupervisedMoveOutcome.Failed, "NO_ANSWER", evidence),
            MapChangeOutcome.NotPlaced => Result(SupervisedMoveOutcome.Failed, "NOT_PLACED", evidence),
            _ => Result(SupervisedMoveOutcome.Failed, "ERROR", evidence),
        };
    }

    private static string Interrupted(Interruption reason)
    {
        return reason switch
        {
            Interruption.Player => "INTERRUPTED_PLAYER",
            Interruption.Disconnected => "INTERRUPTED_DISCONNECTED",
            Interruption.Fight => "INTERRUPTED_FIGHT",
            _ => "INTERRUPTED_HOST_STOPPING",
        };
    }

    private static SupervisedMoveResult Result(SupervisedMoveOutcome outcome, string reason, SupervisedMoveEvidence evidence, string? detail = null)
    {
        return new SupervisedMoveResult(outcome, reason, evidence, detail);
    }
}
