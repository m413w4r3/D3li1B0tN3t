namespace D3B.Bot.Settings;

/// <summary>A sub-area where resources asked for grow: what a sub-area setting suggests (D127).</summary>
/// <param name="SubArea">The sub-area, with its level.</param>
/// <param name="Maps">How many of its maps a character may travel to.</param>
/// <param name="Resources">The resources asked for that grow there, in the order asked.</param>
public sealed record SubAreaResources(GameReference SubArea, int Maps, IReadOnlyList<int> Resources);
