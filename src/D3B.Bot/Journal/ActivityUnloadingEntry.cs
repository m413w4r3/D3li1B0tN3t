namespace D3B.Bot.Journal;

/// <summary>An activity unloading the character, full before a step (D112).</summary>
/// <param name="At">When it began.</param>
/// <param name="Activity">The activity's name.</param>
/// <param name="Weight">The weight the character carried.</param>
/// <param name="MaxWeight">The most it can carry.</param>
public sealed record ActivityUnloadingEntry(DateTimeOffset At, string Activity, int Weight, int MaxWeight) : JournalEntry(At, 0);
