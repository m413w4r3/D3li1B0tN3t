using D3B.Bot.Characters;

namespace D3B.Storage.Accounts;

/// <summary>A character of an account D3B saw, and the profile it plays (D99).</summary>
public sealed class KnownCharacter
{
    /// <summary>Gets the account's id.</summary>
    public long AccountId { get; init; }

    /// <summary>Gets the character's id.</summary>
    public long Id { get; init; }

    /// <summary>Gets the character's name, as the server last listed it.</summary>
    public string Name { get; init; } = "";

    /// <summary>Gets the character's level, as the server last listed it, if it did.</summary>
    public int? Level { get; init; }

    /// <summary>Gets when the client last chose the character, or <see langword="null"/> if it only listed it.</summary>
    public DateTimeOffset? LastPlayed { get; init; }

    /// <summary>Gets the id of the profile the character plays, preselected on its bot's card, if it has one.</summary>
    public int? ProfileId { get; init; }

    /// <summary>Gets whether the character's profile starts once its client first connects while the host runs.</summary>
    public bool StartsOnConnection { get; init; }

    /// <summary>Gets who the character is, as its bot names it (D65).</summary>
    public CharacterKey Key => new(AccountId, Id);
}
