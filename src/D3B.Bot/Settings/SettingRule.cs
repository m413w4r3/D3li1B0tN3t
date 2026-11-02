using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>A rule an activity's settings must hold together, such as a minimum below a maximum, and what it says when they do not.</summary>
/// <param name="Holds">Tells whether the settings hold it.</param>
/// <param name="Message">What the app says when they do not.</param>
internal sealed record SettingRule(Func<object, bool> Holds, LocalizedText Message);
