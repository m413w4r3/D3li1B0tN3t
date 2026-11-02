using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Pages;

/// <summary>The pages the host's modules registered, which the app shows in its rail after its own (D96).</summary>
/// <remarks>Registered by the bots module, from every <see cref="PageDefinition"/> in the services, in the order they were registered.</remarks>
public sealed class PageCatalog
{
    private readonly Dictionary<string, PageDefinition> _byId = new(StringComparer.Ordinal);

    /// <summary>Gets every page, in the order the modules registered them.</summary>
    public IReadOnlyList<PageDefinition> All { get; }

    /// <summary>Initializes a new instance of the <see cref="PageCatalog"/> class.</summary>
    /// <param name="definitions">The pages.</param>
    /// <exception cref="InvalidOperationException">Two pages share an id.</exception>
    public PageCatalog(IEnumerable<PageDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        All = [.. definitions];

        foreach (var definition in All)
        {
            if (!_byId.TryAdd(definition.Id, definition))
                throw new InvalidOperationException($"Two pages have the id {definition.Id}.");
        }
    }

    /// <summary>Finds a page by its id.</summary>
    /// <param name="id">The page's id.</param>
    /// <param name="definition">The page, when one has the id.</param>
    /// <returns>Whether a page has the id.</returns>
    public bool TryGet(string id, [NotNullWhen(true)] out PageDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _byId.TryGetValue(id, out definition);
    }
}
