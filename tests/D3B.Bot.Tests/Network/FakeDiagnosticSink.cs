using System.Collections.Concurrent;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;

namespace D3B.Bot.Tests.Network;

internal sealed class FakeDiagnosticSink : IDiagnosticSink
{
    public ConcurrentQueue<DiagnosticFrame> Frames { get; } = new();

    public DiagnosticSessionSnapshot Snapshot { get; } = new();

    public bool TryEnqueue(DiagnosticFrame frame)
    {
        Frames.Enqueue(frame);
        return true;
    }

    public void ReportDropped(long count)
    {
    }

    public void Complete()
    {
    }
}
