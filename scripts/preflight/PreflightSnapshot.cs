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
        PreflightDirectoryIndex directoryIndex = PreflightDirectoryIndex.Capture(rootHandle);
        return Capture(rootHandle, directoryIndex);
    }

    public static PreflightManifest Capture(SafeFileHandle root)
    {
        PreflightDirectoryIndex directoryIndex = PreflightDirectoryIndex.Capture(root);
        return Capture(root, directoryIndex);
    }

    public static PreflightManifest Capture(SafeFileHandle root, PreflightDirectoryIndex directoryIndex)
    {
        PreflightFileStatus rootStatus = PreflightUnix.StatHandle(root);

        if (rootStatus.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("root_not_directory");
        }

        if (!directoryIndex.Contains(rootStatus))
        {
            throw new PreflightBlockedException("directory_changed_during_index");
        }

        var entries = new List<PreflightManifestEntry>();
        Walk(root, rootStatus, directoryIndex, [], entries);
        entries.Sort(ComparePaths);
        return new PreflightManifest(entries);
    }

    private static void Walk(
        SafeFileHandle directory,
        PreflightFileStatus rootStatus,
        PreflightDirectoryIndex directoryIndex,
        byte[] relativeDirectory,
        List<PreflightManifestEntry> entries)
    {
        PreflightFileStatus before = PreflightUnix.StatHandle(directory);

        if (before.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("directory_changed_during_snapshot");
        }

        if (!directoryIndex.Contains(before))
        {
            throw new PreflightBlockedException("directory_changed_during_index");
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

            PreflightUnix.EnsureSameMount(status, rootStatus, pathBytes);

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

                if (!PreflightUnix.IsSameObject(status, openedStatus) || !PreflightUnix.IsStableStatus(status, openedStatus))
                {
                    throw new PreflightBlockedException("entry_changed_during_snapshot");
                }

                if (!directoryIndex.Contains(openedStatus))
                {
                    throw new PreflightBlockedException("directory_changed_during_index");
                }

                PreflightUnix.EnsureSameMount(openedStatus, rootStatus, pathBytes);

                Walk(childDirectory, rootStatus, directoryIndex, pathBytes, entries);
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

        if (!PreflightUnix.IsStableStatus(before, after))
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

            if (opened.Type != PreflightUnix.RegularFileType || !PreflightUnix.IsStableStatus(before, opened))
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            using var stream = new FileStream(file, FileAccess.Read, 131072, false);
            byte[] hash = SHA256.HashData(stream);
            PreflightFileStatus after = PreflightUnix.StatHandle(file);

            if (!PreflightUnix.IsStableStatus(opened, after))
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

            if (after.Type != PreflightUnix.SymbolicLinkType || !PreflightUnix.IsStableStatus(before, after))
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

}
