namespace D3B.Bot.Characters;

/// <summary>The use of an element the character is in, from the server's start to its end, such as a harvest.</summary>
/// <param name="ElementId">The element's id on the map.</param>
/// <param name="SkillId">The skill used, by id in the client's tables.</param>
public readonly record struct CurrentUse(int ElementId, int SkillId);
