namespace D3B.Network.Clients;

/// <summary>What D3B keeps of the client's configuration while it sends the client to itself (D35).</summary>
/// <remarks>
/// In one directory, <c>%LOCALAPPDATA%\D3B\client</c> by default: the configuration D3B gives the client, the URL of
/// Ankama's that the client's version file named before, and the last configuration read from it.
/// </remarks>
public static class ClientRedirectFiles
{
    /// <summary>The file holding the configuration D3B gives the client.</summary>
    public const string ConfigFileName = "config.json";

    /// <summary>The file holding the URL of Ankama's configuration, which the version file named before D3B.</summary>
    public const string OriginalUrlFileName = "config-url";

    /// <summary>The file holding the last configuration read from Ankama.</summary>
    public const string OriginalConfigFileName = "ankama-config.json";

    /// <summary>Gets the URL the client's version file names while the client is sent to D3B.</summary>
    /// <param name="directory">Where D3B keeps the client's configuration.</param>
    /// <returns>A <c>file:</c> URL: the client refuses plain HTTP, and reads a local file.</returns>
    public static string ConfigUrl(string directory)
    {
        return new Uri(Path.Combine(directory, ConfigFileName)).AbsoluteUri;
    }

    /// <summary>Gets the URL of Ankama's configuration, kept when the client was sent to D3B.</summary>
    /// <param name="directory">Where D3B keeps the client's configuration.</param>
    /// <returns>The URL, or null when none is kept.</returns>
    public static string? OriginalUrl(string directory)
    {
        var path = Path.Combine(directory, OriginalUrlFileName);

        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
}
