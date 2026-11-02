using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>What a bot knows of its character's fight, at one moment: part of the character's snapshot (D179).</summary>
/// <remarks>
/// <para>
/// Measured (client.md): the server creates a fight's context, shows each fighter and the teams' placement cells, moves
/// the fighters it places, then starts the fight. Each turn it names the fighter playing, frames every action in
/// sequences, and announces what each did: casts, steps, life lost and healed, teleports, swaps, summons and deaths. It
/// ends with each fighter's outcome and what the character gained, then creates the roleplay's context again.
/// </para>
/// <para>
/// <see cref="FightEvents"/> keeps it; what the server has not said yet is empty or <see langword="null"/>.
/// </para>
/// </remarks>
public sealed record FightState
{
    /// <summary>Gets where the fight stands.</summary>
    public FightPhase Phase { get; init; } = FightPhase.Placement;

    /// <summary>Gets the character's team: the challenger's, the attacker's, until the placement says otherwise.</summary>
    public FightTeam Team { get; init; }

    /// <summary>Gets the challengers' placement cells: the attacking character's, in every fight captured.</summary>
    public ImmutableArray<int> ChallengerCells { get; init; } = [];

    /// <summary>Gets the defenders' placement cells: the monsters attacked, in every fight captured.</summary>
    public ImmutableArray<int> DefenderCells { get; init; } = [];

    /// <summary>Gets the character's team's placement cells.</summary>
    public ImmutableArray<int> OwnCells => Team is FightTeam.Defender ? DefenderCells : ChallengerCells;

    /// <summary>Gets whether the character said it is ready to fight, during the placement.</summary>
    public bool IsReady { get; init; }

    /// <summary>Gets the fighters, by id.</summary>
    public ImmutableSortedDictionary<long, Fighter> Fighters { get; init; } = ImmutableSortedDictionary<long, Fighter>.Empty;

    /// <summary>Gets the order the fighters play in, the dead ones kept.</summary>
    public ImmutableArray<long> TurnOrder { get; init; } = [];

    /// <summary>Gets the round of turns, from 1 once the fight started.</summary>
    public int Round { get; init; }

    /// <summary>Gets the fighter whose turn it is, from its turn's start to its end.</summary>
    public long? PlayingId { get; init; }

    /// <summary>Gets whether the client may act: the turn of the character, or of a summon it controls, began and the server let the client play (<see cref="PlayingId"/>).</summary>
    public bool IsOwnTurn { get; init; }

    /// <summary>Gets the sequences the server opened and has not closed, the innermost last.</summary>
    public ImmutableArray<FightSequence> Sequences { get; init; } = [];

    /// <summary>Gets the place in the turns' order of the fighter whose turn it is, or -1 between turns.</summary>
    public int PlayingTurn => PlayingId is { } playing ? TurnOrder.IndexOf(playing) : -1;

    /// <summary>Gets the spells the client plays the character with in this fight, each with its grade, by spell, as the server's context listed them (D203).</summary>
    /// <remarks>Measured: in the 55 fights whose context listed the character's spells, its 300 casts were of them, at their grade; a transformed character's are its form's.</remarks>
    public ImmutableSortedDictionary<int, int> Spells { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets the marks on the fight's cells, glyphs, traps and bombs' walls, by id, as the server set and took them back (9f4).</summary>
    public ImmutableSortedDictionary<int, GroundMark> Marks { get; init; } = ImmutableSortedDictionary<int, GroundMark>.Empty;

    /// <summary>Gets the options of the character's team's fight the server said are set, or not, by option (D203).</summary>
    public ImmutableSortedDictionary<FightOption, bool> Options { get; init; } = ImmutableSortedDictionary<FightOption, bool>.Empty;

    /// <summary>Gets how the fight ended for the character, once the server sent the results.</summary>
    public FightEnd? End { get; init; }
}
