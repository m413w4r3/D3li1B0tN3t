using System.Text;

namespace D3B.Gui.Reporting;

/// <summary>Writes offline preview artifacts atomically with private Linux permissions.</summary>
public static class PreviewOutputWriter
{
    private const UnixFileMode PrivateDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Writes text through a private temporary file and atomically replaces the destination.</summary>
    /// <param name="directory">The output directory.</param>
    /// <param name="fileName">A single destination file name.</param>
    /// <param name="content">The text to write as UTF-8 without a byte-order mark.</param>
    /// <returns>The absolute destination path.</returns>
    public static string WriteText(string directory, string fileName, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return WriteBytes(directory, fileName, Encoding.UTF8.GetBytes(content));
    }

    /// <summary>Writes bytes through a private temporary file and atomically replaces the destination.</summary>
    /// <param name="directory">The output directory.</param>
    /// <param name="fileName">A single destination file name.</param>
    /// <param name="content">The bytes to write.</param>
    /// <returns>The absolute destination path.</returns>
    public static string WriteBytes(string directory, string fileName, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Private preview output currently requires Linux.");
        }

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName is "." or "..")
        {
            throw new ArgumentException("The output file name must be a single path component.", nameof(fileName));
        }

        var fullDirectory = Path.GetFullPath(directory);
        if (IsWithinD3BRepository(fullDirectory))
        {
            throw new IOException("The preview output directory cannot be inside the repository.");
        }

        var root = Path.GetPathRoot(fullDirectory);
        var home = Environment.GetEnvironmentVariable("HOME");
        if (root is null
            || PathsEqual(fullDirectory, Path.TrimEndingDirectorySeparator(root))
            || PathsEqual(fullDirectory, Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
            || (!string.IsNullOrWhiteSpace(home) && PathsEqual(fullDirectory, Path.GetFullPath(home))))
        {
            throw new IOException("The preview output directory must be a private child directory.");
        }

        EnsureNoReparsePointInPath(fullDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            Directory.CreateDirectory(fullDirectory, PrivateDirectoryMode);
            File.SetUnixFileMode(fullDirectory, PrivateDirectoryMode);
        }

        EnsureNoReparsePointInPath(fullDirectory);
        if ((File.GetAttributes(fullDirectory) & FileAttributes.Directory) == 0
            || File.GetUnixFileMode(fullDirectory) != PrivateDirectoryMode)
        {
            throw new IOException("The preview output directory must already have mode 0700.");
        }

        var destinationPath = Path.Combine(fullDirectory, fileName);
        EnsureSafeTarget(destinationPath);

        var temporaryPath = Path.Combine(fullDirectory, $".d3b-preview-{Guid.NewGuid():N}.tmp");
        var temporaryCreated = false;

        try
        {
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
                UnixCreateMode = PrivateFileMode,
            };

            using (var stream = new FileStream(temporaryPath, options))
            {
                temporaryCreated = true;
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.SetUnixFileMode(temporaryPath, PrivateFileMode);
            File.Move(temporaryPath, destinationPath, overwrite: true);
            temporaryCreated = false;
            return destinationPath;
        }
        finally
        {
            if (temporaryCreated)
            {
                DeleteBestEffort(temporaryPath);
            }
        }
    }

    private static void EnsureSafeTarget(string path)
    {
        if (!TryGetAttributes(path, out var attributes))
        {
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException("The preview output target must be a regular file path.");
        }
    }

    private static bool IsWithinD3BRepository(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "D3B.slnx")))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static void EnsureNoReparsePointInPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new IOException("The preview output path must be absolute.");
        }

        var current = root;
        if (TryGetAttributes(current, out var rootAttributes) && (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The preview output path cannot contain symbolic links.");
        }

        var components = path[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var component in components)
        {
            current = Path.Combine(current, component);
            if (!TryGetAttributes(current, out var attributes))
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The preview output path cannot contain symbolic links.");
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

    private static void DeleteBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        var normalizedFirst = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        var normalizedSecond = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        return string.Equals(normalizedFirst, normalizedSecond, StringComparison.Ordinal);
    }
}
