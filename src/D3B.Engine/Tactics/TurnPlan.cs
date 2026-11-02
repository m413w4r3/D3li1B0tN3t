using System.Collections.Immutable;

namespace D3B.Engine.Tactics;

/// <summary>The best sequence of walks and casts a search found for the turn, and what it weighed.</summary>
/// <param name="Actions">The actions in order, a last walk to where the turn ends best among them; none when nothing beats ending the turn now.</param>
/// <param name="Score">The fight's worth once they are done, as <see cref="FightJudge"/> judges it.</param>
/// <param name="Standing">The fight's worth were the turn ended now.</param>
/// <param name="Weighed">How many imagined fights the search judged.</param>
/// <param name="Elapsed">How long it searched.</param>
public sealed record TurnPlan(ImmutableArray<TacticAction> Actions, double Score, double Standing, int Weighed, TimeSpan Elapsed)
{
    /// <summary>Gets whether the turn is best ended now.</summary>
    public bool IsEmpty => Actions.IsDefaultOrEmpty;
}
