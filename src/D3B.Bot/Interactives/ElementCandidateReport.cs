using System.Collections.Immutable;

namespace D3B.Bot.Interactives;

/// <summary>The passive candidate list and its two independent verdicts.</summary>
internal sealed record ElementCandidateReport
{
    public ElementUseSnapshot? Snapshot { get; init; }

    public string Reason { get; init; } = "READY";

    public ImmutableArray<ElementCandidate> Candidates { get; init; } = [];

    public ElementCandidateTargetVerdict Target { get; init; }

    public ElementCandidateActionVerdict Action { get; init; }

    public string ActionReason { get; init; } = "TARGET_UNKNOWN";

    public bool AttemptConsumed { get; init; }

    public bool ActionEnabled { get; init; }

    public static ElementCandidateReport Unavailable(string reason, bool actionEnabled, bool attemptConsumed = false)
    {
        return new ElementCandidateReport
        {
            Reason = reason,
            Target = ElementCandidateTargetVerdict.Unknown,
            Action = ElementCandidateActionVerdict.Blocked,
            ActionReason = reason,
            AttemptConsumed = attemptConsumed,
            ActionEnabled = actionEnabled,
        };
    }
}
