using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.Versioning;
using System.Globalization;

namespace D3B.Scripts.Preflight;

[SupportedOSPlatform("linux")]
internal static class PreflightOperations
{
    private const string BackupTreeName = "tree";
    private const string BackupManifestName = "manifest.json";
    private const int PrivateFileMode = 0x180;

    public static PreflightManifest Snapshot(string root)
    {
        return PreflightSnapshot.Capture(root);
    }

    public static PreflightManifest SnapshotToFile(string root, string outputPath)
    {
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(root);
        PreflightManifest manifest = PreflightSnapshot.Capture(source);
        PreflightManifestStore.WriteAtomically(outputPath, manifest, source);
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
        using SafeFileHandle rootHandle = PreflightUnix.OpenDirectoryPath(root);
        PreflightManifest expected = PreflightManifestStore.Read(manifestPath, rootHandle);
        Verify(rootHandle, expected);
    }

    public static void Verify(string root, PreflightManifest expected)
    {
        using SafeFileHandle rootHandle = PreflightUnix.OpenDirectoryPath(root);
        Verify(rootHandle, expected);
    }

    public static bool Compare(string root, string beforePath, string afterOutputPath)
    {
        using SafeFileHandle rootHandle = PreflightUnix.OpenDirectoryPath(root);
        PreflightManifest before = PreflightManifestStore.Read(beforePath, rootHandle);
        PreflightManifest after = PreflightSnapshot.Capture(rootHandle);
        PreflightManifestStore.WriteAtomically(afterOutputPath, after, rootHandle);
        return AreEqual(before, after);
    }

    public static string Backup(string root, string outputDirectory)
    {
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(root);
        PreflightManifest manifest = PreflightSnapshot.Capture(source);
        string fullOutput = PreflightPath.FullPath(outputDirectory);
        string outputName = Path.GetFileName(fullOutput);

        if (string.IsNullOrEmpty(outputName))
        {
            throw new PreflightBlockedException("backup_destination_invalid");
        }

        string parentPath = Path.GetDirectoryName(fullOutput) ?? "/";
        using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(
            parentPath,
            true,
            directory =>
            {
                PreflightUnix.EnsureNoRepositoryMarker(directory);
                PreflightUnix.EnsureOutsideTree(directory, source);
            });
        byte[] outputNameBytes = PreflightUnix.EncodeName(outputName);

        if (PreflightUnix.TryStatAt(parent, outputNameBytes, out _))
        {
            throw new PreflightBlockedException("backup_destination_exists");
        }

        PreflightUnix.CreateDirectoryAt(parent, outputNameBytes);
        using SafeFileHandle backup = PreflightUnix.OpenDirectoryAt(parent, outputNameBytes);
        PreflightUnix.CreateDirectoryAt(backup, PreflightUnix.EncodeName(BackupTreeName));
        using SafeFileHandle tree = PreflightUnix.OpenDirectoryAt(backup, PreflightUnix.EncodeName(BackupTreeName));
        CopyEntries(source, tree, manifest, false);
        VerifyBackupTree(tree, manifest);
        string manifestPath = Path.Combine(fullOutput, BackupManifestName);
        PreflightManifestStore.WriteAtomically(manifestPath, manifest, source);
        return manifestPath;
    }

    public static void Restore(string backupDirectory, string manifestPath, string root)
    {
        _ = RestoreWithResult(backupDirectory, manifestPath, root, null);
    }

    public static string? RestoreWithResult(
        string backupDirectory,
        string manifestPath,
        string root,
        Action? afterStageCreated = null)
    {
        try
        {
            return RestoreCore(backupDirectory, manifestPath, root, afterStageCreated);
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

    private static string? RestoreCore(
        string backupDirectory,
        string manifestPath,
        string root,
        Action? afterStageCreated)
    {
        using SafeFileHandle backup = PreflightUnix.OpenDirectoryPath(backupDirectory);
        PreflightFileStatus backupStatus = PreflightUnix.StatHandle(backup);

        if (backupStatus.Type != PreflightUnix.DirectoryType)
        {
            throw new PreflightBlockedException("backup_directory_invalid");
        }

        using SafeFileHandle backupTree = PreflightUnix.OpenDirectoryAt(backup, PreflightUnix.EncodeName(BackupTreeName));
        string fullRoot = PreflightPath.FullPath(root);
        string rootName = Path.GetFileName(fullRoot);

        if (string.IsNullOrEmpty(rootName))
        {
            throw new PreflightBlockedException("restore_root_invalid");
        }

        string rootParentPath = Path.GetDirectoryName(fullRoot) ?? "/";
        using SafeFileHandle rootParent = PreflightUnix.OpenDirectoryPath(rootParentPath);
        PreflightUnix.EnsureNoRepositoryMarker(rootParent);
        byte[] rootNameBytes = PreflightUnix.EncodeName(rootName);
        PreflightFileStatus parentStatus = PreflightUnix.StatHandle(rootParent);
        SafeFileHandle? originalRoot = null;
        PreflightFileStatus? originalRootStatus = null;

        if (PreflightUnix.TryStatAt(rootParent, rootNameBytes, out PreflightFileStatus? destinationStatus))
        {
            if (destinationStatus!.Type != PreflightUnix.DirectoryType)
            {
                throw new PreflightBlockedException("restore_root_invalid");
            }

            PreflightUnix.EnsureSameMount(destinationStatus, parentStatus);
            originalRoot = PreflightUnix.OpenDirectoryAt(rootParent, rootNameBytes);
            originalRootStatus = PreflightUnix.StatHandle(originalRoot);

            if (!PreflightUnix.IsSameObject(destinationStatus, originalRootStatus))
            {
                originalRoot.Dispose();
                throw new PreflightBlockedException("restore_root_changed");
            }

            PreflightUnix.EnsureNoRepositoryMarker(originalRoot);
            RejectOverlappingRestorePaths(backup, originalRoot);
        }
        else
        {
            if (PreflightUnix.IsDescendantOrSelf(rootParent, backup))
            {
                throw new PreflightBlockedException("restore_paths_overlap");
            }
        }

        using (originalRoot)
        {
            if (originalRoot is not null)
            {
                using SafeFileHandle manifestParent = PreflightUnix.OpenDirectoryPath(Path.GetDirectoryName(PreflightPath.FullPath(manifestPath)) ?? "/");
                PreflightUnix.EnsureOutsideTree(manifestParent, originalRoot);
            }

            PreflightManifest manifest = PreflightManifestStore.Read(manifestPath, originalRoot);
            VerifyBackupTree(backupTree, manifest);
            byte[] stageName = PreflightUnix.EncodeName(".preflight-stage-" + Guid.NewGuid().ToString("N"));
            PreflightUnix.CreateDirectoryAt(rootParent, stageName);
            using SafeFileHandle stage = PreflightUnix.OpenDirectoryAt(rootParent, stageName);
            afterStageCreated?.Invoke();
            CopyEntries(backupTree, stage, manifest, true);
            Verify(stage, manifest, "restore_staging_mismatch");
            byte[]? supersededName = null;

            if (originalRoot is not null)
            {
                supersededName = FindAvailableName(rootParent, ".preflight-superseded-");
                PreflightUnix.RenameAt(rootParent, rootNameBytes, rootParent, supersededName);
            }

            try
            {
                PreflightUnix.RenameAt(rootParent, stageName, rootParent, rootNameBytes);
            }
            catch
            {
                if (supersededName is not null)
                {
                    PreflightUnix.RenameAt(rootParent, supersededName, rootParent, rootNameBytes);
                }

                throw;
            }

            try
            {
                using SafeFileHandle installed = PreflightUnix.OpenDirectoryAt(rootParent, rootNameBytes);
                Verify(installed, manifest, "restore_verification_failed");
                PreflightUnix.SyncDirectory(rootParent);
                return supersededName is null ? null : System.Text.Encoding.UTF8.GetString(supersededName);
            }
            catch
            {
                byte[] failedName = FindAvailableName(rootParent, ".preflight-failed-");
                PreflightUnix.RenameAt(rootParent, rootNameBytes, rootParent, failedName);

                if (supersededName is not null)
                {
                    PreflightUnix.RenameAt(rootParent, supersededName, rootParent, rootNameBytes);
                }

                throw new PreflightBlockedException("restore_verification_failed");
            }
        }
    }

    private static void RejectOverlappingRestorePaths(SafeFileHandle backup, SafeFileHandle root)
    {
        if (PreflightUnix.IsDescendantOrSelf(backup, root) || PreflightUnix.IsDescendantOrSelf(root, backup))
        {
            throw new PreflightBlockedException("restore_paths_overlap");
        }
    }

    private static void Verify(SafeFileHandle root, PreflightManifest expected)
    {
        Verify(root, expected, "tree_mismatch");
    }

    private static void Verify(SafeFileHandle root, PreflightManifest expected, string mismatchCode)
    {
        PreflightManifestCodec.Validate(expected);
        PreflightManifest actual = PreflightSnapshot.Capture(root);

        if (!AreEqual(expected, actual))
        {
            throw new PreflightBlockedException(mismatchCode);
        }
    }

    private static void VerifyBackupTree(SafeFileHandle root, PreflightManifest expected)
    {
        PreflightManifest actual = PreflightSnapshot.Capture(root);

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

    private static void CopyEntries(
        SafeFileHandle sourceRoot,
        SafeFileHandle destinationRoot,
        PreflightManifest manifest,
        bool preserveModes)
    {
        PreflightFileStatus sourceRootStatus = PreflightUnix.StatHandle(sourceRoot);
        PreflightFileStatus destinationRootStatus = PreflightUnix.StatHandle(destinationRoot);

        foreach (PreflightManifestEntry entry in manifest.Entries)
        {
            byte[] relativePath = PreflightPath.DecodePath(entry.PathBase64!);
            List<byte[]> segments = PreflightPath.SplitRelativePath(relativePath);
            byte[] fileName = segments[^1];
            using SafeFileHandle sourceParent = OpenRelativeParent(sourceRoot, sourceRootStatus, segments, false);
            using SafeFileHandle destinationParent = OpenRelativeParent(destinationRoot, destinationRootStatus, segments, true);

            if (entry.Kind == "file")
            {
                CopyRegularFile(sourceParent, destinationParent, fileName, entry, sourceRootStatus, preserveModes);
            }
            else if (entry.Kind == "symlink")
            {
                CopySymbolicLink(sourceParent, destinationParent, fileName, entry, sourceRootStatus);
            }
            else
            {
                throw new PreflightBlockedException("manifest_kind_invalid");
            }
        }
    }

    private static SafeFileHandle OpenRelativeParent(
        SafeFileHandle root,
        PreflightFileStatus rootStatus,
        List<byte[]> segments,
        bool createMissing)
    {
        SafeFileHandle current = PreflightUnix.DuplicateHandle(root);

        try
        {
            for (int index = 0; index < segments.Count - 1; index++)
            {
                byte[] segment = segments[index];

                if (!PreflightUnix.TryStatAt(current, segment, out PreflightFileStatus? status))
                {
                    if (!createMissing)
                    {
                        throw new PreflightBlockedException("backup_entry_missing");
                    }

                    try
                    {
                        PreflightUnix.CreateDirectoryAt(current, segment);
                    }
                    catch (IOException) when (Marshal.GetLastPInvokeError() == 17)
                    {
                    }

                    status = PreflightUnix.StatAt(current, segment);
                }

                if (status!.Type != PreflightUnix.DirectoryType)
                {
                    throw new PreflightBlockedException("copy_parent_invalid");
                }

                PreflightUnix.EnsureSameMount(status, rootStatus);
                SafeFileHandle next = PreflightUnix.OpenDirectoryAt(current, segment);
                PreflightFileStatus opened = PreflightUnix.StatHandle(next);

                if (!PreflightUnix.IsSameObject(status, opened))
                {
                    next.Dispose();
                    throw new PreflightBlockedException("copy_parent_changed");
                }

                PreflightUnix.EnsureSameMount(opened, rootStatus);

                current.Dispose();
                current = next;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private static void CopyRegularFile(
        SafeFileHandle sourceParent,
        SafeFileHandle destinationParent,
        byte[] name,
        PreflightManifestEntry entry,
        PreflightFileStatus sourceRootStatus,
        bool preserveModes)
    {
        PreflightFileStatus before = PreflightUnix.StatAt(sourceParent, name);

        if (before.Type != PreflightUnix.RegularFileType)
        {
            throw new PreflightBlockedException("copy_source_kind_changed");
        }

        PreflightUnix.EnsureSameMount(before, sourceRootStatus);

        using SafeFileHandle source = PreflightUnix.OpenFileAt(sourceParent, name);
        PreflightFileStatus opened = PreflightUnix.StatHandle(source);

        if (!PreflightUnix.IsSameObject(before, opened) || opened.Size != before.Size)
        {
            throw new PreflightBlockedException("copy_source_changed");
        }

        PreflightUnix.EnsureSameMount(opened, sourceRootStatus);

        using SafeFileHandle destination = PreflightUnix.CreateFileAt(destinationParent, name);
        using var input = new FileStream(source, FileAccess.Read, 131072, false);
        using var output = new FileStream(destination, FileAccess.Write, 131072, false);
        input.CopyTo(output);
        output.Flush(true);
        PreflightFileStatus after = PreflightUnix.StatHandle(source);

        if (after.Size != opened.Size
            || after.ChangeSeconds != opened.ChangeSeconds
            || after.ChangeNanoseconds != opened.ChangeNanoseconds
            || after.ModifiedSeconds != opened.ModifiedSeconds
            || after.ModifiedNanoseconds != opened.ModifiedNanoseconds)
        {
            throw new PreflightBlockedException("copy_source_changed");
        }

        PreflightUnix.SetMode(destination, preserveModes ? entry.Mode : PrivateFileMode);
        output.Flush(true);
    }

    private static void CopySymbolicLink(
        SafeFileHandle sourceParent,
        SafeFileHandle destinationParent,
        byte[] name,
        PreflightManifestEntry entry,
        PreflightFileStatus rootStatus)
    {
        PreflightFileStatus before = PreflightUnix.StatAt(sourceParent, name);

        if (before.Type != PreflightUnix.SymbolicLinkType)
        {
            throw new PreflightBlockedException("copy_source_kind_changed");
        }

        PreflightUnix.EnsureSameMount(before, rootStatus);
        byte[] target = PreflightUnix.ReadLinkAt(sourceParent, name);
        PreflightFileStatus after = PreflightUnix.StatAt(sourceParent, name);

        if (!PreflightUnix.IsSameObject(before, after)
            || before.ChangeSeconds != after.ChangeSeconds
            || before.ChangeNanoseconds != after.ChangeNanoseconds
            || Convert.ToBase64String(target) != entry.LinkTargetBase64)
        {
            throw new PreflightBlockedException("copy_source_changed");
        }

        PreflightUnix.CreateSymbolicLinkAt(destinationParent, name, target);
    }

    private static bool AreEqual(PreflightManifest left, PreflightManifest right)
    {
        byte[] leftBytes = PreflightManifestCodec.Serialize(left);
        byte[] rightBytes = PreflightManifestCodec.Serialize(right);
        return leftBytes.AsSpan().SequenceEqual(rightBytes);
    }

    private static byte[] FindAvailableName(SafeFileHandle parent, string prefix)
    {
        for (int index = 1; ; index++)
        {
            byte[] name = PreflightUnix.EncodeName(prefix + index.ToString("D4", CultureInfo.InvariantCulture));

            if (!PreflightUnix.TryStatAt(parent, name, out _))
            {
                return name;
            }
        }
    }
}
