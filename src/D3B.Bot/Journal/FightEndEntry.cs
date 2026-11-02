using D3B.Protocol.Game;

namespace D3B.Bot.Journal;

/// <summary>A fight ended: how for the character, and what it gained (D179).</summary>
/// <param name="At">When the server sent the results.</param>
/// <param name="Outcome">The character's outcome.</param>
/// <param name="Experience">The experience the fight gave.</param>
/// <param name="Kamas">The kamas gained.</param>
/// <param name="Objects">How many objects were gained, of every item.</param>
public sealed record FightEndEntry(DateTimeOffset At, FightOutcome Outcome, long Experience, long Kamas, int Objects) : JournalEntry(At, 0);
