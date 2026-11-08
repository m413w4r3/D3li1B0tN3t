using System.Security;
using System.Runtime.Versioning;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightOperations
{
    private const string BackupTreeName = "tree";
    private const string BackupManifestName = "manifest.json";

    public static PreflightManifest Snapshot(string root)
    {
        return PreflightSnapshot.Capture(root);
    }

    public static PreflightManifest SnapshotToFile(string root, string outputPath)
    {
        string fullOutput = PreflightPath.FullPath(outputPath);
        PreflightPath.EnsureNoRepositoryMarker(fullOutput);
        PreflightPath.EnsureOutsideTree(root, fullOutput);
        PreflightManifest manifest = Snapshot(root);
        PreflightManifestStore.WriteAtomically(fullOutput, manifest);
        return manifest;
    }

    public static byte[] SerializeManifest(PreflightManifest manifest)
    {
        return PreflightManifestCodec.Serialize(manifest);
    }

    public static PreflightManifest ParseManifest(ReadOnlySpan<byte> bytes)
    {
        return PreflightManifestCodec.Parse(bytes);
    }

    public static void WriteManifest(string path, PreflightManifest manifest)
    {
        PreflightManifestStore.WriteAtomically(path, manifest);
    }

    public static void Verify(string root, string manifestPath)
    {
        PreflightPath.EnsureOutsideTree(root, manifestPath);
        PreflightManifest expected = PreflightManifestStore.Read(manifestPath);
        Verify(root, expected);
    }

    public static void Verify(string root, PreflightManifest expected)
    {
        PreflightManifest actual = CaptureForVerification(root);

        if (!AreEqual(expected, actual))
        {
            throw new PreflightBlockedException("tree_mismatch");
        }
    }

    public static bool Compare(string root, string beforePath, string afterOutputPath)
    {
        PreflightPath.EnsureOutsideTree(root, beforePath);
        PreflightManifest before = PreflightManifestStore.Read(beforePath);
        string fullOutput = PreflightPath.FullPath(afterOutputPath);
        PreflightPath.EnsureNoRepositoryMarker(fullOutput);
        PreflightPath.EnsureOutsideTree(root, fullOutput);
        PreflightManifest after = CaptureForVerification(root);
        PreflightManifestStore.WriteAtomically(fullOutput, after);
        return AreEqual(before, after);
    }

    public static string Backup(string root, string outputDirectory)
    {
        PreflightManifest manifest = Snapshot(root);
        string fullOutput = PreflightPath.FullPath(outputDirectory);
        PreflightPath.EnsureNoRepositoryMarker(fullOutput);
        PreflightPath.EnsureOutsideTree(root, fullOutput);

        if (Directory.Exists(fullOutput) || File.Exists(fullOutput))
        {
            throw new PreflightBlockedException("backup_destination_exists");
        }

        Directory.CreateDirectory(fullOutput);
        PreflightUnix.SetPrivateDirectoryMode(fullOutput);
        string tree = Path.Combine(fullOutput, BackupTreeName);
        Directory.CreateDirectory(tree);
        PreflightUnix.SetPrivateDirectoryMode(tree);

        try
        {
            CopyEntries(root, tree, manifest, false);
            VerifyBackupTree(tree, manifest);
            string manifestPath = Path.Combine(fullOutput, BackupManifestName);
            PreflightManifestStore.WriteAtomically(manifestPath, manifest);
            return manifestPath;
        }
        catch
        {
            if (Directory.Exists(fullOutput))
            {
                Directory.Delete(fullOutput, true);
            }

            throw;
        }
    }

    public static void Restore(string backupDirectory, string manifestPath, string root)
    {
        try
        {
            RestoreCore(backupDirectory, manifestPath, root);
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            throw new PreflightBlockedException("restore_failed");
        }
    }

    private static void RestoreCore(string backupDirectory, string manifestPath, string root)
    {
        string fullBackup = PreflightPath.FullPath(backupDirectory);
        string fullRoot = PreflightPath.FullPath(root);
        PreflightPath.EnsureNoRepositoryMarker(fullRoot);
        PreflightPath.EnsureOutsideTree(fullRoot, manifestPath);
        RejectOverlappingRestorePaths(fullBackup, fullRoot);
        var backupStatus = PreflightUnix.Stat(fullBackup);

        if (backupStatus.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("backup_directory_invalid");
        }

        PreflightManifest manifest = PreflightManifestStore.Read(manifestPath);
        string backupTree = Path.Combine(fullBackup, BackupTreeName);
        VerifyBackupTree(backupTree, manifest);
        EnsureRootDirectory(fullRoot);
        ClearDirectoryContents(fullRoot);
        CopyEntries(backupTree, fullRoot, manifest, true);
        Verify(fullRoot, manifest);
    }

    private static void RejectOverlappingRestorePaths(string backup, string root)
    {
        if (PreflightPath.IsWithin(backup, root) || PreflightPath.IsWithin(root, backup))
        {
            throw new PreflightBlockedException("restore_paths_overlap");
        }
    }

    private static void EnsureRootDirectory(string root)
    {
        if (Directory.Exists(root))
        {
            var status = PreflightUnix.Stat(root);

            if (status.Type != PreflightUnix.DirectoryType)
            {
                throw new PreflightBlockedException("restore_root_invalid");
            }

            return;
        }

        if (File.Exists(root))
        {
            throw new PreflightBlockedException("restore_root_invalid");
        }

        Directory.CreateDirectory(root);
        PreflightUnix.SetPrivateDirectoryMode(root);
    }

    private static void ClearDirectoryContents(string directory)
    {
        string[] entries;

        try
        {
            entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new PreflightBlockedException("restore_root_unreadable");
        }

        foreach (string entry in entries)
        {
            var status = PreflightUnix.Stat(entry);

            if (status.Type == PreflightUnix.DirectoryType)
            {
                ClearDirectoryContents(entry);
                Directory.Delete(entry);
            }
            else
            {
                File.Delete(entry);
            }
        }
    }

    private static void CopyEntries(string sourceRoot, string destinationRoot, PreflightManifest manifest, bool preserveModes)
    {
        foreach (PreflightManifestEntry entry in manifest.Entries)
        {
            byte[] relativeBytes = PreflightPath.DecodePath(entry.PathBase64!);
            string relative = PreflightPath.DecodeUtf8(relativeBytes).Replace('/', Path.DirectorySeparatorChar);
            string sourcePath = Path.Combine(sourceRoot, relative);
            string destinationPath = Path.Combine(destinationRoot, relative);
            EnsureParentDirectories(destinationRoot, relative);

            if (entry.Kind == "file")
            {
                int destinationMode = preserveModes ? entry.Mode : 0x180;
                CopyFile(sourcePath, destinationPath, destinationMode);
            }
            else if (entry.Kind == "symlink")
            {
                byte[] target = Convert.FromBase64String(entry.LinkTargetBase64!);
                PreflightUnix.CreateSymbolicLink(destinationPath, target);
            }
            else
            {
                throw new PreflightBlockedException("manifest_kind_invalid");
            }
        }
    }

    private static void EnsureParentDirectories(string root, string relative)
    {
        string? parentRelative = Path.GetDirectoryName(relative);

        if (parentRelative is null)
        {
            return;
        }

        string current = root;
        string[] segments = parentRelative.Split(Path.DirectorySeparatorChar);

        foreach (string segment in segments)
        {
            current = Path.Combine(current, segment);

            if (Directory.Exists(current))
            {
                if (PreflightUnix.Stat(current).Type != PreflightUnix.DirectoryType)
                {
                    throw new PreflightBlockedException("restore_parent_invalid");
                }

                continue;
            }

            if (File.Exists(current))
            {
                throw new PreflightBlockedException("restore_parent_invalid");
            }

            Directory.CreateDirectory(current);
            PreflightUnix.SetPrivateDirectoryMode(current);
        }
    }

    private static void CopyFile(string source, string destination, int mode)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.SequentialScan);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.WriteThrough);
        input.CopyTo(output);
        output.Flush(true);
        File.SetUnixFileMode(destination, (UnixFileMode)mode);
    }

    private static bool AreEqual(PreflightManifest left, PreflightManifest right)
    {
        byte[] leftBytes = PreflightManifestCodec.Serialize(left);
        byte[] rightBytes = PreflightManifestCodec.Serialize(right);
        return leftBytes.AsSpan().SequenceEqual(rightBytes);
    }

    private static PreflightManifest CaptureForVerification(string root)
    {
        try
        {
            return Snapshot(root);
        }
        catch (PreflightBlockedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
        {
            throw new PreflightBlockedException("tree_unreadable");
        }
    }

    private static void VerifyBackupTree(string tree, PreflightManifest expected)
    {
        PreflightManifest actual = Snapshot(tree);

        if (expected.SchemaVersion != actual.SchemaVersion || expected.Entries.Count != actual.Entries.Count)
        {
            throw new PreflightBlockedException("backup_mismatch");
        }

        for (int index = 0; index < expected.Entries.Count; index++)
        {
            PreflightManifestEntry before = expected.Entries[index];
            PreflightManifestEntry backup = actual.Entries[index];

            if (before.PathBase64 != backup.PathBase64
                || before.Kind != backup.Kind
                || before.Size != backup.Size
                || before.Sha256 != backup.Sha256
                || before.LinkTargetBase64 != backup.LinkTargetBase64)
            {
                throw new PreflightBlockedException("backup_mismatch");
            }
        }
    }
}
