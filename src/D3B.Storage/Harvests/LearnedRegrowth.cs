namespace D3B.Storage.Harvests;

/// <summary>How long the resource a skill gathers takes to grow back, as the bots saw it (D145).</summary>
public sealed class LearnedRegrowth
{
    /// <summary>Gets the skill that gathers the resource, as the client's tables name it.</summary>
    public int SkillId { get; init; }

    /// <summary>Gets or sets the longest growing back seen whole, in milliseconds, if one was.</summary>
    public long? LongestMilliseconds { get; set; }

    /// <summary>Gets or sets how many growings back were seen whole.</summary>
    public int Seen { get; set; }

    /// <summary>Gets or sets the least time after a harvest a later visit found it grown back, in milliseconds, if one did.</summary>
    public long? AtMostMilliseconds { get; set; }
}
