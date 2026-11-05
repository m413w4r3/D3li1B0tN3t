using System.Collections.Concurrent;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;

namespace D3B.Bot.Tests.Network;

internal sealed class FakeDiagnosticSink : IDiagnosticSink
{
    public ConcurrentQueue<DiagnosticFrame> Frames { get; } = new();

    public DiagnosticSessionSnapshot Snapshot { get; } = new();

    public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource DisposeRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool BlockDisposeAsync { get; set; }

    public int DisposeCount;

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

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref DisposeCount);
        DisposeStarted.TrySetResult();
        if (BlockDisposeAsync)
        {
            await DisposeRelease.Task.ConfigureAwait(false);
        }
    }
}
