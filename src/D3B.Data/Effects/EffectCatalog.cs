using MemoryPack;

namespace D3B.Data.Effects;

/// <summary>The effects spells and items name.</summary>
/// <remarks>Sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class EffectCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "effects/catalog";

    /// <summary>Gets the effects, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Effect> Effects { get; }

    /// <summary>Initializes a new instance of the <see cref="EffectCatalog"/> class.</summary>
    /// <param name="effects">The effects, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">The effects are not in strictly ascending order.</exception>
    [MemoryPackConstructor]
    public EffectCatalog(ReadOnlyMemory<Effect> effects)
    {
        Tables.CheckAscending(effects.Span, static effect => effect.Id, nameof(effects));

        Effects = effects;
    }

    /// <summary>Finds an effect, such as one a spell's effect names or the server announces.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="effect">The effect, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetEffect(int id, out Effect effect)
    {
        return Tables.TryFind(Effects.Span, id, static effect => effect.Id, out effect);
    }
}
