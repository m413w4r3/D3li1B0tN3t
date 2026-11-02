using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>A value a choice offers: one of its enumeration's values, named in the app's language.</summary>
/// <param name="Value">The enumeration's value, boxed.</param>
/// <param name="Text">Its name, as the app shows it.</param>
public sealed record SettingOption(object Value, LocalizedText Text);
