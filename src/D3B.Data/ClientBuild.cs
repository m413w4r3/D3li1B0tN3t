using System.Globalization;

namespace D3B.Data;

/// <summary>A build of the official client, as its <c>Dofus_Data/StreamingAssets/version</c> file names it.</summary>
/// <remarks>
/// The version names the pack made from the build and the release that ships it (D24), so it ends up in paths and URLs:
/// only digits and dots are accepted, which also keeps a crafted file from pointing outside the pack directory.
/// </remarks>
public sealed class ClientBuild
{
    /// <summary>Gets the client's version, such as <c>3.7.12.12</c>.</summary>
    public string Version { get; }

    /// <summary>Gets when the client was built, when its version file says so.</summary>
    public DateTimeOffset? BuildDate { get; }

    /// <summary>Initializes a new instance of the <see cref="ClientBuild"/> class.</summary>
    /// <param name="version">The client's version: numbers separated by dots.</param>
    /// <param name="buildDate">When the client was built, if known.</param>
    /// <exception cref="ArgumentException"><paramref name="version"/> is not numbers separated by dots.</exception>
    public ClientBuild(string version, DateTimeOffset? buildDate)
    {
        if (!IsVersion(version))
            throw new ArgumentException($"'{version}' is not a client version: numbers separated by dots, such as 3.7.12.12.", nameof(version));

        Version = version;
        BuildDate = buildDate;
    }

    /// <summary>Reads the build of the client installed in a directory.</summary>
    /// <param name="clientDirectory">The client's installation directory, which holds <c>Dofus.exe</c>.</param>
    /// <returns>The build.</returns>
    /// <exception cref="FileNotFoundException">The directory holds no version file.</exception>
    /// <exception cref="InvalidDataException">The version file names no valid version.</exception>
    public static ClientBuild Read(string clientDirectory)
    {
        var path = Path.Combine(clientDirectory, "Dofus_Data", "StreamingAssets", "version");

        if (!File.Exists(path))
            throw new FileNotFoundException($"No client version file at '{path}'.", path);

        return Parse(File.ReadAllText(path));
    }

    /// <summary>Parses the content of a version file: <c>Key=Value</c> lines, among them <c>Version</c> and <c>BuildDate</c>.</summary>
    /// <param name="text">The file's content.</param>
    /// <returns>The build.</returns>
    /// <exception cref="InvalidDataException">The text names no valid version, or an unreadable build date.</exception>
    public static ClientBuild Parse(string text)
    {
        string? version = null;
        DateTimeOffset? buildDate = null;

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (key == "Version")
            {
                version = value;
            }
            else if (key == "BuildDate")
            {
                if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                    throw new InvalidDataException($"The client's build date '{value}' is not a date.");

                buildDate = date;
            }
        }

        if (version is null || !IsVersion(version))
            throw new InvalidDataException(version is null ? "The client's version file has no Version line." : $"The client's version '{version}' is not numbers separated by dots.");

        return new ClientBuild(version, buildDate);
    }

    private static bool IsVersion(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
            return false;

        foreach (var part in value.Split('.'))
        {
            if (part.Length is 0 || !part.All(char.IsAsciiDigit))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return BuildDate is { } date ? $"{Version}, built {date:u}" : Version;
    }
}
