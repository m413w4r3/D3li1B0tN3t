namespace D3B.Storage.Accounts;

/// <summary>An Ankama account D3B saw, as its client's identification named it: D3B never holds its password (D23, D99).</summary>
public sealed class KnownAccount
{
    /// <summary>Gets the account's id.</summary>
    public long Id { get; init; }

    /// <summary>Gets the account's nickname.</summary>
    public string Nickname { get; init; } = "";

    /// <summary>Gets the tag that tells accounts of one nickname apart.</summary>
    public string Tag { get; init; } = "";

    /// <summary>Gets when the account's subscription ends, as its last identification said, if it said.</summary>
    public DateTimeOffset? SubscriptionEnd { get; init; }

    /// <summary>Gets when D3B first saw the account.</summary>
    public DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets when D3B last saw the account's client list its characters, or choose one.</summary>
    public DateTimeOffset LastSeen { get; init; }
}
