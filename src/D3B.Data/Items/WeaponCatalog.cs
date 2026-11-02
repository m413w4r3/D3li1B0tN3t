using MemoryPack;

namespace D3B.Data.Items;

/// <summary>The weapons, their effects, and the zones of their types.</summary>
/// <remarks>
/// <para>
/// The weapons and their types are sorted by identifier and found by binary search; a weapon's effects are a slice of one
/// array (<see cref="Tables"/>), and the zones' texts, a few distinct among the types, are held once in <see cref="Zones"/>.
/// </para>
/// <para>Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class WeaponCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "items/weapons";

    /// <summary>Gets the weapons, in ascending order of their item's identifier.</summary>
    public ReadOnlyMemory<Weapon> Weapons { get; }

    /// <summary>Gets the weapons' effects, each weapon's together, in the order of the weapons.</summary>
    public ReadOnlyMemory<WeaponEffect> Effects { get; }

    /// <summary>Gets the types of weapon, in ascending order of identifier.</summary>
    public ReadOnlyMemory<WeaponType> Types { get; }

    /// <summary>Gets the texts of the types' zones, each once.</summary>
    public ReadOnlyMemory<string> Zones { get; }

    /// <summary>Initializes a new instance of the <see cref="WeaponCatalog"/> class.</summary>
    /// <param name="weapons">The weapons, in strictly ascending order of their item's identifier, each naming its own effects.</param>
    /// <param name="effects">The weapons' effects, each weapon's together and in the order of the weapons.</param>
    /// <param name="types">The types of weapon, in strictly ascending order of identifier.</param>
    /// <param name="zones">The texts the types' zones name.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, a slice does not follow the one before, or a type names a zone the catalog lacks.</exception>
    [MemoryPackConstructor]
    public WeaponCatalog(ReadOnlyMemory<Weapon> weapons, ReadOnlyMemory<WeaponEffect> effects, ReadOnlyMemory<WeaponType> types, ReadOnlyMemory<string> zones)
    {
        Tables.CheckAscending(weapons.Span, static weapon => weapon.ItemId, nameof(weapons));
        Tables.CheckAscending(types.Span, static type => type.Id, nameof(types));
        Tables.CheckSlices(weapons.Span, static weapon => (weapon.FirstEffect, weapon.EffectCount), effects.Length, nameof(effects));

        foreach (var type in types.Span)
        {
            if (type.Zone < 0 || type.Zone >= zones.Length)
                throw new ArgumentException($"Weapon type {type.Id} names zone {type.Zone}, where there are {zones.Length}.", nameof(types));
        }

        Weapons = weapons;
        Effects = effects;
        Types = types;
        Zones = zones;
    }

    /// <summary>Finds a weapon.</summary>
    /// <param name="itemId">Its item's identifier.</param>
    /// <param name="weapon">The weapon, when found.</param>
    /// <returns>Whether the item is a weapon the catalog holds.</returns>
    public bool TryGetWeapon(int itemId, out Weapon weapon)
    {
        return Tables.TryFind(Weapons.Span, itemId, static weapon => weapon.ItemId, out weapon);
    }

    /// <summary>Finds a type of weapon.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="type">The type, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetType(int id, out WeaponType type)
    {
        return Tables.TryFind(Types.Span, id, static type => type.Id, out type);
    }

    /// <summary>Gets a weapon's effects.</summary>
    /// <param name="weapon">A weapon of the catalog.</param>
    /// <returns>Its effects, in the order the client lists them.</returns>
    public ReadOnlySpan<WeaponEffect> EffectsOf(in Weapon weapon)
    {
        return Effects.Span.Slice(weapon.FirstEffect, weapon.EffectCount);
    }

    /// <summary>Gets the zone a type's blows hit.</summary>
    /// <param name="type">A type of the catalog.</param>
    /// <returns>Its zone as the client writes it, such as <c>P</c>.</returns>
    public string ZoneOf(in WeaponType type)
    {
        return Zones.Span[type.Zone];
    }
}
