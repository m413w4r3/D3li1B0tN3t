using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>A sub-area of the world, from the client's <c>SubAreasDataRoot</c>: what a bot needs to travel to it, harvest and hunt there.</summary>
/// <remarks>
/// Unmanaged and packed, as <see cref="WorldVertex"/>. Its resources and its monsters are slices of
/// <see cref="WorldAtlas.Harvestables"/> and <see cref="WorldAtlas.Monsters"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SubArea
{
    /// <summary>The <see cref="AssociatedZaapMapId"/> of a sub-area that names no zaap.</summary>
    public const long NoZaap = 0;

    /// <summary>Gets the sub-area's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the area it belongs to.</summary>
    public int AreaId { get; }

    /// <summary>Gets its level, from 0 to 200.</summary>
    public int Level { get; }

    /// <summary>Gets the map of the zaap the client associates with it, or <see cref="NoZaap"/>.</summary>
    public long AssociatedZaapMapId { get; }

    /// <summary>Gets whether an account without a subscription may enter it.</summary>
    public bool BasicAccountAllowed { get; }

    /// <summary>Gets the index of its first resource in <see cref="WorldAtlas.Harvestables"/>.</summary>
    public int FirstHarvestable { get; }

    /// <summary>Gets how many resources it has, 0 for most sub-areas.</summary>
    public int HarvestableCount { get; }

    /// <summary>Gets the index of its first monster in <see cref="WorldAtlas.Monsters"/>.</summary>
    public int FirstMonster { get; }

    /// <summary>Gets how many monsters live there, 0 for a sub-area without any, such as a town's.</summary>
    public int MonsterCount { get; }

    /// <summary>Initializes a new instance of the <see cref="SubArea"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="areaId">Its area.</param>
    /// <param name="level">Its level.</param>
    /// <param name="associatedZaapMapId">The map of its zaap, or <see cref="NoZaap"/>.</param>
    /// <param name="basicAccountAllowed">Whether an account without a subscription may enter it.</param>
    /// <param name="firstHarvestable">The index of its first resource.</param>
    /// <param name="harvestableCount">How many resources it has.</param>
    /// <param name="firstMonster">The index of its first monster.</param>
    /// <param name="monsterCount">How many monsters live there.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index or the count of its resources or of its monsters is negative.</exception>
    public SubArea(int id, int nameId, int areaId, int level, long associatedZaapMapId, bool basicAccountAllowed, int firstHarvestable, int harvestableCount, int firstMonster, int monsterCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstHarvestable);
        ArgumentOutOfRangeException.ThrowIfNegative(harvestableCount);
        ArgumentOutOfRangeException.ThrowIfNegative(firstMonster);
        ArgumentOutOfRangeException.ThrowIfNegative(monsterCount);

        Id = id;
        NameId = nameId;
        AreaId = areaId;
        Level = level;
        AssociatedZaapMapId = associatedZaapMapId;
        BasicAccountAllowed = basicAccountAllowed;
        FirstHarvestable = firstHarvestable;
        HarvestableCount = harvestableCount;
        FirstMonster = firstMonster;
        MonsterCount = monsterCount;
    }
}
