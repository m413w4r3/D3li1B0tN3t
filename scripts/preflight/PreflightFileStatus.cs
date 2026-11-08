namespace D3B.Scripts.Preflight;

internal sealed class PreflightFileStatus
{
    public int Type { get; }

    public long Size { get; }

    public int Mode { get; }

    public ulong Inode { get; }

    public uint DeviceMajor { get; }

    public uint DeviceMinor { get; }

    public ulong MountId { get; }

    public long ChangeSeconds { get; }

    public uint ChangeNanoseconds { get; }

    public long ModifiedSeconds { get; }

    public uint ModifiedNanoseconds { get; }

    public PreflightFileStatus(
        int type,
        long size,
        int mode,
        ulong inode,
        uint deviceMajor,
        uint deviceMinor,
        ulong mountId,
        long changeSeconds,
        uint changeNanoseconds,
        long modifiedSeconds,
        uint modifiedNanoseconds)
    {
        Type = type;
        Size = size;
        Mode = mode;
        Inode = inode;
        DeviceMajor = deviceMajor;
        DeviceMinor = deviceMinor;
        MountId = mountId;
        ChangeSeconds = changeSeconds;
        ChangeNanoseconds = changeNanoseconds;
        ModifiedSeconds = modifiedSeconds;
        ModifiedNanoseconds = modifiedNanoseconds;
    }
}
