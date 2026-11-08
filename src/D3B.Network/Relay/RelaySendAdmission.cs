namespace D3B.Network.Relay;

/// <summary>A session-issued, frame-bound permission to send one encoded request.</summary>
internal sealed class RelaySendAdmission
{
    private readonly byte[] _frame;
    private readonly Func<bool> _stillAdmitted;

    /// <summary>Gets the admission version that must still be active when the relay writes.</summary>
    public long Version { get; }

    internal RelaySendAdmission(long version, ReadOnlyMemory<byte> frame, Func<bool> stillAdmitted)
    {
        ArgumentNullException.ThrowIfNull(stillAdmitted);

        Version = version;
        _frame = frame.ToArray();
        _stillAdmitted = stillAdmitted;
    }

    internal bool IsBoundTo(ReadOnlySpan<byte> frame)
    {
        return frame.SequenceEqual(_frame);
    }

    internal bool IsStillAdmitted()
    {
        try
        {
            return _stillAdmitted();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
