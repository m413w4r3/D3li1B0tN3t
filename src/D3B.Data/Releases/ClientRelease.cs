using System.Globalization;

namespace D3B.Data.Releases;

/// <summary>How the files made from a client build are published: one GitHub release per build, an asset per file (D24).</summary>
/// <remarks>
/// The tool that publishes an asset and the one that ships it beside the app share these names, so that they cannot drift
/// apart (D172).
/// Each asset comes with a second one, its SHA-256 in the format <c>sha256sum</c> writes and checks.
/// </remarks>
public static class ClientRelease
{
    /// <summary>The repository whose releases ship the packs, as <c>owner/name</c>.</summary>
    public const string DefaultRepository = "AerafalDev/D3B";

    private const int HashLength = 64;

    /// <summary>Gets the tag of the release of a client build, such as <c>client-3.7.12.12</c>.</summary>
    /// <param name="build">The client build.</param>
    /// <returns>The tag.</returns>
    public static string TagOf(ClientBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        return $"client-{build.Version}";
    }

    /// <summary>Gets the name of the asset holding another asset's SHA-256.</summary>
    /// <param name="assetName">The asset's name, such as <c>data-3.pack</c>.</param>
    /// <returns>The checksum asset's name, such as <c>data-3.pack.sha256</c>.</returns>
    public static string ChecksumNameOf(string assetName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetName);

        return $"{assetName}.sha256";
    }

    /// <summary>Writes the content of a checksum asset.</summary>
    /// <param name="hash">The asset's SHA-256.</param>
    /// <param name="assetName">The asset's name.</param>
    /// <returns>One line: the hash in lowercase hexadecimal, two spaces and the asset's name.</returns>
    /// <exception cref="ArgumentException"><paramref name="hash"/> is not 32 bytes long.</exception>
    public static string FormatChecksum(ReadOnlySpan<byte> hash, string assetName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assetName);

        if (hash.Length * 2 != HashLength)
            throw new ArgumentException($"A SHA-256 is 32 bytes long, not {hash.Length}.", nameof(hash));

        return string.Create(CultureInfo.InvariantCulture, $"{Convert.ToHexStringLower(hash)}  {assetName}\n");
    }

    /// <summary>Reads the content of a checksum asset.</summary>
    /// <param name="text">The asset's content.</param>
    /// <param name="assetName">The name of the asset it should describe.</param>
    /// <returns>The SHA-256, in lowercase hexadecimal.</returns>
    /// <exception cref="InvalidDataException">The text is not one line holding a SHA-256 and <paramref name="assetName"/>.</exception>
    public static string ParseChecksum(string text, string assetName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(assetName);

        var line = text.TrimEnd('\r', '\n');
        var separator = line.IndexOf(' ', StringComparison.Ordinal);

        // sha256sum marks a binary file with '*' before its name; both forms name the same bytes.
        if (separator != HashLength || line.Contains('\n', StringComparison.Ordinal))
            throw new InvalidDataException($"The checksum of {assetName} is not one line starting with a SHA-256.");

        var hash = line[..separator];
        var name = line[(separator + 1)..].TrimStart(' ', '*');

        if (!hash.All(char.IsAsciiHexDigit))
            throw new InvalidDataException($"The checksum of {assetName} is not a SHA-256: '{hash}'.");

        if (name != assetName)
            throw new InvalidDataException($"The checksum of {assetName} describes '{name}'.");

        return hash.ToLowerInvariant();
    }

    /// <summary>Tells whether a value names a GitHub repository as <c>owner/name</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it does, with only the characters GitHub allows, so that it is safe in a URL.</returns>
    public static bool IsRepository(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 200)
            return false;

        var parts = value.Split('/');

        return parts.Length is 2 && parts.All(part => part.Length > 0 && part is not "." and not ".." && part.All(IsRepositoryCharacter));
    }

    private static bool IsRepositoryCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '.';
    }
}
