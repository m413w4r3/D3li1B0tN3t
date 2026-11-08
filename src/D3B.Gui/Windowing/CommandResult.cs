namespace D3B.Gui.Windowing;

/// <summary>Contains the bounded result of one external process command.</summary>
/// <param name="ExitCode">The process exit code, or null when the process did not start or timed out.</param>
/// <param name="TimedOut">Whether the process exceeded its time limit.</param>
/// <param name="StandardOutput">At most 64 KiB of captured standard output.</param>
/// <param name="StandardError">At most 64 KiB of captured and sanitized standard error.</param>
/// <param name="Failure">A sanitized process startup or wait failure, when one occurred.</param>
internal sealed record CommandResult(
    int? ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError,
    string? Failure)
{
    /// <summary>Whether the command exited successfully without a startup or timeout failure.</summary>
    public bool Succeeded
    {
        get
        {
            return !TimedOut && Failure is null && ExitCode == 0;
        }
    }
}
