using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Ipc;

/// <summary>A sidecar failure represented by a stable, payload-free code.</summary>
public sealed class DiagnosticSidecarException : Exception
{
    /// <summary>Creates a redacted sidecar exception.</summary>
    public DiagnosticSidecarException(DiagnosticErrorCode errorCode)
        : base($"The diagnostic sidecar failed with code {errorCode}.")
    {
        ErrorCode = errorCode;
    }

    /// <summary>The stable failure code.</summary>
    public DiagnosticErrorCode ErrorCode { get; }
}
