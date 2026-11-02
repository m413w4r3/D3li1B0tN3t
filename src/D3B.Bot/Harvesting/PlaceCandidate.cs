namespace D3B.Bot.Harvesting;

/// <summary>A sub-area the harvesting activity may choose for a character (<see cref="AutomaticPlace"/>, D166).</summary>
/// <param name="SubAreaId">The sub-area.</param>
/// <param name="Score">What it holds of what the character can harvest, each resource as much as it is worth.</param>
/// <param name="Maps">How many of its maps a character may travel to.</param>
public sealed record PlaceCandidate(int SubAreaId, double Score, int Maps);
