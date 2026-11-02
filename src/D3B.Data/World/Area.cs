using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>An area of the world, which groups sub-areas, from the client's <c>AreasDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Area
{
    /// <summary>Gets the area's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the super-area, such as a continent, it belongs to.</summary>
    public int SuperAreaId { get; }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="superAreaId">Its super-area.</param>
    public Area(int id, int nameId, int superAreaId)
    {
        Id = id;
        NameId = nameId;
        SuperAreaId = superAreaId;
    }
}
