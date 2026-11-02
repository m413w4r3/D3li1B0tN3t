namespace D3B.App.Controls;

/// <summary>A map of a zone's mini-map, at its coordinates on the world map.</summary>
/// <param name="X">Its horizontal coordinate.</param>
/// <param name="Y">Its vertical coordinate.</param>
/// <param name="Kind">What it is.</param>
/// <param name="IsProbable">Whether a mini-map coloured by its parts marks it probable, likely holding a resource wanted (D138).</param>
internal readonly record struct ZoneSquare(int X, int Y, ZoneSquareKind Kind, bool IsProbable = false);
