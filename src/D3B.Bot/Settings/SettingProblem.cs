using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Something wrong with an activity's settings, found by <see cref="ActivitySettings.Check"/>.</summary>
/// <param name="Setting">The setting whose value does not hold, or <see langword="null"/> for a rule of the activity's.</param>
/// <param name="Kind">Why, which the app says in its own language.</param>
/// <param name="Message">What the activity's rule says, for <see cref="SettingProblemKind.Rule"/>.</param>
public sealed record SettingProblem(SettingDefinition? Setting, SettingProblemKind Kind, LocalizedText? Message = null);
