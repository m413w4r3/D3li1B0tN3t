using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Settings an activity gives ready to play, which D3B stores once as a profile (D168).</summary>
/// <param name="Key">What names the preset among the activity's, never shown and never changed.</param>
/// <param name="Name">The profile's name, as the app shows it.</param>
/// <param name="Json">The settings, as a profile stores them.</param>
public sealed record SettingsPreset(string Key, LocalizedText Name, string Json);
