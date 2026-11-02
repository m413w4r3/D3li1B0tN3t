using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>How the fight ended for the character, and what it gained.</summary>
/// <param name="Outcome">The character's outcome: won or lost.</param>
/// <param name="Experience">The experience the fight gave the character.</param>
/// <param name="Kamas">The kamas it gained.</param>
/// <param name="Items">The objects it gained, by item id: how many of each.</param>
public sealed record FightEnd(FightOutcome Outcome, long Experience, long Kamas, ImmutableSortedDictionary<int, int> Items);
