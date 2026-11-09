using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using D3B.Scripts.Preflight;

namespace D3B.Scripts.Tests.Preflight;

[SupportedOSPlatform("linux")]
public sealed class PreflightOperationsTests
{
    [Fact]
    public void SnapshotRoundTripsUnusualNamesWithoutLineSplitting()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        var names = new[] { "with spaces", "apostrophe's", "double\"quote", "back\\slash", "line\nbreak", "unicode-雪", "-leading" };
        foreach (string name in names)
        {
            directory.WriteFile(root, name, Encoding.UTF8.GetBytes(name));
        }

        string manifestPath = directory.Combine("private/manifest.json");
        var manifest = PreflightOperations.SnapshotToFile(root, manifestPath);

        PreflightOperations.Verify(root, manifestPath);
        Assert.Equal(names.Length, manifest.Entries.Count);
        foreach (string name in names)
        {
            string encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
            Assert.Contains(manifest.Entries, entry => entry.PathBase64 == encodedName);
        }
    }

    [Fact]
    public void ManifestSerializationIsDeterministicAndRoundTripsCanonically()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "one", [1, 2, 3]);
        directory.WriteFile(root, "two", [4, 5]);

        byte[] first = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root));
        byte[] second = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root));
        var parsed = PreflightOperations.ParseManifest(first);

        Assert.Equal(first, second);
        Assert.Equal(first, PreflightOperations.SerializeManifest(parsed));

        byte[] malformed = (byte[])first.Clone();
        malformed[0] = (byte)'[';
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.ParseManifest(malformed));
    }

    [Fact]
    public void ParserRejectsWhitespaceInBase64Fields()
    {
        using var directory = new PreflightTestDirectory();
        string root = directory.Root;
        directory.WriteFile(root, "file", [1, 2, 3]);
        JsonObject json = JsonNode.Parse(PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root)))!.AsObject();
        JsonObject entry = json["entries"]!.AsArray()[0]!.AsObject();
        string pathBase64 = entry["pathBase64"]!.GetValue<string>();
        entry["pathBase64"] = pathBase64.Insert(1, " ");

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.ParseManifest(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void ParserRejectsWhitespaceInLinkTargetBase64()
    {
        using var directory = new PreflightTestDirectory();
        string root = directory.Root;
        File.CreateSymbolicLink(System.IO.Path.Combine(root, "link"), "relative target");
        JsonObject json = JsonNode.Parse(PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root)))!.AsObject();
        JsonObject entry = json["entries"]!.AsArray()[0]!.AsObject();
        string linkTargetBase64 = entry["linkTargetBase64"]!.GetValue<string>();
        entry["linkTargetBase64"] = linkTargetBase64.Insert(1, " ");

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.ParseManifest(JsonSerializer.SerializeToUtf8Bytes(json)));
    }

    [Fact]
    public void TamperedManifestHashSizeAndKindAreRejected()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "file", [1, 2, 3]);
        var manifest = PreflightOperations.Snapshot(root);

        AssertManifestTamperRejected(directory, root, manifest, entry => entry["sha256"] = new string('0', 64));
        AssertManifestTamperRejected(directory, root, manifest, entry => entry["size"] = 4);
        AssertManifestTamperRejected(directory, root, manifest, entry => entry["kind"] = "other");
    }

    [Fact]
    public void VerifyRejectsAddedDeletedRenamedAndChangedFiles()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        string original = directory.WriteFile(root, "original", [1, 2, 3]);
        var manifest = PreflightOperations.Snapshot(root);
        string manifestPath = directory.Combine("before.json");
        PreflightOperations.WriteManifest(manifestPath, manifest);

        directory.WriteFile(root, "added", [4]);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));
        File.Delete(directory.Combine("added"));

        File.Delete(original);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));
        directory.WriteFile(root, "renamed", [1, 2, 3]);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));
        File.Delete(directory.Combine("renamed"));

        original = directory.WriteFile(root, "original", [1, 2, 4]);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void ParserRejectsDuplicateAndMissingEntries()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "one", [1]);
        directory.WriteFile(root, "two", [2]);
        byte[] canonical = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root));

        JsonObject duplicate = JsonNode.Parse(canonical)!.AsObject();
        JsonArray duplicateEntries = duplicate["entries"]!.AsArray();
        duplicateEntries.Add(duplicateEntries[0]!.DeepClone());
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.ParseManifest(JsonSerializer.SerializeToUtf8Bytes(duplicate)));

        JsonObject missing = JsonNode.Parse(canonical)!.AsObject();
        missing["entries"]!.AsArray().RemoveAt(1);
        var parsedMissing = PreflightOperations.ParseManifest(JsonSerializer.SerializeToUtf8Bytes(missing));
        string manifestPath = directory.Combine("missing-entry.json");
        PreflightOperations.WriteManifest(manifestPath, parsedMissing);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));
    }

    [Fact]
    public void CompareWritesAfterSnapshotAndReportsOnlyExactEqualityAsSuccess()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "config", [1, 2, 3]);
        string beforePath = directory.Combine("before.json");
        string afterPath = directory.Combine("private/after.json");
        PreflightOperations.WriteManifest(beforePath, PreflightOperations.Snapshot(root));

        Assert.True(PreflightOperations.Compare(root, beforePath, afterPath));
        Assert.True(PreflightOperations.Compare(root, beforePath, afterPath));

        File.WriteAllBytes(System.IO.Path.Combine(root, "config"), [1, 2, 4]);
        Assert.False(PreflightOperations.Compare(root, beforePath, afterPath));
        PreflightOperations.Verify(root, afterPath);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, beforePath));
    }

    [Fact]
    public void BackupRestoresExactFilesModesAndSymlinkTargets()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "target", [8, 9, 10]);
        string config = directory.WriteFile(root, "config file", [1, 2, 3, 4]);
        File.SetUnixFileMode(config, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        File.CreateSymbolicLink(System.IO.Path.Combine(root, "link"), "config file");
        var before = PreflightOperations.Snapshot(root);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(root, backup);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(backup));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(manifestPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(System.IO.Path.Combine(backup, "tree", "config file")));

        File.WriteAllBytes(config, [0]);
        File.SetUnixFileMode(config, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Delete(System.IO.Path.Combine(root, "link"));
        File.CreateSymbolicLink(System.IO.Path.Combine(root, "link"), "target");
        directory.WriteFile(root, "extra", [5]);
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, manifestPath));

        PreflightManifest supersededManifest = PreflightOperations.Snapshot(root);
        int syncedStageDirectories = 0;
        bool parentSyncedAfterSwap = false;
        string? supersededName = PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            root,
            afterSwap: () => parentSyncedAfterSwap = true,
            afterStageDirectorySynced: () => syncedStageDirectories++);
        Assert.NotNull(supersededName);
        Assert.StartsWith(".preflight-stage-", supersededName, StringComparison.Ordinal);
        Assert.True(syncedStageDirectories > 0);
        Assert.True(parentSyncedAfterSwap);
        string supersededPath = directory.Combine(supersededName);
        Assert.True(Directory.Exists(supersededPath));
        PreflightOperations.Verify(supersededPath, supersededManifest);
        PreflightOperations.Verify(root, manifestPath);
        Assert.Equal(PreflightOperations.SerializeManifest(before), PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root)));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, File.GetUnixFileMode(config));
        Assert.Equal("config file", new FileInfo(System.IO.Path.Combine(root, "link")).LinkTarget);
        Assert.False(File.Exists(System.IO.Path.Combine(root, "extra")));
        Assert.Equal([8, 9, 10], File.ReadAllBytes(System.IO.Path.Combine(supersededPath, "target")));
    }

    [Fact]
    public void RestoreRejectsCorruptBackupBeforeChangingTheTree()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        string source = directory.WriteFile(root, "config", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(root, backup);
        File.WriteAllBytes(source, [4, 5, 6]);
        File.WriteAllBytes(System.IO.Path.Combine(backup, "tree", "config"), [7, 8, 9]);

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Restore(backup, manifestPath, root));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(source));
    }

    [Fact]
    public void OutputUnderRepositoryMarkerIsRejectedAndPrivateModesAreSet()
    {
        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        directory.WriteFile(root, "file", [1]);
        string repository = directory.Combine("repository");
        Directory.CreateDirectory(repository);
        File.WriteAllText(System.IO.Path.Combine(repository, "D3B.slnx"), "synthetic marker");

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.SnapshotToFile(root, System.IO.Path.Combine(repository, "manifest.json")));
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.SnapshotToFile(root, System.IO.Path.Combine(root, "manifest.json")));

        string privatePath = directory.Combine("private/manifest.json");
        PreflightOperations.SnapshotToFile(root, privatePath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(privatePath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(System.IO.Path.GetDirectoryName(privatePath)!));
    }

    [Fact]
    public void SymlinkedAncestorAliasesIntoSourceAndRepositoryAreRejected()
    {
        using var directory = new PreflightTestDirectory();
        string root = directory.Root;
        directory.WriteFile(root, "file", [1]);
        string beforePath = directory.Combine("before.json");
        PreflightOperations.WriteManifest(beforePath, PreflightOperations.Snapshot(root));
        string sourceAlias = directory.Combine("source-alias");
        Directory.CreateSymbolicLink(sourceAlias, root);

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.SnapshotToFile(root, System.IO.Path.Combine(sourceAlias, "manifest.json")));
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Compare(root, beforePath, System.IO.Path.Combine(sourceAlias, "after.json")));

        string repository = directory.Combine("repository");
        Directory.CreateDirectory(repository);
        File.WriteAllText(System.IO.Path.Combine(repository, "D3B.slnx"), "synthetic marker");
        string repositoryAlias = directory.Combine("repository-alias");
        Directory.CreateSymbolicLink(repositoryAlias, repository);

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.SnapshotToFile(root, System.IO.Path.Combine(repositoryAlias, "manifest.json")));
    }

    [Fact]
    public void SnapshotRecordsDirectorySymlinksWithoutFollowingThem()
    {
        using var directory = new PreflightTestDirectory();
        string root = directory.Root;
        string outside = directory.Combine("outside");
        Directory.CreateDirectory(outside);
        directory.WriteFile(outside, "secret-shaped-synthetic-file", [9, 8, 7]);
        File.CreateSymbolicLink(System.IO.Path.Combine(root, "linked-directory"), outside);

        PreflightManifest manifest = PreflightOperations.Snapshot(root);

        Assert.Single(manifest.Entries);
        Assert.Equal("symlink", manifest.Entries[0].Kind);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("linked-directory")), manifest.Entries[0].PathBase64);
    }

    [Fact]
    public void FailedRestoreStagingLeavesOriginalDestinationByteIdentical()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 2, 3, 4]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        string oldFile = directory.WriteFile(destination, "old config", [7, 6, 5]);
        File.SetUnixFileMode(oldFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        File.CreateSymbolicLink(System.IO.Path.Combine(destination, "old-link"), "old config");
        byte[] before = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination));

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            () => throw new IOException("Synthetic staging failure.")));

        byte[] after = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination));
        Assert.Equal(before, after);
        Assert.Equal([7, 6, 5], File.ReadAllBytes(oldFile));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, File.GetUnixFileMode(oldFile));
        Assert.Equal("old config", new FileInfo(System.IO.Path.Combine(destination, "old-link")).LinkTarget);
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(stagePath));
    }

    [Fact]
    public void BackupFailureLeavesAnIncompletePrivateBackupWithoutChangingSource()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        string original = directory.WriteFile(source, "config", [5, 4, 3, 2]);
        string backup = directory.Combine("incomplete-backup");

        Assert.Throws<IOException>(() => PreflightOperations.Backup(
            source,
            backup,
            () => throw new IOException("Synthetic backup failure.")));

        Assert.Equal(["tree"], Directory.GetFileSystemEntries(backup).Select(System.IO.Path.GetFileName));
        Assert.Empty(Directory.EnumerateFileSystemEntries(System.IO.Path.Combine(backup, "tree")));
        Assert.Equal([5, 4, 3, 2], File.ReadAllBytes(original));
    }

    [Fact]
    public void AtomicSwapFailureLeavesOriginalTreeAndVerifiedStageInPlace()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 3, 5]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        string original = directory.WriteFile(destination, "original", [2, 4, 6]);
        byte[] before = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination));

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            exchange: (_, _, _, _) => throw new IOException("Synthetic exchange failure.")));

        Assert.Equal(before, PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination)));
        Assert.Equal([2, 4, 6], File.ReadAllBytes(original));
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        PreflightOperations.Verify(stagePath, manifestPath);
    }

    [Fact]
    public void ConcurrentDestinationCreationIsBlockedWithoutOverwritingIt()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Combine("new-destination");

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            () =>
            {
                Directory.CreateDirectory(destination);
                File.WriteAllBytes(System.IO.Path.Combine(destination, "concurrent"), [9, 8, 7]);
            }));

        Assert.Equal([9, 8, 7], File.ReadAllBytes(System.IO.Path.Combine(destination, "concurrent")));
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        PreflightOperations.Verify(stagePath, manifestPath);
    }

    [Fact]
    public void RestoreInstallsIntoMissingDestinationWithoutReplacingAnotherEntry()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [3, 1, 4, 1]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Combine("new-destination");

        Assert.Null(PreflightOperations.RestoreWithResult(backup, manifestPath, destination));

        PreflightOperations.Verify(destination, manifestPath);
        Assert.Empty(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
    }

    [Fact]
    public void PostExchangeVerificationFailureExchangesOriginalTreeBackAndRetainsStage()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        string original = directory.WriteFile(destination, "original", [7, 8, 9]);
        byte[] before = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination));

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            afterSwap: () => File.WriteAllBytes(System.IO.Path.Combine(destination, "post-swap-invalid"), [6, 6])));

        Assert.Equal(before, PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination)));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(original));
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(System.IO.Path.Combine(stagePath, "replacement")));
        Assert.Equal([6, 6], File.ReadAllBytes(System.IO.Path.Combine(stagePath, "post-swap-invalid")));
    }

    [Fact]
    public void RestoreDoesNotTouchContentReachedThroughDestinationSymlink()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        string outside = directory.Combine("outside");
        Directory.CreateDirectory(outside);
        string externalFile = directory.WriteFile(outside, "synthetic external file", [9, 8, 7, 6]);
        File.CreateSymbolicLink(System.IO.Path.Combine(destination, "external-link"), outside);
        var original = PreflightOperations.Snapshot(destination);

        string? superseded = PreflightOperations.RestoreWithResult(backup, manifestPath, destination);

        Assert.NotNull(superseded);
        Assert.Equal([9, 8, 7, 6], File.ReadAllBytes(externalFile));
        PreflightOperations.Verify(directory.Combine(superseded), original);
    }

    [Fact]
    public void EmptyDirectoriesAreNotRepresentedOrRestored()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("empty-source");
        Directory.CreateDirectory(source);
        PreflightManifest empty = PreflightOperations.Snapshot(source);
        Assert.Empty(empty.Entries);
        Directory.CreateDirectory(System.IO.Path.Combine(source, "nested-empty"));
        Assert.Equal(PreflightOperations.SerializeManifest(empty), PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(source)));

        string backup = directory.Combine("empty-backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Combine("empty-destination");
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(System.IO.Path.Combine(destination, "old-empty"));
        PreflightOperations.Restore(backup, manifestPath, destination);

        Assert.Empty(PreflightOperations.Snapshot(destination).Entries);
        Assert.False(Directory.Exists(System.IO.Path.Combine(destination, "old-empty")));
        Assert.False(Directory.Exists(System.IO.Path.Combine(destination, "nested-empty")));
    }

    [Fact]
    public void MountBoundaryCheckRejectsDifferentMountIdentity()
    {
        var root = new PreflightFileStatus(PreflightUnix.DirectoryType, 0, 0x1c0, 1, 8, 1, 10, 0, 0, 0, 0);
        var entry = new PreflightFileStatus(PreflightUnix.RegularFileType, 1, 0x180, 2, 8, 1, 11, 0, 0, 0, 0);

        Assert.Throws<PreflightBlockedException>(() => PreflightUnix.EnsureSameMount(entry, root));
        Assert.Throws<PreflightBlockedException>(() => PreflightUnix.EnsureAncestrySameMount(entry, root));
    }

    [Fact]
    public void SnapshotRejectsSpecialFilesWhenLinuxCanCreateAFifo()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new PreflightTestDirectory();
        var root = directory.Root;
        string fifo = System.IO.Path.Combine(root, "pipe");
        byte[] fifoPath = Encoding.UTF8.GetBytes(fifo + "\0");
        if (Mkfifo(fifoPath, 0x180) != 0)
        {
            throw new IOException("Could not create a synthetic FIFO.", Marshal.GetLastPInvokeError());
        }

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Snapshot(root));
    }

    private static void AssertManifestTamperRejected(
        PreflightTestDirectory directory,
        string root,
        PreflightManifest manifest,
        Action<JsonObject> mutate)
    {
        JsonObject json = JsonNode.Parse(PreflightOperations.SerializeManifest(manifest))!.AsObject();
        mutate(json["entries"]!.AsArray()[0]!.AsObject());
        string path = directory.Combine(Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(json));
        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.Verify(root, path));
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int Mkfifo(byte[] path, uint mode);
}
