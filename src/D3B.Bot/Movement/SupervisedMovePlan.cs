using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Bot.Movement;

/// <summary>What a supervised move would do, after checking the session, position, neighbour and single scroll transition.</summary>
/// <param name="Ready">Whether the move can be attempted.</param>
/// <param name="Reason">A stable code naming the first blocker, or <c>PLAN_READY</c>.</param>
/// <param name="Readiness">The passive readiness the plan was checked against.</param>
/// <param name="Step">The single world-graph step, when ready.</param>
/// <param name="Rules">The path rules the step was chosen with, when ready.</param>
/// <param name="Transition">The step's transition type, when ready.</param>
/// <param name="ExitCell">A cell the source map can be left from, when ready.</param>
internal sealed record SupervisedMovePlan(
    bool Ready,
    string Reason,
    MovementReadiness Readiness,
    WorldStep? Step,
    WorldPathRules? Rules,
    WorldTransitionType? Transition,
    int? ExitCell)
{
    /// <summary>Makes a plan that refuses to move.</summary>
    /// <param name="reason">The stable code naming the first blocker.</param>
    /// <param name="readiness">The passive readiness the plan checked.</param>
    /// <returns>The blocked plan.</returns>
    internal static SupervisedMovePlan Blocked(string reason, MovementReadiness readiness)
    {
        return new SupervisedMovePlan(false, reason, readiness, null, null, null, null);
    }
}
