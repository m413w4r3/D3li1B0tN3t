namespace D3B.Bot.Characters;

/// <summary>The exchange with a storage the character is in, such as its bank's, from the server's opening it to its end.</summary>
/// <param name="Kamas">The kamas the storage holds; the guild's chest holds none.</param>
/// <param name="IsGuildChest">Whether it is the guild's chest, rather than the character's own in the bank (D147, D153).</param>
/// <remarks>
/// The objects it holds are not kept: the server sends them as it opens and as stacks are put in, but what it sends as
/// stacks are taken out is not measured, so a list kept here would go wrong.
/// </remarks>
public sealed record OpenStorage(long Kamas, bool IsGuildChest = false);
