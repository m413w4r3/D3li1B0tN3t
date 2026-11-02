using D3B.Bot.Activities;

namespace D3B.Bot.Journal;

/// <summary>An activity over, and how it ended (D112).</summary>
/// <param name="At">When.</param>
/// <param name="Activity">The activity's name.</param>
/// <param name="Steps">The steps it ran to their end.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Error">The message of the error it failed with, if any.</param>
/// <param name="StillFull">Whether it failed for the character still full once unloaded.</param>
public sealed record ActivityOverEntry(DateTimeOffset At, string Activity, int Steps, ActivityOutcome Outcome, string? Error, bool StillFull) : JournalEntry(At, 0);
