namespace D3B.Diagnostic.Reporting;

/// <summary>A report input or output could not be handled within the contract.</summary>
public sealed class DiagnosticReportException : Exception
{
/// <summary>Provides the DiagnosticReportException report operation.</summary>
    public DiagnosticReportException(string message) : base(message)
    {
    }
}
