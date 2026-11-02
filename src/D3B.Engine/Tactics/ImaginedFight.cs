using System.Collections.Immutable;
using D3B.Engine.Fights;

namespace D3B.Engine.Tactics;

/// <summary>A fight as the AI imagines it: the fighters as they would stand once some actions are done, from the one playing's view.</summary>
/// <remarks>
/// The fight's state the bot keeps gives the first (D193), as the engine sees it (<see cref="Combatant"/>); each action the
/// search weighs gives the next (<see cref="Imagination"/>), immutable, so that a search keeps every branch it weighs. The
/// fighters other than the one playing hold the AP and MP they start their next turn with, which what removes them lowers.
/// </remarks>
public sealed record ImaginedFight
{
    /// <summary>Gets the fight's map.</summary>
    public required FightMap Map { get; init; }

    /// <summary>Gets every fighter, the dead among them.</summary>
    public required ImmutableArray<Combatant> Fighters { get; init; }

    /// <summary>Gets the fighter whose turn it is: the bot's character.</summary>
    public required long ActorId { get; init; }

    /// <summary>Gets the fight's round, which the rules of a cast read.</summary>
    public int Round { get; init; }

    /// <summary>Gets the casts the fighter playing chose this fight.</summary>
    public CastHistory History { get; init; } = CastHistory.Empty;

    /// <summary>Gets the buffs the casts imagined gave, which the fighters' characteristics and states already hold, with how long each lasts.</summary>
    public ImmutableArray<ImaginedBuff> Buffs { get; init; } = [];

    /// <summary>Gets the marks on the fight's cells, glyphs and traps, the server's and those the casts imagined set.</summary>
    public ImmutableArray<CellMark> Marks { get; init; } = [];

    /// <summary>Gets the fighter playing.</summary>
    /// <exception cref="InvalidOperationException">No fighter has its id.</exception>
    public Combatant Actor => Find(ActorId) ?? throw new InvalidOperationException($"No fighter {ActorId} in the fight.");

    /// <summary>Finds a fighter.</summary>
    /// <param name="id">The fighter's id.</param>
    /// <returns>The fighter, or null.</returns>
    public Combatant? Find(long id)
    {
        foreach (var fighter in Fighters)
        {
            if (fighter.Id == id)
                return fighter;
        }

        return null;
    }

    /// <summary>Gets the fight with one fighter changed.</summary>
    /// <param name="fighter">The fighter as it becomes, by its id.</param>
    /// <returns>The fight with it; the same when no fighter has its id.</returns>
    public ImaginedFight With(Combatant fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        for (var index = 0; index < Fighters.Length; index++)
        {
            if (Fighters[index].Id == fighter.Id)
                return this with { Fighters = Fighters.SetItem(index, fighter) };
        }

        return this;
    }

    /// <summary>Gets the living fighter on a cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The fighter, or null.</returns>
    public Combatant? On(int cell)
    {
        foreach (var fighter in Fighters)
        {
            if (fighter.IsAlive && fighter.Cell == cell)
                return fighter;
        }

        return null;
    }
}
