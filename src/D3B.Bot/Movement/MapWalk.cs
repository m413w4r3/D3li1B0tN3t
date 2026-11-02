using D3B.Bot.Journal;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Movement;

/// <summary>The first behavior: walking to a cell of the character's map (D69).</summary>
/// <remarks>
/// <para>
/// The path is found in the pack's cells (<see cref="CellPathfinder"/>), from the cell the state says the character
/// stands on, and sent as the client sends its own: the path's turning points, each the direction times 4096 plus the
/// cell (D60). The server then shows the walk, or refuses it; the client walks it, confirms once arrived, and the server
/// answers the confirmation: the walk is over. D3B never confirms: the client does (D60).
/// </para>
/// <para>
/// Delays, measured (client.md): the server answered every path within 150 ms, so 3 s; the client confirmed 0.38 to
/// 2.9 s later, a step taking at most 186 ms running and 491 ms walking, and the server answered within 100 ms, so 1 s
/// and 500 ms a step (<see cref="BotDelays"/>, which the user may raise). They only bound a wait that went wrong: the
/// server's answers end the walk.
/// </para>
/// <para>
/// The client does not follow a walk ending on the cell its previous walk ended on, even on another map, measured (9g5):
/// such a walk goes in two, to the cell before its last, then the last step, or a step aside first when it has one step.
/// A walk the client did not follow leaves the server with the character on the path's last cell, which the state then
/// takes, and the client follows the next walk from there (D85). The server may still hold the character walking it,
/// refusing what is not a walk from there: a step aside lets it go (<see cref="ReleaseHeldWalkAsync"/>, D89).
/// </para>
/// </remarks>
public static class MapWalk
{
    private const int DirectionShift = 12;

    /// <summary>Walks the bot's character to a cell of its map, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="cell">The cell to walk to.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>How the walk ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<WalkOutcome> WalkAsync(this BotActor bot, int cell, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        CheckCell(cell);

        return bot.RunAsync((context, token) => context.WalkAsync(cell, token), cancellationToken);
    }

    /// <summary>Walks the character to a cell of its map, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cell">The cell to walk to.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the walk ended.</returns>
    public static async Task<WalkOutcome> WalkAsync(this BotContext context, int cell, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CheckCell(cell);

        using var action = context.BeginAction();
        var mapId = context.State.MapId ?? 0;
        var outcome = await WalkToAsync(context, cell, cancellationToken).ConfigureAwait(false);

        // A walk made for another action, to a map's exit or an element, is part of it: only one asked for itself has its entry.
        if (action.IsOutermost)
            context.Record(action, (at, took) => new WalkEntry(at, 0, mapId, cell, outcome, took));

        return outcome;
    }

    /// <summary>Lets go of a walk the server still holds, which the client did not follow: a step to a neighbour, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the step ended: <see cref="WalkOutcome.Arrived"/> at once when the server holds no walk.</returns>
    /// <remarks>
    /// Measured (client.md, D85): after a walk the client did not confirm, the server refused a map change, the haven bag
    /// and teleports, 9 and 11 s later, and walked a path from the walk's last cell, which the client followed, first
    /// bringing its character there. A behavior whose first request is not such a walk calls this first (D89).
    /// </remarks>
    public static async Task<WalkOutcome> ReleaseHeldWalkAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        if (!state.IsWalkHeld)
            return WalkOutcome.Arrived;

        if (state.MapId is not { } mapId || state.Cell is not { } from || state.IsBusy)
            return WalkOutcome.Busy;

        var neighbours = Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(from, direction)).Where(neighbour => neighbour >= 0).ToArray();

        if (!context.Maps.TryGet(mapId, out var map) || !CellPathfinder.TryFindNearest(map.Cells.Span, from, neighbours, out var aside))
            return WalkOutcome.NoPath;

        return await WalkAlongAsync(context, mapId, aside, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the request to walk a path, as the client words its own.</summary>
    /// <param name="mapId">The character's map.</param>
    /// <param name="path">The path, from the cell the character stands on.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentException">The path has no step.</exception>
    public static MapMovementRequest Request(long mapId, CellPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // A request naming no cell, measured once, was refused, and the server closed the game connection (D89).
        if (path.Steps is 0)
            throw new ArgumentException("A walk has at least one step: the server closes the connection of a request naming no cell.", nameof(path));

        var request = new MapMovementRequest { MapId = mapId };

        request.KeyMovements.AddRange(path.TurningPoints().Select(turn => (turn.Direction << DirectionShift) | turn.Cell));

        return request;
    }

    private static async Task<WalkOutcome> WalkToAsync(BotContext context, int cell, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } from || state.IsBusy)
            return WalkOutcome.Busy;

        if (from == cell)
        {
            if (!state.IsWalkHeld)
                return WalkOutcome.Arrived;

            var aside = await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

            if (aside is not WalkOutcome.Arrived || context.State.Cell is not { } stepped || context.State.MapId != mapId)
                return aside;

            from = stepped;
        }

        if (!context.Maps.TryGet(mapId, out var map) || !CellPathfinder.TryFind(map.Cells.Span, from, cell, out var path))
            return WalkOutcome.NoPath;

        if (path.End == context.State.LastWalkEnd && await AwayFromLastEndAsync(context, mapId, map, path, cancellationToken).ConfigureAwait(false) is { } away)
        {
            if (away is not WalkOutcome.Arrived || context.State.Cell is not { } between || context.State.MapId != mapId)
                return away;

            if (!CellPathfinder.TryFind(map.Cells.Span, between, cell, out path))
                return WalkOutcome.NoPath;
        }

        return await WalkAlongAsync(context, mapId, path, cancellationToken).ConfigureAwait(false);
    }

    // A first walk that ends elsewhere, so that the client follows the walk to the cell its previous one ended on: to the
    // path's cell before its last, or a step aside when the path has one step; none when no other cell can be reached.
    private static async Task<WalkOutcome?> AwayFromLastEndAsync(BotContext context, long mapId, MapData map, CellPath path, CancellationToken cancellationToken)
    {
        var cells = map.Cells.Span;
        CellPath? away;

        if (path.Steps > 1)
        {
            if (!CellPathfinder.TryFind(cells, path.Start, path.Cells.Span[^2], out away))
                return null;
        }
        else
        {
            var neighbours = Enumerable.Range(0, MapCell.DirectionCount)
                .Select(direction => MapGeometry.Neighbour(path.Start, direction))
                .Where(neighbour => neighbour >= 0 && neighbour != path.End)
                .ToArray();

            if (!CellPathfinder.TryFindNearest(cells, path.Start, neighbours, out away) || away.Steps is 0)
                return null;
        }

        return await WalkAlongAsync(context, mapId, away, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WalkOutcome> WalkAlongAsync(BotContext context, long mapId, CellPath path, CancellationToken cancellationToken)
    {
        var self = context.State.Key.CharacterId;

        // Both are expected before the request goes, so that no answer can come before the bot looks for it.
        var answer = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is MapMovementEvent movement && movement.ActorId == self || message is MapMovementRefusedEvent));
        var arrival = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is MapMovementConfirmResponse);

        BotLog.Walking(context.Log, self, path.Start, path.End, path.Steps);
        await context.SendAsync(Request(mapId, path), cancellationToken).ConfigureAwait(false);

        var outcome = await AnswerAsync(context, answer, arrival, path, cancellationToken).ConfigureAwait(false);

        BotLog.Walked(context.Log, self, path.End, outcome);

        return outcome;
    }

    private static async Task<WalkOutcome> AnswerAsync(BotContext context, GameExpectation answer, GameExpectation arrival, CellPath path, CancellationToken cancellationToken)
    {
        var answered = await answer.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        if (answered is null)
            return WalkOutcome.NoAnswer;

        if (answered is MapMovementRefusedEvent)
            return WalkOutcome.Refused;

        var arrived = await arrival.WaitAsync(context.Delays.Confirmation(path.Steps), cancellationToken).ConfigureAwait(false);

        if (arrived is not null)
            return WalkOutcome.Arrived;

        await context.SettleWalkAsync().ConfigureAwait(false);

        return WalkOutcome.NotConfirmed;
    }

    private static void CheckCell(int cell)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cell);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cell, MapData.CellCount);
    }
}
