using System.Runtime.InteropServices;

namespace D3B.Data.Breeds;

/// <summary>A role characters play in a fight, from the client's <c>BreedRolesDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="Breed"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct BreedRole
{
    /// <summary>Gets the role's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Initializes a new instance of the <see cref="BreedRole"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    public BreedRole(int id, int nameId)
    {
        Id = id;
        NameId = nameId;
    }
}
