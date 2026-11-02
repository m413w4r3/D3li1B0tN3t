namespace D3B.Bot.Network;

/// <summary>The Ankama account a client logged in with, as the connection phase's identification names it.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Nickname">The account's nickname.</param>
/// <param name="Tag">The tag that tells accounts of one nickname apart.</param>
/// <param name="SubscriptionEnd">When the account's subscription ends, or <see langword="null"/> when the identification gave no date it could read (D72).</param>
public sealed record GameAccount(long Id, string Nickname, string Tag, DateTimeOffset? SubscriptionEnd = null);
