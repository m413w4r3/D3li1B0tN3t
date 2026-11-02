using D3B.Bot.Journal;

namespace D3B.Storage.Profiles;

/// <summary>A character's profile that did not start on its client's connection, and why (D99, D112).</summary>
/// <remarks>
/// The host's log names the bot by its character, so its problems leave it out (D114): without this entry, the app would
/// show a bot that does nothing, and nowhere why.
/// </remarks>
/// <param name="At">When.</param>
/// <param name="Profile">The profile's name.</param>
/// <param name="ActivityId">The id of the activity it names, such as one no module registers any more.</param>
/// <param name="Failure">Why it did not start.</param>
/// <param name="Error">What was thrown, for <see cref="ProfileStartFailure.NotStarted"/>.</param>
public sealed record ProfileNotStartedEntry(DateTimeOffset At, string Profile, string ActivityId, ProfileStartFailure Failure, string? Error) : JournalEntry(At, 0);
