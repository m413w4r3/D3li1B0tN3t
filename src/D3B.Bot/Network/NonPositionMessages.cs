using D3B.Network.Relay;

namespace D3B.Bot.Network;

/// <summary>The messages of one client build that cannot carry the character's position, so an unread one leaves movement open (P2K).</summary>
/// <remarks>
/// <para>
/// A build's schema holds messages the readable protocol lacks. An unknown carried type normally closes movement for the
/// session, because a frame D3B cannot read may change what the bot knows of its map or cell. This list holds the types
/// whose identity a reference proves field by field, whose family is outside the map, gamemap, the fight, the house, the
/// haven bag, the game action, the role-play challenge, the NPC map state, the client's verification, BAK/HAAPI and the
/// account role/security, and whose schema the build's own payload validates under the game's type URL. A listed type
/// arriving from the side other than the proving GO 1 or S2B observation is contradictory and stays latched. The reference is BillBot's
/// <c>game_mappings_detailed.json</c>; a field is proven only when the build's schema names every field of the message
/// and the reference names it, recursively for message-typed fields. A partial match, an anonymous field, a map-like
/// family or an unidentified type stays out and keeps closing movement.
/// </para>
/// <para>
/// The list is not a claim that these messages are unimportant: it is a build-scoped safety classification. The mapping's
/// client version must be exactly the build it was demonstrated on, so a client change makes every entry inactive. The
/// payload must walk that build's schema and end on its last byte; a frame carrying the name but other bytes still closes
/// movement.
/// </para>
/// </remarks>
internal static class NonPositionMessages
{
    /// <summary>The build the list was demonstrated on.</summary>
    public const string ClientVersion = "3.7.13.17";

    // Each comment gives the schema field names and the reference's name for them. The value is the reference's readable
    // name and whether the proving GO 1 or S2B observation saw the client send it: a listed type from the other side is contradictory and stays latched.
    private static readonly Dictionary<string, (string Reference, bool FromClient)> _messages = new(StringComparer.Ordinal)
    {
        // TitlesAndOrnamentsEvent: fngc -> ornaments, fngd -> titles.
        ["hgf"] = ("com.ankama.dofus.server.game.protocol.tinsel.TitlesAndOrnamentsEvent", false),
        // OrnamentSelectErrorEvent: fngh -> reason.
        ["hgg"] = ("com.ankama.dofus.server.game.protocol.tinsel.OrnamentSelectErrorEvent", false),
        // TitleSelectErrorEvent: fngl -> reason.
        ["hgh"] = ("com.ankama.dofus.server.game.protocol.tinsel.TitleSelectErrorEvent", false),
        // SpouseStatusEvent: fnyb -> has_spouse.
        ["hla"] = ("com.ankama.dofus.server.game.protocol.social.SpouseStatusEvent", false),
        // ChatCommunityChannelSetCommunityRequest: fnyp -> community.
        ["hlf"] = ("com.ankama.dofus.server.game.protocol.social.ChatCommunityChannelSetCommunityRequest", true),
        // AlignmentRankUpdateEvent: fobm -> alignment_rank, fobn -> verbose.
        ["hlw"] = ("com.ankama.dofus.server.game.protocol.social.AlignmentRankUpdateEvent", false),
        // ServerSettingsEvent: focq -> item_max_level, focr -> language, focs -> has_free_autopilot, foct -> arena_leave_ban_time, focu -> community, focv -> game_type, focw -> is_mono_account.
        ["hmd"] = ("com.ankama.dofus.server.game.protocol.server.ServerSettingsEvent", false),
        // ExchangeErrorEvent: fyjq -> error_type.
        ["kbi"] = ("com.ankama.dofus.server.game.protocol.exchange.ExchangeErrorEvent", false),
        // JobBookSubscribeRequest: fynf -> jobs_id.
        ["kcf"] = ("com.ankama.dofus.server.game.protocol.exchange.JobBookSubscribeRequest", true),
        // EmotesEvent: fyvy -> emotes_id.
        ["kes"] = ("com.ankama.dofus.server.game.protocol.emote.EmotesEvent", false),
        // ContactWarnOnAchievementCompleteSetRequest: fzmx -> enable.
        ["kkn"] = ("com.ankama.dofus.server.game.protocol.contact.ContactWarnOnAchievementCompleteSetRequest", true),
        // FriendSetWarnOnLevelGainRequest: fzrx -> enable.
        ["klu"] = ("com.ankama.dofus.server.game.protocol.contact.FriendSetWarnOnLevelGainRequest", true),
        // FriendSetWarnOnConnectionRequest: fzty -> enable.
        ["kmj"] = ("com.ankama.dofus.server.game.protocol.contact.FriendSetWarnOnConnectionRequest", true),
        // AcquaintanceListRequest: empty schema; the reference names no fields.
        ["kmk"] = ("com.ankama.dofus.server.game.protocol.contact.AcquaintanceListRequest", true),
        // FriendListRequest: empty schema; the reference names no fields.
        ["kmu"] = ("com.ankama.dofus.server.game.protocol.contact.FriendListRequest", true),
        // QueueStatusEvent: fzwj -> position, fzwk -> total. The position is the queue's, not the character's.
        ["kna"] = ("com.ankama.dofus.server.game.protocol.connection.QueueStatusEvent", false),
        // PongEvent: fzwo -> quiet.
        ["knb"] = ("com.ankama.dofus.server.game.protocol.connection.PongEvent", false),
        // OptionalFeaturesEvent: fzxv -> features.
        ["kni"] = ("com.ankama.dofus.server.game.protocol.connection.OptionalFeaturesEvent", false),
        // SubscribeMultipleChannelRequest: gahn -> channel_enabled, gaho -> channel_disabled.
        ["kpu"] = ("com.ankama.dofus.server.game.protocol.chat.SubscribeMultipleChannelRequest", true),
        // ChatChannelsEnabledEvent: gahs -> disabled_channels, gaht -> active_channels.
        ["kpv"] = ("com.ankama.dofus.server.game.protocol.chat.ChatChannelsEnabledEvent", false),
        // CharacterExperienceGainEvent: gaor -> character_experience.
        ["krm"] = ("com.ankama.dofus.server.game.protocol.character.CharacterExperienceGainEvent", false),
        // CharacterCapabilitiesEvent: gapo -> guild_emblem_symbol_category.
        ["krw"] = ("com.ankama.dofus.server.game.protocol.character.management.CharacterCapabilitiesEvent", false),
        // CharacterLoadingCompleteEvent: empty schema; the reference names no fields.
        ["ktc"] = ("com.ankama.dofus.server.game.protocol.character.management.CharacterLoadingCompleteEvent", false),
        // SequenceNumberRequest: gecd -> number.
        ["lnt"] = ("com.ankama.dofus.server.game.protocol.basic.SequenceNumberRequest", true),
        // SequenceNumberEvent: empty schema; the reference names no fields.
        ["lnu"] = ("com.ankama.dofus.server.game.protocol.basic.SequenceNumberEvent", false),
        // TimeEvent: geei -> timestamp, geej -> timezone_offset.
        ["lok"] = ("com.ankama.dofus.server.game.protocol.basic.TimeEvent", false),
        // ArenaUpdatePlayerInformationEvent: gesc -> ban_end_date, gesd -> arena_ranks.
        ["lsh"] = ("com.ankama.dofus.server.game.protocol.arena.ArenaUpdatePlayerInformationEvent", false),
        // OutfitAuraChangedEvent: gfls -> emote_id.
        ["lwn"] = ("com.ankama.dofus.server.game.protocol.cosmetic.OutfitAuraChangedEvent", false),
        // SessionElapsedDurationEvent: ggqj -> duration_seconds.
        ["meq"] = ("com.ankama.dofus.server.game.protocol.account.SessionElapsedDurationEvent", false),
    };

    /// <summary>Gets the build's messages the list holds, with the reference's readable name and whether the proving GO 1 or S2B observation saw the client send it.</summary>
    public static IReadOnlyDictionary<string, (string Reference, bool FromClient)> Messages => _messages;

    /// <summary>Gets whether a build's carried type is one of its proven non-position messages.</summary>
    /// <param name="clientVersion">The mapping's client version.</param>
    /// <param name="carriedType">The build's name of the carried type.</param>
    /// <param name="direction">The side that sent the frame: a listed request from the server, or a listed event from the client, is contradictory.</param>
    /// <returns>Whether the exact build, type and observed side are listed.</returns>
    public static bool IsNonPosition(string clientVersion, string? carriedType, RelayDirection direction)
    {
        return carriedType is not null
            && string.Equals(clientVersion, ClientVersion, StringComparison.Ordinal)
            && _messages.TryGetValue(carriedType, out var entry)
            && entry.FromClient == (direction is RelayDirection.FromClient);
    }
}
