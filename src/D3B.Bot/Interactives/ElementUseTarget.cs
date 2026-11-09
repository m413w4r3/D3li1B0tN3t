namespace D3B.Bot.Interactives;

/// <summary>The exact map position, element and skill a supervised use names.</summary>
/// <param name="MapId">The map that must contain the element.</param>
/// <param name="Cell">The cell from which the skill will be used.</param>
/// <param name="ElementId">The element's identifier on the map.</param>
/// <param name="SkillId">The skill's identifier in the game tables.</param>
internal readonly record struct ElementUseTarget(long MapId, int Cell, int ElementId, int SkillId);
