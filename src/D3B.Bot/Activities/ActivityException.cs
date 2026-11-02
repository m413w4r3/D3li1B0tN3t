using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>An activity that cannot go on, saying why in the app's language: the error its run ends with (D131).</summary>
/// <remarks>The message, in English, is the logs'; the app says <see cref="Reason"/>.</remarks>
public sealed class ActivityException : Exception
{
    /// <summary>Gets why the activity cannot go on, as the app says it.</summary>
    public GameText? Reason { get; }

    /// <summary>Initializes a new instance of the <see cref="ActivityException"/> class.</summary>
    public ActivityException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityException"/> class.</summary>
    /// <param name="message">Why, in English, for the logs.</param>
    public ActivityException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityException"/> class.</summary>
    /// <param name="message">Why, in English, for the logs.</param>
    /// <param name="innerException">The error that caused it.</param>
    public ActivityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityException"/> class.</summary>
    /// <param name="message">Why, in English, for the logs.</param>
    /// <param name="reason">Why, as the app says it.</param>
    public ActivityException(string message, GameText reason)
        : base(message)
    {
        Reason = reason;
    }
}
