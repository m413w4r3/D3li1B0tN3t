using LibCpp2IL.Logging;

namespace D3B.Scripts.Schema;

/// <summary>Drops what LibCpp2IL logs, so that the tool's output is its own and failures surface as exceptions.</summary>
internal sealed class SilentLogWriter : LogWriter
{
    public override void Info(string message)
    {
    }

    public override void Warn(string message)
    {
    }

    public override void Error(string message)
    {
    }

    public override void Verbose(string message)
    {
    }
}
