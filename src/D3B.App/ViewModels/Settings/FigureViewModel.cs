namespace D3B.App.ViewModels.Settings;

/// <summary>A figure of what an activity's settings lead to: a value shown large, and what it counts (D127).</summary>
/// <param name="Value">The value, such as "54".</param>
/// <param name="Label">What it counts, such as "maps to go through".</param>
internal sealed record FigureViewModel(string Value, string Label);
