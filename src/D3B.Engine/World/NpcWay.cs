using System.Collections.Frozen;

namespace D3B.Engine.World;

/// <summary>A way from map to map through an NPC's dialog, which the client's world graph does not hold (D253).</summary>
/// <remarks>
/// The client's data names an NPC's replies but neither what a reply does nor where it leads: only the server knows, so a
/// way is what a dialog was measured to do. The NPC is talked to from anywhere on its map, as a banker is, and whichever of
/// <see cref="Replies"/> each question offers is given, until the server moves the character.
/// </remarks>
/// <param name="MapId">The map the NPC stands on.</param>
/// <param name="NpcId">The NPC, by id in the client's tables, as the server names it on the map.</param>
/// <param name="Replies">The replies that lead on, by id in the client's tables, whichever question offers them.</param>
/// <param name="ToMapId">The map the server moves the character to.</param>
public sealed record NpcWay(long MapId, int NpcId, FrozenSet<int> Replies, long ToMapId);
