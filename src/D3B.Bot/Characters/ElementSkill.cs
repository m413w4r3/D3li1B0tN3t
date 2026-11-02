namespace D3B.Bot.Characters;

/// <summary>A skill an interactive element of the map offers, as the server names it for that element.</summary>
/// <param name="SkillId">The skill's id in the client's tables (D29).</param>
/// <param name="InstanceId">The id the client asks for the skill on this element with.</param>
public readonly record struct ElementSkill(int SkillId, int InstanceId);
