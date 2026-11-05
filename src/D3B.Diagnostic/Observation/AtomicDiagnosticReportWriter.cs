using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Observation;

/// <summary>Writes bounded reports to private files using a temporary file and atomic rename.</summary>
public sealed class AtomicDiagnosticReportWriter : IDiagnosticReportWriter
{
    /// <summary>Creates a writer for one private report directory.</summary>
    public AtomicDiagnosticReportWriter(DiagnosticReportOptions options, string directory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(timeProvider);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(DiagnosticReport report, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
