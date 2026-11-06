using System.Runtime.InteropServices;
using D3B.Diagnostic.Reporting;

namespace D3B.Diagnostic.Tests.Reporting;

[CollectionDefinition("Atomic private file writer", DisableParallelization = true)]
public sealed class AtomicPrivateFileWriterTestGroup
{
}

[Collection("Atomic private file writer")]
public sealed class AtomicPrivateFileWriterTests
{
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [Fact]
    public void WriteCreatesPrivateParentAndFileUnderZeroUmask()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = NewPath();
        string targetPath = Path.Combine(directory, "output.json");
        uint previousUmask = UnixFileSystem.SetUmask(0);
        try
        {
            AtomicPrivateFileWriter.Write(targetPath, "private output");

            Assert.Equal(PrivateDirectoryMode, File.GetUnixFileMode(directory));
            Assert.Equal(PrivateFileMode, File.GetUnixFileMode(targetPath));
            Assert.Equal("private output", File.ReadAllText(targetPath));
        }
        finally
        {
            _ = UnixFileSystem.SetUmask(previousUmask);
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteSafelyReplacesExistingFileAndLeavesNoTemporaryFiles()
    {
        string directory = CreatePrivateDirectory();
        string targetPath = Path.Combine(directory, "output.json");
        try
        {
            File.WriteAllText(targetPath, "old content");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(targetPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }

            AtomicPrivateFileWriter.Write(targetPath, "new content");

            Assert.Equal("new content", File.ReadAllText(targetPath));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(PrivateFileMode, File.GetUnixFileMode(targetPath));
            }

            Assert.Equal([targetPath], Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteLeavesExistingTargetUntouchedWhenTemporaryCreationFails()
    {
        if (OperatingSystem.IsWindows() || UnixFileSystem.GetEffectiveUserId() == 0)
        {
            return;
        }

        string directory = CreatePrivateDirectory();
        string targetPath = Path.Combine(directory, "output.json");
        File.WriteAllText(targetPath, "old content");
        try
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            Exception? exception = Record.Exception(() => AtomicPrivateFileWriter.Write(targetPath, "new content"));

            Assert.NotNull(exception);
            Assert.IsNotType<NotImplementedException>(exception);
            Assert.Equal("old content", File.ReadAllText(targetPath));
            Assert.Equal([targetPath], Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            File.SetUnixFileMode(directory, PrivateDirectoryMode);
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteRefusesNonPrivateExistingParent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = CreatePrivateDirectory();
        string targetPath = Path.Combine(directory, "output.json");
        try
        {
            File.SetUnixFileMode(directory, PrivateDirectoryMode | UnixFileMode.GroupRead);

            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(targetPath, "private output"));
            Assert.Empty(Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteRefusesSymlinkedParent()
    {
        string root = CreatePrivateDirectory();
        string realDirectory = Path.Combine(root, "real");
        string linkedDirectory = Path.Combine(root, "linked");
        Directory.CreateDirectory(realDirectory);
        string targetPath = Path.Combine(linkedDirectory, "output.json");
        try
        {
            Directory.CreateSymbolicLink(linkedDirectory, realDirectory);

            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(targetPath, "private output"));
            Assert.Empty(Directory.GetFileSystemEntries(realDirectory));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void WriteRefusesSymlinkTargetWithoutChangingItsDestination()
    {
        string directory = CreatePrivateDirectory();
        string destinationPath = Path.Combine(directory, "destination.json");
        string targetPath = Path.Combine(directory, "output.json");
        try
        {
            File.WriteAllText(destinationPath, "destination content");
            File.CreateSymbolicLink(targetPath, destinationPath);

            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(targetPath, "private output"));

            Assert.Equal("destination content", File.ReadAllText(destinationPath));
            Assert.True((File.GetAttributes(targetPath) & FileAttributes.ReparsePoint) != 0);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteRefusesDirectoryTarget()
    {
        string directory = CreatePrivateDirectory();
        string targetPath = Path.Combine(directory, "output");
        Directory.CreateDirectory(targetPath);
        try
        {
            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(targetPath, "private output"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void WriteRefusesFilesystemAndSystemTemporaryRoots()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        string rootTarget = Path.Combine(root, $"atomic-private-{Guid.NewGuid():N}.json");
        string temporaryRootTarget = Path.Combine(Path.GetTempPath(), $"atomic-private-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(rootTarget, "private output"));
            Assert.Throws<IOException>(() => AtomicPrivateFileWriter.Write(temporaryRootTarget, "private output"));
        }
        finally
        {
            File.Delete(rootTarget);
            File.Delete(temporaryRootTarget);
        }
    }

    [Fact]
    public void WriteRejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => AtomicPrivateFileWriter.Write(" ", "private output"));
    }

    private static string CreatePrivateDirectory()
    {
        string path = NewPath();
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, PrivateDirectoryMode);
        }

        return path;
    }

    private static string NewPath()
    {
        return Path.Combine(Path.GetTempPath(), $"atomic-private-{Guid.NewGuid():N}");
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, PrivateDirectoryMode);
            }

            Directory.Delete(path, recursive: true);
        }
    }

    private static class UnixFileSystem
    {
        [DllImport("libc", EntryPoint = "umask")]
        internal static extern uint SetUmask(uint mask);

        [DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();
    }
}
