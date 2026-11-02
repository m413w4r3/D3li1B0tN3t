namespace D3B.Engine.Criteria;

/// <summary>What a criterion answers, on three values: what a bot does not know is neither met nor failed.</summary>
public enum CriterionAnswer
{
    /// <summary>The criterion asks what the bot does not know, such as a quest's progress, or could not be read.</summary>
    Unknown,

    /// <summary>The criterion fails.</summary>
    False,

    /// <summary>The criterion is met.</summary>
    True,
}
