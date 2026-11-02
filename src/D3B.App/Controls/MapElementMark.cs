using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>An interactive element of the map, where it stands and what the server says of it (D121).</summary>
/// <param name="ElementId">The element's id on its map.</param>
/// <param name="Cell">The cell it stands on.</param>
/// <param name="Mark">How the map draws it.</param>
/// <param name="Icon">The icon of the resource it gathers, which the map draws in place of its mark, when the pack keeps one (D135).</param>
internal readonly record struct MapElementMark(int ElementId, int Cell, MapMark Mark, IImage? Icon = null);
