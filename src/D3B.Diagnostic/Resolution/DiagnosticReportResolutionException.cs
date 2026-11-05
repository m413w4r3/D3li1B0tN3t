namespace D3B.Diagnostic.Resolution;

/// <summary>A diagnostic report cannot safely be resolved against the supplied build.</summary>
public sealed class DiagnosticReportResolutionException : Exception
{
    /// <summary>Creates a redacted report resolution failure.</summary>
    public DiagnosticReportResolutionException(string message) : base(message)
    {
    }
}
