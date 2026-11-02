using MemoryPack;

namespace D3B.Data.Spells;

/// <summary>The states fighters may be in.</summary>
/// <remarks>Sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class SpellStateCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "spells/states";

    /// <summary>Gets the states, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SpellState> States { get; }

    /// <summary>Gets the states' effects, each state's together, in the order of the states.</summary>
    public ReadOnlyMemory<int> StateEffects { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellStateCatalog"/> class.</summary>
    /// <param name="states">The states, in strictly ascending order of identifier, each naming its own effects.</param>
    /// <param name="stateEffects">The states' effects, each state's together and in the order of the states.</param>
    /// <exception cref="ArgumentException">The states are not in strictly ascending order, or their effects do not follow one another.</exception>
    [MemoryPackConstructor]
    public SpellStateCatalog(ReadOnlyMemory<SpellState> states, ReadOnlyMemory<int> stateEffects)
    {
        Tables.CheckAscending(states.Span, static state => state.Id, nameof(states));
        Tables.CheckSlices(states.Span, static state => (state.FirstEffect, state.EffectCount), stateEffects.Length, nameof(stateEffects));

        States = states;
        StateEffects = stateEffects;
    }

    /// <summary>Finds a state.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="state">The state, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetState(int id, out SpellState state)
    {
        return Tables.TryFind(States.Span, id, static state => state.Id, out state);
    }

    /// <summary>Gets a state's effects.</summary>
    /// <param name="state">A state of the catalog.</param>
    /// <returns>Its effects, as the client lists them.</returns>
    public ReadOnlySpan<int> EffectsOf(in SpellState state)
    {
        return StateEffects.Span.Slice(state.FirstEffect, state.EffectCount);
    }
}
