using System.IO.Enumeration;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Inspect;

/// <summary>The installed official client, as the Ankama launcher lays it out.</summary>
internal sealed class Client
{
    private const string ContentDirectory = "Content";

    private IReadOnlyList<BundlePath>? _bundles;

    /// <summary>The installation directory, which holds <c>Dofus.exe</c> and the launcher's <c>manifest.json</c>.</summary>
    public string Root { get; }

    /// <summary>The Unity player's data directory.</summary>
    public string DataDirectory => Path.Combine(Root, "Dofus_Data");

    /// <summary>The directory that holds every bundle, the texts and the audio banks.</summary>
    public string StreamingAssets => Path.Combine(DataDirectory, "StreamingAssets");

    private Client(string root)
    {
        Root = root;
    }

    /// <summary>Finds the client in <paramref name="directory"/>, or where the launcher installs it by default.</summary>
    /// <exception cref="DirectoryNotFoundException">No client is there.</exception>
    public static Client Locate(DirectoryInfo? directory)
    {
        return new Client(ClientInstall.Locate(directory).Root);
    }

    /// <summary>The area of a file under <c>StreamingAssets</c>: its directory below <c>Content</c>, or its top directory.</summary>
    public static string AreaOf(string relativePath)
    {
        var parts = relativePath.Split('/');

        if (parts.Length is 1)
            return ".";

        return parts[0] is ContentDirectory ? string.Join('/', parts[1..^1]) : parts[0];
    }

    /// <summary>Every bundle of the client, in ordinal order of their relative paths.</summary>
    public IReadOnlyList<BundlePath> Bundles()
    {
        return _bundles ??= Directory.EnumerateFiles(StreamingAssets, "*.bundle", SearchOption.AllDirectories)
            .Select(ToBundlePath)
            .OrderBy(bundle => bundle.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The bundles whose relative path matches <paramref name="pattern"/>.</summary>
    /// <param name="pattern">
    /// A pattern where <c>*</c> matches any run of characters, slashes included, and <c>?</c> one character, matched
    /// against the path under <c>StreamingAssets</c> without regard to case. A pattern without a wildcard matches any
    /// path that contains it. <see langword="null"/> matches every bundle.
    /// </param>
    public IReadOnlyList<BundlePath> Bundles(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return Bundles();

        var expression = pattern.Replace('\\', '/');

        if (!expression.Contains('*', StringComparison.Ordinal) && !expression.Contains('?', StringComparison.Ordinal))
            expression = $"*{expression}*";

        return Bundles().Where(bundle => FileSystemName.MatchesSimpleExpression(expression, bundle.RelativePath)).ToList();
    }

    /// <summary>The path of a file under <c>StreamingAssets</c>, with forward slashes.</summary>
    public string RelativePath(string fullPath)
    {
        return Path.GetRelativePath(StreamingAssets, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private BundlePath ToBundlePath(string fullPath)
    {
        var relative = RelativePath(fullPath);

        return new BundlePath(fullPath, relative, AreaOf(relative));
    }
}
