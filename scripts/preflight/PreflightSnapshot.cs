using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightSnapshot
{
    public static PreflightManifest Capture(string root)
    {
        PreflightUnix.EnsureLinux();

        using SafeFileHandle rootHandle = PreflightUnix.OpenDirectoryPath(root);
        return Capture(rootHandle);
    }

    public static PreflightManifest Capture(SafeFileHandle root)
    {
        PreflightFileStatus rootStatus = PreflightUnix.StatHandle(root);

        if (rootStatus.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("root_not_directory");
        }

        var entries = new List<PreflightManifestEntry>();
        Walk(root, rootStatus, [], entries);
        entries.Sort(ComparePaths);
        return new PreflightManifest(entries);
    }

    private static void Walk(
        SafeFileHandle directory,
        PreflightFileStatus rootStatus,
        byte[] relativeDirectory,
        List<PreflightManifestEntry> entries)
    {
        PreflightFileStatus before = PreflightUnix.StatHandle(directory);

        if (before.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("directory_changed_during_snapshot");
        }

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
            byte[] pathBytes = PreflightPath.JoinRelativePath(relativeDirectory, childName);
            PreflightFileStatus status;

            try
            {
                status = PreflightUnix.StatAt(directory, childName);
            }
            catch (IOException)
            {
                throw new PreflightBlockedException("entry_unreadable");
            }

            PreflightUnix.EnsureSameMount(status, rootStatus);

            if (status.Type == PreflightUnix.DirectoryType)
            {
                SafeFileHandle openedDirectory;

                try
                {
                    openedDirectory = PreflightUnix.OpenDirectoryAt(directory, childName);
                }
                catch (IOException)
                {
                    throw new PreflightBlockedException("entry_unreadable");
                }

                using SafeFileHandle childDirectory = openedDirectory;
                PreflightFileStatus openedStatus = PreflightUnix.StatHandle(childDirectory);

                if (!PreflightUnix.IsSameObject(status, openedStatus) || !IsStable(status, openedStatus))
                {
                    throw new PreflightBlockedException("entry_changed_during_snapshot");
                }

                PreflightUnix.EnsureSameMount(openedStatus, rootStatus);

                Walk(childDirectory, rootStatus, pathBytes, entries);
                continue;
            }

            if (status.Type == PreflightUnix.RegularFileType)
            {
                entries.Add(ReadRegularFile(directory, childName, pathBytes, status));
                continue;
            }

            if (status.Type == PreflightUnix.SymbolicLinkType)
            {
                entries.Add(ReadSymbolicLink(directory, childName, pathBytes, status));
                continue;
            }

            throw new PreflightBlockedException("special_file_unsupported");
        }

        PreflightFileStatus after = PreflightUnix.StatHandle(directory);

        if (!IsStable(before, after))
        {
            throw new PreflightBlockedException("directory_changed_during_snapshot");
        }
    }

    private static PreflightManifestEntry ReadRegularFile(
        SafeFileHandle parent,
        byte[] name,
        byte[] pathBytes,
        PreflightFileStatus before)
    {
        try
        {
            using SafeFileHandle file = PreflightUnix.OpenFileAt(parent, name);
            PreflightFileStatus opened = PreflightUnix.StatHandle(file);

            if (opened.Type != PreflightUnix.RegularFileType || !IsStable(before, opened))
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            using var stream = new FileStream(file, FileAccess.Read, 131072, false);
            byte[] hash = SHA256.HashData(stream);
            PreflightFileStatus after = PreflightUnix.StatHandle(file);

            if (!IsStable(opened, after))
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            return new PreflightManifestEntry(
                Convert.ToBase64String(pathBytes),
                "file",
                opened.Size,
                opened.Mode,
                Convert.ToHexStringLower(hash),
                null);
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PreflightBlockedException("entry_unreadable");
        }
    }

    private static PreflightManifestEntry ReadSymbolicLink(
        SafeFileHandle parent,
        byte[] name,
        byte[] pathBytes,
        PreflightFileStatus before)
    {
        try
        {
            byte[] target = PreflightUnix.ReadLinkAt(parent, name);
            PreflightFileStatus after = PreflightUnix.StatAt(parent, name);

            if (after.Type != PreflightUnix.SymbolicLinkType || !IsStable(before, after))
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            return new PreflightManifestEntry(
                Convert.ToBase64String(pathBytes),
                "symlink",
                target.Length,
                before.Mode,
                null,
                Convert.ToBase64String(target));
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PreflightBlockedException("entry_unreadable");
        }
    }

    private static int ComparePaths(PreflightManifestEntry left, PreflightManifestEntry right)
    {
        byte[] leftBytes = PreflightPath.DecodePath(left.PathBase64!);
        byte[] rightBytes = PreflightPath.DecodePath(right.PathBase64!);
        return PreflightUnix.CompareBytes(leftBytes, rightBytes);
    }

    private static bool IsStable(PreflightFileStatus before, PreflightFileStatus after)
    {
        return before.Type == after.Type
            && before.Size == after.Size
            && before.Mode == after.Mode
            && before.Inode == after.Inode
            && before.DeviceMajor == after.DeviceMajor
            && before.DeviceMinor == after.DeviceMinor
            && before.MountId == after.MountId
            && before.ChangeSeconds == after.ChangeSeconds
            && before.ChangeNanoseconds == after.ChangeNanoseconds
            && before.ModifiedSeconds == after.ModifiedSeconds
            && before.ModifiedNanoseconds == after.ModifiedNanoseconds;
    }
}
