namespace D3B.Diagnostic.Contract;

/// <summary>An invalid or unsupported diagnostic protocol message.</summary>
public sealed class DiagnosticProtocolException : Exception
{
    /// <summary>Creates a protocol exception with a payload-free message.</summary>
    internal DiagnosticProtocolException(string message)
        : base(message)
    {
    }
}
