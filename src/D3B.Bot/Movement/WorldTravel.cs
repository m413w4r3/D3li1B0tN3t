using System.Collections.Frozen;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Bot.Movement;

/// <summary>Travelling from map to map: a path through the world, followed one map change at a time (D74).</summary>
/// <remarks>
/// <para>
/// After each change, the path is found again from where the character stands: a scroll with an action may send it
/// elsewhere, two zones of a map may share an edge, and a search costs under a millisecond (D71). A change that fails
/// leaves its transition out of the rest of the journey, as its criterion may not be met for reasons the bot cannot know;
/// a teleport that fails leaves every teleport out (D83). An NPC's way is followed as a transition is (D253).
/// </para>
/// <para>
/// A character in its haven bag, which the world graph does not hold, leaves it first, back to where it entered it (D83).
/// </para>
/// <para>
/// A journey fails after 3 failed changes, or after twice the first path's changes and 10 more, so that a character sent
/// round in circles stops.
/// </para>
/// </remarks>
public static class WorldTravel
{
    private const int MostFailures = 3;
    private const int SpareChanges = 10;

    /// <summary>Travels to a map, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="mapId">The map to go to, any of its zones.</param>
    /// <param name="cancellationToken">Stops the journey.</param>
    /// <returns>How the journey ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<TravelOutcome> TravelAsync(this BotActor bot, long mapId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        FrozenSet<long> maps = [mapId];

        return bot.RunAsync((context, token) => context.TravelAsync(maps, token), cancellationToken);
    }

    /// <summary>Travels to the nearest of several maps, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="maps">The maps sought.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the journey ended.</returns>
    public static Task<TravelOutcome> TravelAsync(this BotContext context, IReadOnlySet<long> maps, CancellationToken cancellationToken)
    {
        return context.TravelAsync(maps, null, cancellationToken);
    }

    /// <summary>Travels to the nearest of several maps, within a behavior, stopping on the first map of the way where the activity has work.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="maps">The maps sought.</param>
    /// <param name="stopAt">Whether the journey ends on a map it reached, the map's information received; never asked of the map it starts from.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the journey ended: <see cref="TravelOutcome.Arrived"/> on a map sought or one <paramref name="stopAt"/> stopped on.</returns>
    /// <remarks>
    /// Measured (D256, 9g5): the fighting activity crossed maps of its zone holding groups it would attack on its way to the
    /// tour's farthest maps. The rest of a shortest path is the shortest from where it stops, so that the journeys after it
    /// do not come back through the maps it left.
    /// </remarks>
    public static async Task<TravelOutcome> TravelAsync(this BotContext context, IReadOnlySet<long> maps, Func<CharacterState, bool>? stopAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(maps);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var from = context.State.MapId;
        long? sought = maps.Count is 1 ? maps.First() : null;
        var (outcome, changes) = await JourneyAsync(context, maps, stopAt, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new TravelEntry(at, depth, from, sought, context.State.MapId, changes, outcome, took));

        return outcome;
    }

    /// <summary>Gets what a character may follow through the world, from its state now.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <returns>
    /// The rules, with what the character's criteria are answered on (<see cref="FactsOf"/>), the zaaps it knows, the kamas it
    /// carries, what a teleport counts as for the running activity (D167), and the teleports and NPCs' ways it was refused
    /// (D248, D253).
    /// </returns>
    public static WorldPathRules RulesOf(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        return new WorldPathRules { Facts = FactsOf(state, context.Time.GetUtcNow()), KnownZaaps = state.KnownZaaps, Kamas = state.Kamas, TeleportChanges = context.TeleportChanges, NoTeleportFrom = context.NoTeleportFrom, NoNpcWays = context.NoNpcWays };
    }

    /// <summary>Gets what a character's criteria are answered on, from its state.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="now">The moment, by the bot's clock, which says whether the account is still subscribed.</param>
    /// <returns>Its level, the objects it holds by item, its professions' levels, its subscription and its map.</returns>
    public static CriterionFacts FactsOf(CharacterState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new CriterionFacts
        {
            Level = state.Level,
            Items = state.Inventory.Values.GroupBy(stack => stack.ItemId).ToDictionary(stacks => stacks.Key, stacks => stacks.Sum(stack => stack.Quantity)),
            JobLevels = state.Jobs.Values.ToDictionary(job => job.JobId, job => job.Level),
            Subscribed = state.IsSubscribedAt(now),
            MapId = state.MapId,
            SubAreaId = state.SubAreaId,
            Cell = state.Cell,
        };
    }

    private static async Task<(TravelOutcome Outcome, int Changes)> JourneyAsync(BotContext context, IReadOnlySet<long> maps, Func<CharacterState, bool>? stopAt, CancellationToken cancellationToken)
    {
        var banned = new HashSet<int>();
        var teleports = true;
        var failures = 0;
        var changes = 0;
        int? mostChanges = null;

        while (true)
        {
            var state = context.State;

            if (state.MapId is not { } mapId || state.Cell is not { } cell || state.IsBusy)
                return (changes is 0 && failures is 0 ? TravelOutcome.Busy : TravelOutcome.Failed, changes);

            if (maps.Contains(mapId) || (changes > 0 && !state.IsInHavenBag && stopAt is not null && stopAt(state)))
                return Over(context, state, TravelOutcome.Arrived, changes);

            if (state.IsInHavenBag)
            {
                if (await context.LeaveHavenBagAsync(cancellationToken).ConfigureAwait(false) is not MapChangeOutcome.Changed)
                    return Over(context, context.State, TravelOutcome.Failed, changes);

                changes++;

                continue;
            }

            var rules = RulesOf(context) with { Banned = banned };

            if (!teleports)
                rules = rules with { KnownZaaps = FrozenSet<long>.Empty };

            if (!context.Maps.TryGet(mapId, out var map) || !context.World.TryGetVertex(map, cell, out var vertex)
                || !context.World.TryFind(vertex, maps, rules, out var path))
            {
                return Over(context, state, TravelOutcome.NoPath, changes);
            }

            mostChanges ??= 2 * path.Steps.Count + SpareChanges;

            if (changes >= mostChanges)
                return Over(context, state, TravelOutcome.Failed, changes);

            if (changes is 0 && failures is 0)
                BotLog.Travelling(context.Log, state.Key.CharacterId, mapId, path.Steps.Count);

            var step = path.Steps[0];

            switch (await context.ChangeMapAsync(step, rules, cancellationToken).ConfigureAwait(false))
            {
                case MapChangeOutcome.Changed or MapChangeOutcome.Elsewhere:
                    changes++;
                    break;
                case MapChangeOutcome.Busy or MapChangeOutcome.NotPlaced:
                    return Over(context, context.State, TravelOutcome.Failed, changes);
                case var failed:
                    if (step.IsTeleport)
                    {
                        teleports = false;

                        // Measured (D248): Incarnam's maps carry the flag, and the server refuses a teleport from them, text 416;
                        // the next journeys tried it again, and failed as many times.
                        if (failed is MapChangeOutcome.Refused)
                            context.RefuseTeleportFrom(mapId);
                    }
                    else
                    {
                        banned.Add(step.TransitionIndex);

                        // A dialog that offers none of its way's replies, such as one asking for a quest first, would offer
                        // none the next time either.
                        if (step.IsNpcWay && failed is MapChangeOutcome.Refused)
                            context.RefuseNpcWay(step.NpcWay);
                    }

                    if (++failures >= MostFailures)
                        return Over(context, context.State, TravelOutcome.Failed, changes);

                    break;
            }
        }
    }

    private static (TravelOutcome Outcome, int Changes) Over(BotContext context, CharacterState state, TravelOutcome outcome, int changes)
    {
        BotLog.Travelled(context.Log, state.Key.CharacterId, state.MapId ?? 0, changes, outcome);

        return (outcome, changes);
    }
}
