namespace D3B.Bot.Interactives;

/// <summary>The verdict and sanitized evidence for a supervised element-use attempt.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Reason">The stable code naming the outcome.</param>
/// <param name="Evidence">The observed preparation and attempt evidence.</param>
/// <param name="Detail">An optional exception type or additional diagnostic.</param>
internal sealed record SupervisedElementUseResult(
    SupervisedElementUseOutcome Outcome,
    string Reason,
    SupervisedElementUseEvidence Evidence,
    string? Detail = null)
{
    /// <summary>Gets the stable outcome code.</summary>
    public string Code => Outcome switch
    {
        SupervisedElementUseOutcome.PassHarvest => "PASS_HARVEST",
        SupervisedElementUseOutcome.Blocked => "BLOCKED",
        SupervisedElementUseOutcome.Refused => "REFUSED",
        SupervisedElementUseOutcome.Unconfirmed => "UNCONFIRMED",
        SupervisedElementUseOutcome.Interrupted => "INTERRUPTED",
        _ => "UNKNOWN",
    };
}
