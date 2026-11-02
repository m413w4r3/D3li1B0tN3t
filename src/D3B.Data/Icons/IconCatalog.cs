using MemoryPack;

namespace D3B.Data.Icons;

/// <summary>The icons the pack holds: those of the items, of the professions, of the classes' spells, and of the monsters a hunter meets (D126).</summary>
/// <remarks>
/// <para>
/// Items, jobs, spells and monsters number their icons apart, so each kind has its table, each its own size: items' are
/// 32 pixels square, the client's halved (D265), professions' and monsters' 64, spells' 48, the client's at 1x. Each table
/// is keyed by the client's identifier of the picture, which an item, a job, a spell or a monster names: monsters share
/// pictures, an archmonster its monster's, so that a monster's icon is found through its
/// <see cref="D3B.Data.Monsters.Monster.GfxId"/>.
/// </para>
/// <para>
/// The pack keeps every item's icon the client holds (D265); those of the breeds' spells and their variants, not every
/// spell's; and those of the monsters a sub-area lists, but those hidden from the bestiary or that are bombs, not every
/// monster's: one without an icon is drawn without it.
/// One entry, read whole the first time an icon is asked for.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class IconCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "icons/catalog";

    /// <summary>Gets the items' icons, by <see cref="D3B.Data.Items.Item.IconId"/>.</summary>
    public IconTable Items { get; }

    /// <summary>Gets the professions' icons, by <see cref="D3B.Data.Jobs.Job.IconId"/>.</summary>
    public IconTable Jobs { get; }

    /// <summary>Gets the spells' icons, by <see cref="D3B.Data.Spells.Spell.IconId"/>.</summary>
    public IconTable Spells { get; }

    /// <summary>Gets the monsters' icons, by <see cref="D3B.Data.Monsters.Monster.GfxId"/>.</summary>
    public IconTable Monsters { get; }

    /// <summary>Initializes a new instance of the <see cref="IconCatalog"/> class.</summary>
    /// <param name="items">The items' icons.</param>
    /// <param name="jobs">The professions' icons.</param>
    /// <param name="spells">The spells' icons.</param>
    /// <param name="monsters">The monsters' icons.</param>
    [MemoryPackConstructor]
    public IconCatalog(IconTable items, IconTable jobs, IconTable spells, IconTable monsters)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsters);

        Items = items;
        Jobs = jobs;
        Spells = spells;
        Monsters = monsters;
    }
}
