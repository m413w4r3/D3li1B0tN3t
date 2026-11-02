using D3B.Bot.Movement;

namespace D3B.Bot.Journal;

/// <summary>A map change: one step of a path through the world, a teleport, or leaving the haven bag (D112).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">0 when an activity asked it, 1 within a travel.</param>
/// <param name="From">The map left.</param>
/// <param name="To">The map sought, or <see langword="null"/> when leaving the haven bag, whose map only the server says.</param>
/// <param name="Reached">The map the character stood on once it ended, if any.</param>
/// <param name="Kind">How it went.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
public sealed record MapChangeEntry(DateTimeOffset At, int Depth, long From, long? To, long? Reached, MapChangeKind Kind, MapChangeOutcome Outcome, TimeSpan Duration)
    : JournalEntry(At, Depth);
