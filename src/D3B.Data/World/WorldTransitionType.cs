namespace D3B.Data.World;

/// <summary>How a transition of the world graph leaves a map, as client 3.7.12.12 types them (client.md).</summary>
/// <remarks>
/// The client's values are powers of two, but each transition has exactly one. Other values may exist in the client's
/// code, such as zaaps; none is in the graph of 3.7.12.12, and the extractor fails on one rather than guess its meaning.
/// </remarks>
public enum WorldTransitionType : byte
{
    /// <summary>Walking off the map's edge onto its neighbour, from the transition's cell, in its direction.</summary>
    Scroll = 1,

    /// <summary>Walking off the map's edge, where the server sends the character to another map than the neighbour.</summary>
    /// <remarks>The transition's map is the neighbour the client asks for; the edge's destination is where it lands.</remarks>
    ScrollAction = 2,

    /// <summary>Stepping on the transition's cell, which moves the character to another map.</summary>
    MapAction = 8,

    /// <summary>Using an interactive element of the map, such as a door, with the transition's skill.</summary>
    Interactive = 32,
}
