using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Aggregates analyses on the session pump into a bounded report.</summary>
public sealed class DiagnosticReportAggregator
{
/// <summary>Provides the DiagnosticReportAggregator report operation.</summary>
    public DiagnosticReportAggregator(DiagnosticSchema schema, DiagnosticReportOptions options, DiagnosticReportBuildContext context)
    {
        throw new NotImplementedException();
    }

/// <summary>Provides the Observe report operation.</summary>
    public void Observe(DiagnosticAnalysis analysis)
    {
        throw new NotImplementedException();
    }

/// <summary>Provides the Build report operation.</summary>
    public DiagnosticReport Build(DiagnosticReportBuildOutcome outcome)
    {
        throw new NotImplementedException();
    }
}
