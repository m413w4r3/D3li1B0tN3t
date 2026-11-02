namespace D3B.Protocol.Translation;

/// <summary>The exception thrown when a build's mapping does not hold, so that D3B cannot read the build's wire (D47).</summary>
public sealed class ProtocolMismatchException : Exception
{
    /// <summary>Gets what does not hold, one line each.</summary>
    public IReadOnlyList<string> Failures { get; } = [];

    /// <summary>Initializes a new instance of the <see cref="ProtocolMismatchException"/> class.</summary>
    public ProtocolMismatchException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProtocolMismatchException"/> class.</summary>
    /// <param name="message">What does not hold.</param>
    public ProtocolMismatchException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProtocolMismatchException"/> class.</summary>
    /// <param name="message">What does not hold.</param>
    /// <param name="innerException">The error met while checking it.</param>
    public ProtocolMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProtocolMismatchException"/> class.</summary>
    /// <param name="clientVersion">The build whose mapping does not hold.</param>
    /// <param name="failures">What does not hold, one line each.</param>
    public ProtocolMismatchException(string clientVersion, IReadOnlyList<string> failures)
        : base(Describe(clientVersion, failures))
    {
        Failures = failures;
    }

    private static string Describe(string clientVersion, IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        return failures.Count is 0
            ? $"The mapping of client {clientVersion} does not hold."
            : $"The mapping of client {clientVersion} does not hold: {failures.Count} failures, the first: {failures[0]}";
    }
}
