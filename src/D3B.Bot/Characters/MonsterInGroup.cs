namespace D3B.Bot.Characters;

/// <summary>A monster of a group on the character's map, as the server describes it: the fight against the group has these monsters, measured.</summary>
/// <param name="MonsterId">Which monster it is, by id in the client's tables, as a fighter's (D184).</param>
/// <param name="Grade">Its grade, from 1.</param>
/// <param name="Level">Its level, the grade's in the pack.</param>
public sealed record MonsterInGroup(int MonsterId, int Grade, int Level);
