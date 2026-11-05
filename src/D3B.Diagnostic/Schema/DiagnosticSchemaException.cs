namespace D3B.Diagnostic.Schema;

/// <summary>Thrown when a protocol schema cannot be represented by the diagnostic contract.</summary>
public sealed class DiagnosticSchemaException : Exception
{
    /// <summary>Creates an exception for an invalid diagnostic schema.</summary>
    /// <param name="message">A description that contains schema metadata only.</param>
    public DiagnosticSchemaException(string message)
        : base(message)
    {
    }
}
