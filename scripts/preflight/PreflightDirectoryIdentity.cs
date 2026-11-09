using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal readonly record struct PreflightDirectoryIdentity
{
    public uint DeviceMajor { get; }

    public uint DeviceMinor { get; }

    public ulong Inode { get; }

    public PreflightDirectoryIdentity(uint deviceMajor, uint deviceMinor, ulong inode)
    {
        DeviceMajor = deviceMajor;
        DeviceMinor = deviceMinor;
        Inode = inode;
    }

    public static PreflightDirectoryIdentity FromStatus(PreflightFileStatus? status)
    {
        if (status is null || status.Type != PreflightUnix.DirectoryType || !status.IdentityAvailable)
        {
            throw new PreflightBlockedException("directory_identity_unavailable");
        }

        return new PreflightDirectoryIdentity(status.DeviceMajor, status.DeviceMinor, status.Inode);
    }
}
