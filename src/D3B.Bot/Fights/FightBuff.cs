using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>A buff on a fighter, as the server gave it: a characteristic changed, a state, a spell changed, or an effect waiting for its trigger.</summary>
/// <remarks>
/// Measured on the 49 fights captured (client.md): each buff names the spell's effect it comes from, which the pack holds,
/// and when it ends. The server announces no buff's end of time, only the buffs an effect removes before it: the fight's
/// state lets a buff go as the turn it ends at begins (D193).
/// </remarks>
public sealed record FightBuff
{
    /// <summary>The <see cref="EndRound"/> of a buff that lasts the whole fight.</summary>
    public const int Forever = -1;

    /// <summary>Gets the buff's id on its fighter, which a single removal names.</summary>
    public required int Uid { get; init; }

    /// <summary>Gets who gave the buff.</summary>
    public long SourceId { get; init; }

    /// <summary>Gets the buff's action, by the effect's id: 950 a state, 280 to 299 a spell changed, a characteristic's effect for a boost.</summary>
    public int ActionId { get; init; }

    /// <summary>Gets what the buff is.</summary>
    public FightBuffKind Kind { get; init; }

    /// <summary>Gets the spell that gave the buff.</summary>
    public int SpellId { get; init; }

    /// <summary>Gets that spell's grade.</summary>
    public int Grade { get; init; }

    /// <summary>Gets the spell's effect the buff comes from, by its uid in the pack's spells.</summary>
    public int EffectUid { get; init; }

    /// <summary>Gets a boost's change to its characteristic, or the spell a spell modifier changes or a triggered buff casts.</summary>
    public int Value { get; init; }

    /// <summary>Gets a state's id, or a spell modifier's value.</summary>
    public int Parameter { get; init; }

    /// <summary>Gets the effect's triggers, as the pack writes them: <c>I</c> at once.</summary>
    public string Triggers { get; init; } = string.Empty;

    /// <summary>Gets the round the buff ends in, or <see cref="Forever"/>.</summary>
    public int EndRound { get; init; } = Forever;

    /// <summary>Gets the turn the buff ends at in its round, by the place of the fighter playing it in the turns' order.</summary>
    public int EndTurn { get; init; }

    /// <summary>Tells whether the buff is over once a turn begins.</summary>
    /// <param name="round">The round of the turn beginning.</param>
    /// <param name="turn">The place in the turns' order of the fighter whose turn begins.</param>
    /// <returns>Whether its end is that turn or one before.</returns>
    public bool IsOverAt(int round, int turn)
    {
        return EndRound is not Forever && (round > EndRound || (round == EndRound && turn >= EndTurn));
    }
}
