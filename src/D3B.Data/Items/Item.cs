using System.Runtime.InteropServices;

namespace D3B.Data.Items;

/// <summary>An item, from the client's <c>ItemsDataRoot</c>: what a bot needs to name it, sort it, weigh it and eat it.</summary>
/// <remarks>
/// About 21,500 items, so the struct keeps the client's own widths: 21 bytes, packed and unmanaged, which MemoryPack
/// copies as raw bytes. Of the effects, only the life an item's use gives back is kept, and only when that is all its use
/// does: recipes, criteria and the client's <c>m_flags</c>, whose bits are not measured, are left out (client.md).
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Item
{
    /// <summary>Gets the item's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets its type, in <see cref="ItemCatalog.Types"/>; the client lacks the type of a few test items.</summary>
    public ushort TypeId { get; }

    /// <summary>Gets its level, from 0 to 200.</summary>
    public byte Level { get; }

    /// <summary>Gets the weight of one, in pods: what it costs of a character's carrying capacity.</summary>
    public ushort Weight { get; }

    /// <summary>Gets its icon, which <see cref="Icons.IconCatalog.Items"/> holds for the items a harvest names, and not for the others.</summary>
    public int IconId { get; }

    /// <summary>Gets the least life one use gives back, when its use only heals; 0 otherwise.</summary>
    public ushort HealMin { get; }

    /// <summary>Gets the most life one use gives back, when its use only heals; 0 otherwise.</summary>
    public ushort HealMax { get; }

    /// <summary>Gets whether its use gives life back and does nothing else a bot must not bring about by eating it.</summary>
    public bool Heals => HealMax > 0;

    /// <summary>Initializes a new instance of the <see cref="Item"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="typeId">Its type.</param>
    /// <param name="level">Its level.</param>
    /// <param name="weight">The weight of one, in pods.</param>
    /// <param name="iconId">Its icon.</param>
    /// <param name="healMin">The least life one use gives back, when its use only heals.</param>
    /// <param name="healMax">The most life one use gives back, when its use only heals.</param>
    public Item(int id, int nameId, ushort typeId, byte level, ushort weight, int iconId, ushort healMin = 0, ushort healMax = 0)
    {
        Id = id;
        NameId = nameId;
        TypeId = typeId;
        Level = level;
        Weight = weight;
        IconId = iconId;
        HealMin = healMin;
        HealMax = healMax;
    }
}
