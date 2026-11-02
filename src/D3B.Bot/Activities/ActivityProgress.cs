using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>How far an activity is in what it goes through, such as the maps of a tour.</summary>
/// <param name="Text">What it says of it, such as "Tour 2, map 12 of 54".</param>
/// <param name="Done">How much is done.</param>
/// <param name="Total">How much there is.</param>
public sealed record ActivityProgress(GameText Text, int Done, int Total);
