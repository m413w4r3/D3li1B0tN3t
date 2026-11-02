using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Activities;

/// <summary>The activities the host's modules registered, which the app starts on bots (D92).</summary>
/// <remarks>Registered by the bots module, from every <see cref="ActivityDefinition"/> in the services, in the order they were registered.</remarks>
public sealed class ActivityCatalog
{
    private readonly Dictionary<string, ActivityDefinition> _byId = new(StringComparer.Ordinal);

    /// <summary>Gets every activity, in the order the modules registered them.</summary>
    public IReadOnlyList<ActivityDefinition> All { get; }

    /// <summary>Initializes a new instance of the <see cref="ActivityCatalog"/> class.</summary>
    /// <param name="definitions">The activities.</param>
    /// <exception cref="InvalidOperationException">Two activities share an id.</exception>
    public ActivityCatalog(IEnumerable<ActivityDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        All = [.. definitions];

        foreach (var definition in All)
        {
            if (!_byId.TryAdd(definition.Id, definition))
                throw new InvalidOperationException($"Two activities have the id {definition.Id}.");
        }
    }

    /// <summary>Finds an activity by its id.</summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="definition">The activity, when one has the id.</param>
    /// <returns>Whether an activity has the id.</returns>
    public bool TryGet(string id, [NotNullWhen(true)] out ActivityDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _byId.TryGetValue(id, out definition);
    }
}
