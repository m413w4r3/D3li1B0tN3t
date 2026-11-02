namespace D3B.Engine.Maps;

/// <summary>A turning point of a path: a cell, and the direction the walk leaves it in, or arrives on it in for the last.</summary>
/// <param name="Cell">The cell.</param>
/// <param name="Direction">A direction, from 0 (east) clockwise to 7.</param>
public readonly record struct PathTurn(int Cell, int Direction);
