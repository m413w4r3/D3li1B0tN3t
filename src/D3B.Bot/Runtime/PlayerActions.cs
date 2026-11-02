using D3B.Bot.Characters;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Runtime;

/// <summary>Tells the client's requests that act on the character, which only the player sends, from those the client sends by itself (D66).</summary>
/// <remarks>
/// <para>
/// An observer never sees the requests D3B sends (D62), so every request it sees is the client's. The client sends some
/// by itself after an action, its own or D3B's: the confirmation of a walk, the request for a new map's information, and
/// the two requests to leave once the server ended an exchange the client did not ask to leave (D60). Those are not the
/// player's.
/// </para>
/// <para>
/// The requests that start an action are the player's: the kinds D3B sends (D60, D83), and the stop of a walk, which the
/// client sends when the player gives a new path during one (client.md). A request to leave is the player's while the
/// character is in a dialog or an exchange, as when the player closes the banker's dialog, the bank or a zaap's list:
/// the client sends its own only once the server ended the exchange, and the bot's loop sees that end first, since the
/// relay shows the server's frames before the client gets them (D81). It also closes by itself a dialog whose question
/// offers no reply, a few milliseconds after it, which is not the player's either (D253).
/// </para>
/// <para>
/// In a fight, the player's are the placement's move and readiness, a cast, a walk and the turn's end: the client sends by
/// itself only its acknowledgements of the sequences and its readiness for the next turn (client.md), so that the player's
/// hand in a fight the AI plays pauses the bot, and the fight is the player's (D202).
/// </para>
/// <para>
/// The client also ends a turn by itself, along its inactivity popup, and marks that end as its own: it cut the AI's
/// turn and paused the bot, though no one touched the client, measured on a friend's capture (D252).
/// </para>
/// </remarks>
public static class PlayerActions
{
    /// <summary>Tells whether a request the client sent acts on the character, so the player sent it.</summary>
    /// <param name="state">The character's state when the client sent it.</param>
    /// <param name="request">A request the client sent, readable.</param>
    /// <returns>Whether the request starts or stops an action.</returns>
    public static bool Starts(CharacterState state, IMessage request)
    {
        ArgumentNullException.ThrowIfNull(state);

        return request is MapMovementRequest or MapMovementCancelRequest or MapChangeRequest or InteractiveUseRequest or NpcActionRequest
            or NpcDialogReplyRequest or ExchangeTransferFromInventoryRequest or ExchangeKamasRequest or TeleportRequest
            or HavenBagEnterRequest or FightPlacementPositionRequest or FightReadyRequest or GameActionFightCastRequest
            or FightTurnFinishRequest { Automatic: false }
            || (request is DialogLeaveRequest && state.IsInDialog && !OffersNoReply(state));
    }

    private static bool OffersNoReply(CharacterState state)
    {
        // Measured (D253): Incarnam's portal moves a character of level 92 at once, then asks on the new map a question
        // offering no reply, which the client closes 2 to 9 ms after, by itself.
        return state is { Dialog: { MessageId: not null, Replies.IsEmpty: true }, Storage: null, ZaapDialog: null };
    }
}
