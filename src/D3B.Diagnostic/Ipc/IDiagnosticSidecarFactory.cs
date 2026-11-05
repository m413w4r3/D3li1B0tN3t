namespace D3B.Diagnostic.Ipc;

/// <summary>Creates diagnostic sidecars for injection and test isolation.</summary>
public interface IDiagnosticSidecarFactory
{
    /// <summary>Starts an engine and connects its Unix socket.</summary>
    Task<SidecarStartResult> StartAsync(
        SidecarOptions options,
        CancellationToken cancellationToken = default);
}
