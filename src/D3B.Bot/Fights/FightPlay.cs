using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>The AI playing the character's fight, one behavior from the placement to the end (D201, D202).</summary>
/// <remarks>
/// <para>
/// The bot sends every request the player would, the client following the server's answers as after its own (D60): the
/// placement's cell, its readiness, then each turn its walks and casts, one at a time, and the turn's end. Once the fight's
/// map is drawn, its display time, it judges the fight's odds, prudent when it would lose (D198), places the character on
/// the cell the AI judges best and says it is ready. Each turn it searches the best sequence (<see cref="FightTactics.Plan"/>),
/// sends its first action, waits for the server's answer and the client's showing it, and searches again from what the
/// server said; it ends its turn at once when nothing is worth doing, instead of waiting for the turn's time. A summon the
/// client controls plays its turns the same, with the same requests, which the server takes as the summon's (9f2).
/// </para>
/// <para>
/// A cast the server does not answer within its delay counts as refused, and is not weighed again this turn; a walk not
/// answered ends the turn. The client acknowledges the sequences and says it is ready for each turn by itself (client.md).
/// </para>
/// </remarks>
public static class FightPlay
{
    private const int MostActionsATurn = 30;
    private const int MostRefusedATurn = 2;

    // The owner's ask (D203, D216): the AI's fight is closed to other players, then to spectators.
    private static readonly FightOption[] _closing = [FightOption.Closed, FightOption.NoSpectators];

    private static readonly TimeSpan _placementWait = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan _turnWait = TimeSpan.FromMinutes(5);

    /// <summary>Plays the character's fight to its end.</summary>
    /// <param name="context">The behavior's context, in a fight.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the fight ended for the character, once the server said; null when it ended unsaid.</returns>
    public static async Task<FightEnd?> PlayAsync(BotContext context, FightTactics tactics, GameTables tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tactics);
        ArgumentNullException.ThrowIfNull(tables);

        var self = context.State.Key.CharacterId;
        var weights = TacticWeights.Default;
        var judged = false;
        var refused = new HashSet<int>();
        FightMap? map = null;

        // The client draws the fight's map first, as it does a map out of fights (D77); a fight over meanwhile ends the wait.
        await context.WaitUntilAsync(Over, context.Delays.Display, cancellationToken).ConfigureAwait(false);

        while (true)
        {
            var state = context.State;

            if (state.Fight is not { Phase: not FightPhase.Ended } fight)
                return state.Fight?.End;

            if (state.MapId is not { } mapId || !context.Maps.TryGet(mapId, out var data))
                return null;

            map = map?.Id == mapId ? map : FightMap.Of(data);

            if (!judged && ImaginedFights.Of(state, map, tables, tactics.Blows, null) is { } start)
            {
                var odds = tactics.Odds(start.Fight, start.Knowledge);

                weights = odds.IsRisky ? TacticWeights.Prudent : TacticWeights.Default;
                judged = true;
                FightLog.Judged(context.Log, self, odds.TurnsToWin, odds.TurnsToLose, odds.IsRisky);
            }

            if (fight.Phase is FightPhase.Placement)
            {
                // Measured live: the fight's context comes before the server shows the character and its team's cells.
                if (!Shown(fight, self))
                {
                    await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Placement } placing || Shown(placing, self), context.Delays.Placed, cancellationToken).ConfigureAwait(false);

                    continue;
                }

                if (!fight.IsReady)
                    await PlaceAsync(context, tactics, tables, map, weights, cancellationToken).ConfigureAwait(false);

                await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Placement }, _placementWait, cancellationToken).ConfigureAwait(false);

                continue;
            }

            if (!fight.IsOwnTurn)
            {
                await context.WaitUntilAsync(next => next.Fight is not { Phase: FightPhase.Fighting, IsOwnTurn: false }, _turnWait, cancellationToken).ConfigureAwait(false);

                continue;
            }

            await PlayTurnAsync(context, tactics, tables, map, weights, refused, cancellationToken).ConfigureAwait(false);
            await context.WaitUntilAsync(next => next.Fight is not { IsOwnTurn: true }, context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool Placing(FightState fight)
    {
        return fight is { Phase: FightPhase.Placement, IsReady: false };
    }

    private static bool Playing(FightState fight)
    {
        return fight is { Phase: FightPhase.Fighting, IsOwnTurn: true };
    }

    private static bool Shown(FightState fight, long self)
    {
        return fight.Fighters.TryGetValue(self, out var own) && own.Cell is not null && !fight.OwnCells.IsEmpty;
    }

    private static bool Over(CharacterState state)
    {
        return state.Fight is not { Phase: not FightPhase.Ended };
    }

    private static async Task<bool> PacedAsync(BotContext context, Func<FightState, bool> still, CancellationToken cancellationToken)
    {
        // The player's pace, so that the player sees each action (D202). Measured live (D203): a fight may end meanwhile, the
        // last enemy killed, and a request sent after its end left the bot waiting there, its player's next walk out of the
        // fight taken for the player's hand: the pace ends with the fight, and what it held is sent only while the fight asks it.
        await context.WaitUntilAsync(state => state.Fight is not { Phase: not FightPhase.Ended } fight || !still(fight), context.Delays.FightAction, cancellationToken).ConfigureAwait(false);

        return context.State.Fight is { Phase: not FightPhase.Ended } fight && still(fight);
    }

    private static async Task PlaceAsync(BotContext context, FightTactics tactics, GameTables tables, FightMap map, TacticWeights weights, CancellationToken cancellationToken)
    {
        var self = context.State.Key.CharacterId;

        if (ImaginedFights.Of(context.State, map, tables, tactics.Blows, null) is not { } imagined || context.State.Fight is not { } fight)
            return;

        foreach (var option in _closing)
        {
            if (context.State.Fight?.Options.GetValueOrDefault(option) is true || !await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
                continue;

            var set = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is FightOptionUpdateEvent { Enabled: true } update && update.Option == option);

            await context.SendAsync(new FightOptionToggleRequest { Option = option }, cancellationToken).ConfigureAwait(false);
            await set.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }

        var cell = tactics.Place(imagined.Fight, fight.OwnCells, imagined.Knowledge, weights);

        if (!await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
            return;

        if (cell != imagined.Fight.Actor.Cell)
        {
            var moved = context.Expect((side, _, state) => side is RelayDirection.FromServer && state.Fight?.Fighters.GetValueOrDefault(self)?.Cell == cell);

            await context.SendAsync(new FightPlacementPositionRequest { CellId = cell, FighterId = self }, cancellationToken).ConfigureAwait(false);
            await moved.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        }

        FightLog.Placed(context.Log, self, context.State.Fight?.Fighters.GetValueOrDefault(self)?.Cell ?? cell);

        if (!await PacedAsync(context, Placing, cancellationToken).ConfigureAwait(false))
            return;

        var ready = context.Expect((side, _, state) => side is RelayDirection.FromServer && state.Fight is { IsReady: true } or not { Phase: FightPhase.Placement });

        await context.SendAsync(new FightReadyRequest { Ready = true }, cancellationToken).ConfigureAwait(false);
        await ready.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PlayTurnAsync(BotContext context, FightTactics tactics, GameTables tables, FightMap map, TacticWeights weights, HashSet<int> refusedSpells, CancellationToken cancellationToken)
    {
        // The character's turn, or a summon's it controls.
        var self = context.State.Fight?.PlayingId ?? context.State.Key.CharacterId;
        var refused = new HashSet<(int SpellId, int Cell)>();
        var refusals = 0;
        var acted = 0;
        (int ActionPoints, int MovementPoints)? turnPoints = context.State.Fight?.Fighters.GetValueOrDefault(self) is { } own ? (own.ActionPoints, own.MovementPoints) : null;

        for (; acted < MostActionsATurn; acted++)
        {
            // What the server answered last is whole once its sequences closed.
            await context.WaitUntilAsync(state => state.Fight is not { Sequences.IsEmpty: false }, context.Delays.Answer, cancellationToken).ConfigureAwait(false);

            if (context.State.Fight is not { IsOwnTurn: true } || ImaginedFights.Of(context.State, map, tables, tactics.Blows, turnPoints) is not { } imagined)
                return;

            if (refusedSpells.Count > 0)
                imagined = (imagined.Fight, imagined.Knowledge.Without(refusedSpells));

            var plan = tactics.Plan(imagined.Fight, imagined.Knowledge, weights, TurnSearchLimits.Default, refused);

            FightLog.Planned(context.Log, self, plan.Actions.Length, plan.Score, plan.Standing, plan.Weighed, (long)plan.Elapsed.TotalMilliseconds);

            if (plan.IsEmpty)
                break;

            var action = plan.Actions[0];

            if (!await PacedAsync(context, Playing, cancellationToken).ConfigureAwait(false))
                return;

            if (action.Kind is TacticActionKind.Walk)
            {
                if (!await WalkAsync(context, tactics, imagined.Fight, action.Cell, cancellationToken).ConfigureAwait(false))
                    break;
            }
            else if (!await CastAsync(context, self, action, cancellationToken).ConfigureAwait(false))
            {
                // Measured live (D203): a spell the server leaves unanswered, such as a transformed character's own, is
                // refused again on every cell: it is not cast again this fight, and refusals end the turn.
                refused.Add((action.SpellId, action.Cell));
                refusedSpells.Add(action.SpellId);

                if (++refusals >= MostRefusedATurn)
                    break;
            }
        }

        if (context.State.Fight is not { IsOwnTurn: true } || !await PacedAsync(context, Playing, cancellationToken).ConfigureAwait(false))
            return;

        var ended = context.Expect((side, message, state) => side is RelayDirection.FromServer && (message is FightTurnEndEvent { FighterId: var fighter } && fighter == self || state.Fight is not { IsOwnTurn: true }));

        await context.SendAsync(new FightTurnFinishRequest(), cancellationToken).ConfigureAwait(false);
        await ended.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);
        FightLog.TurnEnded(context.Log, self, acted);
    }

    private static async Task<bool> WalkAsync(BotContext context, FightTactics tactics, ImaginedFight fight, int cell, CancellationToken cancellationToken)
    {
        var self = fight.ActorId;
        var path = tactics.PathTo(fight, cell);

        if (path.Length < 2 || context.State.MapId is not { } mapId)
            return false;

        var shown = Shown(context, self, SequenceType.Move);
        var walked = context.Expect((side, message, _) => side is RelayDirection.FromServer && (message is MapMovementEvent { ActorId: var actor } && actor == self || message is MapMovementRefusedEvent));

        await context.SendAsync(MapWalk.Request(mapId, new CellPath(path)), cancellationToken).ConfigureAwait(false);

        var answer = await walked.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        FightLog.Walked(context.Log, self, cell, path.Length - 1, answer?.Descriptor.Name ?? "unanswered");

        if (answer is not MapMovementEvent)
            return false;

        // The client shows a step as out of fights, then acknowledges the walk's sequence.
        if (await shown.WaitAsync(context.Delays.Confirmation(path.Length - 1), cancellationToken).ConfigureAwait(false) is null)
            FightLog.NotShown(context.Log, self, TacticActionKind.Walk);

        return true;
    }

    private static async Task<bool> CastAsync(BotContext context, long self, TacticAction action, CancellationToken cancellationToken)
    {
        // Measured (D218): the server frames a weapon's blow in a sequence of its own, which the client acknowledges as a spell's.
        var shown = Shown(context, self, action.SpellId is WeaponBlows.SpellId ? SequenceType.Weapon : SequenceType.Spell);
        // Measured live (D203): the server refuses a cast with an error text, 29 ms after the request, and nothing else.
        var cast = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, SourceId: var source } && source == self
                || message is TextInformationEvent { Type: TextInformationType.Error }));

        await context.SendAsync(new GameActionFightCastRequest { SpellId = action.SpellId, CellId = action.Cell }, cancellationToken).ConfigureAwait(false);

        var answer = await cast.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        if (answer is TextInformationEvent refusal)
        {
            FightLog.Refused(context.Log, self, action.SpellId, action.Cell, refusal.TextId);

            return false;
        }

        FightLog.Cast(context.Log, self, action.SpellId, action.Cell, answer is null ? "unanswered" : "cast");

        if (answer is null)
            return false;

        if (await shown.WaitAsync(context.Delays.Answer + context.Delays.Display, cancellationToken).ConfigureAwait(false) is null)
            FightLog.NotShown(context.Log, self, TacticActionKind.Cast);

        return true;
    }

    private static GameExpectation Shown(BotContext context, long self, SequenceType type)
    {
        // The client acknowledges a sequence of the character's once it showed it, naming the action its end gave.
        int? ended = null;

        return context.Expect((side, message, state) =>
        {
            if (side is RelayDirection.FromServer && message is SequenceEndEvent end && end.AuthorId == self && end.Type == type && ended is null)
                ended = end.ActionId;

            // A fight that ended needs nothing more shown.
            return (side is RelayDirection.FromClient && message is GameActionAcknowledgementRequest acknowledged && ended is { } id && acknowledged.ActionId == id)
                || state.Fight is not { Phase: not FightPhase.Ended };
        });
    }
}
