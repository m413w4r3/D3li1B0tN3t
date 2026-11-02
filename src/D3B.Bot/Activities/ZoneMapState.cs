namespace D3B.Bot.Activities;

/// <summary>Where an activity stands in a map it goes through.</summary>
public enum ZoneMapState
{
    /// <summary>Still to go through, what it holds not known.</summary>
    ToVisit,

    /// <summary>Gone through.</summary>
    Visited,

    /// <summary>Seen holding nothing the activity wants, or its elements' graphics all known to offer none of it: left out.</summary>
    Empty,

    /// <summary>Not reached: the way there failed.</summary>
    Unreachable,

    /// <summary>Still to go through, likely holding what the activity wants: seen so, or drawing an element with a graphic that offers it (D138).</summary>
    Probable,

    /// <summary>Reached only far round from the rest of its part of the zone: left out (D149).</summary>
    Isolated,

    /// <summary>Gone through, what it holds growing back: to go through again once it should have (D145).</summary>
    Growing,
}
