using MemoryPack;

namespace D3B.Data.Characteristics;

/// <summary>The characteristics, and the formulas in Lua that scale a monster's to its level.</summary>
/// <remarks>
/// <para>
/// A formula is the client's <c>LuaFormulaData</c>, a Lua chunk that declares its parameters, such as
/// <c>monster_level</c> and <c>stat_ratio</c>, and returns the value from <c>main</c>; the pack keeps those a
/// characteristic names, as the client writes them. D3B does not run them: the server gives a fighter's characteristics,
/// and the grades hold the values it gave in every fight captured (client.md).
/// </para>
/// <para>Sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</para>
/// </remarks>
[MemoryPackable]
public sealed partial class CharacteristicCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "characteristics/catalog";

    /// <summary>Gets the characteristics, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Characteristic> Characteristics { get; }

    /// <summary>Gets each characteristic's keyword, such as <c>hitPoints</c>, in the order of the characteristics.</summary>
    public ReadOnlyMemory<string> Keywords { get; }

    /// <summary>Gets the identifiers of the formulas the characteristics name, in ascending order.</summary>
    public ReadOnlyMemory<int> FormulaIds { get; }

    /// <summary>Gets the formulas' Lua chunks, in the order of their identifiers.</summary>
    public ReadOnlyMemory<string> Formulas { get; }

    /// <summary>Initializes a new instance of the <see cref="CharacteristicCatalog"/> class.</summary>
    /// <param name="characteristics">The characteristics, in strictly ascending order of identifier.</param>
    /// <param name="keywords">Their keywords, in their order.</param>
    /// <param name="formulaIds">The formulas' identifiers, in strictly ascending order.</param>
    /// <param name="formulas">The formulas, in the order of their identifiers.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or a list is not as long as what it goes with.</exception>
    [MemoryPackConstructor]
    public CharacteristicCatalog(ReadOnlyMemory<Characteristic> characteristics, ReadOnlyMemory<string> keywords, ReadOnlyMemory<int> formulaIds, ReadOnlyMemory<string> formulas)
    {
        Tables.CheckAscending(characteristics.Span, static characteristic => characteristic.Id, nameof(characteristics));
        Tables.CheckAscending(formulaIds.Span, static id => id, nameof(formulaIds));

        if (keywords.Length != characteristics.Length)
            throw new ArgumentException($"{keywords.Length} keywords for {characteristics.Length} characteristics.", nameof(keywords));

        if (formulas.Length != formulaIds.Length)
            throw new ArgumentException($"{formulas.Length} formulas for {formulaIds.Length} identifiers.", nameof(formulas));

        Characteristics = characteristics;
        Keywords = keywords;
        FormulaIds = formulaIds;
        Formulas = formulas;
    }

    /// <summary>Finds a characteristic, such as one the server gives a fighter's by.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="characteristic">The characteristic, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetCharacteristic(int id, out Characteristic characteristic)
    {
        return Tables.TryFind(Characteristics.Span, id, static characteristic => characteristic.Id, out characteristic);
    }

    /// <summary>Gets a characteristic's keyword.</summary>
    /// <param name="id">The characteristic.</param>
    /// <returns>Its keyword, or null when the catalog lacks it.</returns>
    public string? KeywordOf(int id)
    {
        var index = Tables.IndexOf(Characteristics.Span, id, static characteristic => characteristic.Id);

        return index >= 0 ? Keywords.Span[index] : null;
    }

    /// <summary>Gets a formula.</summary>
    /// <param name="id">Its identifier, such as a characteristic's <see cref="Characteristic.ScaleFormulaId"/>.</param>
    /// <returns>Its Lua chunk, or null when the catalog lacks it.</returns>
    public string? FormulaOf(int id)
    {
        var index = Tables.IndexOf(FormulaIds.Span, id, static formula => formula);

        return index >= 0 ? Formulas.Span[index] : null;
    }
}
