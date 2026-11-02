using MemoryPack;

namespace D3B.Data.Items;

/// <summary>The items and their types.</summary>
/// <remarks>Each table is sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class ItemCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "items/catalog";

    /// <summary>Gets the items, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Item> Items { get; }

    /// <summary>Gets the item types, in ascending order of identifier.</summary>
    public ReadOnlyMemory<ItemType> Types { get; }

    /// <summary>Initializes a new instance of the <see cref="ItemCatalog"/> class.</summary>
    /// <param name="items">The items, in strictly ascending order of identifier.</param>
    /// <param name="types">The item types, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public ItemCatalog(ReadOnlyMemory<Item> items, ReadOnlyMemory<ItemType> types)
    {
        Tables.CheckAscending(items.Span, static item => item.Id, nameof(items));
        Tables.CheckAscending(types.Span, static type => type.Id, nameof(types));

        Items = items;
        Types = types;
    }

    /// <summary>Finds an item.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="item">The item, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetItem(int id, out Item item)
    {
        return Tables.TryFind(Items.Span, id, static item => item.Id, out item);
    }

    /// <summary>Finds an item type.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="type">The type, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetType(int id, out ItemType type)
    {
        return Tables.TryFind(Types.Span, id, static type => type.Id, out type);
    }
}
