using System.Collections.Immutable;

namespace D3B.Engine.Fights;

/// <summary>The spells a fighter cast this fight: when each was last cast, and how many times this turn, on each target.</summary>
/// <remarks>
/// A turn is told by the fight's round, the server's (D179): a cast of another round than the last starts the turn's
/// counts again. Only the casts the fighter chose count, the first of each of its sequences of a spell (client.md); those
/// its effects trigger do not. Immutable: <see cref="Cast"/> gives the next history.
/// </remarks>
public sealed record CastHistory
{
    /// <summary>Gets a history with no cast.</summary>
    public static CastHistory Empty { get; } = new();

    /// <summary>Gets the round this turn's counts are of, or 0 before the first cast.</summary>
    public int Round { get; private init; }

    /// <summary>Gets the round each spell was last cast in, by spell.</summary>
    public ImmutableDictionary<int, int> LastRounds { get; private init; } = ImmutableDictionary<int, int>.Empty;

    /// <summary>Gets how many times each spell was cast this turn, by spell.</summary>
    public ImmutableDictionary<int, int> ThisTurn { get; private init; } = ImmutableDictionary<int, int>.Empty;

    /// <summary>Gets how many times each spell was cast on each fighter this turn.</summary>
    public ImmutableDictionary<(int SpellId, long TargetId), int> OnTargets { get; private init; } = ImmutableDictionary<(int SpellId, long TargetId), int>.Empty;

    /// <summary>Gives the history once the fighter cast a spell.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="targetId">The fighter on the cell cast on, or null for none.</param>
    /// <param name="round">The fight's round.</param>
    /// <returns>The next history.</returns>
    public CastHistory Cast(int spellId, long? targetId, int round)
    {
        var turn = round == Round ? this : this with { Round = round, ThisTurn = ThisTurn.Clear(), OnTargets = OnTargets.Clear() };

        return turn with
        {
            LastRounds = turn.LastRounds.SetItem(spellId, round),
            ThisTurn = turn.ThisTurn.SetItem(spellId, turn.ThisTurn.GetValueOrDefault(spellId) + 1),
            OnTargets = targetId is { } target ? turn.OnTargets.SetItem((spellId, target), turn.OnTargets.GetValueOrDefault((spellId, target)) + 1) : turn.OnTargets,
        };
    }

    /// <summary>Gets how many times a spell was cast in a round's turn.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="round">The round.</param>
    /// <returns>The count.</returns>
    public int CastsIn(int spellId, int round)
    {
        return round == Round ? ThisTurn.GetValueOrDefault(spellId) : 0;
    }

    /// <summary>Gets how many times a spell was cast on a fighter in a round's turn.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="targetId">The fighter.</param>
    /// <param name="round">The round.</param>
    /// <returns>The count.</returns>
    public int CastsOn(int spellId, long targetId, int round)
    {
        return round == Round ? OnTargets.GetValueOrDefault((spellId, targetId)) : 0;
    }
}
