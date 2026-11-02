using MemoryPack;

namespace D3B.Data.Maps;

/// <summary>What a bot needs of a map: its neighbours, its cells and the interactive elements placed on them.</summary>
/// <remarks>
/// Each map is its own pack entry, <see cref="EntryName"/>, read when first needed and kept by a
/// <see cref="Packs.PackCache"/> (D25). A map keeps the arrays it is given and never changes them, so one instance is
/// shared by every bot on the map.
/// </remarks>
[MemoryPackable]
public sealed partial class MapData
{
    /// <summary>The number of cells of every map: 14 columns of 40 staggered rows.</summary>
    public const int CellCount = 560;

    /// <summary>Gets the map's identifier.</summary>
    public long Id { get; }

    /// <summary>Gets the map north of this one, as the client's map data names it.</summary>
    public long TopNeighbourId { get; }

    /// <summary>Gets the map south of this one.</summary>
    public long BottomNeighbourId { get; }

    /// <summary>Gets the map west of this one.</summary>
    public long LeftNeighbourId { get; }

    /// <summary>Gets the map east of this one.</summary>
    public long RightNeighbourId { get; }

    /// <summary>Gets the map's cells, by cell identifier.</summary>
    public ReadOnlyMemory<MapCell> Cells { get; }

    /// <summary>Gets the interactive elements placed on the map, in ascending order of identifier.</summary>
    public ReadOnlyMemory<MapInteractive> Interactives { get; }

    /// <summary>Initializes a new instance of the <see cref="MapData"/> class.</summary>
    /// <param name="id">The map's identifier.</param>
    /// <param name="topNeighbourId">The map north of it.</param>
    /// <param name="bottomNeighbourId">The map south of it.</param>
    /// <param name="leftNeighbourId">The map west of it.</param>
    /// <param name="rightNeighbourId">The map east of it.</param>
    /// <param name="cells">Its <see cref="CellCount"/> cells, by identifier.</param>
    /// <param name="interactives">Its interactive elements, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A map does not have <see cref="CellCount"/> cells, or its elements are not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public MapData(long id, long topNeighbourId, long bottomNeighbourId, long leftNeighbourId, long rightNeighbourId, ReadOnlyMemory<MapCell> cells, ReadOnlyMemory<MapInteractive> interactives)
    {
        if (cells.Length != CellCount)
            throw new ArgumentException($"Map {id} has {cells.Length} cells, not {CellCount}.", nameof(cells));

        var elements = interactives.Span;

        for (var i = 1; i < elements.Length; i++)
        {
            if (elements[i].ElementId <= elements[i - 1].ElementId)
                throw new ArgumentException($"The elements of map {id} are not in strictly ascending order: {elements[i].ElementId} follows {elements[i - 1].ElementId}.", nameof(interactives));
        }

        Id = id;
        TopNeighbourId = topNeighbourId;
        BottomNeighbourId = bottomNeighbourId;
        LeftNeighbourId = leftNeighbourId;
        RightNeighbourId = rightNeighbourId;
        Cells = cells;
        Interactives = interactives;
    }

    /// <summary>Gets the name of the pack entry holding a map.</summary>
    /// <param name="id">The map's identifier.</param>
    /// <returns>The entry's name, such as <c>maps/191104002</c>.</returns>
    public static string EntryName(long id)
    {
        return $"maps/{id}";
    }

    /// <summary>Gets a cell.</summary>
    /// <param name="cellId">The cell's identifier, from 0 to <see cref="CellCount"/> - 1.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The map has no such cell.</exception>
    public MapCell Cell(int cellId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cellId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cellId, CellCount);

        return Cells.Span[cellId];
    }

    /// <summary>Finds an interactive element of the map.</summary>
    /// <param name="elementId">The element's identifier.</param>
    /// <param name="interactive">The element, when found.</param>
    /// <returns>Whether the map holds it.</returns>
    public bool TryGetInteractive(int elementId, out MapInteractive interactive)
    {
        var elements = Interactives.Span;
        int low = 0, high = elements.Length - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var found = elements[middle].ElementId;

            if (found == elementId)
            {
                interactive = elements[middle];

                return true;
            }

            if (found < elementId)
                low = middle + 1;
            else
                high = middle - 1;
        }

        interactive = default;

        return false;
    }
}
