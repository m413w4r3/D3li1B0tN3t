namespace D3B.Bot.Harvesting;

/// <summary>A skill the harvesting activity uses: the resource it gathers, the profession and level it needs, and the resource's priority.</summary>
/// <param name="SkillId">The skill.</param>
/// <param name="ItemId">The resource it gathers.</param>
/// <param name="JobId">The profession it belongs to.</param>
/// <param name="Level">The profession's level it needs.</param>
/// <param name="Priority">The resource's priority, the lowest first: its place in a list, 0 first; by profession, 0 for every resource, or its level's opposite, the highest first (D165).</param>
public readonly record struct HarvestTarget(int SkillId, int ItemId, int JobId, int Level, int Priority);
