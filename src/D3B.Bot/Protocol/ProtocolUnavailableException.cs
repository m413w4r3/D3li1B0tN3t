namespace D3B.Bot.Protocol;

/// <summary>The exception thrown when the installed build's protocol cannot be read: no schema or mapping of it, or one that does not hold.</summary>
/// <remarks>Its message says what is missing or wrong, and how to map the build; the host shows it and does not start.</remarks>
public sealed class ProtocolUnavailableException : Exception
{
    /// <summary>Gets the installed client's build when no schema or mapping of it is found, rather than one found and refused.</summary>
    /// <remarks>The app tells such a build apart, which a newer version of D3B ships (D172).</remarks>
    public string? MissingBuild { get; init; }

    /// <summary>Initializes a new instance of the <see cref="ProtocolUnavailableException"/> class.</summary>
    public ProtocolUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProtocolUnavailableException"/> class.</summary>
    /// <param name="message">What is missing or wrong, and how to map the build.</param>
    public ProtocolUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProtocolUnavailableException"/> class.</summary>
    /// <param name="message">What is missing or wrong, and how to map the build.</param>
    /// <param name="innerException">The error met while reading it.</param>
    public ProtocolUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
