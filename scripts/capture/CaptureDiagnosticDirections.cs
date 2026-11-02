namespace D3B.Scripts.Capture;

/// <summary>Summaries for client, server and injected bytes.</summary>
public sealed class CaptureDiagnosticDirections
{
    /// <summary>Gets the client-to-server summary.</summary>
    public required CaptureDiagnosticDirection Client { get; init; }

    /// <summary>Gets the server-to-client summary.</summary>
    public required CaptureDiagnosticDirection Server { get; init; }

    /// <summary>Gets the D3B-injected-to-server summary.</summary>
    public required CaptureDiagnosticDirection Injected { get; init; }
}
