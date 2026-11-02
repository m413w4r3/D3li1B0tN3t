namespace D3B.Bot.Activities;

/// <summary>How an activity ended (D87).</summary>
public enum ActivityOutcome
{
    /// <summary>A step said the activity is done.</summary>
    Done,

    /// <summary>A step threw, or the character stayed full once unloaded.</summary>
    Failed,

    /// <summary>The activity was stopped, by its run, by its answer to an interruption, or by the host stopping.</summary>
    Stopped,
}
