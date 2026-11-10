using D3B.Bot.Interactives;

namespace D3B.Bot.Harvesting;

/// <summary>A candidate rejected from exact target selection and the reason for its rejection.</summary>
internal sealed record HarvestResourceTargetRejection(ElementCandidate Candidate, string Reason);
