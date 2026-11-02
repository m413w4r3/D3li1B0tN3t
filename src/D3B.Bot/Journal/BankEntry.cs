using D3B.Bot.Banks;

namespace D3B.Bot.Journal;

/// <summary>Banking: going to a banker, putting stacks and kamas in, and leaving, written after its travel (D112).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">How many actions it was made within.</param>
/// <param name="MapId">The map it ended on, if any.</param>
/// <param name="Stacks">The stacks it asked to put in, once the bank was open; 0 before.</param>
/// <param name="Kamas">The kamas it asked to put in, once the bank was open; 0 before.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="GuildChest">Whether it went to the guild's chest, rather than the character's own (D147).</param>
public sealed record BankEntry(DateTimeOffset At, int Depth, long? MapId, int Stacks, long Kamas, BankOutcome Outcome, TimeSpan Duration, bool GuildChest = false)
    : JournalEntry(At, Depth);
