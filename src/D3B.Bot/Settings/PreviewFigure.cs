using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>A figure of a <see cref="SettingsPreview"/>: a value shown large, and what it counts below it.</summary>
/// <param name="Value">The value, such as "54" or "5 maps".</param>
/// <param name="Label">What it counts, such as "maps to go through".</param>
public sealed record PreviewFigure(GameText Value, GameText Label);
