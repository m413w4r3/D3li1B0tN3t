using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Movement;

/// <summary>What a bot's session and character state allow for an automated movement, and why they do not.</summary>
/// <remarks>
/// The readiness is passive: it reads the current snapshot and the session's sticky translation latch, and never sends a
/// request. <see cref="Guard"/> and <see cref="TranslationBlocked"/> are separate so that a position refusal is not
/// mistaken for a session whose wire content could not be read, or the other way round.
/// </remarks>
/// <param name="Connected">Whether a game connection plays the character.</param>
/// <param name="SessionAttached">Whether that connection is the bot's current game session.</param>
/// <param name="TranslationBlocked">Whether an unread frame latched movement closed for the current session.</param>
/// <param name="TranslationBlockedAtFirstPlacement">Whether the latch was closed when the character was first placed, or <see langword="null"/> until then.</param>
/// <param name="Paused">Whether the player took the character over until <see cref="BotActor.Resume"/>.</param>
/// <param name="Acting">Whether a behavior already runs.</param>
/// <param name="InHavenBag">Whether the character is in a haven bag, whose map the world graph does not hold.</param>
/// <param name="MapId">The map the server last named, when it did.</param>
/// <param name="Cell">The cell the state holds, when it holds one.</param>
/// <param name="Provenance">Where that cell came from.</param>
/// <param name="PositionTrusted">Whether the position is server-derived and usable by an automated action.</param>
/// <param name="Guard">The first position or activity refusal, or <see cref="MovementRefusal.None"/>.</param>
/// <param name="Reason">A stable code naming the first blocker, or <c>READY</c>.</param>
internal sealed record MovementReadiness(
    bool Connected,
    bool SessionAttached,
    bool TranslationBlocked,
    bool? TranslationBlockedAtFirstPlacement,
    bool Paused,
    bool Acting,
    bool InHavenBag,
    long? MapId,
    int? Cell,
    CellProvenance Provenance,
    bool PositionTrusted,
    MovementRefusal Guard,
    string Reason)
{
    /// <summary>Gets whether a movement action can start from this snapshot.</summary>
    public bool CanMove => Connected
        && SessionAttached
        && !TranslationBlocked
        && !Paused
        && !Acting
        && !InHavenBag
        && Guard is MovementRefusal.None;

    /// <summary>Reads the readiness of a bot without sending anything.</summary>
    /// <param name="bot">The bot.</param>
    /// <returns>Its session, position and activity readiness.</returns>
    /// <exception cref="ArgumentNullException">The bot is <see langword="null"/>.</exception>
    internal static MovementReadiness Read(BotActor bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var state = bot.State;
        var connected = bot.IsConnected;
        var sessionAttached = bot.Session is not null;
        var translationBlocked = bot.IsMovementTranslationBlocked;
        var paused = bot.IsPaused;
        var acting = bot.IsActing;
        var guard = MovementGuard.Refuse(state, connected);
        var reason = !connected ? "DISCONNECTED"
            : !sessionAttached ? "NO_SESSION"
            : translationBlocked ? "TRANSLATION_LATCH_CLOSED"
            : paused ? "BOT_PAUSED"
            : acting ? "BOT_ACTING"
            : state.IsInHavenBag ? "HAVEN_BAG"
            : guard is MovementRefusal.None ? "READY"
            : CodeOf(guard);

        return new MovementReadiness(
            connected,
            sessionAttached,
            translationBlocked,
            bot.TranslationBlockedAtFirstPlacement,
            paused,
            acting,
            state.IsInHavenBag,
            state.MapId,
            state.Cell,
            state.Provenance,
            state.IsPositionTrusted,
            guard,
            reason);
    }

    /// <summary>Gets the stable code naming a guard refusal.</summary>
    /// <param name="guard">The refusal.</param>
    /// <returns>Its code, such as <c>GUARD_CELL_UNKNOWN</c>.</returns>
    internal static string CodeOf(MovementRefusal guard)
    {
        return guard switch
        {
            MovementRefusal.Disconnected => "GUARD_DISCONNECTED",
            MovementRefusal.MapChanged => "GUARD_MAP_CHANGED",
            MovementRefusal.MapUnknown => "GUARD_MAP_UNKNOWN",
            MovementRefusal.CellUnknown => "GUARD_CELL_UNKNOWN",
            MovementRefusal.Walking => "GUARD_WALKING",
            MovementRefusal.WalkHeld => "GUARD_WALK_HELD",
            MovementRefusal.Fighting => "GUARD_FIGHTING",
            MovementRefusal.Busy => "GUARD_BUSY",
            MovementRefusal.AmbiguousPosition => "GUARD_AMBIGUOUS_POSITION",
            MovementRefusal.ClientReported => "GUARD_CLIENT_REPORTED",
            MovementRefusal.Estimated => "GUARD_ESTIMATED",
            MovementRefusal.ProvenanceUnknown => "GUARD_PROVENANCE_UNKNOWN",
            _ => "GUARD_NONE",
        };
    }
}
