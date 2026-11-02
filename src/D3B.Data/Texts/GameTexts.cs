using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D3B.Data.Packs;

namespace D3B.Data.Texts;

/// <summary>The texts of a pack, each language read from it the first time one of its texts is asked for, then kept.</summary>
public sealed class GameTexts
{
    private readonly GamePack _pack;
    private readonly ConcurrentDictionary<string, Lazy<TextTable>> _tables = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="GameTexts"/> class.</summary>
    /// <param name="pack">The pack holding the texts.</param>
    public GameTexts(GamePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        _pack = pack;
    }

    /// <summary>Finds a text.</summary>
    /// <param name="language">The language's code, such as <see cref="TextLanguages.French"/>.</param>
    /// <param name="id">The text's identifier.</param>
    /// <param name="text">The text, when found.</param>
    /// <returns>Whether the language holds the identifier.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no texts in that language.</exception>
    public bool TryGet(string language, int id, [NotNullWhen(true)] out string? text)
    {
        return Table(language).TryGet(id, out text);
    }

    /// <summary>Gets a text.</summary>
    /// <param name="language">The language's code, such as <see cref="TextLanguages.French"/>.</param>
    /// <param name="id">The text's identifier.</param>
    /// <returns>The text.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no texts in that language, or no text of that identifier.</exception>
    public string Get(string language, int id)
    {
        return TryGet(language, id, out var text) ? text : throw new KeyNotFoundException($"No text {id} in '{language}'.");
    }

    private TextTable Table(string language)
    {
        // Publication only: a failed read is not remembered, so that a later call reads again.
        return _tables.GetOrAdd(
            language,
            code => new Lazy<TextTable>(() => _pack.Read<TextTable>(TextTable.EntryName(code)), LazyThreadSafetyMode.PublicationOnly)).Value;
    }
}
