using System.Runtime.InteropServices;

namespace D3B.Data.Characteristics;

/// <summary>A characteristic, from the client's <c>CharacteristicsDataRoot</c>, by the identifier the server names it by.</summary>
/// <remarks>
/// Its keyword, such as <c>hitPoints</c>, is held apart, in <see cref="CharacteristicCatalog.Keywords"/>. Ten
/// characteristics name the formula that scales a monster's, in <see cref="CharacteristicCatalog.Formulas"/> (client.md).
/// Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Characteristic
{
    /// <summary>The <see cref="ScaleFormulaId"/> of a characteristic no formula scales.</summary>
    public const int NoFormula = 0;

    /// <summary>Gets the characteristic's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier; 0 for two of client 3.7.12.12, one naming none and one a text the client lacks.</summary>
    public int NameId { get; }

    /// <summary>Gets the client's <c>categoryId</c>.</summary>
    public int CategoryId { get; }

    /// <summary>Gets the client's <c>order</c>.</summary>
    public int Order { get; }

    /// <summary>Gets the formula that scales a monster's, or <see cref="NoFormula"/>.</summary>
    public int ScaleFormulaId { get; }

    /// <summary>Gets the client's <c>visible</c>.</summary>
    public bool Visible { get; }

    /// <summary>Gets the client's <c>upgradable</c>.</summary>
    public bool Upgradable { get; }

    /// <summary>Initializes a new instance of the <see cref="Characteristic"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="categoryId">The client's categoryId.</param>
    /// <param name="order">The client's order.</param>
    /// <param name="scaleFormulaId">The formula that scales a monster's, or <see cref="NoFormula"/>.</param>
    /// <param name="visible">The client's visible.</param>
    /// <param name="upgradable">The client's upgradable.</param>
    public Characteristic(int id, int nameId, int categoryId, int order, int scaleFormulaId, bool visible, bool upgradable)
    {
        Id = id;
        NameId = nameId;
        CategoryId = categoryId;
        Order = order;
        ScaleFormulaId = scaleFormulaId;
        Visible = visible;
        Upgradable = upgradable;
    }
}
