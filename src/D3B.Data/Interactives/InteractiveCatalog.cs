using MemoryPack;

namespace D3B.Data.Interactives;

/// <summary>The kinds of interactive elements.</summary>
/// <remarks>Sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class InteractiveCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "interactives/catalog";

    /// <summary>Gets the interactives, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Interactive> Interactives { get; }

    /// <summary>Initializes a new instance of the <see cref="InteractiveCatalog"/> class.</summary>
    /// <param name="interactives">The interactives, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">The interactives are not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public InteractiveCatalog(ReadOnlyMemory<Interactive> interactives)
    {
        Tables.CheckAscending(interactives.Span, static interactive => interactive.Id, nameof(interactives));

        Interactives = interactives;
    }

    /// <summary>Finds an interactive.</summary>
    /// <param name="id">Its identifier, such as the one the server gives an element of the map.</param>
    /// <param name="interactive">The interactive, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGet(int id, out Interactive interactive)
    {
        return Tables.TryFind(Interactives.Span, id, static interactive => interactive.Id, out interactive);
    }
}
