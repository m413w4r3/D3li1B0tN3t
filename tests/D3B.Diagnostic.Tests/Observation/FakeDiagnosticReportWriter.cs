using System.Collections.Concurrent;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Tests.Observation;

internal sealed class FakeDiagnosticReportWriter : IDiagnosticReportWriter
{
    private readonly ConcurrentQueue<DiagnosticReport> _reports = new();

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<DiagnosticReport> Reports => [.. _reports];

    public Exception? Failure { get; set; }

    public bool Block { get; set; }

    public ValueTask WriteAsync(DiagnosticReport report, CancellationToken cancellationToken = default)
    {
        _reports.Enqueue(report);
        Started.TrySetResult();
        return WriteCoreAsync(cancellationToken);
    }

    private async ValueTask WriteCoreAsync(CancellationToken cancellationToken)
    {
        if (Block)
        {
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
