using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Observation;

internal sealed class FakeDiagnosticSidecar : IDiagnosticSidecar
{
    public Func<DiagnosticFrame, Task<DiagnosticAnalysis>> Analyze { get; set; } = _ =>
        Task.FromResult(new DiagnosticAnalysis());

    public Func<Task<DiagnosticSummary>> Complete { get; set; } = () =>
        Task.FromResult(new DiagnosticSummary { Coverage = "complete" });

    public Task<DiagnosticEngineHello> StartAsync(DiagnosticSchema schema, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DiagnosticEngineHello());
    }

    public Task<DiagnosticAnalysis> AnalyzeAsync(DiagnosticFrame frame, CancellationToken cancellationToken = default)
    {
        return Analyze(frame);
    }

    public Task<DiagnosticSummary> CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Complete();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
