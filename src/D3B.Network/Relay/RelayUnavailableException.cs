namespace D3B.Network.Relay;

/// <summary>The exception thrown when the relay cannot listen where its settings say, such as on a port another program holds.</summary>
/// <remarks>Its message says which address and port, and what to change; the host shows it and does not start.</remarks>
public sealed class RelayUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RelayUnavailableException"/> class.</summary>
    public RelayUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RelayUnavailableException"/> class.</summary>
    /// <param name="message">Where the relay cannot listen, and what to change.</param>
    public RelayUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RelayUnavailableException"/> class.</summary>
    /// <param name="message">Where the relay cannot listen, and what to change.</param>
    /// <param name="innerException">The error Kestrel met.</param>
    public RelayUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
