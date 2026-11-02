namespace D3B.Bot.Harvesting;

/// <summary>How long a resource takes to grow back once harvested, as the host's bots saw it (D145).</summary>
/// <param name="Longest">The longest growing back seen whole, a bot on the map from the harvest's end to the element offering its skill again; none until one was.</param>
/// <param name="Seen">How many growings back were seen whole.</param>
/// <param name="AtMost">The least time after a harvest a later visit found the element grown back: it takes no longer, another player having maybe harvested it meanwhile; none until one was.</param>
public readonly record struct Regrowth(TimeSpan? Longest, int Seen, TimeSpan? AtMost)
{
    /// <summary>Gets how long a tour reckons the resource takes: the longest seen whole, else the least bound, else not known.</summary>
    /// <remarks>
    /// Whether one resource always takes the same is not measured yet: the longest seen whole is the safe reckoning, and a
    /// bound only shortens as visits come.
    /// </remarks>
    public TimeSpan? Reckoned => Longest ?? AtMost;
}
