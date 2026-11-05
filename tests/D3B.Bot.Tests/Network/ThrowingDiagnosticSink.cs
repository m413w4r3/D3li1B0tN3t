using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;

namespace D3B.Bot.Tests.Network;

internal sealed class ThrowingDiagnosticSink : IDiagnosticSink
{
    public DiagnosticSessionSnapshot Snapshot { get; } = new();

    public bool TryEnqueue(DiagnosticFrame frame)
    {
        throw new InvalidOperationException();
    }

    public void ReportDropped(long count)
    {
    }

    public void Complete()
    {
    }
}
