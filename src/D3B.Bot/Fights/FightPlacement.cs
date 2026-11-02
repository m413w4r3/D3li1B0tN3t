using System.Collections.Immutable;

namespace D3B.Bot.Fights;

/// <summary>A fight's placement cells, the map fought on and the one its fighters came from, and how often the bots saw them.</summary>
/// <param name="FromMapId">The map the character was on as the fight began.</param>
/// <param name="FromSubAreaId">That map's sub-area, as the pack places it, or null when it does not.</param>
/// <param name="FromAreaId">That sub-area's area, or null.</param>
/// <param name="PosX">That map's horizontal coordinate on the world's map, or null.</param>
/// <param name="PosY">That map's vertical coordinate on the world's map, or null.</param>
/// <param name="MapId">The map fought on: the map the server names last before giving the cells, the one left in every fight captured.</param>
/// <param name="RedCells">The challengers' cells, red, in ascending order.</param>
/// <param name="BlueCells">The defenders' cells, blue, in ascending order.</param>
/// <param name="Seen">How many fights began with them.</param>
/// <param name="FirstSeen">When the first did.</param>
/// <param name="LastSeen">When the last did.</param>
public sealed record FightPlacement(
    long FromMapId,
    int? FromSubAreaId,
    int? FromAreaId,
    int? PosX,
    int? PosY,
    long MapId,
    ImmutableArray<int> RedCells,
    ImmutableArray<int> BlueCells,
    int Seen,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);
