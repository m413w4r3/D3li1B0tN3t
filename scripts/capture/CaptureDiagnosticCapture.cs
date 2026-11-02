namespace D3B.Scripts.Capture;

/// <summary>Diagnostics and summaries for one capture file.</summary>
public sealed class CaptureDiagnosticCapture
{
    /// <summary>Gets the capture's file identity and header.</summary>
    public required CaptureDiagnosticFile Capture { get; init; }

    /// <summary>Gets the client and build artifact identities.</summary>
    public required CaptureDiagnosticBuild Build { get; init; }

    /// <summary>Gets the record and direction counts.</summary>
    public required CaptureDiagnosticSummary Summary { get; init; }

    /// <summary>Gets diagnostics in deterministic discovery order.</summary>
    public required IReadOnlyList<CaptureDiagnostic> Diagnostics { get; init; }

    /// <summary>Gets this capture's status: ok, warning or error.</summary>
    public required string Status { get; init; }
}
