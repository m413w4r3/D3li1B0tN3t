namespace D3B.Bot.Characters;

/// <summary>A non-player character of the character's map, as the server places it.</summary>
/// <param name="ActorId">Its actor id on the map, negative, which the requests acting on it name.</param>
/// <param name="NpcId">Which NPC it is, by id in the client's tables, such as a banker (D29).</param>
/// <param name="Cell">The cell it stands on, when the server gave one.</param>
public sealed record MapNpc(int ActorId, int NpcId, int? Cell);
