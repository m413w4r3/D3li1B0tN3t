using D3B.Bot.Movement;

namespace D3B.Bot.Journal;

/// <summary>A travel through the world, written after its map changes, which are 1 deeper (D112).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">How many actions it was made within, such as banking.</param>
/// <param name="From">The map it left from, if the character stood on one.</param>
/// <param name="Sought">The map sought, or <see langword="null"/> when the nearest of several was.</param>
/// <param name="Reached">The map the character stood on once it ended, if any.</param>
/// <param name="Changes">The map changes it made.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
public sealed record TravelEntry(DateTimeOffset At, int Depth, long? From, long? Sought, long? Reached, int Changes, TravelOutcome Outcome, TimeSpan Duration)
    : JournalEntry(At, Depth);
