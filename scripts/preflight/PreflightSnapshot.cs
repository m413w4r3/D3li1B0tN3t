using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightSnapshot
{
    public static PreflightManifest Capture(string root)
    {
        PreflightUnix.EnsureLinux();
        string fullRoot = PreflightPath.FullPath(root);

        if (!Directory.Exists(fullRoot))
        {
            throw new IOException("The snapshot root is unavailable.");
        }

        var rootStatus = PreflightUnix.Stat(fullRoot);

        if (rootStatus.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("root_not_directory");
        }

        var entries = new List<PreflightManifestEntry>();
        var normalizedPaths = new HashSet<string>(StringComparer.Ordinal);
        Walk(fullRoot, fullRoot, entries, normalizedPaths);
        entries.Sort(ComparePaths);
        return new PreflightManifest(entries);
    }

    private static void Walk(
        string root,
        string directory,
        List<PreflightManifestEntry> entries,
        HashSet<string> normalizedPaths)
    {
        var before = GetEntryStatus(directory);

        if (before.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("directory_changed_during_snapshot");
        }

        IEnumerable<string> children;

        try
        {
            children = Directory.EnumerateFileSystemEntries(directory).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PreflightBlockedException("directory_unreadable");
        }

        foreach (string child in children)
        {
            string fullChild = PreflightPath.FullPath(child);

            if (!PreflightPath.IsWithin(root, fullChild))
            {
                throw new PreflightBlockedException("entry_escapes_root");
            }

            string relativePath = Path.GetRelativePath(root, fullChild).Replace(Path.DirectorySeparatorChar, '/');
            byte[] pathBytes = PreflightPath.EncodePath(relativePath);
            string normalizedPath = PreflightPath.DecodeUtf8(pathBytes);

            if (!normalizedPaths.Add(normalizedPath))
            {
                throw new PreflightBlockedException("duplicate_normalized_path");
            }

            (int type, long size, int mode, ulong inode, long changeSeconds, uint changeNanoseconds) = GetEntryStatus(fullChild);

            if (type == PreflightUnix.DirectoryType)
            {
                Walk(root, fullChild, entries, normalizedPaths);
                continue;
            }

            if (type == PreflightUnix.RegularFileType)
            {
                entries.Add(ReadRegularFile(fullChild, pathBytes, size, mode, inode, changeSeconds, changeNanoseconds));
                continue;
            }

            if (type == PreflightUnix.SymbolicLinkType)
            {
                entries.Add(ReadSymbolicLink(fullChild, pathBytes, size, mode, inode, changeSeconds, changeNanoseconds));
                continue;
            }

            throw new PreflightBlockedException("special_file_unsupported");
        }

        var after = GetEntryStatus(directory);

        if (!IsStable(before, after))
        {
            throw new PreflightBlockedException("directory_changed_during_snapshot");
        }
    }

    private static PreflightManifestEntry ReadRegularFile(
        string path,
        byte[] pathBytes,
        long size,
        int mode,
        ulong inode,
        long changeSeconds,
        uint changeNanoseconds)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.SequentialScan);
            byte[] hash = SHA256.HashData(stream);
            var after = PreflightUnix.Stat(path);

            if (after.Type != PreflightUnix.RegularFileType
                || after.Size != size
                || after.Mode != mode
                || after.Inode != inode
                || after.ChangeSeconds != changeSeconds
                || after.ChangeNanoseconds != changeNanoseconds)
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            return new PreflightManifestEntry(
                Convert.ToBase64String(pathBytes),
                "file",
                size,
                mode,
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
        string path,
        byte[] pathBytes,
        long size,
        int mode,
        ulong inode,
        long changeSeconds,
        uint changeNanoseconds)
    {
        try
        {
            byte[] target = PreflightUnix.ReadLink(path);
            var after = PreflightUnix.Stat(path);

            if (after.Type != PreflightUnix.SymbolicLinkType
                || after.Size != size
                || after.Mode != mode
                || after.Inode != inode
                || after.ChangeSeconds != changeSeconds
                || after.ChangeNanoseconds != changeNanoseconds)
            {
                throw new PreflightBlockedException("entry_changed_during_snapshot");
            }

            return new PreflightManifestEntry(
                Convert.ToBase64String(pathBytes),
                "symlink",
                target.Length,
                mode,
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
        byte[] leftBytes = Convert.FromBase64String(left.PathBase64!);
        byte[] rightBytes = Convert.FromBase64String(right.PathBase64!);
        return leftBytes.AsSpan().SequenceCompareTo(rightBytes);
    }

    private static (int Type, long Size, int Mode, ulong Inode, long ChangeSeconds, uint ChangeNanoseconds) GetEntryStatus(string path)
    {
        try
        {
            return PreflightUnix.Stat(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PreflightBlockedException("entry_unreadable");
        }
    }

    private static bool IsStable(
        (int Type, long Size, int Mode, ulong Inode, long ChangeSeconds, uint ChangeNanoseconds) before,
        (int Type, long Size, int Mode, ulong Inode, long ChangeSeconds, uint ChangeNanoseconds) after)
    {
        return before.Type == after.Type
            && before.Size == after.Size
            && before.Mode == after.Mode
            && before.Inode == after.Inode
            && before.ChangeSeconds == after.ChangeSeconds
            && before.ChangeNanoseconds == after.ChangeNanoseconds;
    }
}
