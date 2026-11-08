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
        string target = directory.WriteFile(root, "target", [8, 9, 10]);
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

        PreflightOperations.Restore(backup, manifestPath, root);
        PreflightOperations.Verify(root, manifestPath);
        Assert.Equal(PreflightOperations.SerializeManifest(before), PreflightOperations.SerializeManifest(PreflightOperations.Snapshot(root)));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, File.GetUnixFileMode(config));
        Assert.Equal("config file", new FileInfo(System.IO.Path.Combine(root, "link")).LinkTarget);
        Assert.False(File.Exists(System.IO.Path.Combine(root, "extra")));
        Assert.Equal([8, 9, 10], File.ReadAllBytes(target));
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
