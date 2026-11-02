using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Interactives;

/// <summary>Using an interactive element of the character's map, such as harvesting a resource, until the use is over.</summary>
/// <remarks>
/// <para>
/// Measured (client.md): the client walks to a cell the element is used from (<see cref="ElementCells"/>), then, 1 to 5 ms
/// after the server answered its confirmation, asks for the element's skill by the id the server gave it on this element.
/// The server starts the use 28 ms later, for every actor on the map. A harvest ends its duration later, 1.5 or 3 s in
/// the captures, to the millisecond, with the objects obtained, the weight and the experience within 2 ms, in no set
/// order; the client sends nothing, and walked on 5 to 6 ms after. A door's use has no duration: the server names the
/// new map at once (<see cref="MapChange"/>).
/// </para>
/// <para>
/// The server takes a use from no further than the skill's range, or from next to the element, whatever lies between. It
/// refuses one from too far, or of a skill the element does not offer now, with <see cref="InteractiveUseErrorEvent"/>,
/// and answers nothing to a use of an element that is not on the map (D75).
/// </para>
/// </remarks>
public static class ElementUse
{
    // The unit of the duration the server gives a use: the protocol's, not a delay of D3B's.
    private static readonly TimeSpan _durationUnit = TimeSpan.FromMilliseconds(100);

    /// <summary>Uses an element of the character's map with a skill, until the use is over, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="elementId">The element's id on the map.</param>
    /// <param name="skillId">The skill's id in the client's tables.</param>
    /// <param name="cancellationToken">Stops the use.</param>
    /// <returns>How the use ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<UseOutcome> UseAsync(this BotActor bot, int elementId, int skillId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        return bot.RunAsync((context, token) => context.UseAsync(elementId, skillId, token), cancellationToken);
    }

    /// <summary>Uses an element of the character's map with a skill, until the use is over, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="elementId">The element's id on the map.</param>
    /// <param name="skillId">The skill's id in the client's tables.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the use ended.</returns>
    public static async Task<UseOutcome> UseAsync(this BotContext context, int elementId, int skillId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var state = context.State;
        var mapId = state.MapId ?? 0;
        int? interactive = state.Elements.TryGetValue(elementId, out var element) ? element.InteractiveId : null;
        var outcome = await UseOnMapAsync(context, elementId, skillId, cancellationToken).ConfigureAwait(false);

        context.Record(action, (at, took) => new UseEntry(at, depth, mapId, elementId, interactive, skillId, outcome, took));

        return outcome;
    }

    /// <summary>Gets the request to use a skill of an element, as the client words its own.</summary>
    /// <param name="elementId">The element's id on the map.</param>
    /// <param name="skill">The skill, with the id the server gave it on this element.</param>
    /// <returns>The request.</returns>
    public static InteractiveUseRequest Request(int elementId, ElementSkill skill)
    {
        return new InteractiveUseRequest { SkillInstanceId = skill.InstanceId, ElementId = elementId };
    }

    /// <summary>Finds the cell the map's data places an element on.</summary>
    /// <param name="map">The map.</param>
    /// <param name="elementId">The element's id on the map.</param>
    /// <param name="cell">The element's cell, when the map holds it on one.</param>
    /// <returns>Whether the map holds the element on a cell.</returns>
    internal static bool TryGetCell(MapData map, int elementId, out int cell)
    {
        cell = map.TryGetInteractive(elementId, out var interactive) ? interactive.CellId : MapInteractive.NoCell;

        return cell != MapInteractive.NoCell;
    }

    private static async Task<UseOutcome> UseOnMapAsync(BotContext context, int elementId, int skillId, CancellationToken cancellationToken)
    {
        // The use may be asked from where the character stands, which the server refuses while it holds a walk (D89).
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || state.IsBusy)
            return UseOutcome.Busy;

        if (!state.Elements.TryGetValue(elementId, out var element) || !element.TryGetEnabled(skillId, out _))
            return UseOutcome.Unavailable;

        if (!context.Maps.TryGet(mapId, out var map) || !TryGetCell(map, elementId, out var elementCell))
            return UseOutcome.NoPath;

        var cells = map.Cells.Span;

        if (!CellPathfinder.TryFindNearest(cells, cell, [.. ElementCells.UsedFrom(cells, elementCell, cells[cell].Zone)], out var path))
            return UseOutcome.NoPath;

        BotLog.Using(context.Log, state.Key.CharacterId, elementId, skillId, path.End);

        var outcome = await UseFromAsync(context, path, elementId, skillId, cancellationToken).ConfigureAwait(false);

        BotLog.Used(context.Log, state.Key.CharacterId, elementId, skillId, outcome);

        return outcome;
    }

    private static async Task<UseOutcome> UseFromAsync(BotContext context, CellPath path, int elementId, int skillId, CancellationToken cancellationToken)
    {
        if (path.Steps > 0 && await context.WalkAsync(path.End, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived)
            return UseOutcome.NotWalked;

        // Another actor may have started harvesting the element during the walk.
        if (!context.State.Elements.TryGetValue(elementId, out var element) || !element.TryGetEnabled(skillId, out var skill))
            return UseOutcome.Unavailable;

        var self = context.State.Key.CharacterId;

        // Both are expected before the request goes, so that no answer can come before the bot looks for it.
        var answer = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && (message is InteractiveUsedEvent used && used.ActorId == self && used.ElementId == elementId
                || message is InteractiveUseErrorEvent error && error.ElementId == elementId));
        var end = context.Expect((side, message, _) => side is RelayDirection.FromServer
            && message is InteractiveUseEndedEvent ended && ended.ElementId == elementId && ended.SkillId == skillId);

        await context.SendAsync(Request(elementId, skill), cancellationToken).ConfigureAwait(false);

        var answered = await answer.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false);

        if (answered is null)
            return UseOutcome.NoAnswer;

        if (answered is not InteractiveUsedEvent started)
            return UseOutcome.Refused;

        if (started.Duration <= 0)
            return UseOutcome.Used;

        var ended = await end.WaitAsync(started.Duration * _durationUnit + context.Delays.UseEnd, cancellationToken).ConfigureAwait(false);

        return ended is null ? UseOutcome.NotEnded : UseOutcome.Used;
    }
}
