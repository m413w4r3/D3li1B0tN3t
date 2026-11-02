namespace D3B.Network.Relay;

/// <summary>The exception thrown when the relay cannot tell, or reach, the server a client meant to connect to.</summary>
internal sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException()
    {
    }

    public UpstreamUnavailableException(string message)
        : base(message)
    {
    }

    public UpstreamUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
