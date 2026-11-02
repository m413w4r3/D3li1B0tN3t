namespace D3B.Engine.Maps;

/// <summary>A cell of a map as coordinates on its two diagonal axes, as the server names a cell in some messages.</summary>
/// <param name="X">The coordinate growing towards the south-east: one more for each cell south-east.</param>
/// <param name="Y">The coordinate growing towards the north-east: one more for each cell north-east.</param>
/// <remarks>
/// A step east adds one to both, a step south one to <see cref="X"/> and takes one from <see cref="Y"/>: each of the
/// eight directions moves one or both by one (<see cref="MapGeometry"/>). Measured on the server's refusal of a path,
/// whose two numbers were these coordinates of the cell the character stood on (client.md).
/// </remarks>
public readonly record struct MapPoint(int X, int Y);
