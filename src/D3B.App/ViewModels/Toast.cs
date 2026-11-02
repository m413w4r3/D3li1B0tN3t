using D3B.App.Controls;

namespace D3B.App.ViewModels;

/// <summary>A toast: a few words, their tone, and what its button does, if it has one.</summary>
/// <param name="Text">What it says.</param>
/// <param name="Tone">Its tone: success for what was done, warning or danger for what a bot met.</param>
/// <param name="ActionText">Its button's text, if it has one.</param>
/// <param name="Action">What its button does.</param>
internal sealed record Toast(string Text, Tone Tone = Tone.Neutral, string? ActionText = null, Action? Action = null);
