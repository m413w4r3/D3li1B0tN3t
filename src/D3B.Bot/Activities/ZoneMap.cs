namespace D3B.Bot.Activities;

/// <summary>A map an activity goes through, and where it stands in it.</summary>
/// <param name="MapId">The map.</param>
/// <param name="State">Where the activity stands in it.</param>
/// <param name="Group">Which part of the zone it belongs to, such as its sub-area, which the app tints apart.</param>
public readonly record struct ZoneMap(long MapId, ZoneMapState State, int Group = 0);
