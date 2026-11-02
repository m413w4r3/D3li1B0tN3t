using System.Collections.Frozen;
using D3B.Engine.World;

namespace D3B.Bot.Movement;

/// <summary>The ways through an NPC's dialog that journeys take, each measured in game (D253).</summary>
/// <remarks>
/// The client's world graph holds none, and its data says neither what a reply does nor where it leads: the NPC is known by
/// the id the server names it by on the map, and its replies by the ids its questions offer, never by their text.
/// </remarks>
public static class NpcWays
{
    /// <summary>Gets the ways measured, which the bots' pathfinder goes through.</summary>
    /// <remarks>
    /// <para>
    /// Incarnam's portal to Astrub, measured on 3.7.12.12 with a character of level 1: NPC 4398 on map 153880835, question
    /// 30637 offering reply 36979, then question 30638 offering 36977 to leave and 36978 to stay; 36977 ends the dialog and
    /// the server moves the character to map 192416776, Astrub's temple, in the same millisecond. A character of level 92 is
    /// moved there as soon as it talks to the NPC, no question asked. Replies 36982 then 36980, which BillBot gives where
    /// those are not offered, are the NPC's other way out, not measured.
    /// </para>
    /// <para>
    /// The boat from the Port de Madrestam to Sarakech, measured by hand on 3.7.12.12 with a character of level 92 (D266):
    /// NPC 3464 on map 68422145, question 25721 offering 30858 and 30860; 30858 ends the dialog and the server moves the
    /// character to map 173277701, Sarakech's port, in the same millisecond, no kamas taken. Without Sarakech's zaap, no
    /// other way leads there. The way back is not measured.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<NpcWay> Measured { get; } =
    [
        new NpcWay(153880835, 4398, new[] { 36979, 36977, 36982, 36980 }.ToFrozenSet(), 192416776),
        new NpcWay(68422145, 3464, new[] { 30858 }.ToFrozenSet(), 173277701),
    ];
}
