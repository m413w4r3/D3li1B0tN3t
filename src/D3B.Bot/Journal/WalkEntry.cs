using D3B.Bot.Movement;

namespace D3B.Bot.Journal;

/// <summary>A walk to a cell of the character's map, written only when it is no part of another action (D112).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">0: a walk made for another action, to a map's exit or an element, has no entry.</param>
/// <param name="MapId">The map walked on.</param>
/// <param name="Cell">The cell walked to.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
public sealed record WalkEntry(DateTimeOffset At, int Depth, long MapId, int Cell, WalkOutcome Outcome, TimeSpan Duration)
    : JournalEntry(At, Depth);
