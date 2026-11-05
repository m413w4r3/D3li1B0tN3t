using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Observation;

/// <summary>Writes one bounded, redacted report away from the relay frame path.</summary>
public interface IDiagnosticReportWriter
{
    /// <summary>Writes a report asynchronously.</summary>
    ValueTask WriteAsync(DiagnosticReport report, CancellationToken cancellationToken = default);
}
