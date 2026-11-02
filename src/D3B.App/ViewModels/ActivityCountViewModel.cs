using Avalonia.Media;

namespace D3B.App.ViewModels;

/// <summary>Something an activity counts, as a bot's card shows it: its text, and an item's icon beside it (D131, D135).</summary>
/// <param name="Text">What it says, such as "Ash 142".</param>
/// <param name="IsItem">Whether it counts an item, whose icon, or its neutral square, goes beside it.</param>
/// <param name="Icon">The item's icon, when the pack keeps one.</param>
internal sealed record ActivityCountViewModel(string Text, bool IsItem, IImage? Icon = null);
