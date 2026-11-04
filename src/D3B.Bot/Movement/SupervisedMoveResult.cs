namespace D3B.Bot.Movement;

/// <summary>The result of a supervised one-step map move: its verdict, a stable reason code and the evidence it rests on.</summary>
/// <param name="Outcome">Whether the move passed, failed, was blocked or was interrupted.</param>
/// <param name="Reason">A stable code naming the outcome, such as <c>PASS</c>, <c>SERVER_REFUSED</c> or <c>ALREADY_CONSUMED</c>.</param>
/// <param name="Evidence">Where the move started, the transition planned, and where it ended.</param>
/// <param name="Detail">The unexpected error's type name only, or <see langword="null"/>.</param>
internal sealed record SupervisedMoveResult(SupervisedMoveOutcome Outcome, string Reason, SupervisedMoveEvidence Evidence, string? Detail = null);
