using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>An interactive element of the character's map, as the server describes it: which interactive it is, the skills it offers now, and its state.</summary>
/// <remarks>
/// The client's data places the element on a cell, but neither says which interactive it is nor which skills it offers:
/// the server does, element by element, in the map's information, then each change (D29).
/// </remarks>
/// <param name="ElementId">The element's id on its map, as the map's data holds it (D27).</param>
/// <param name="InteractiveId">The interactive's id in the client's tables, or -1, which the tables lack, or when the server gave only the element's state.</param>
/// <param name="EnabledSkills">The skills offered now.</param>
/// <param name="DisabledSkills">The skills not offered now: a resource's, from the start of its harvest until it grows back.</param>
public sealed record MapElement(int ElementId, int InteractiveId, ImmutableArray<ElementSkill> EnabledSkills, ImmutableArray<ElementSkill> DisabledSkills)
{
    /// <summary>Gets the element's cell, as the server states it for an element with a state, such as a resource.</summary>
    public int? Cell { get; init; }

    /// <summary>Gets the element's state, for an element with one: 0 in the map's information, 2 while a resource is being harvested, 1 once it is harvested, in the captures.</summary>
    public int? State { get; init; }

    /// <summary>Finds a skill the element offers now.</summary>
    /// <param name="skillId">The skill's id in the client's tables.</param>
    /// <param name="skill">The skill, with the id to ask for it with on this element.</param>
    /// <returns>Whether the element offers it now.</returns>
    public bool TryGetEnabled(int skillId, out ElementSkill skill)
    {
        foreach (var enabled in EnabledSkills)
        {
            if (enabled.SkillId == skillId)
            {
                skill = enabled;

                return true;
            }
        }

        skill = default;

        return false;
    }
}
