using System.Globalization;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Movement;

/// <summary>Checks the current position before an automated movement action starts.</summary>
internal static class MovementGuard
{
    /// <summary>Gets the first reason the state cannot start an automated movement action.</summary>
    /// <param name="state">The current character snapshot.</param>
    /// <param name="connected">Whether the live game connection is active.</param>
    /// <returns><see cref="MovementRefusal.None"/> only when the position is server-derived and the character can act.</returns>
    internal static MovementRefusal Refuse(CharacterState state, bool connected)
    {
        if (!connected)
            return MovementRefusal.Disconnected;

        if (state.MapId is null)
            return MovementRefusal.MapUnknown;

        if (state.Cell is null)
            return MovementRefusal.CellUnknown;

        if (state.IsWalking)
            return MovementRefusal.Walking;

        if (state.IsWalkHeld)
            return MovementRefusal.WalkHeld;

        if (state.IsFighting)
            return MovementRefusal.Fighting;

        if (state.IsUsing || state.IsInDialog)
            return MovementRefusal.Busy;

        return state.Provenance switch
        {
            CellProvenance.ServerPlaced or CellProvenance.ServerConfirmed or CellProvenance.ServerRefused => MovementRefusal.None,
            CellProvenance.ClientReported => MovementRefusal.ClientReported,
            CellProvenance.Estimated => MovementRefusal.Estimated,
            _ => MovementRefusal.ProvenanceUnknown,
        };
    }

    /// <summary>Checks and logs one action's refusal from its current state.</summary>
    /// <param name="context">The behavior attempting to act.</param>
    /// <param name="expectedMapId">The map the planned action starts on, when one was selected.</param>
    /// <returns>Whether the position permits an automated movement action.</returns>
    internal static bool IsAllowed(BotContext context, long? expectedMapId = null)
    {
        var state = context.State;
        var refusal = Refuse(state, context.IsConnected);

        if (refusal is MovementRefusal.None)
        {
            if (expectedMapId is null || state.MapId == expectedMapId)
                return true;

            LogRefusal(context.Log, state, MovementRefusal.MapChanged);

            return false;
        }

        LogRefusal(context.Log, state, refusal);

        return false;
    }

    internal static void LogRefusal(ILogger logger, CharacterState state, MovementRefusal refusal)
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var map = state.MapId is { } mapId ? mapId.ToString(CultureInfo.InvariantCulture) : "unknown";
        var cell = state.Cell is { } cellId ? cellId.ToString(CultureInfo.InvariantCulture) : "unknown";

        BotLog.NavigationRefused(logger, state.Key.CharacterId, map, cell, state.Provenance, refusal);
    }
}
