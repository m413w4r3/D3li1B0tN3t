namespace D3B.Diagnostic.Reporting;

/// <summary>A report input or output could not be handled within the contract.</summary>
public sealed class DiagnosticReportException : Exception
{
    internal DiagnosticReportException(string message) : base(message)
    {
    }
}
