namespace D3B.Bot.Journal;

/// <summary>What became of the bot: its connection, the player's pause and its resuming, a fight (D112).</summary>
/// <param name="At">When.</param>
/// <param name="Event">What.</param>
public sealed record BotEventEntry(DateTimeOffset At, BotEvent Event) : JournalEntry(At, 0);
