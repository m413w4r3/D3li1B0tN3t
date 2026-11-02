using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.Packs;
using D3B.Data.Spells;

namespace D3B.Data;

/// <summary>The game tables of a pack, each read once, when first asked for, then kept.</summary>
public sealed class GameTables
{
    private readonly Lazy<ItemCatalog> _items;
    private readonly Lazy<WeaponCatalog> _weapons;
    private readonly Lazy<JobCatalog> _jobs;
    private readonly Lazy<InteractiveCatalog> _interactives;
    private readonly Lazy<BankCatalog> _banks;
    private readonly Lazy<IconCatalog> _icons;
    private readonly Lazy<SpellCatalog> _spells;
    private readonly Lazy<SpellStateCatalog> _states;
    private readonly Lazy<EffectCatalog> _effects;
    private readonly Lazy<MonsterCatalog> _monsters;
    private readonly Lazy<BreedCatalog> _breeds;
    private readonly Lazy<CharacteristicCatalog> _characteristics;

    /// <summary>Gets the items and their types.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no items.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public ItemCatalog Items => _items.Value;

    /// <summary>Gets the weapons, their effects and the zones of their types.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no weapons.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public WeaponCatalog Weapons => _weapons.Value;

    /// <summary>Gets the jobs and the skills.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no jobs.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public JobCatalog Jobs => _jobs.Value;

    /// <summary>Gets the kinds of interactive elements.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no interactives.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public InteractiveCatalog Interactives => _interactives.Value;

    /// <summary>Gets the banks and the bankers.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no banks.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public BankCatalog Banks => _banks.Value;

    /// <summary>Gets the icons of the items a harvest names and of the professions.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no icons.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public IconCatalog Icons => _icons.Value;

    /// <summary>Gets the spells, their levels and effects, their variants, and the Rogue's bombs and walls.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no spells.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public SpellCatalog Spells => _spells.Value;

    /// <summary>Gets the states fighters may be in.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no states.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public SpellStateCatalog States => _states.Value;

    /// <summary>Gets the effects spells and items name.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no effects.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public EffectCatalog Effects => _effects.Value;

    /// <summary>Gets the monsters, their grades and spells, and their races.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no monsters.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public MonsterCatalog Monsters => _monsters.Value;

    /// <summary>Gets the breeds, their spells, roles and costs.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no breeds.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public BreedCatalog Breeds => _breeds.Value;

    /// <summary>Gets the characteristics, and the formulas that scale a monster's.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no characteristics.</exception>
    /// <exception cref="PackFormatException">Their entry is damaged.</exception>
    public CharacteristicCatalog Characteristics => _characteristics.Value;

    /// <summary>Initializes a new instance of the <see cref="GameTables"/> class.</summary>
    /// <param name="pack">The pack holding the tables.</param>
    public GameTables(GamePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        // Publication only: a failed read is not remembered, so that a later call reads again.
        _items = new Lazy<ItemCatalog>(() => pack.Read<ItemCatalog>(ItemCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _weapons = new Lazy<WeaponCatalog>(() => pack.Read<WeaponCatalog>(WeaponCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _jobs = new Lazy<JobCatalog>(() => pack.Read<JobCatalog>(JobCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _interactives = new Lazy<InteractiveCatalog>(() => pack.Read<InteractiveCatalog>(InteractiveCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _banks = new Lazy<BankCatalog>(() => pack.Read<BankCatalog>(BankCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _icons = new Lazy<IconCatalog>(() => pack.Read<IconCatalog>(IconCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _spells = new Lazy<SpellCatalog>(() => pack.Read<SpellCatalog>(SpellCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _states = new Lazy<SpellStateCatalog>(() => pack.Read<SpellStateCatalog>(SpellStateCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _effects = new Lazy<EffectCatalog>(() => pack.Read<EffectCatalog>(EffectCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _monsters = new Lazy<MonsterCatalog>(() => pack.Read<MonsterCatalog>(MonsterCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _breeds = new Lazy<BreedCatalog>(() => pack.Read<BreedCatalog>(BreedCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
        _characteristics = new Lazy<CharacteristicCatalog>(() => pack.Read<CharacteristicCatalog>(CharacteristicCatalog.EntryName), LazyThreadSafetyMode.PublicationOnly);
    }
}
