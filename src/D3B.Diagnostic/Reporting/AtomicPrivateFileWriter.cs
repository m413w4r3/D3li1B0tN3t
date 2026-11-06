using System.Text;

namespace D3B.Diagnostic.Reporting;

/// <summary>Writes UTF-8 text through a private, atomically replaced file.</summary>
public static class AtomicPrivateFileWriter
{
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode GroupOrOtherMode = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    /// <summary>Writes UTF-8 text to an explicit path using private permissions.</summary>
    public static void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        string fullPath = Path.GetFullPath(path);
        string? parentDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new IOException("The output path has no parent directory.");
        }

        EnsurePrivateParent(parentDirectory);
        EnsureSafeTarget(fullPath);

        string temporaryPath = Path.Combine(parentDirectory, $".d3b-{Guid.NewGuid():N}.tmp");
        bool temporaryCreated = false;
        try
        {
            var fileOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                fileOptions.UnixCreateMode = PrivateFileMode;
            }

            using (var stream = new FileStream(temporaryPath, fileOptions))
            {
                temporaryCreated = true;
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporaryPath, PrivateFileMode);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
            temporaryCreated = false;
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fullPath, PrivateFileMode);
            }
        }
        catch
        {
            if (temporaryCreated)
            {
                DeleteBestEffort(temporaryPath);
            }

            throw;
        }
    }

    private static void EnsurePrivateParent(string parentDirectory)
    {
        string? root = Path.GetPathRoot(parentDirectory);
        if (root is null || PathsEqual(parentDirectory, root) || PathsEqual(parentDirectory, Path.GetTempPath()))
        {
            throw new IOException("The output directory must be private.");
        }

        EnsureNoReparsePointInPath(parentDirectory);
        if (!TryGetAttributes(parentDirectory, out FileAttributes attributes))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(parentDirectory);
            }
            else
            {
                Directory.CreateDirectory(parentDirectory, PrivateDirectoryMode);
                File.SetUnixFileMode(parentDirectory, PrivateDirectoryMode);
            }
        }

        EnsureNoReparsePointInPath(parentDirectory);
        attributes = File.GetAttributes(parentDirectory);
        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException("The output directory must be a private directory.");
        }

        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(parentDirectory) & GroupOrOtherMode) != 0)
        {
            throw new IOException("The output directory must be private.");
        }
    }

    private static void EnsureSafeTarget(string fullPath)
    {
        if (!TryGetAttributes(fullPath, out FileAttributes attributes))
        {
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException("The output target must be a regular file path.");
        }
    }

    private static void EnsureNoReparsePointInPath(string path)
    {
        string? root = Path.GetPathRoot(path);
        if (root is null)
        {
            throw new IOException("The output path is invalid.");
        }

        string current = root;
        if (TryGetAttributes(current, out FileAttributes rootAttributes) && (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The output path contains a reparse point.");
        }

        string relativePath = path[root.Length..];
        string[] components = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (string component in components)
        {
            current = Path.Combine(current, component);
            if (!TryGetAttributes(current, out FileAttributes attributes))
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The output path contains a reparse point.");
            }
        }
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        string normalizedFirst = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        string normalizedSecond = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(normalizedFirst, normalizedSecond, comparison);
    }

    private static void DeleteBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Preserve the write failure; cleanup is best effort.
        }
    }
}
