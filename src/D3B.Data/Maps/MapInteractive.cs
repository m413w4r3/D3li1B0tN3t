using System.Runtime.InteropServices;

namespace D3B.Data.Maps;

/// <summary>An interactive element placed on a map, such as a resource, a door or a zaap: its identifier, its cell and its graphic.</summary>
/// <remarks>
/// <para>
/// The identifier is the one the client's map data gives the element, which the world graph's interactive transitions
/// name it by. It is unique within a map, not across maps: a third of the identifiers of client 3.7.12.12 appear on
/// several maps. Unmanaged and packed, as <see cref="MapCell"/>.
/// </para>
/// <para>
/// The graphic is what the client draws the element with. The client's data never says which interactive it is, but the
/// elements one graphic draws mostly are the same: what the server says of one tells of the others (client.md).
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MapInteractive : IEquatable<MapInteractive>
{
    /// <summary>The cell of an element the map does not place on a cell.</summary>
    public const int NoCell = -1;

    /// <summary>Gets the element's identifier on its map.</summary>
    public int ElementId { get; }

    /// <summary>Gets the cell the element stands on, or <see cref="NoCell"/> for the few elements drawn without one.</summary>
    public short CellId { get; }

    /// <summary>Gets the graphic the client draws the element with.</summary>
    public int GfxId { get; }

    /// <summary>Initializes a new instance of the <see cref="MapInteractive"/> struct.</summary>
    /// <param name="elementId">The element's identifier on its map.</param>
    /// <param name="cellId">The cell it stands on, or <see cref="NoCell"/>.</param>
    /// <param name="gfxId">The graphic it is drawn with.</param>
    /// <exception cref="ArgumentOutOfRangeException">The identifier is negative, or the cell is not one of a map's.</exception>
    public MapInteractive(int elementId, int cellId, int gfxId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementId);

        if (cellId is < NoCell or >= MapData.CellCount)
            throw new ArgumentOutOfRangeException(nameof(cellId), cellId, $"A cell lies between 0 and {MapData.CellCount - 1}, or is {NoCell} for none.");

        ElementId = elementId;
        CellId = (short)cellId;
        GfxId = gfxId;
    }

    /// <inheritdoc/>
    public bool Equals(MapInteractive other)
    {
        return ElementId == other.ElementId && CellId == other.CellId && GfxId == other.GfxId;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MapInteractive other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(ElementId, CellId, GfxId);
    }

    /// <summary>Compares two elements.</summary>
    /// <param name="left">An element.</param>
    /// <param name="right">Another element.</param>
    /// <returns>Whether they have the same identifier, cell and graphic.</returns>
    public static bool operator ==(MapInteractive left, MapInteractive right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two elements.</summary>
    /// <param name="left">An element.</param>
    /// <param name="right">Another element.</param>
    /// <returns>Whether their identifiers, cells or graphics differ.</returns>
    public static bool operator !=(MapInteractive left, MapInteractive right)
    {
        return !left.Equals(right);
    }
}
