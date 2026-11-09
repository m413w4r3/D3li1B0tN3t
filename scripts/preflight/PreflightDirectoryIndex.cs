using Microsoft.Win32.SafeHandles;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal sealed class PreflightDirectoryIndex
{
    private readonly HashSet<PreflightDirectoryIdentity> _identities;

    public PreflightFileStatus RootStatus { get; }

    public PreflightDirectoryIdentity RootIdentity { get; }

    internal PreflightDirectoryIndex(PreflightFileStatus rootStatus, IEnumerable<PreflightFileStatus> directories)
    {
        RootStatus = rootStatus;
        RootIdentity = PreflightDirectoryIdentity.FromStatus(rootStatus);
        _identities = new HashSet<PreflightDirectoryIdentity> { RootIdentity };

        foreach (PreflightFileStatus status in directories)
        {
            _identities.Add(PreflightDirectoryIdentity.FromStatus(status));
        }
    }

    private PreflightDirectoryIndex(PreflightFileStatus rootStatus, HashSet<PreflightDirectoryIdentity> identities)
    {
        RootStatus = rootStatus;
        RootIdentity = PreflightDirectoryIdentity.FromStatus(rootStatus);
        _identities = identities;
        _identities.Add(RootIdentity);
    }

    public static PreflightDirectoryIndex Capture(SafeFileHandle root)
    {
        return Capture(root, false);
    }

    public static PreflightDirectoryIndex Capture(SafeFileHandle root, bool includeMountBoundaries)
    {
        PreflightUnix.EnsureLinux();
        PreflightFileStatus rootStatus = ReadDirectoryStatus(root);
        PreflightDirectoryIdentity rootIdentity = PreflightDirectoryIdentity.FromStatus(rootStatus);
        var identities = new HashSet<PreflightDirectoryIdentity> { rootIdentity };
        CaptureDirectory(root, rootStatus, rootStatus, [], identities, includeMountBoundaries);
        return new PreflightDirectoryIndex(rootStatus, identities);
    }

    public bool Contains(PreflightFileStatus status)
    {
        return _identities.Contains(PreflightDirectoryIdentity.FromStatus(status));
    }

    private static void CaptureDirectory(
        SafeFileHandle directory,
        PreflightFileStatus expectedStatus,
        PreflightFileStatus rootStatus,
        byte[] relativeDirectory,
        HashSet<PreflightDirectoryIdentity> identities,
        bool includeMountBoundaries)
    {
        PreflightFileStatus before = ReadDirectoryStatus(directory);

        if (!PreflightUnix.IsSameEntry(before, expectedStatus) || !PreflightUnix.IsStableStatus(before, expectedStatus))
        {
            throw new PreflightBlockedException("directory_changed_during_index");
        }

        identities.Add(PreflightDirectoryIdentity.FromStatus(before));
        List<byte[]> children;

        try
        {
            children = PreflightUnix.EnumerateNames(directory);
        }
        catch (IOException)
        {
            throw new PreflightBlockedException("directory_unreadable");
        }

        foreach (byte[] childName in children)
        {
            byte[] relativePath = PreflightPath.JoinRelativePath(relativeDirectory, childName);
            PreflightFileStatus status;

            try
            {
                status = PreflightUnix.StatAt(directory, childName);
            }
            catch (IOException)
            {
                throw new PreflightBlockedException("entry_unreadable");
            }

            if (status.Type != PreflightUnix.DirectoryType)
            {
                if (!includeMountBoundaries)
                {
                    PreflightUnix.EnsureSameMount(status, rootStatus, relativePath);
                }

                continue;
            }

            PreflightDirectoryIdentity identity = PreflightDirectoryIdentity.FromStatus(status);

            if (includeMountBoundaries && identities.Contains(identity))
            {
                // The inode may reappear through a bind mount; indexing it once also prevents mount cycles.
                continue;
            }

            if (!includeMountBoundaries
                && (status.MountId != rootStatus.MountId
                || status.DeviceMajor != rootStatus.DeviceMajor
                || status.DeviceMinor != rootStatus.DeviceMinor))
            {
                PreflightUnix.EnsureSameMount(status, rootStatus, relativePath);
            }

            SafeFileHandle childDirectory;

            try
            {
                childDirectory = PreflightUnix.OpenDirectoryAt(directory, childName);
            }
            catch (PreflightBlockedException)
            {
                throw;
            }
            catch (IOException)
            {
                throw new PreflightBlockedException("directory_changed_during_index");
            }

            using (childDirectory)
            {
                PreflightFileStatus openedStatus = ReadDirectoryStatus(childDirectory);

                if (!PreflightUnix.IsSameEntry(status, openedStatus) || !PreflightUnix.IsStableStatus(status, openedStatus))
                {
                    throw new PreflightBlockedException("directory_changed_during_index");
                }

                if (!includeMountBoundaries
                    && (openedStatus.MountId != rootStatus.MountId
                    || openedStatus.DeviceMajor != rootStatus.DeviceMajor
                    || openedStatus.DeviceMinor != rootStatus.DeviceMinor))
                {
                    PreflightUnix.EnsureSameMount(openedStatus, rootStatus, relativePath);
                }

                identities.Add(PreflightDirectoryIdentity.FromStatus(openedStatus));
                CaptureDirectory(childDirectory, openedStatus, rootStatus, relativePath, identities, includeMountBoundaries);
            }
        }

        PreflightFileStatus after = ReadDirectoryStatus(directory);

        if (!PreflightUnix.IsSameEntry(before, after) || !PreflightUnix.IsStableStatus(before, after))
        {
            throw new PreflightBlockedException("directory_changed_during_index");
        }
    }

    private static PreflightFileStatus ReadDirectoryStatus(SafeFileHandle directory)
    {
        PreflightFileStatus status;

        try
        {
            status = PreflightUnix.StatHandle(directory);
        }
        catch (IOException)
        {
            throw new PreflightBlockedException("directory_identity_unavailable");
        }

        _ = PreflightDirectoryIdentity.FromStatus(status);
        return status;
    }
}
