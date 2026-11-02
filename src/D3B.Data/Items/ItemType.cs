using System.Runtime.InteropServices;

namespace D3B.Data.Items;

/// <summary>A type of item, such as a wood or an amulet, from the client's <c>ItemTypesDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="Item"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct ItemType
{
    /// <summary>Gets the type's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the client's <c>superTypeId</c>, which groups types, such as every resource; its values are not named in the client's data.</summary>
    public int SuperTypeId { get; }

    /// <summary>Gets the client's <c>categoryId</c>, from 0 to 5, which groups types more broadly; its values are not named in the client's data.</summary>
    public int CategoryId { get; }

    /// <summary>Initializes a new instance of the <see cref="ItemType"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="superTypeId">The client's superTypeId.</param>
    /// <param name="categoryId">The client's categoryId.</param>
    public ItemType(int id, int nameId, int superTypeId, int categoryId)
    {
        Id = id;
        NameId = nameId;
        SuperTypeId = superTypeId;
        CategoryId = categoryId;
    }
}
