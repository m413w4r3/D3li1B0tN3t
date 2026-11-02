using D3B.Bot.Activities;
using D3B.Bot.Settings;

namespace D3B.Storage.Profiles;

/// <summary>A profile started on a bot, or why not.</summary>
/// <param name="Run">The activity's run, once started.</param>
/// <param name="Failure">Why it did not start, else <see langword="null"/>.</param>
/// <param name="Problems">What is wrong with the settings, for <see cref="ProfileStartFailure.SettingsInvalid"/>.</param>
/// <param name="Error">What was thrown, for <see cref="ProfileStartFailure.NotStarted"/>.</param>
public sealed record ProfileStart(ActivityRun? Run, ProfileStartFailure? Failure, IReadOnlyList<SettingProblem> Problems, string? Error);
