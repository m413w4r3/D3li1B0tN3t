namespace D3B.Bot.Journal;

/// <summary>An activity the bot starts (D112).</summary>
/// <param name="At">When.</param>
/// <param name="Activity">The activity's name.</param>
public sealed record ActivityStartedEntry(DateTimeOffset At, string Activity) : JournalEntry(At, 0);
