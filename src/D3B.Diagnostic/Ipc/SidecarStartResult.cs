using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Ipc;

/// <summary>Result of attempting to start a diagnostic sidecar.</summary>
public sealed record SidecarStartResult(IDiagnosticSidecar? Sidecar, DiagnosticErrorCode? ErrorCode)
{
    /// <summary>Whether startup produced a usable sidecar.</summary>
    public bool IsEnabled => Sidecar is not null;
}
