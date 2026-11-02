using System.Diagnostics.CodeAnalysis;
using MemoryPack;

namespace D3B.Data.Texts;

/// <summary>The texts of one language, by the numeric identifiers game data names them with, such as an item's <c>nameId</c>.</summary>
/// <remarks>Identifiers are kept sorted in one array and found by binary search: a third of a million texts take two arrays, not a hash table.</remarks>
[MemoryPackable]
public sealed partial class TextTable
{
    /// <summary>Gets the language's code, such as <c>fr</c>.</summary>
    public string Language { get; }

    /// <summary>Gets how many texts the table holds.</summary>
    [MemoryPackIgnore]
    public int Count => Ids.Length;

    [MemoryPackInclude]
    private int[] Ids { get; }

    [MemoryPackInclude]
    private string[] Texts { get; }

    /// <summary>Initializes a new instance of the <see cref="TextTable"/> class.</summary>
    /// <param name="language">The language's code, such as <c>fr</c>.</param>
    /// <param name="ids">The identifiers, in strictly ascending order.</param>
    /// <param name="texts">The text of each identifier, at the same index.</param>
    /// <exception cref="ArgumentException">The identifiers are not strictly ascending, or the two arrays differ in length.</exception>
    [MemoryPackConstructor]
    public TextTable(string language, int[] ids, string[] texts)
    {
        ArgumentException.ThrowIfNullOrEmpty(language);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(texts);

        if (ids.Length != texts.Length)
            throw new ArgumentException($"{ids.Length} identifiers for {texts.Length} texts.", nameof(texts));

        for (var i = 1; i < ids.Length; i++)
        {
            if (ids[i] <= ids[i - 1])
                throw new ArgumentException($"The identifiers are not strictly ascending: {ids[i]} follows {ids[i - 1]}.", nameof(ids));
        }

        Language = language;
        Ids = ids;
        Texts = texts;
    }

    /// <summary>Gets the name of the pack entry holding a language's texts.</summary>
    /// <param name="language">The language's code.</param>
    /// <returns>The entry's name, such as <c>texts/fr</c>.</returns>
    public static string EntryName(string language)
    {
        return $"texts/{language}";
    }

    /// <summary>Finds a text.</summary>
    /// <param name="id">The text's identifier.</param>
    /// <param name="text">The text, when found.</param>
    /// <returns>Whether the table holds the identifier.</returns>
    public bool TryGet(int id, [NotNullWhen(true)] out string? text)
    {
        var index = Array.BinarySearch(Ids, id);

        text = index >= 0 ? Texts[index] : null;

        return text is not null;
    }
}
