using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>Something an activity counts, such as an item harvested, or the banks it went to.</summary>
/// <param name="Text">What it says, such as "Ash 142".</param>
/// <param name="ItemId">The item it counts, whose icon the app shows beside it, if any.</param>
public sealed record ActivityCount(GameText Text, int? ItemId = null);
