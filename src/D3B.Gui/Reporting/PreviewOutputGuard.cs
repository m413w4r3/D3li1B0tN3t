namespace D3B.Gui.Reporting;

/// <summary>Applies path-only rules for offline preview output directories.</summary>
public static class PreviewOutputGuard
{
    /// <summary>Checks whether an output directory is permitted by the preview policy.</summary>
    /// <param name="outputDirectory">The requested output directory.</param>
    /// <param name="homeDirectory">The user's home directory.</param>
    /// <param name="repositoryRoot">The repository root identified by its solution or Git metadata.</param>
    /// <param name="wasExplicitlyProvided">Whether the caller supplied the output path.</param>
    /// <param name="normalizedPath">The normalized absolute path when allowed.</param>
    /// <param name="reason">A short rejection reason when denied.</param>
    /// <returns>Whether the path is allowed.</returns>
    public static bool TryAuthorize(
        string outputDirectory,
        string homeDirectory,
        string repositoryRoot,
        bool wasExplicitlyProvided,
        out string? normalizedPath,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(homeDirectory);
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        normalizedPath = null;
        reason = null;

        if (!Path.IsPathFullyQualified(outputDirectory))
        {
            reason = "The output directory must be an absolute path.";
            return false;
        }

        if (!Path.IsPathFullyQualified(homeDirectory) || !Path.IsPathFullyQualified(repositoryRoot))
        {
            reason = "The home directory and repository root must be absolute paths.";
            return false;
        }

        string fullPath;
        string fullHome;
        string fullRepository;
        try
        {
            fullPath = Normalize(outputDirectory);
            fullHome = Normalize(homeDirectory);
            fullRepository = Normalize(repositoryRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "The output path is invalid.";
            return false;
        }

        if (IsWithin(fullPath, fullRepository))
        {
            reason = "The output directory cannot be inside the repository.";
            return false;
        }

        if (PathsEqual(fullPath, fullHome) || IsFilesystemRoot(fullPath) || PathsEqual(fullPath, Normalize(Path.GetTempPath())))
        {
            reason = "The output directory must be a private child directory, not a shared or root directory.";
            return false;
        }

        if (!IsWithin(fullPath, fullHome) && !wasExplicitlyProvided)
        {
            reason = "An output path outside the home directory must be provided explicitly.";
            return false;
        }

        normalizedPath = fullPath;
        return true;
    }

    private static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsWithin(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(path, directory, comparison))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, comparison);
    }

    private static bool IsFilesystemRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return root is not null && PathsEqual(path, Normalize(root));
    }

    private static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(first, second, comparison);
    }
}
