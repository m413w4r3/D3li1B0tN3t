namespace D3B.Bot.Characters;

/// <summary>Who a bot plays: a character of an account, one bot each (D65).</summary>
/// <param name="AccountId">The account's id, as the connection phase's identification names it.</param>
/// <param name="CharacterId">The character's id, as the character choice names it.</param>
public readonly record struct CharacterKey(long AccountId, long CharacterId);
