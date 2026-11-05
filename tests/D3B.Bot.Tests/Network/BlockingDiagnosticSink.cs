using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;

namespace D3B.Bot.Tests.Network;

internal sealed class BlockingDiagnosticSink : IDiagnosticSink
{
    public ManualResetEventSlim Started { get; } = new();

    public ManualResetEventSlim Release { get; } = new();

    public long DroppedFrames;

    public DiagnosticSessionSnapshot Snapshot { get; } = new();

    public bool TryEnqueue(DiagnosticFrame frame)
    {
        Started.Set();
        Release.Wait(TimeSpan.FromSeconds(3));
        return true;
    }

    public void ReportDropped(long count)
    {
        Interlocked.Add(ref DroppedFrames, count);
    }

    public void Complete()
    {
        Release.Set();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
