using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;

namespace D3B.Bot.Journal;

/// <summary>An interruption that cut an activity's step, and what the activity answered (D112).</summary>
/// <param name="At">When.</param>
/// <param name="Activity">The activity's name.</param>
/// <param name="Reason">What cut it.</param>
/// <param name="Response">Whether it resumes, pauses the bot or stops.</param>
/// <param name="Note">What the activity says of it, such as a fight's kind (D144), if it says.</param>
public sealed record ActivityInterruptedEntry(DateTimeOffset At, string Activity, Interruption Reason, InterruptionResponse Response, GameText? Note = null) : JournalEntry(At, 0);
