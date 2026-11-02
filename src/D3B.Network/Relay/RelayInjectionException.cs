namespace D3B.Network.Relay;

/// <summary>A frame D3B meant to send the server did not go: the connection closed first, or the client's bytes are no frames to put it between.</summary>
public sealed class RelayInjectionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RelayInjectionException"/> class.</summary>
    public RelayInjectionException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RelayInjectionException"/> class.</summary>
    /// <param name="message">Why the frame did not go.</param>
    public RelayInjectionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RelayInjectionException"/> class.</summary>
    /// <param name="message">Why the frame did not go.</param>
    /// <param name="innerException">What made it fail.</param>
    public RelayInjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
