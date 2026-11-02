namespace D3B.App.Controls;

/// <summary>How an exit of the map leaves it, as the world graph's transitions do (D27).</summary>
internal enum MapExitKind
{
    /// <summary>Walking off an edge, onto the neighbour or where the server sends.</summary>
    Scroll,

    /// <summary>Stepping on a cell, which moves the character to another map.</summary>
    MapAction,

    /// <summary>Using an interactive element, such as a door.</summary>
    Door,
}
