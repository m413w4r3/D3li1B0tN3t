using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32.SafeHandles;
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
    public void ParserRejectsNoncanonicalLinkTargetBase64()
    {
        using var directory = new PreflightTestDirectory();
        string root = directory.Root;
        File.CreateSymbolicLink(System.IO.Path.Combine(root, "link"), "x");
        JsonObject json = JsonNode.Parse(PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root)))!.AsObject();
        JsonObject entry = json["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("eA==", entry["linkTargetBase64"]!.GetValue<string>());
        entry["linkTargetBase64"] = "eB==";

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
        string nested = System.IO.Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);
        directory.WriteFile(nested, "nested-config", [11, 12]);
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
        var durabilityEvents = new List<string>();
        string? supersededName = PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            root,
            beforeSwap: () =>
            {
                Assert.Equal(2, syncedStageDirectories);
                durabilityEvents.Add("before-exchange");
            },
            afterSwap: () =>
            {
                parentSyncedAfterSwap = true;
                durabilityEvents.Add("after-exchange");
            },
            afterStageDirectorySynced: () =>
            {
                syncedStageDirectories++;
                durabilityEvents.Add("stage-directory");
            });
        Assert.NotNull(supersededName);
        Assert.StartsWith(".preflight-stage-", supersededName, StringComparison.Ordinal);
        Assert.Equal(2, syncedStageDirectories);
        Assert.True(parentSyncedAfterSwap);
        Assert.Equal(["stage-directory", "stage-directory", "before-exchange", "after-exchange"], durabilityEvents);
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
    public void RestoreStagingFailureAfterCopyBeginsRetainsPartialStageAndOriginalDestination()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "first", [1, 2, 3]);
        directory.WriteFile(source, "second", [4, 5, 6]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        string original = directory.WriteFile(destination, "original", [7, 8, 9]);
        byte[] before = PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination));
        int copiedEntries = 0;

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            afterStageEntryCopied: () =>
            {
                copiedEntries++;
                if (copiedEntries == 1)
                {
                    throw new IOException("Synthetic mid-copy failure.");
                }
            }));

        Assert.Equal(1, copiedEntries);
        Assert.Equal(before, PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(destination)));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(original));
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(System.IO.Path.Combine(stagePath, "first")));
        Assert.False(File.Exists(System.IO.Path.Combine(stagePath, "second")));
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

        PreflightBlockedException symlinkedSource = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.SnapshotToFile(root, System.IO.Path.Combine(sourceAlias, "manifest.json")));
        Assert.Equal("path_component_unsafe", symlinkedSource.Code);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoreRejectsStagePathReplacedBeforeExchange(bool replaceWithSymlink)
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 3, 5]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        directory.WriteFile(destination, "original", [2, 4, 6]);
        PreflightManifest originalManifest = PreflightOperations.Snapshot(destination);
        using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(directory.Path);
        byte[] destinationName = PreflightUnix.EncodeName("tree");
        PreflightFileStatus originalStatus = PreflightUnix.StatAt(parent, destinationName);
        string movedStagePath = directory.Combine("writer-moved-stage");
        string stagePath = string.Empty;
        byte[] stageName = [];
        PreflightFileStatus? concurrentStageStatus = null;
        int exchangeCalls = 0;

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            exchange: (firstParent, firstName, secondParent, secondName) =>
            {
                exchangeCalls++;
                PreflightUnix.ExchangeAt(firstParent, firstName, secondParent, secondName);
            },
            beforeExchange: () =>
            {
                stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
                stageName = PreflightUnix.EncodeName(System.IO.Path.GetFileName(stagePath));
                Directory.Move(stagePath, movedStagePath);
                if (replaceWithSymlink)
                {
                    File.CreateSymbolicLink(stagePath, "concurrent-stage-target");
                }
                else
                {
                    Directory.CreateDirectory(stagePath);
                    File.WriteAllBytes(System.IO.Path.Combine(stagePath, "concurrent"), [9, 8, 7]);
                }

                concurrentStageStatus = PreflightUnix.StatAt(parent, stageName);
            }));

        Assert.NotEmpty(stagePath);
        Assert.Equal(0, exchangeCalls);
        Assert.NotNull(concurrentStageStatus);
        PreflightFileStatus currentDestinationStatus = PreflightUnix.StatAt(parent, destinationName);
        PreflightFileStatus currentStageStatus = PreflightUnix.StatAt(parent, stageName);
        Assert.True(PreflightUnix.IsSameEntry(originalStatus, currentDestinationStatus));
        Assert.True(PreflightUnix.IsSameEntry(concurrentStageStatus!, currentStageStatus));
        Assert.Equal(replaceWithSymlink ? PreflightUnix.SymbolicLinkType : PreflightUnix.DirectoryType, currentStageStatus.Type);
        PreflightOperations.Verify(destination, originalManifest);
        PreflightOperations.Verify(movedStagePath, manifestPath);

        if (replaceWithSymlink)
        {
            Assert.Equal("concurrent-stage-target", new FileInfo(stagePath).LinkTarget);
        }
        else
        {
            Assert.Equal([9, 8, 7], File.ReadAllBytes(System.IO.Path.Combine(stagePath, "concurrent")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestorePreservesDestinationReplacedAfterIdentityCheck(bool replaceWithSymlink)
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 3, 5]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        directory.WriteFile(destination, "original", [2, 4, 6]);
        var originalManifest = PreflightOperations.Snapshot(destination);
        string displacedOriginal = directory.Combine("writer-moved-original");
        using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(directory.Path);
        byte[] destinationName = PreflightUnix.EncodeName("tree");
        PreflightFileStatus? concurrentStatus = null;
        int exchangeCalls = 0;

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            exchange: (firstParent, firstName, secondParent, secondName) =>
            {
                if (exchangeCalls++ == 0)
                {
                    Directory.Move(destination, displacedOriginal);
                    if (replaceWithSymlink)
                    {
                        File.CreateSymbolicLink(destination, "concurrent-target");
                    }
                    else
                    {
                        Directory.CreateDirectory(destination);
                        File.WriteAllBytes(System.IO.Path.Combine(destination, "concurrent"), [9, 8, 7]);
                    }

                    concurrentStatus = PreflightUnix.StatAt(parent, destinationName);
                }

                PreflightUnix.ExchangeAt(firstParent, firstName, secondParent, secondName);
            }));

        Assert.Equal(2, exchangeCalls);
        Assert.NotNull(concurrentStatus);
        PreflightFileStatus currentStatus = PreflightUnix.StatAt(parent, destinationName);
        Assert.True(PreflightUnix.IsSameEntry(concurrentStatus!, currentStatus));
        Assert.Equal(replaceWithSymlink ? PreflightUnix.SymbolicLinkType : PreflightUnix.DirectoryType, currentStatus.Type);
        if (replaceWithSymlink)
        {
            Assert.Equal("concurrent-target", new FileInfo(destination).LinkTarget);
        }
        else
        {
            Assert.Equal([9, 8, 7], File.ReadAllBytes(System.IO.Path.Combine(destination, "concurrent")));
        }

        PreflightOperations.Verify(displacedOriginal, originalManifest);
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        PreflightOperations.Verify(stagePath, manifestPath);
    }

    [Fact]
    public void RestoreDoesNotExchangeBackOverDestinationReplacedAfterTheSwap()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 3, 5]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Root;
        directory.WriteFile(destination, "original", [2, 4, 6]);
        var originalManifest = PreflightOperations.Snapshot(destination);
        string displacedReplacement = directory.Combine("writer-moved-replacement");
        using SafeFileHandle parent = PreflightUnix.OpenDirectoryPath(directory.Path);
        byte[] destinationName = PreflightUnix.EncodeName("tree");
        PreflightFileStatus? concurrentStatus = null;

        Assert.Throws<PreflightBlockedException>(() => PreflightOperations.RestoreWithResult(
            backup,
            manifestPath,
            destination,
            afterSwap: () =>
            {
                Directory.Move(destination, displacedReplacement);
                Directory.CreateDirectory(destination);
                File.WriteAllBytes(System.IO.Path.Combine(destination, "concurrent"), [8, 6, 4]);
                concurrentStatus = PreflightUnix.StatAt(parent, destinationName);
            }));

        Assert.NotNull(concurrentStatus);
        PreflightFileStatus currentStatus = PreflightUnix.StatAt(parent, destinationName);
        Assert.True(PreflightUnix.IsSameEntry(concurrentStatus!, currentStatus));
        Assert.Equal([8, 6, 4], File.ReadAllBytes(System.IO.Path.Combine(destination, "concurrent")));
        PreflightOperations.Verify(displacedReplacement, manifestPath);
        string stagePath = Assert.Single(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
        PreflightOperations.Verify(stagePath, originalManifest);
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
    public void RestoreRejectsDestinationInsideBackupBeforeCreatingStage()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        string sourceNested = System.IO.Path.Combine(source, "nested");
        Directory.CreateDirectory(sourceNested);
        directory.WriteFile(sourceNested, "source-file", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = System.IO.Path.Combine(backup, "tree", "nested");
        string unchanged = directory.WriteFile(destination, "sentinel", [9, 8, 7]);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.Restore(backup, manifestPath, destination));

        Assert.Equal("restore_paths_overlap", exception.Code);
        Assert.Equal([9, 8, 7], File.ReadAllBytes(unchanged));
        Assert.Empty(Directory.GetDirectories(System.IO.Path.Combine(backup, "tree"), ".preflight-stage-*"));
    }

    [Fact]
    public void RestoreRejectsBackupInsideDestinationBeforeCreatingStage()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "replacement", [1, 2, 3]);
        string destination = directory.Combine("destination");
        Directory.CreateDirectory(destination);
        string unchanged = directory.WriteFile(destination, "sentinel", [9, 8, 7]);
        string backup = System.IO.Path.Combine(destination, "backup");
        string manifestPath = PreflightOperations.Backup(source, backup);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.Restore(backup, manifestPath, destination));

        Assert.Equal("restore_paths_overlap", exception.Code);
        Assert.Equal([9, 8, 7], File.ReadAllBytes(unchanged));
        Assert.Empty(Directory.GetDirectories(directory.Path, ".preflight-stage-*"));
    }

    [Fact]
    public void RestoreOverlapCheckRejectsBackupDescendantAliasAcrossMountIds()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        string sourceNested = System.IO.Path.Combine(source, "nested");
        Directory.CreateDirectory(sourceNested);
        directory.WriteFile(sourceNested, "file", [1]);
        string backup = directory.Combine("backup");
        _ = PreflightOperations.Backup(source, backup);
        using SafeFileHandle backupRoot = PreflightUnix.OpenDirectoryPath(backup);
        PreflightDirectoryIndex backupIndex = PreflightDirectoryIndex.Capture(backupRoot);
        using SafeFileHandle descendantHandle = PreflightUnix.OpenDirectoryPath(System.IO.Path.Combine(backup, "tree", "nested"));
        PreflightFileStatus descendant = PreflightUnix.StatHandle(descendantHandle);
        PreflightFileStatus alias = WithMountId(descendant, checked(descendant.MountId + 1));

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.RejectOverlappingRestorePaths([], backupIndex, [alias], null));

        Assert.Equal("restore_paths_overlap", exception.Code);
    }

    [Fact]
    public void RestoreOverlapCheckRejectsDestinationDescendantAliasAcrossMountIds()
    {
        using var directory = new PreflightTestDirectory();
        string destination = directory.Combine("destination");
        string nested = System.IO.Path.Combine(destination, "nested");
        Directory.CreateDirectory(nested);
        using SafeFileHandle destinationRoot = PreflightUnix.OpenDirectoryPath(destination);
        PreflightDirectoryIndex destinationIndex = PreflightDirectoryIndex.Capture(destinationRoot);
        using SafeFileHandle descendantHandle = PreflightUnix.OpenDirectoryPath(nested);
        PreflightFileStatus descendant = PreflightUnix.StatHandle(descendantHandle);
        PreflightFileStatus alias = WithMountId(descendant, checked(descendant.MountId + 1));

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.RejectOverlappingRestorePaths([alias], null, [], destinationIndex));

        Assert.Equal("restore_paths_overlap", exception.Code);
    }

    [Fact]
    public void RestoreToExternalDestinationSucceeds()
    {
        using var directory = new PreflightTestDirectory();
        string source = directory.Combine("source");
        Directory.CreateDirectory(source);
        directory.WriteFile(source, "restored", [1, 2, 3]);
        string backup = directory.Combine("backup");
        string manifestPath = PreflightOperations.Backup(source, backup);
        string destination = directory.Combine("destination");
        Directory.CreateDirectory(destination);

        PreflightOperations.Restore(backup, manifestPath, destination);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(System.IO.Path.Combine(destination, "restored")));
    }

    [Fact]
    public void RepositoryIdentityCheckRejectsDescendantAliasAcrossMountIds()
    {
        using var directory = new PreflightTestDirectory();
        string repository = directory.Combine("synthetic-repository");
        string descendantPath = System.IO.Path.Combine(repository, "nested");
        Directory.CreateDirectory(descendantPath);
        File.WriteAllText(System.IO.Path.Combine(repository, "D3B.slnx"), "synthetic marker");
        using SafeFileHandle repositoryRoot = PreflightUnix.OpenDirectoryPath(repository);
        PreflightDirectoryIndex repositoryIndex = PreflightDirectoryIndex.Capture(repositoryRoot);
        using SafeFileHandle descendantHandle = PreflightUnix.OpenDirectoryPath(descendantPath);
        PreflightFileStatus descendant = PreflightUnix.StatHandle(descendantHandle);
        PreflightFileStatus alias = WithMountId(descendant, checked(descendant.MountId + 1));

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureNoRepositoryIdentity([alias], [repositoryIndex]));

        Assert.Equal("repository_output_forbidden", exception.Code);
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
    public void MountBoundaryDetailEscapesPathAndContainsOnlySafeIdentityFields()
    {
        var root = new PreflightFileStatus(PreflightUnix.DirectoryType, 0, 0x1c0, 1, 8, 1, 10, 0, 0, 0, 0);
        var entry = new PreflightFileStatus(PreflightUnix.RegularFileType, 1, 0x180, 2, 9, 2, 11, 0, 0, 0, 0);
        byte[] relativePath = Encoding.UTF8.GetBytes("nested/line\nbreak");

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureSameMount(entry, root, relativePath));

        string line = exception.Detail!.FormatSafeLine();
        Assert.Equal("mount_boundary_unsupported", exception.Code);
        Assert.Contains("category=mount_boundary", line, StringComparison.Ordinal);
        Assert.Contains("relative_path=\"nested/line\\x0Abreak\"", line, StringComparison.Ordinal);
        Assert.Contains("expected_mount_id=10", line, StringComparison.Ordinal);
        Assert.Contains("observed_mount_id=11", line, StringComparison.Ordinal);
        Assert.Contains("expected_device_major=8 expected_device_minor=1", line, StringComparison.Ordinal);
        Assert.Contains("observed_device_major=9 observed_device_minor=2", line, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic file contents", line, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputInsideSourceTreeIsRejected()
    {
        using var directory = new PreflightTestDirectory();
        string sourcePath = directory.Root;
        string outputPath = System.IO.Path.Combine(sourcePath, "nested-output");
        Directory.CreateDirectory(outputPath);
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(sourcePath);
        using SafeFileHandle output = PreflightUnix.OpenDirectoryPath(outputPath);
        PreflightDirectoryIndex sourceIndex = PreflightDirectoryIndex.Capture(source);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree(output, sourceIndex));

        Assert.Equal("output_inside_source_tree", exception.Code);
    }

    [Fact]
    public void DestinationAncestryWithSourceRootIdentityOnSameMountIsBlockedAsContainment()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot]);
        PreflightFileStatus directRoot = DirectoryStatus(42, 8, 1, 10);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree([directRoot], sourceIndex));

        Assert.Equal("output_inside_source_tree", exception.Code);
    }

    [Fact]
    public void RealSourceDescendantIdentityOnDifferentMountIsBlockedAsAlias()
    {
        using var directory = new PreflightTestDirectory();
        string nestedPath = System.IO.Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nestedPath);
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(directory.Root);
        PreflightDirectoryIndex sourceIndex = PreflightDirectoryIndex.Capture(source);
        PreflightFileStatus descendant = PreflightUnix.StatAt(source, PreflightUnix.EncodeName("nested"));
        var descendantAlias = new PreflightFileStatus(
            descendant.Type,
            descendant.Size,
            descendant.Mode,
            descendant.Inode,
            descendant.DeviceMajor,
            descendant.DeviceMinor,
            checked(descendant.MountId + 1),
            descendant.ChangeSeconds,
            descendant.ChangeNanoseconds,
            descendant.ModifiedSeconds,
            descendant.ModifiedNanoseconds,
            descendant.IdentityAvailable);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree([descendantAlias], sourceIndex));

        Assert.Equal("destination_aliases_source", exception.Code);
    }

    [Fact]
    public void SnapshotToFileRejectsDestinationInsideSourceTree()
    {
        using var directory = new PreflightTestDirectory();
        string sourcePath = directory.Root;
        string outputPath = System.IO.Path.Combine(sourcePath, "nested", "manifest.json");

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightOperations.SnapshotToFile(sourcePath, outputPath));

        Assert.Equal("output_inside_source_tree", exception.Code);
    }

    [Fact]
    public void SnapshotToFileAcceptsDestinationOutsideSourceTree()
    {
        using var directory = new PreflightTestDirectory();
        string sourcePath = directory.Root;
        string outputPath = directory.Combine("outside/manifest.json");

        PreflightOperations.SnapshotToFile(sourcePath, outputPath);

        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void DestinationAncestryWithSourceRootIdentityIsBlockedAcrossMountIds()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot]);
        PreflightFileStatus rootAlias = DirectoryStatus(42, 8, 1, 11);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree([rootAlias], sourceIndex));

        Assert.Equal("destination_aliases_source", exception.Code);
    }

    [Fact]
    public void DestinationAncestryWithSourceDescendantIdentityIsBlockedAcrossMountIds()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        PreflightFileStatus sourceDescendant = DirectoryStatus(43, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot, sourceDescendant]);
        PreflightFileStatus descendantAlias = DirectoryStatus(43, 8, 1, 11);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree([descendantAlias], sourceIndex));

        Assert.Equal("destination_aliases_source", exception.Code);
    }

    [Fact]
    public void DestinationAncestryChecksEveryExistingAncestorForSourceAliases()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        PreflightFileStatus sourceDescendant = DirectoryStatus(43, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot, sourceDescendant]);
        PreflightFileStatus destination = DirectoryStatus(50, 8, 1, 10);
        PreflightFileStatus parent = DirectoryStatus(51, 8, 1, 10);
        PreflightFileStatus descendantAlias = DirectoryStatus(43, 8, 1, 11);
        PreflightFileStatus outsideParent = DirectoryStatus(52, 8, 1, 10);

        PreflightBlockedException exception = Assert.Throws<PreflightBlockedException>(() =>
            PreflightUnix.EnsureOutsideTree([destination, parent, descendantAlias, outsideParent], sourceIndex));

        Assert.Equal("destination_aliases_source", exception.Code);
    }

    [Fact]
    public void DestinationOnSameFilesystemWithDifferentDirectoryIdentityIsAccepted()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot]);
        PreflightFileStatus destination = DirectoryStatus(43, 8, 1, 10);

        PreflightUnix.EnsureOutsideTree([destination], sourceIndex);
    }

    [Fact]
    public void DestinationAncestryAcrossUnrelatedMountsIsAcceptedWithoutIdentityMatch()
    {
        PreflightFileStatus sourceRoot = DirectoryStatus(42, 8, 1, 10);
        var sourceIndex = new PreflightDirectoryIndex(sourceRoot, [sourceRoot]);
        PreflightFileStatus destination = DirectoryStatus(50, 8, 1, 10);
        PreflightFileStatus unrelatedMount = DirectoryStatus(51, 9, 2, 11);
        PreflightFileStatus outsideRoot = DirectoryStatus(52, 9, 2, 11);

        PreflightUnix.EnsureOutsideTree([destination, unrelatedMount, outsideRoot], sourceIndex);
    }

    [Fact]
    public void DirectoryIdentityRejectsNonDirectoriesAndUnavailableIdentity()
    {
        var file = new PreflightFileStatus(PreflightUnix.RegularFileType, 1, 0x180, 42, 8, 1, 10, 0, 0, 0, 0);
        PreflightFileStatus unavailable = new(PreflightUnix.DirectoryType, 0, 0x1c0, 42, 8, 1, 10, 0, 0, 0, 0, false);

        PreflightBlockedException nonDirectory = Assert.Throws<PreflightBlockedException>(() =>
            PreflightDirectoryIdentity.FromStatus(file));
        PreflightBlockedException missingIdentity = Assert.Throws<PreflightBlockedException>(() =>
            PreflightDirectoryIdentity.FromStatus(unavailable));

        Assert.Equal("directory_identity_unavailable", nonDirectory.Code);
        Assert.Equal("directory_identity_unavailable", missingIdentity.Code);
    }

    [Fact]
    public void OutputContainmentAndRepositoryWalkAllowUnrelatedMountTransitions()
    {
        using var directory = new PreflightTestDirectory();
        using SafeFileHandle source = PreflightUnix.OpenDirectoryPath(directory.Root);
        using SafeFileHandle output = PreflightUnix.OpenDirectoryPath("/proc");
        PreflightDirectoryIndex sourceIndex = PreflightDirectoryIndex.Capture(source);

        PreflightUnix.EnsureOutsideTree(output, sourceIndex);
        PreflightUnix.EnsureNoRepositoryMarker(output);
    }

    [Fact]
    public void AncestryGuardRejectsBindMountAliasOfTheAncestor()
    {
        var sourceRoot = new PreflightFileStatus(PreflightUnix.DirectoryType, 0, 0x1c0, 42, 8, 1, 10, 0, 0, 0, 0);
        var bindAlias = new PreflightFileStatus(PreflightUnix.DirectoryType, 0, 0x1c0, 42, 8, 1, 11, 0, 0, 0, 0);

        Assert.True(PreflightUnix.IsSameObject(sourceRoot, bindAlias));
        Assert.Throws<PreflightBlockedException>(() => PreflightUnix.IsSameObjectOnSameMount(bindAlias, sourceRoot));
    }

    private static PreflightFileStatus DirectoryStatus(ulong inode, uint deviceMajor, uint deviceMinor, ulong mountId)
    {
        return new PreflightFileStatus(PreflightUnix.DirectoryType, 0, 0x1c0, inode, deviceMajor, deviceMinor, mountId, 0, 0, 0, 0);
    }

    private static PreflightFileStatus WithMountId(PreflightFileStatus status, ulong mountId)
    {
        return new PreflightFileStatus(
            status.Type,
            status.Size,
            status.Mode,
            status.Inode,
            status.DeviceMajor,
            status.DeviceMinor,
            mountId,
            status.ChangeSeconds,
            status.ChangeNanoseconds,
            status.ModifiedSeconds,
            status.ModifiedNanoseconds,
            status.IdentityAvailable);
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
