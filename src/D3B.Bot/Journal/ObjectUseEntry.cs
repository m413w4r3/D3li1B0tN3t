using D3B.Bot.Inventory;

namespace D3B.Bot.Journal;

/// <summary>A use of an object of the inventory, such as food eaten or a resource bag opened (D148).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">How many actions it was made within.</param>
/// <param name="ItemId">The object's item.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
public sealed record ObjectUseEntry(DateTimeOffset At, int Depth, int ItemId, ObjectUseOutcome Outcome, TimeSpan Duration)
    : JournalEntry(At, Depth);
