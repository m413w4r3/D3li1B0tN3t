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

    public DiagnosticEngineHello Hello { get; set; } = new()
    {
        ProtocolVersion = DiagnosticProtocol.ProtocolVersion,
        Engine = "d3b-diagnostic",
        EngineVersion = "0.1.0",
        SnsCommit = "abcdef0",
    };

    public Func<CancellationToken, Task<DiagnosticEngineHello>>? Start { get; set; }

    public Func<DiagnosticFrame, CancellationToken, Task<DiagnosticAnalysis>>? AnalyzeWithToken { get; set; }

    public Task<DiagnosticEngineHello> StartAsync(DiagnosticSchema schema, CancellationToken cancellationToken = default)
    {
        return Start?.Invoke(cancellationToken) ?? Task.FromResult(Hello);
    }

    public Task<DiagnosticAnalysis> AnalyzeAsync(DiagnosticFrame frame, CancellationToken cancellationToken = default)
    {
        return AnalyzeWithToken?.Invoke(frame, cancellationToken) ?? Analyze(frame);
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
