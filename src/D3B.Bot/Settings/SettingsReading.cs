namespace D3B.Bot.Settings;

/// <summary>Settings read from what was stored, and the settings whose stored value no longer read, given their default.</summary>
/// <param name="Settings">The settings: the defaults, then every stored value that reads; <see langword="null"/> for an activity with none.</param>
/// <param name="Reset">The settings whose stored value did not read, such as a choice the activity dropped: a profile to look at again.</param>
public sealed record SettingsReading(object? Settings, IReadOnlyList<SettingDefinition> Reset);
