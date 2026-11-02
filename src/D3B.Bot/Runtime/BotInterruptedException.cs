namespace D3B.Bot.Runtime;

/// <summary>Thrown when a bot's behavior is interrupted: the player acted, the game connection closed, a fight began, or the host is stopping.</summary>
public sealed class BotInterruptedException : OperationCanceledException
{
    /// <summary>Gets why the behavior stopped.</summary>
    public Interruption Reason { get; }

    /// <summary>Initializes a new instance of the <see cref="BotInterruptedException"/> class.</summary>
    public BotInterruptedException()
        : this(Interruption.Stopped)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BotInterruptedException"/> class.</summary>
    /// <param name="message">What happened.</param>
    public BotInterruptedException(string message)
        : base(message)
    {
        Reason = Interruption.Stopped;
    }

    /// <summary>Initializes a new instance of the <see cref="BotInterruptedException"/> class.</summary>
    /// <param name="message">What happened.</param>
    /// <param name="innerException">The cancellation it comes from.</param>
    public BotInterruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = Interruption.Stopped;
    }

    /// <summary>Initializes a new instance of the <see cref="BotInterruptedException"/> class.</summary>
    /// <param name="reason">Why the behavior stopped.</param>
    /// <param name="innerException">The cancellation it comes from, if any.</param>
    public BotInterruptedException(Interruption reason, Exception? innerException = null)
        : base(MessageOf(reason), innerException)
    {
        Reason = reason;
    }

    private static string MessageOf(Interruption reason)
    {
        return reason switch
        {
            Interruption.Player => "The player acted on the character: the bot pauses until it is resumed.",
            Interruption.Disconnected => "The game connection closed.",
            Interruption.Fight => "A fight began.",
            _ => "The host is stopping.",
        };
    }
}
