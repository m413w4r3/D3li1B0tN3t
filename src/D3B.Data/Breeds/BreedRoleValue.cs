using System.Runtime.InteropServices;

namespace D3B.Data.Breeds;

/// <summary>How much a breed plays a role, from the client's <c>breedRoles</c> of a breed.</summary>
/// <remarks>Unmanaged and packed, as <see cref="Breed"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct BreedRoleValue
{
    /// <summary>Gets the role, in <see cref="BreedCatalog.Roles"/>.</summary>
    public int RoleId { get; }

    /// <summary>Gets the client's <c>value</c>.</summary>
    public int Value { get; }

    /// <summary>Gets the client's <c>order</c>.</summary>
    public int Order { get; }

    /// <summary>Initializes a new instance of the <see cref="BreedRoleValue"/> struct.</summary>
    /// <param name="roleId">The role.</param>
    /// <param name="value">The client's value.</param>
    /// <param name="order">The client's order.</param>
    public BreedRoleValue(int roleId, int value, int order)
    {
        RoleId = roleId;
        Value = value;
        Order = order;
    }
}
