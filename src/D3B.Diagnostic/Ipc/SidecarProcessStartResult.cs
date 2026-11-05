using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Ipc;

/// <summary>Result of starting the diagnostic engine process.</summary>
public sealed record SidecarProcessStartResult(SidecarProcess? Process, DiagnosticErrorCode? ErrorCode)
{
    /// <summary>Whether a process was started.</summary>
    public bool IsEnabled => Process is not null;
}
