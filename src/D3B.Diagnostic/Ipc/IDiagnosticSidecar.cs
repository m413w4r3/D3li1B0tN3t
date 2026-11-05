using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Ipc;

/// <summary>One connected, stateful diagnostic engine session.</summary>
public interface IDiagnosticSidecar : IAsyncDisposable
{
    /// <summary>Sends the schema and completes the engine handshake.</summary>
    Task<DiagnosticEngineHello> StartAsync(DiagnosticSchema schema, CancellationToken cancellationToken = default);

    /// <summary>Sends one frame and returns its matching analysis.</summary>
    Task<DiagnosticAnalysis> AnalyzeAsync(DiagnosticFrame frame, CancellationToken cancellationToken = default);

    /// <summary>Sends the end marker and reads the aggregate summary.</summary>
    Task<DiagnosticSummary> CompleteAsync(CancellationToken cancellationToken = default);
}
