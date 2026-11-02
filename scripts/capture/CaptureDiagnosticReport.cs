namespace D3B.Scripts.Capture;

/// <summary>A deterministic, machine-readable report for one or more captures.</summary>
public sealed class CaptureDiagnosticReport
{
    /// <summary>Gets the version of this JSON contract.</summary>
    public int SchemaVersion { get; init; }

    /// <summary>Gets the captures, sorted by basename.</summary>
    public required IReadOnlyList<CaptureDiagnosticCapture> Captures { get; init; }

    /// <summary>Gets the worst status across the captures.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the process exit code represented by the report.</summary>
    public int ExitCode { get; init; }
}
