using System.Runtime.InteropServices;

namespace D3B.Data.Interactives;

/// <summary>A kind of interactive element, such as an ash, a zaap or a door, from the client's <c>InteractivesDataRoot</c>.</summary>
/// <remarks>
/// The server names an element's kind by this identifier; the client's maps do not, so which kind an element of a map is
/// is known only once a character stands there (client.md). Unmanaged and packed: MemoryPack copies an array of it as
/// raw bytes, so its layout is part of the pack format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Interactive
{
    /// <summary>Gets the interactive's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="Interactive"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    public Interactive(int id, int nameId)
    {
        Id = id;
        NameId = nameId;
    }
}
