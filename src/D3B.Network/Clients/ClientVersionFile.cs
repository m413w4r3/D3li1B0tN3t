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

    /// <summary>Replaces the URL of the client's configuration.</summary>
    /// <param name="path">The version file.</param>
    /// <param name="url">The new URL.</param>
    /// <exception cref="InvalidDataException">The file has no <c>ConfigUrl</c> line.</exception>
    public static void WriteConfigUrl(string path, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (url.AsSpan().ContainsAny('\r', '\n'))
            throw new ArgumentException("A URL holds no line break.", nameof(url));

        var lines = Read(path).Split('\n');
        var index = Array.FindIndex(lines, line => Value(line) is not null);

        if (index < 0)
            throw new InvalidDataException($"{path} has no {ConfigUrlKey} line.");

        lines[index] = $"{ConfigUrlKey}={url}{(lines[index].EndsWith('\r') ? "\r" : string.Empty)}";

        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(string.Join('\n', lines)));
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
