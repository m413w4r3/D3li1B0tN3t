namespace D3B.Diagnostic.Replay;

/// <summary>Reports a failed diagnostic engine process without exposing its output.</summary>
public sealed class DiagnosticEngineException : Exception
{
    /// <summary>Creates a redacted engine failure.</summary>
    public DiagnosticEngineException(int exitCode, string stderrTail)
        : base($"The diagnostic engine exited with code {exitCode}. {stderrTail}")
    {
        ExitCode = exitCode;
        StderrTail = stderrTail;
    }

    /// <summary>The process exit code.</summary>
    public int ExitCode { get; }

    /// <summary>A redacted description of stderr contents.</summary>
    public string StderrTail { get; }
}
