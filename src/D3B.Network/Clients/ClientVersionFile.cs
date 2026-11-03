using System.Globalization;
using System.Text;

namespace D3B.Network.Clients;

/// <summary>The client's <c>Dofus_Data/StreamingAssets/version</c> file, whose <c>ConfigUrl</c> line names its configuration.</summary>
/// <remarks>
/// <c>Key=Value</c> lines: <c>Version</c>, <c>BuildDate</c> and <c>ConfigUrl</c>. The file is read and written as Latin-1,
/// which maps every byte to one character and back, so that changing <c>ConfigUrl</c> leaves every other byte as it was.
/// </remarks>
public static class ClientVersionFile
{
    private const string ConfigUrlKey = "ConfigUrl";
    private const string VersionKey = "Version";
    private const string BuildDateKey = "BuildDate";

    /// <summary>Gets the version file of a client.</summary>
    /// <param name="clientDirectory">The client's installation directory.</param>
    /// <returns>The file's path.</returns>
    public static string PathIn(string clientDirectory)
    {
        return Path.Combine(clientDirectory, "Dofus_Data", "StreamingAssets", "version");
    }

    /// <summary>Reads the URL of the client's configuration.</summary>
    /// <param name="path">The version file.</param>
    /// <returns>The URL, or null when the file has no <c>ConfigUrl</c> line.</returns>
    public static string? ReadConfigUrl(string path)
    {
        foreach (var line in Read(path).Split('\n'))
        {
            if (Value(line) is { } value)
                return value;
        }

        return null;
    }

    /// <summary>Reads the build the client's version file names.</summary>
    /// <param name="path">The version file.</param>
    /// <returns>The build, or null when the file has no <c>Version</c> line.</returns>
    public static ClientBuildIdentity? ReadBuild(string path)
    {
        string? version = null;
        DateTimeOffset? buildDate = null;

        foreach (var line in Read(path).Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (key == VersionKey)
            {
                version = value;
            }
            else if (key == BuildDateKey && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            {
                buildDate = date;
            }
        }

        return string.IsNullOrWhiteSpace(version) ? null : new ClientBuildIdentity(version, buildDate);
    }

    /// <summary>Replaces the URL of the client's configuration.</summary>
    /// <param name="path">The version file.</param>
    /// <param name="url">The new URL.</param>
    /// <exception cref="InvalidDataException">The file has no <c>ConfigUrl</c> line.</exception>
    public static void WriteConfigUrl(string path, string url)
    {
        WriteConfigUrl(path, url, static (temporaryPath, destinationPath) => File.Move(temporaryPath, destinationPath, overwrite: true));
    }

    internal static void WriteConfigUrl(string path, string url, Action<string, string> commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(commit);

        if (url.AsSpan().ContainsAny('\r', '\n'))
            throw new ArgumentException("A URL holds no line break.", nameof(url));

        var lines = Read(path).Split('\n');
        var index = Array.FindIndex(lines, line => Value(line) is not null);

        if (index < 0)
            throw new InvalidDataException($"{path} has no {ConfigUrlKey} line.");

        lines[index] = $"{ConfigUrlKey}={url}{(lines[index].EndsWith('\r') ? "\r" : string.Empty)}";

        var bytes = Encoding.Latin1.GetBytes(string.Join('\n', lines));
        var destinationPath = System.IO.Path.GetFullPath(path);
        var directory = System.IO.Path.GetDirectoryName(destinationPath)!;
        var temporaryPath = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        // Atomic replacement creates a new file entry: preserve its mode, but cannot retain symlink identity, ACLs, or ownership.
        var originalMode = OperatingSystem.IsWindows() ? (UnixFileMode?)null : File.GetUnixFileMode(path);

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);

                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporaryPath, originalMode!.Value);

                stream.Flush(flushToDisk: true);
            }

            commit(temporaryPath, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string Read(string path)
    {
        // Bytes, never text: a reader decoding text would drop a byte order mark. Shared for writing, as the launcher may
        // be writing the file, and so that D3B's own reads never make a write fail.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var bytes = new MemoryStream();

        stream.CopyTo(bytes);

        return Encoding.Latin1.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
    }

    private static string? Value(string line)
    {
        var separator = line.IndexOf('=', StringComparison.Ordinal);

        return separator >= 0 && line[..separator].Trim() == ConfigUrlKey ? line[(separator + 1)..].Trim() : null;
    }
}
