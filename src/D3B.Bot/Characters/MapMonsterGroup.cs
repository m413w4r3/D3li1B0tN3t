using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>A group of monsters on the character's map, as the server places it.</summary>
/// <param name="ActorId">Its actor id on the map, negative, which the attack names.</param>
/// <param name="Cell">The cell it stands on, when the server gave one: the client attacks once its walk reaches it.</param>
/// <param name="Monsters">Its monsters, the main one first, then the others in the server's order.</param>
public sealed record MapMonsterGroup(long ActorId, int? Cell, ImmutableArray<MonsterInGroup> Monsters);
