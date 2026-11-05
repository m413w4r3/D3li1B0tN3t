using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Observation;

/// <summary>A nonblocking destination for diagnostic frames from one game connection.</summary>
public interface IDiagnosticSink
{
    /// <summary>Attempts to queue one frame without waiting.</summary>
    /// <param name="frame">The frame to observe.</param>
    /// <returns><see langword="true"/> when queued; otherwise, the frame was dropped or the sink is closed.</returns>
    bool TryEnqueue(DiagnosticFrame frame);

    /// <summary>Counts frames dropped by an upstream bounded observer queue.</summary>
    /// <param name="count">The number of dropped observations.</param>
    void ReportDropped(long count);

    /// <summary>Gets a redacted snapshot of the session's counters and last result.</summary>
    DiagnosticSessionSnapshot Snapshot { get; }

    /// <summary>Stops accepting frames and begins asynchronous drain and shutdown.</summary>
    void Complete();
}
