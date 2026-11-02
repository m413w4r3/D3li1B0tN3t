using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>A mark on a fight's cells as the server set it: a glyph, a trap or a bombs' wall (9f4).</summary>
/// <remarks>
/// The server sets it with action 400 or 401 and takes it back with 310, by its id (client.md). When it strikes, as the turn
/// of a fighter on it begins or ends or as one comes onto it, is the effect's of the spell that set it, which the pack says.
/// </remarks>
public sealed record GroundMark
{
    /// <summary>Gets the mark's id in the fight.</summary>
    public required int Id { get; init; }

    /// <summary>Gets what it is.</summary>
    public FightMarkType Type { get; init; }

    /// <summary>Gets the fighter who set it.</summary>
    public long AuthorId { get; init; }

    /// <summary>Gets its author's team.</summary>
    public FightTeam Team { get; init; }

    /// <summary>Gets the spell that set it.</summary>
    public int SetterId { get; init; }

    /// <summary>Gets the spell it casts; 0 for a bombs' wall.</summary>
    public int SpellId { get; init; }

    /// <summary>Gets the grade it casts that spell at.</summary>
    public int Grade { get; init; }

    /// <summary>Gets the cell it was cast on, its zone's middle; -1 for a trap its enemies do not see.</summary>
    public int Centre { get; init; }

    /// <summary>Gets the cells it covers.</summary>
    public ImmutableArray<int> Cells { get; init; } = [];

    /// <summary>Gets the round it goes in, as its author's turn of that round begins.</summary>
    public int EndRound { get; init; }
}
