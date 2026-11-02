using MemoryPack;

namespace D3B.Data.Breeds;

/// <summary>The breeds, their spells, roles and costs.</summary>
/// <remarks>
/// Sorted by identifier and found by binary search; a breed's lists are slices of arrays the catalog holds
/// (<see cref="Tables"/>). A breed's spells and their variants are in <see cref="Spells.SpellCatalog.Variants"/>, with
/// the spells every character has. Loaded once and shared: nothing in it changes.
/// </remarks>
[MemoryPackable]
public sealed partial class BreedCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "breeds/catalog";

    /// <summary>Gets the breeds, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Breed> Breeds { get; }

    /// <summary>Gets the breeds' spells, each breed's together in the client's order, in the order of the breeds.</summary>
    public ReadOnlyMemory<int> Spells { get; }

    /// <summary>Gets the roles characters play, in ascending order of identifier.</summary>
    public ReadOnlyMemory<BreedRole> Roles { get; }

    /// <summary>Gets how much each breed plays each role, each breed's together, in the order of the breeds.</summary>
    public ReadOnlyMemory<BreedRoleValue> BreedRoles { get; }

    /// <summary>Gets what the breeds' characteristics cost, each breed's together, in the order of the breeds.</summary>
    public ReadOnlyMemory<BreedStatCost> StatCosts { get; }

    /// <summary>Initializes a new instance of the <see cref="BreedCatalog"/> class.</summary>
    /// <param name="breeds">The breeds, in strictly ascending order of identifier, each naming its own lists.</param>
    /// <param name="spells">The breeds' spells, each breed's together and in the order of the breeds.</param>
    /// <param name="roles">The roles, in strictly ascending order of identifier.</param>
    /// <param name="breedRoles">The breeds' roles, each breed's together and in the order of the breeds.</param>
    /// <param name="statCosts">The breeds' costs, each breed's together and in the order of the breeds.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or a slice does not follow the one before.</exception>
    [MemoryPackConstructor]
    public BreedCatalog(
        ReadOnlyMemory<Breed> breeds,
        ReadOnlyMemory<int> spells,
        ReadOnlyMemory<BreedRole> roles,
        ReadOnlyMemory<BreedRoleValue> breedRoles,
        ReadOnlyMemory<BreedStatCost> statCosts)
    {
        Tables.CheckAscending(breeds.Span, static breed => breed.Id, nameof(breeds));
        Tables.CheckAscending(roles.Span, static role => role.Id, nameof(roles));
        Tables.CheckSlices(breeds.Span, static breed => (breed.FirstSpell, breed.SpellCount), spells.Length, nameof(spells));
        Tables.CheckSlices(breeds.Span, static breed => (breed.FirstRole, breed.RoleCount), breedRoles.Length, nameof(breedRoles));
        Tables.CheckSlices(breeds.Span, static breed => (breed.FirstStatCost, breed.StatCostCount), statCosts.Length, nameof(statCosts));

        Breeds = breeds;
        Spells = spells;
        Roles = roles;
        BreedRoles = breedRoles;
        StatCosts = statCosts;
    }

    /// <summary>Finds a breed.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="breed">The breed, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetBreed(int id, out Breed breed)
    {
        return Tables.TryFind(Breeds.Span, id, static breed => breed.Id, out breed);
    }

    /// <summary>Gets a breed's spells.</summary>
    /// <param name="breed">A breed of the catalog.</param>
    /// <returns>Its spells, in the client's order.</returns>
    public ReadOnlySpan<int> SpellsOf(in Breed breed)
    {
        return Spells.Span.Slice(breed.FirstSpell, breed.SpellCount);
    }

    /// <summary>Gets how much a breed plays each role.</summary>
    /// <param name="breed">A breed of the catalog.</param>
    /// <returns>Its roles.</returns>
    public ReadOnlySpan<BreedRoleValue> RolesOf(in Breed breed)
    {
        return BreedRoles.Span.Slice(breed.FirstRole, breed.RoleCount);
    }

    /// <summary>Gets what a breed's characteristics cost.</summary>
    /// <param name="breed">A breed of the catalog.</param>
    /// <returns>Its costs, each characteristic's together in ascending order of threshold.</returns>
    public ReadOnlySpan<BreedStatCost> StatCostsOf(in Breed breed)
    {
        return StatCosts.Span.Slice(breed.FirstStatCost, breed.StatCostCount);
    }
}
